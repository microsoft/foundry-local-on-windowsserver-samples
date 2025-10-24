using PatientSummaryTool.Models.Objects;

namespace PatientSummaryTool.Models
{
    public interface ITranslationRequestRepository
    {
        void AddTranslationRequest(Patient patient);
        void RemoveTranslationRequest(Patient patient);
        bool IsTranslationRequested(Patient patient);
    }
}
