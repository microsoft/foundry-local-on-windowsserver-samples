using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PatientSummaryTool.Models;
using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.CustomExceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PatientSummaryTool.Services
{
    public class Translator : BindableBase, ITranslator
    {
        private readonly IConfigurationManager configurationManager;

        private static readonly List<string> DefaultSectionNames = new List<string>
        {
            "DEMOGRAPHICS", "ALLERGIES", "MEDICATIONS", "CONDITIONS",
            "CARE PLANS", "REPORTS", "OBSERVATIONS", "PROCEDURES",
            "IMMUNIZATIONS", "ENCOUNTERS", "IMAGING STUDIES"
        };

        private class SectionInfo
        {
            public string Text { get; set; }
            public int SectionIndex { get; set; }
            public string SectionName { get; set; }
            public int TotalSections { get; set; }
        }

        public Translator(IConfigurationManager _configurationManager)
        {
            configurationManager = _configurationManager;
        }

        private List<SectionInfo> SplitIntoSections(string text)
        {
            // Delimiter that separates sections in the synthetic dataset
            const string delimiter = "--------------------------------------------------------------------------------";

            // Split by delimiter, clean up, and return each section
            var sections = text
                .Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            
            var allSections = new List<SectionInfo>();

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                var section = sections[sectionIndex];
                var sectionName = sectionIndex < DefaultSectionNames.Count
                    ? DefaultSectionNames[sectionIndex]
                    : $"SECTION_{sectionIndex + 1}";

                allSections.Add(new SectionInfo
                {
                    Text = sections[sectionIndex],
                    SectionIndex = sectionIndex,
                    SectionName = sectionName,
                    TotalSections = sections.Count
                });
            }
            
            return allSections;
        }

        // Even when prompted not to, the models sometimes add unwanted commentary.
        // This function removes such commentary if detected.
        private string RemoveUnwantedCommentary(string translation)
        {
            if (string.IsNullOrWhiteSpace(translation))
                return translation;

            var lines = translation.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(l => l.Trim())
                                   .Where(l => !string.IsNullOrWhiteSpace(l))
                                   .ToList();

            // List of unwanted words/phrases to check for
            var unwanted = new[] { "translation", "translated", "response" };

            // Ignore lines containing unwanted content
            lines = lines.Where((line, index) =>
                !unwanted.Any(x => line.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)
            ).ToList();

            return string.Join("\n", lines);
        }

        private async Task<string> TranslateSectionAsync(SectionInfo sectionInfo, int totalSections, HttpClient httpClient, string endpoint, string sourceLanguage)
        {
            string apiKey = configurationManager.GetAppSetting("WsaiGatewayApiKey");
            var requestBody = new
            {
                model = configurationManager.GetAppSetting("FoundryLocalLanguageModel"),
                messages = new[]
                {
                        new { role = "system", content = "You are a professional medical translator. If a label (e.g., [ATTUALE], [INTERROTTO]) appears, translate it literally (e.g., [CURRENT], [STOPPED])." },
                        new { role = "user", content = $"Translate the following medical data about a patient from {sourceLanguage} into English.\r\n" +
                                                       "Preserve the structure and formatting of the original text as much as possible.\r\n" +
                                                       "Do not add any translator explanations, or notes, or commentary. The only output should be the translated text.\r\n" +
                                                       $"Text:\n{sectionInfo.Text}"
                        }
                    },
                temperature = 0.0
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint + "/v1/chat/completions") { Content = content };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                var parsed = JObject.Parse(result);
                string translation = parsed["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim() ?? "";
                translation = RemoveUnwantedCommentary(translation);

                return translation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Section {sectionInfo.SectionIndex + 1} [{sectionInfo.SectionName}] failed: {ex.Message}");
                throw;
            }
        }

        private async Task<List<(SectionInfo sectionInfo, string sectionTranslation)>> MapPhaseAsync(List<SectionInfo> sections, string sourceLanguage)
        {
            var endpoint = configurationManager.GetAppSetting("FoundryLocalEndPoint");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

            // Enqueue all of the sections to be translated
            var sectionQueue = new ConcurrentQueue<SectionInfo>(sections);

            // Store the section translations as they complete
            var translations = new ConcurrentBag<(SectionInfo sectionInfo, string sectionTranslation)>();

            // Use one worker per server endpoint, each pulling from the shared queue as they complete work
            await Task.Run(async () =>
            {
                while (sectionQueue.TryDequeue(out var sectionInfo))
                {
                    try
                    {
                        Console.WriteLine($"Sending section {sectionInfo.SectionIndex + 1}/{sectionInfo.TotalSections} ({sectionInfo.Text.Length} chars) to Language Model server at {endpoint}");
                        var sectionTranslation = await TranslateSectionAsync(
                                                        sectionInfo,
                                                        sections.Count,
                                                        httpClient,
                                                        endpoint,
                                                        sourceLanguage
                                                     );
                        Console.WriteLine($"[{endpoint}] Successfully translated section {sectionInfo.SectionIndex + 1} [{sectionInfo.SectionName}] ({sectionTranslation.Length} chars)");

                        translations.Add((sectionInfo, sectionTranslation));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{endpoint}] Failed during translated of section {sectionInfo.SectionIndex + 1} [{sectionInfo.SectionName}] with exception: {ex.Message}");
                        throw new TranslateFailedException(ex);
                    }
                }
            });

            // Order translations by their original global index
            var orderedTranslations = translations
                .OrderBy(c => c.sectionInfo.SectionIndex)
                .ToList();

            return orderedTranslations;
        }

        private string AssembleTranslation(List<(SectionInfo sectionInfo, string sectionTranslation)> translations)
        {
            const string delimiter = "\n--------------------------------------------------------------------------------";
            var result = new StringBuilder();
            int currentSectionIndex = -1;

            foreach (var (sectionInfo, sectionTranslation) in translations)
            {
                // Add delimiter between sections
                if (sectionInfo.SectionIndex != currentSectionIndex)
                {
                    if (currentSectionIndex != -1)
                        result.AppendLine(delimiter);
                    currentSectionIndex = sectionInfo.SectionIndex;
                }

                // Replace first line with section title, except for demographics
                var processedTranslation = sectionTranslation;
                if (sectionInfo.SectionIndex == 0 &&
                    !string.Equals(sectionInfo.SectionName, "DEMOGRAPHICS", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = processedTranslation
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();

                    if (lines.Count > 0)
                    {
                        lines[0] = sectionInfo.SectionName;
                        processedTranslation = string.Join("\n", lines);
                    }
                }

                if (sectionInfo.SectionIndex > 0)
                    result.AppendLine();

                result.Append(processedTranslation);
            }

            return result.ToString();
        }

        public async Task<string> RunAsync(string inputText, string sourceLanguage)
        {
            // Split input document into sections
            var sections = SplitIntoSections(inputText);

            // Translate all sections
            var translatedSections = await MapPhaseAsync(sections, sourceLanguage);

            // Construct translated document
            var translation = AssembleTranslation(translatedSections);

            return translation;
        }
    }
}
