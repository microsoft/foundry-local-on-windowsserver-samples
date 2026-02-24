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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PatientSummaryTool.Services
{
    public class Summarizer : BindableBase, ISummarizer
    {
        private readonly IConfigurationManager configurationManager;

        private readonly int _maxMapTokens = 200;
        private readonly int _maxReduceTokens = 1000;
        private const int MaxSectionLength = 3000; // Max length of a section (in characters) before further splitting is needed

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

        // Split a section into chunks at newline boundaries, each under MaxSectionLength
        // Handles extreme cases where a single section exceeds the model's token limit
        private List<string> SplitSectionIntoChunks(string section)
        {
            if (section.Length <= MaxSectionLength)
            {
                return new List<string> { section };
            }

            var chunks = new List<string>();
            var lines = section.Split(new[] { '\n' }, StringSplitOptions.None);
            var currentChunk = new StringBuilder();

            foreach (var line in lines)
            {
                // Check if adding this line would exceed the limit
                var lineWithNewline = line + "\n";
                if (currentChunk.Length + lineWithNewline.Length > MaxSectionLength && currentChunk.Length > 0)
                {
                    // Save current chunk and start a new one
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }

                currentChunk.Append(lineWithNewline);
            }

            // Add the last chunk if it has content
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }

        private async Task<string> SummarizeChunkAsync(string chunk, HttpClient httpClient, string endpoint)
        {
            string model = configurationManager.GetAppSetting("FoundryLocalLanguageModel");

            var requestBody = new
            {
                model,
                messages = new[]
                {
                new { role = "system", content = "You are a precise summarization assistant of a patient's record. You'll be presented with one or more sections of a patient's medical record." },
                new { role = "user", content = $"Generate a concise summary of the following medical record section(s), prioritizing the most recent information. DO NOT exceed {_maxMapTokens} tokens. DO NOT include the token count in the summary\n\nText:\n{chunk}" }
            },
                max_tokens = _maxMapTokens,
                temperature = 0.0,
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync(endpoint + "/v1/chat/completions", content);
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

        // Map Phase: Summarize each section (or chunk of a section) independently
        // Parallelizes work by distributing chunks across available endpoints using a shared queue
        private async Task<List<string>> MapPhaseAsync(Dictionary<string, string> sections, IProgress<SectionSummary> progress = null)
        {
            var endpoints = new[]
            {
                configurationManager.GetAppSetting("FoundryLocalEndPoint")
            };
            var httpClients = endpoints.Select(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(300) }).ToList();

            // Enqueue all of the chunks to be summarized
            var chunkQueue = new ConcurrentQueue<(string chunk, int sectionIndex, int chunkIndex, int totalChunks, string sectionName)>();
            int sectionNumber = 0;
            foreach (KeyValuePair<string, string> section in sections)
            {
                // A section is consider a single chunk, unless it is too large. If so, split it further.
                var chunks = SplitSectionIntoChunks(section.Value);
                Console.WriteLine($"Section {sectionNumber} [{section.Key}] split into {chunks.Count} chunk(s)");

                for (int chunkNumber = 0; chunkNumber < chunks.Count; chunkNumber++)
                {
                    chunkQueue.Enqueue((chunks[chunkNumber], sectionNumber, chunkNumber, chunks.Count, section.Key));
                }
                sectionNumber++;
            }

            // Store the chunk summaries as they complete
            var chunkSummaries = new ConcurrentBag<(int sectionIndex, int chunkIndex, string summary)>();

            // Use one worker per server endpoint, each pulling from the shared queue as they complete work
            var workers = new List<Task>();
            for (int i = 0; i < endpoints.Length; i++)
            {
                int endpointIndex = i;
                var worker = Task.Run(async () =>
                {
                    var httpClient = httpClients[endpointIndex];
                    var endpoint = endpoints[endpointIndex];

                    while (chunkQueue.TryDequeue(out var workItem))
                    {
                        var (chunk, sectionIndex, chunkIndex, totalChunks, sectionName) = workItem;
                        if (string.IsNullOrWhiteSpace(chunk))
                        {
                            // Skip empty chunks
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
                        var chunkInfo = totalChunks > 1 ? $" (chunk {chunkIndex + 1}/{totalChunks})" : "";
                        try
                        {
                            Console.WriteLine($"Sending section {sectionIndex + 1}/{sections.Count}{chunkInfo} ({chunk.Length} chars) to Language Model server at {endpoint}");
                            var summary = await SummarizeChunkAsync(
                                                    chunk,
                                                    httpClient,
                                                    endpoint
                                                );
                            Console.WriteLine($"[{endpoint}] Successfully summarized section {sectionIndex + 1} [{sectionName}]{chunkInfo} ({summary.Length} chars)");

                            chunkSummaries.Add((sectionIndex, chunkIndex, summary));

                            // Report progress only for the first chunk of each section
                            if (chunkIndex == 0 && !string.IsNullOrWhiteSpace(summary))
                            {
                                progress?.Report(new SectionSummary
                                {
                                    SectionName = sectionName,
                                    Summary = summary,
                                    IsSuccess = true,
                                    Index = sectionIndex + 1,
                                    Total = sections.Count
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[{endpoint}] Failed during summarization of section {sectionIndex + 1} [{sectionName}]{chunkInfo} with exception: {ex.Message}");

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

                workers.Add(worker);
            }

            await Task.WhenAll(workers);

            // Group chunk summaries by section and combine them
            var sectionSummaries = chunkSummaries
                .Where(r => !string.IsNullOrWhiteSpace(r.summary))
                .GroupBy(r => r.sectionIndex)
                .OrderBy(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(x => x.chunkIndex).Select(x => x.summary)))
                .ToList();

            return sectionSummaries;
        }

        // Reduce Phase: Combine summaries into final summary
        private async Task<string> ReducePhaseAsync(List<string> summaries, IProgress<string> streamProgress = null)
        {
            var endpoints = new[]
            {
                configurationManager.GetAppSetting("FoundryLocalEndPoint")
            };
            var httpClients = endpoints.Select(_ => new HttpClient { Timeout = TimeSpan.FromSeconds(300) }).ToList();

            string model = configurationManager.GetAppSetting("FoundryLocalLanguageModel");
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
                    "DO NOT exceed 300 words in generating the final summary.\n\n" +
                    "SUMMARIES:" +
                    intermediateSummariesConcatenated
                }},
                max_tokens = _maxReduceTokens,
                temperature = 0.2,
                stream = true
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoints[0] + "/v1/chat/completions")
                {
                    Content = content
                };

                var response = await httpClients[0].SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
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
                                var chunk = JObject.Parse(data);
                                var deltaContent = chunk["choices"]?[0]?["delta"]?["content"]?.ToString();

                                if (!string.IsNullOrEmpty(deltaContent))
                                {
                                    fullContent.Append(deltaContent);
                                    
                                    // Print to the console (for debugging before adding to the UI)
                                    Console.Write(deltaContent);

                                    // Report each chunk so that the UI can update final summary in real-time
                                    streamProgress?.Report(deltaContent);

                                }
                            }
                            catch (JsonException ex)
                            {
                                Console.WriteLine($"\nError when parsing chunk: {ex.Message}");
                                throw new SummaryFailedException("Error when parsing chunk for streaming", ex);
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
