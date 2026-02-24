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

        private const int MaxChunkLengthDefault = 500; // Max length of a chunk (in characters)

        private const int MaxChunkLengthMedications = 450; // Max length of a chunk for MEDICATIONS section

        private class ChunkInfo
        {
            public string Text { get; set; }
            public int SectionIndex { get; set; }
            public string SectionName { get; set; }
            public int ChunkIndexInSection { get; set; }
            public int TotalChunksInSection { get; set; }
            public int GlobalChunkIndex { get; set; }
            public int TotalSections { get; set; }
        }

        public Translator(IConfigurationManager _configurationManager)
        {
            configurationManager = _configurationManager;
        }

        private List<string> SplitIntoSections(string text)
        {
            // Delimiter that separates sections in the synthetic dataset
            const string delimiter = "--------------------------------------------------------------------------------";

            // Split by delimiter, clean up, and return each section as its own chunk
            var sections = text
                .Split(new[] { delimiter }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            return sections;
        }

        private List<string> SplitIntoChunks(string text, int maxChunkLength = MaxChunkLengthDefault)
        {
            if (text.Length <= maxChunkLength)
            {
                return new List<string> { text };
            }

            var chunks = new List<string>();
            var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
            var currentChunk = new StringBuilder();

            foreach (var line in lines)
            {
                var lineWithNewline = line + "\n";

                // Check if adding this line would exceed the limit
                if (currentChunk.Length + lineWithNewline.Length > maxChunkLength)
                {
                    // Save current chunk (if it has content) and start a new one
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString());
                        currentChunk.Clear();
                    }
                }

                // Add the line to the current chunk
                currentChunk.Append(lineWithNewline);
            }

            // Add the remaining text
            if (currentChunk.Length > 0)
                chunks.Add(currentChunk.ToString().Trim());

            return chunks;
        }

        private List<ChunkInfo> SplitIntoSectionsAndChunks(string text)
        {
            var sections = SplitIntoSections(text);
            var allChunks = new List<ChunkInfo>();
            int globalChunkIndex = 0;

            for (int sectionIndex = 0; sectionIndex < sections.Count; sectionIndex++)
            {
                var section = sections[sectionIndex];
                var sectionName = sectionIndex < DefaultSectionNames.Count
                    ? DefaultSectionNames[sectionIndex]
                    : $"SECTION_{sectionIndex + 1}";

                // Results are better for the OBSERVATIONS section with a smaller chunk size
                int maxChunkLength = sectionName != "OBSERVATIONS" ? MaxChunkLengthDefault : MaxChunkLengthMedications;
                var chunks = SplitIntoChunks(section, maxChunkLength);

                for (int chunkIndexInSection = 0; chunkIndexInSection < chunks.Count; chunkIndexInSection++)
                {
                    allChunks.Add(new ChunkInfo
                    {
                        Text = chunks[chunkIndexInSection],
                        SectionIndex = sectionIndex,
                        SectionName = sectionName,
                        ChunkIndexInSection = chunkIndexInSection,
                        TotalChunksInSection = chunks.Count,
                        GlobalChunkIndex = globalChunkIndex++,
                        TotalSections = sections.Count
                    });
                }
            }
            
            return allChunks;
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

        private async Task<string> TranslateChunkAsync(ChunkInfo chunkInfo, int totalChunks, HttpClient httpClient, string endpoint, string sourceLanguage)
        {
            var requestBody = new
            {
                // Phi 4 mini performs better on the MEDICATIONS section translation.
                // Phi 3.5 mini performs better on all other sections.
                model = chunkInfo.SectionName != "MEDICATIONS" ? configurationManager.GetAppSetting("FoundryLocalLanguageModel") : configurationManager.GetAppSetting("FoundryLocalLanguageModel2"),
                messages = new[]
                {
                        new { role = "system", content = "You are a professional medical translator. If a label (e.g., [ATTUALE], [INTERROTTO]) appears, translate it literally (e.g., [CURRENT], [STOPPED])." },
                        new { role = "user", content = $"Translate the following medical data about a patient from {sourceLanguage} into English.\r\n" +
                                                       "Preserve the structure and formatting of the original text as much as possible.\r\n" +
                                                       "Do not add any translator explanations, or notes, or commentary. The only output should be the translated text.\r\n" +
                                                       $"Text:\n{chunkInfo.Text}"
                        }
                    },
                temperature = 0.0
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(endpoint + "/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                var parsed = JObject.Parse(result);
                string translation = parsed["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim() ?? "";
                translation = RemoveUnwantedCommentary(translation);

                return translation;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chunk {chunkInfo.GlobalChunkIndex + 1} [{chunkInfo.SectionName}] failed: {ex.Message}");
                throw;
            }
        }

        private async Task<List<(ChunkInfo chunkInfo, string chunkTranslation)>> MapPhaseAsync(List<ChunkInfo> chunks, string sourceLanguage)
        {
            var endpoints = new[]
            {
                configurationManager.GetAppSetting("FoundryLocalEndPoint")
            };
            var httpClients = endpoints.Select(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(300) }).ToList();

            // Enqueue all of the chunks to be translated
            var chunkQueue = new ConcurrentQueue<ChunkInfo>(chunks);

            // Store the chunk translations as they complete
            var translations = new ConcurrentBag<(ChunkInfo chunkInfo, string chunkTranslation)>();

            // Use one worker per server endpoint, each pulling from the shared queue as they complete work
            var workers = new List<Task>();
            for (int i = 0; i < endpoints.Length; i++)
            {
                int endpointIndex = i;
                var worker = Task.Run(async () =>
                {
                    var httpClient = httpClients[endpointIndex];
                    var endpoint = endpoints[endpointIndex];

                    while (chunkQueue.TryDequeue(out var chunkInfo))
                    {
                        try
                        {
                            Console.WriteLine($"Sending section {chunkInfo.SectionIndex + 1}/{chunkInfo.TotalSections} (chunk {chunkInfo.ChunkIndexInSection + 1}/{chunkInfo.TotalChunksInSection}) ({chunkInfo.Text.Length} chars) to Language Model server at {endpoint}");
                            var chunkTranslation = await TranslateChunkAsync(
                                                            chunkInfo,
                                                            chunks.Count,
                                                            httpClient,
                                                            endpoint,
                                                            sourceLanguage
                                                         );
                            Console.WriteLine($"[{endpoint}] Successfully translated section {chunkInfo.SectionIndex + 1} [{chunkInfo.SectionName}] (chunk {chunkInfo.ChunkIndexInSection + 1}/{chunkInfo.TotalChunksInSection}) ({chunkTranslation.Length} chars)");

                            translations.Add((chunkInfo, chunkTranslation));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{endpoint}] Failed during translated of section {chunkInfo.SectionIndex + 1} [{chunkInfo.SectionName}] (chunk {chunkInfo.ChunkIndexInSection + 1}/{chunkInfo.TotalChunksInSection}) with exception: {ex.Message}");
                            throw new TranslateFailedException(ex);
                        }
                    }
                });

                workers.Add(worker);
            }

            await Task.WhenAll(workers);

            // Order translations by their original global index
            var orderedTranslations = translations
                .OrderBy(c => c.chunkInfo.GlobalChunkIndex)
                .ToList();

            return orderedTranslations;
        }

        private string AssembleTranslation(List<(ChunkInfo chunkInfo, string chunkTranslation)> translations)
        {
            const string delimiter = "\n--------------------------------------------------------------------------------";
            var result = new StringBuilder();
            int currentSectionIndex = -1;

            foreach (var (chunkInfo, translation) in translations)
            {
                // Add delimiter between sections
                if (chunkInfo.SectionIndex != currentSectionIndex)
                {
                    if (currentSectionIndex != -1)
                        result.AppendLine(delimiter);
                    currentSectionIndex = chunkInfo.SectionIndex;
                }

                // Replace first line with section title, except for demographics
                var processedTranslation = translation;
                if (chunkInfo.ChunkIndexInSection == 0 &&
                    !string.Equals(chunkInfo.SectionName, "DEMOGRAPHICS", StringComparison.OrdinalIgnoreCase))
                {
                    var lines = processedTranslation
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();

                    if (lines.Count > 0)
                    {
                        lines[0] = chunkInfo.SectionName;
                        processedTranslation = string.Join("\n", lines);
                    }
                }

                if (chunkInfo.ChunkIndexInSection > 0)
                    result.AppendLine();

                result.Append(processedTranslation);
            }

            return result.ToString();
        }

        public async Task<string> RunAsync(string inputText, string sourceLanguage)
        {
            // Split input document into chunks
            var chunks = SplitIntoSectionsAndChunks(inputText);

            // Translate all chunks
            var translatedChunks = await MapPhaseAsync(chunks, sourceLanguage);

            // Construct translated document
            var translation = AssembleTranslation(translatedChunks);

            return translation;
        }
    }
}
