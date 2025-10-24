using PatientSummaryTool.Models.Objects;
using PatientSummaryTool.Utils;
using System.Collections.ObjectModel;

namespace PatientSummaryTool.Models
{
    public class Selections : ValidatableBindableBase
    {

        private ObservableCollection<Patient> patients;
        public ObservableCollection<Patient> Patients
        {
            get { return patients; }
            set { SetProperty(ref patients, value); }
        }

        private Patient patientSelected;
        public Patient PatientSelected
        {
            get { return patientSelected; }
            set { SetProperty(ref patientSelected, value); }
        }

        public void ClearSelections()
        {
            Patients = null;
            PatientSelected = null;
        }
    }
}
