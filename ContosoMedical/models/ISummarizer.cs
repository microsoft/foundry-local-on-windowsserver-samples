using PatientSummaryTool.Models.Objects;
using System;
using System.Threading.Tasks;

namespace PatientSummaryTool.Models
{
    public interface ISummarizer
    {
        Task<string> RunAsync(string inputText, IProgress<SectionSummary> sectionsProgress = null, IProgress<string> finalSummaryProgress = null);
    }
}
