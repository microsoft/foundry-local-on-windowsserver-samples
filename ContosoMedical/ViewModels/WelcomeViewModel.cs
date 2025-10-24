using PatientSummaryTool.Models;
using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.Events;

namespace PatientSummaryTool.ViewModels
{
    internal class WelcomeViewModel : BindableBase
    {
        private readonly string PageTitle = Properties.Resources.WelcomePageTitle;
        private readonly ChildToMainViewModelEvent childToMainViewModelEvent;
        private readonly Selections selections;
        public DelegateCommand PatientLookupCommand { get; set; }

        public WelcomeViewModel(Selections _selections, ChildToMainViewModelEvent _childToMainViewModelEvent)
        {
            childToMainViewModelEvent = _childToMainViewModelEvent;
            selections = _selections;
            PatientLookupCommand = new DelegateCommand(OnPatientLookup);
        }

        public void LoadWelcome()
        {
            childToMainViewModelEvent.OnLoadTitle(PageTitle);
        }

        public void OnPatientLookup()
        {
            childToMainViewModelEvent.OnPatientLookup();
        }
    }
}
