using PatientSummaryTool.Models;
using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.CustomExceptions;
using PatientSummaryTool.Utils.Events;
using System;
using System.Windows;

namespace PatientSummaryTool.ViewModels
{
    internal class AddPatientViewModel : BindableBase
    {
        private readonly string PageTitle = Properties.Resources.AddPatientPageTitle;
        private readonly ChildToMainViewModelEvent childToMainViewModelEvent;
        private readonly Selections selections;
        private readonly IPatientsRepository patientsRepository;
        private readonly IDialogService dialogService;

        private string firstName;
        public string FirstName
        {
            get { return firstName; }
            set
            {
                SetProperty(ref firstName, value);
                BrowseReportCommand.RaiseCanExecuteChanged();
            }
        }

        private string lastName;
        public string LastName
        {
            get { return lastName; }
            set
            {
                SetProperty(ref lastName, value);
                BrowseReportCommand.RaiseCanExecuteChanged();
            }
        }

        private string sourceLanguage;
        public string SourceLanguage
        {
            get { return sourceLanguage; }
            set
            {
                SetProperty(ref sourceLanguage, value);
            }
        }

        public DelegateCommand BrowseReportCommand { get; set; }

        public AddPatientViewModel(Selections _selections, ChildToMainViewModelEvent _childToMainViewModelEvent, IPatientsRepository _patientsRepository, IDialogService _dialogService)
        {
            childToMainViewModelEvent = _childToMainViewModelEvent;
            selections = _selections;
            patientsRepository = _patientsRepository;
            dialogService = _dialogService;

            BrowseReportCommand = new DelegateCommand(OnBrowseReport, CanBrowseReport);
        }

        public void LoadAddPatient()
        {
            childToMainViewModelEvent.OnLoadTitle(PageTitle);
        }

        public void OnBrowseReport()
        {
            string filePath = dialogService.OpenFile("Text files (*.txt)|*.txt|All files (*.*)|*.*", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    patientsRepository.AddPatientDetails(FirstName, LastName, SourceLanguage, filePath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                childToMainViewModelEvent.OnAddNewPatientCompleted();
            }
        }

        public bool CanBrowseReport()
        {
            return !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName);
        }

    }
}
