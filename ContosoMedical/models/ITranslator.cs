using System.Threading.Tasks;

namespace PatientSummaryTool.Models
{
    public interface ITranslator
    {
        Task<string> RunAsync(string inputText, string inputLanguage);
    }
}
