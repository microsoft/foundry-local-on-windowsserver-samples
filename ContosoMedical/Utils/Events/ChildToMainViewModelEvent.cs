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

        public virtual void OnBack()
        {
            Back();
        }
        public event Action Back = delegate { };

        public virtual void OnAddNewPatient()
        {
            AddNewPatient();
        }
        public event Action AddNewPatient = delegate { };

        public virtual void OnPatientLookup()
        {
            PatientLookup();
        }
        public event Action PatientLookup = delegate { };
    }
}
