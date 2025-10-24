using PatientSummaryTool.Models.Objects;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PatientSummaryTool.Models
{
    public interface IPatientsRepository
    {
        ObservableCollection<Patient> GetPatients();
        Task<string> GetPatientDetails(Patient patient);
        Task<string> GetPatientSummary(Patient patient);
        Task GetPatientTranslation(Patient patient);
        void AddPatientDetails(string firstName, string lastName, string sourceLanguage, string filePath);

        event Action<string, string, bool> IntermediateSummaryFetched;
        event Action<string> FinalSummaryFetched;
    }
}
