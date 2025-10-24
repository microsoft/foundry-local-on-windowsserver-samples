using System;

namespace PatientSummaryTool.Utils.Events
{
    public class ChildToMainViewModelEvent
    {
        public virtual void OnLoadTitle(string pageTitle)
        {
            LoadTitle(pageTitle);
        }
        public event Action<string> LoadTitle = delegate { };

        public virtual void OnAddNewPatient()
        {
            AddNewPatient();
        }
        public event Action AddNewPatient = delegate { };

        public virtual void OnAddNewPatientCompleted()
        {
            AddNewPatientCompleted();
        }
        public event Action AddNewPatientCompleted = delegate { };

        public virtual void OnPatientLookup()
        {
            PatientLookup();
        }
        public event Action PatientLookup = delegate { };
    }
}
