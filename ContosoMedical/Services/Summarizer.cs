using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PatientSummaryTool.Models;
using PatientSummaryTool.Models.Objects;
using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.CustomExceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PatientSummaryTool.Services
{
    public class Summarizer : BindableBase, ISummarizer
    {
        private readonly IConfigurationManager configurationManager;

        public Summarizer(IConfigurationManager _configurationManager)
        {
            configurationManager = _configurationManager;
        }

        static Dictionary<string, string> SplitIntoSections(string text)
        {
            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string[] headers = new[]
            {
                "PATIENT DETAILS",
                "ALLERGIES",
                "MEDICATIONS",
                "CONDITIONS",
                "CARE PLANS",
                "REPORTS",
                "OBSERVATIONS",
                "PROCEDURES",
                "IMMUNIZATIONS",
                "ENCOUNTERS",
                "IMAGING STUDIES"
            };

            var sections = new Dictionary<string, string>();
            foreach (var header in headers)
                sections[header] = "";

            string currentHeader = "PATIENT DETAILS";
            var buffer = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();

                if (headers.Contains(line.TrimEnd(':'), StringComparer.OrdinalIgnoreCase))
                {
                    sections[currentHeader] = string.Join(Environment.NewLine, buffer).Trim();
                    buffer.Clear();
                    currentHeader = line.TrimEnd(':').ToUpper();
                }

                buffer.Add(rawLine);
            }

            if (buffer.Count > 0)
                sections[currentHeader] = string.Join(Environment.NewLine, buffer).Trim();

            return sections;
        }

        private async Task<string> SummarizeSectionAsync(string sectionContent, HttpClient httpClient, string endpoint)
        {
            string model = configurationManager.GetAppSetting("FoundryLocalLanguageModel");
            string apiKey = configurationManager.GetAppSetting("WsaiGatewayApiKey");

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = "You are a precise summarization assistant of a patient's record. You'll be presented with one or more sections of a patient's medical record." },
                    new { role = "user", content = $"Generate a concise summary of the following medical record section(s), prioritizing the most recent information. DO NOT include the token count in the summary\n\nText:\n{sectionContent}" }
                }
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
                string summary = parsed["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim() ?? "";
                
                return summary;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Map Phase: Summarize each section independently
        // Parallelizes work by distributing sections across available endpoints using a shared queue
        private async Task<List<string>> MapPhaseAsync(Dictionary<string, string> sections, IProgress<SectionSummary> progress = null)
        {
            var endpoint = configurationManager.GetAppSetting("FoundryLocalEndPoint");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

            // Enqueue all of the sections to be summarized
            var sectionQueue = new ConcurrentQueue<(string section, int sectionIndex, string sectionName)>();
            int sectionNumber = 0;
            foreach (KeyValuePair<string, string> section in sections)
            {
                var sectionContent = section.Value;

                sectionQueue.Enqueue((sectionContent, sectionNumber, section.Key));
                sectionNumber++;
            }

            // Store the section summaries as they complete
            var sectionSummaries = new ConcurrentBag<(int sectionIndex, string summary)>();

            await Task.Run(async () =>
            {
                while (sectionQueue.TryDequeue(out var workItem))
                {
                    var (sectionContent, sectionIndex, sectionName) = workItem;
                    if (string.IsNullOrWhiteSpace(sectionContent))
                    {
                        // Skip empty sections
                        progress?.Report(new SectionSummary
                        {
                            SectionName = sectionName,
                            Summary = string.Empty,
                            IsSuccess = true,
                            Index = sectionIndex + 1,
                            Total = sections.Count
                        });
                        continue;
                    }
                    try
                    {
                        Console.WriteLine($"Sending section {sectionIndex + 1}/{sections.Count} ({sectionContent.Length} chars) to Language Model server at {endpoint}");
                        var summary = await SummarizeSectionAsync(
                                                sectionContent,
                                                httpClient,
                                                endpoint
                                            );
                        Console.WriteLine($"[{endpoint}] Successfully summarized section {sectionIndex + 1} [{sectionName}] ({summary.Length} chars)");

                        sectionSummaries.Add((sectionIndex, summary));

                        progress?.Report(new SectionSummary
                        {
                            SectionName = sectionName,
                            Summary = summary,
                            IsSuccess = true,
                            Index = sectionIndex + 1,
                            Total = sections.Count
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{endpoint}] Failed during summarization of section {sectionIndex + 1} [{sectionName}] with exception: {ex.Message}");

                        progress?.Report(new SectionSummary
                        {
                            SectionName = sectionName,
                            Summary = null,
                            IsSuccess = false,
                            Index = sectionIndex + 1,
                            Total = sections.Count
                        });
                        throw new SummaryFailedException(ex);
                    }
                }
            });

            return sectionSummaries.Select(s => s.summary).ToList();
        }

        // Reduce Phase: Combine summaries into final summary
        private async Task<string> ReducePhaseAsync(List<string> summaries, IProgress<string> streamProgress = null)
        {
            var endpoint = configurationManager.GetAppSetting("FoundryLocalEndPoint");
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(300) };

            string model = configurationManager.GetAppSetting("FoundryLocalLanguageModel");
            string apiKey = configurationManager.GetAppSetting("WsaiGatewayApiKey");
            if (summaries.Count == 0)
                return "No summaries to reduce.";

            var intermediateSummariesConcatenated = string.Join("\n\n", summaries);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                new { role = "system", content = "You are an expert summarizer that merges multiple summaries into one cohesive overview in at most 300 words." },
                new { role = "user", content =
                    "You will be given multiple summaries of a medical report. " +
                    "Generate one final concise summary with emphasis on medical data which include details in the following context: (Patient details, allergies, medication, conditions, procedures, treatments, doctor or provider visits and clinical results). " +
                    "SUMMARIES:" +
                    intermediateSummariesConcatenated
                }},
                stream = true
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, endpoint + "/v1/chat/completions") { Content = content };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Build the full response from the stream
                var fullContent = new StringBuilder();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        // Parse SSE format: "data: {json}"
                        if (line.StartsWith("data: "))
                        {
                            var data = line.Substring(6);

                            // Check for end of stream
                            if (data == "[DONE]")
                            {
                                break;
                            }

                            try
                            {
                                var section = JObject.Parse(data);
                                var deltaContent = section["choices"]?[0]?["delta"]?["content"]?.ToString();

                                if (!string.IsNullOrEmpty(deltaContent))
                                {
                                    fullContent.Append(deltaContent);
                                    
                                    // Print to the console (for debugging before adding to the UI)
                                    Console.Write(deltaContent);

                                    // Report each section so that the UI can update final summary in real-time
                                    streamProgress?.Report(deltaContent);

                                }
                            }
                            catch (JsonException ex)
                            {
                                Console.WriteLine($"\nError when parsing section: {ex.Message}");
                                throw new SummaryFailedException("Error when parsing section for streaming", ex);
                            }
                        }
                    }
                }

                Console.WriteLine("\n"); // New line after streaming completes

                stopwatch.Stop();
                Console.WriteLine($"Final summary generated ({fullContent.Length} chars) - Time: {stopwatch.Elapsed.Minutes:D2}:{stopwatch.Elapsed.Seconds:D2}");

                var result = fullContent.ToString().Trim();
                return string.IsNullOrEmpty(result) ? "(empty)" : result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"Reduce phase failed: {ex.Message} after {stopwatch.Elapsed.TotalSeconds:F2}s.");
                throw new SummaryFailedException(ex);
            }
        }

        public async Task<string> RunAsync(string inputText, IProgress<SectionSummary> sectionsProgress = null, IProgress<string> finalSummaryProgress = null)
        {
            // Split input document into sections
            var sections = SplitIntoSections(inputText);

            // Summarize all sections
            var summaries = await MapPhaseAsync(sections, sectionsProgress);

            // Generate final summary
            Console.WriteLine("STREAMING FINAL SUMMARY");
            return await ReducePhaseAsync(summaries, finalSummaryProgress);
        }
    }
}
