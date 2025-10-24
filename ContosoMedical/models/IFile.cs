using PatientSummaryTool.Models.Objects;
using System.Threading.Tasks;

namespace PatientSummaryTool.Utils
{
    public interface IFile
    {
        bool Exists(string path);
        Task<string> ReadAllText(Patient patient);
        void UploadFile(Patient patient, string filePath);
        Task WriteAllText(string text, Patient patient);
    }
}
