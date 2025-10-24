using PatientSummaryTool.Models;
using PatientSummaryTool.Models.Objects;
using System.Collections.Generic;

namespace PatientSummaryTool.Services
{
    public class TranslationRequestRepository : ITranslationRequestRepository
    {
        private HashSet<int> currentTranslationRequests = new HashSet<int>();

        public void AddTranslationRequest(Patient patient)
        {
            currentTranslationRequests.Add(patient.Id);
        }

        public void RemoveTranslationRequest(Patient patient)
        {
            currentTranslationRequests.Remove(patient.Id);
        }

        public bool IsTranslationRequested(Patient patient)
        {
            return currentTranslationRequests.Contains(patient.Id);
        }
    }
}
