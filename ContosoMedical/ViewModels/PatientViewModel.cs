using PatientSummaryTool.Models;
using PatientSummaryTool.Models.Objects;
using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.CustomExceptions;
using PatientSummaryTool.Utils.Events;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PatientSummaryTool.ViewModels
{
    public enum CheckStatus
    {
        Hidden,
        Loading,
        Success,
        Failure
    }
    internal class PatientViewModel : BindableBase
    {
        private readonly string PageTitle = Properties.Resources.PatientPageTitle;
        private readonly ChildToMainViewModelEvent childToMainViewModelEvent;
        private readonly Selections selections;
        private readonly IPatientsRepository patientsRepository;
        private readonly ITranslationRequestRepository translationRequestRepository;

        private ObservableCollection<Patient> patients;
        public ObservableCollection<Patient> Patients
        {
            get { return patients; }
            set
            {
                SetProperty(ref patients, value);
                selections.Patients = value;
                IsPatientBoxEnabled = patients!= null && patients.Count != 0;
            }
        }

        private Patient patientSelected;
        public Patient PatientSelected
        {
            get { return patientSelected; }
            set
            {
                SetProperty(ref patientSelected, value);
                selections.PatientSelected = value;
                SummarizeCommand.RaiseCanExecuteChanged();
                TranslateCommand.RaiseCanExecuteChanged();
            }
        }

        private string patientDetails;
        public string PatientDetails
        {
            get { return patientDetails; }
            set { SetProperty(ref patientDetails, value); }
        }

        private string patientSummary;
        public string PatientSummary
        {
            get { return patientSummary; }
            set { SetProperty(ref patientSummary, value); }
        }

        private string sectionName;
        public string SectionName
        {
            get { return sectionName; }
            set { SetProperty(ref sectionName, value); }
        }

        private string patientIntermediateSummary;
        public string PatientIntermediateSummary
        {
            get { return patientIntermediateSummary; }
            set { SetProperty(ref patientIntermediateSummary, value); }
        }

        private string patientBoxHeading;
        public string PatientBoxHeading
        {
            get { return patientBoxHeading; }
            set { SetProperty(ref patientBoxHeading, value); }
        }

        private bool summaryLoading;
        public bool SummaryLoading
        {
            get { return summaryLoading; }
            set
            {
                SetProperty(ref summaryLoading, value);
                SummarizeCommand.RaiseCanExecuteChanged();
                IsPatientBoxEnabled = !summaryLoading;
                IsTranslationStatusAvailable = false;
            }
        }

        private bool isTranslationStatusAvailable;
        public bool IsTranslationStatusAvailable
        {
            get { return isTranslationStatusAvailable; }
            set { SetProperty(ref isTranslationStatusAvailable, value); }
        }

        private CheckStatus translationStatus;
        public CheckStatus TranslationStatus
        {
            get { return translationStatus; }
            set { SetProperty(ref translationStatus, value); }
        }

        private bool isPatientBoxEnabled;
        public bool IsPatientBoxEnabled
        {
            get { return isPatientBoxEnabled; }
            set
            {
                SetProperty(ref isPatientBoxEnabled, value);
                AddPatientCommand.RaiseCanExecuteChanged();
            }
        }

        private bool isPopupOpen;
        public bool IsPopupOpen
        {
            get { return isPopupOpen; }
            set { SetProperty(ref isPopupOpen, value); }
        }

        private bool isIntermediateSummariesAvailable;
        public bool IsIntermediateSummariesAvailable
        {
            get { return isIntermediateSummariesAvailable; }
            set { SetProperty(ref isIntermediateSummariesAvailable, value); }
        }

        private CheckStatus allergiesSummaryStatus;
        public CheckStatus AllergiesSummaryStatus
        {
            get { return allergiesSummaryStatus; }
            set { SetProperty(ref allergiesSummaryStatus, value); }
        }

        private CheckStatus medicationsSummaryStatus;
        public CheckStatus MedicationsSummaryStatus
        {
            get { return medicationsSummaryStatus; }
            set { SetProperty(ref medicationsSummaryStatus, value); }
        }

        private CheckStatus conditionsSummaryStatus;
        public CheckStatus ConditionsSummaryStatus
        {
            get { return conditionsSummaryStatus; }
            set { SetProperty(ref conditionsSummaryStatus, value); }
        }

        private CheckStatus careplansSummaryStatus;
        public CheckStatus CareplansSummaryStatus
        {
            get { return careplansSummaryStatus; }
            set { SetProperty(ref careplansSummaryStatus, value); }
        }

        private CheckStatus reportsSummaryStatus;
        public CheckStatus ReportsSummaryStatus
        {
            get { return reportsSummaryStatus; }
            set { SetProperty(ref reportsSummaryStatus, value); }
        }

        private CheckStatus observationsSummaryStatus;
        public CheckStatus ObservationsSummaryStatus
        {
            get { return observationsSummaryStatus; }
            set { SetProperty(ref observationsSummaryStatus, value); }
        }

        private CheckStatus proceduresSummaryStatus;
        public CheckStatus ProceduresSummaryStatus
        {
            get { return proceduresSummaryStatus; }
            set { SetProperty(ref proceduresSummaryStatus, value); }
        }

        private CheckStatus immunizationsSummaryStatus;
        public CheckStatus ImmunizationsSummaryStatus
        {
            get { return immunizationsSummaryStatus; }
            set { SetProperty(ref immunizationsSummaryStatus, value); }
        }

        private CheckStatus encountersSummaryStatus;
        public CheckStatus EncountersSummaryStatus
        {
            get { return encountersSummaryStatus; }
            set { SetProperty(ref encountersSummaryStatus, value); }
        }

        private CheckStatus imagingStudiesSummaryStatus;
        public CheckStatus ImagingStudiesSummaryStatus
        {
            get { return imagingStudiesSummaryStatus; }
            set { SetProperty(ref imagingStudiesSummaryStatus, value); }
        }

        private string AllergiesSummary;

        private string MedicationsSummary;

        private string ConditionsSummary;

        private string CareplansSummary;

        private string ReportsSummary;

        private string ObservationsSummary;

        private string ProceduresSummary;

        private string ImmunizationsSummary;

        private string EncountersSummary;

        private string ImagingStudiesSummary;

        public DelegateCommand<string> NavigateToUrlCommand { get; set; }
        public DelegateCommand PatientSelectionChangedCommand { get; set; }
        public DelegateCommand SummarizeCommand { get; set; }
        public DelegateCommand TranslateCommand { get; set; }
        public DelegateCommand AddPatientCommand { get; set; }
        public DelegateCommand<string> ShowIntermediateSummaryPopupCommand { get; set; }
        public DelegateCommand CloseIntermediateSummaryPopupCommand { get; set; }

        public PatientViewModel(Selections _selections, ChildToMainViewModelEvent _childToMainViewModelEvent, IPatientsRepository _patientsRepository, ITranslationRequestRepository _translationRequestRepository)
        {
            childToMainViewModelEvent = _childToMainViewModelEvent;
            selections = _selections;
            patientsRepository = _patientsRepository;
            translationRequestRepository = _translationRequestRepository;
            NavigateToUrlCommand = new DelegateCommand<string>(OnResourceHyperlink);
            PatientSelectionChangedCommand = new DelegateCommand(OnPatientSelectionChanged);
            SummarizeCommand = new DelegateCommand(OnSummarize, CanSummarize);
            TranslateCommand = new DelegateCommand(OnTranslate, CanTranslate);
            AddPatientCommand = new DelegateCommand(OnAddPatient, CanAddPatient);
            ShowIntermediateSummaryPopupCommand = new DelegateCommand<string>(OnShowIntermediateSummary);
            CloseIntermediateSummaryPopupCommand = new DelegateCommand(OnCloseIntermediateSummary);

            patientsRepository.IntermediateSummaryFetched += IntermediateSummaryFetched;
            patientsRepository.FinalSummaryFetched += FinalSummaryFetched;
        }

        public async void LoadPatient()
        {
            childToMainViewModelEvent.OnLoadTitle(PageTitle);

            IsIntermediateSummariesAvailable = false;
            IsTranslationStatusAvailable = false;
            IsPopupOpen = false;
            ResetSummaryStatuses();

            //Set Patient Options
            Patients = selections.Patients;
            PatientSelected = selections.PatientSelected;
            if (Patients == null)
            {
                Patients = patientsRepository.GetPatients();
                PatientDetails = null;
                PatientSummary = null;
                PatientIntermediateSummary = null;
            }

            if (Patients.Any())
            {
                if (PatientSelected != null)
                {
                    await GetPatientDetails(PatientSelected);

                    if (translationRequestRepository.IsTranslationRequested(PatientSelected))
                    {
                        TranslationStatus = CheckStatus.Loading;
                        IsTranslationStatusAvailable = true;
                    }
                }
            }
        }

        public void OnAddPatient()
        {
            childToMainViewModelEvent.OnAddNewPatient();
        }

        private bool CanAddPatient()
        {
            return IsPatientBoxEnabled;
        }

        private async void OnPatientSelectionChanged()
        {
            if (PatientSelected != null)
            {
                IsIntermediateSummariesAvailable = false;
                IsTranslationStatusAvailable = false;
                ResetSummaryStatuses();

                PatientDetails = null;

                await GetPatientDetails(PatientSelected);

                if (translationRequestRepository.IsTranslationRequested(PatientSelected))
                {
                    TranslationStatus = CheckStatus.Loading;
                    IsTranslationStatusAvailable = true;
                }
            }
        }

        private void ResetUserState()
        {
            //Clear previous selections
            selections.ClearSelections();
        }

        private async void OnSummarize()
        {
            if (PatientSelected != null)
            {
                SummaryLoading = true;
                ResetSummaryStatuses();
                PatientSummary = null;
                IsIntermediateSummariesAvailable = true;

                // Run GetPatientSummary asynchronously in a separate thread
                await Task.Run(async () => await GetPatientSummary(PatientSelected));

                SummaryLoading = false;
            }
        }

        private bool CanSummarize()
        {
            return PatientSelected != null && !SummaryLoading && PatientSelected.IsTranslationCompleted;
        }

        private async void OnTranslate()
        {
            if (PatientSelected != null)
            {
                await TranslatePatientDetails(PatientSelected);
            }
        }

        private bool CanTranslate()
        {
            return PatientSelected != null && !PatientSelected.IsTranslationCompleted && !translationRequestRepository.IsTranslationRequested(PatientSelected);
        }

        public async Task GetPatientDetails(Patient patient)
        {
            try
            {
                IsPatientBoxEnabled = false;
                PatientSummary = null;
                PatientIntermediateSummary = null;
                PatientBoxHeading = Properties.Resources.PatientDetailsTextBoxHeading;
                PatientDetails = await patientsRepository.GetPatientDetails(patient);
                IsPatientBoxEnabled = true;
            }
            catch (Exception ex)
            {
                PatientDetails = Properties.Resources.PatientDetailsErrorMessage;
                MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task GetPatientSummary(Patient patient)
        {
            try
            {
                PatientDetails = null;
                PatientBoxHeading = Properties.Resources.PatientSummaryTextBoxHeading;
                PatientSummary = await patientsRepository.GetPatientSummary(patient);
            }
            catch (SummaryFailedException ex)
            {
                FailSummaryStatuses();
                PatientSummary = Properties.Resources.PatientSummaryErrorMessage;
                MessageBox.Show($"{ex.Message}\n{ex.InnerException.Message}", "Summary Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                FailSummaryStatuses();
                PatientSummary = Properties.Resources.PatientSummaryErrorMessage;
                MessageBox.Show($"{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task TranslatePatientDetails(Patient patient)
        {
            try
            {
                IsTranslationStatusAvailable = true;
                TranslationStatus = CheckStatus.Loading;
                translationRequestRepository.AddTranslationRequest(patient);
                TranslateCommand.RaiseCanExecuteChanged();
                await patientsRepository.GetPatientTranslation(patient);
                patient.IsTranslationCompleted = true;

                if (PatientSelected.Id == patient.Id)
                {
                    TranslationStatus = CheckStatus.Success;
                    await GetPatientDetails(patient);
                }
            }
            catch (TranslateFailedException ex)
            {
                if (PatientSelected.Id == patient.Id)
                {
                    TranslationStatus = CheckStatus.Failure;
                }
                MessageBox.Show($"{ex.Message}\n{ex.InnerException.Message}\nPatient name: {patient.FirstName} {patient.LastName}", "Translate Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                if (PatientSelected.Id == patient.Id)
                {
                    TranslationStatus = CheckStatus.Failure;
                }
                MessageBox.Show($"{ex.Message}\nPatient name: {patient.FirstName} {patient.LastName}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                translationRequestRepository.RemoveTranslationRequest(patient);
                TranslateCommand.RaiseCanExecuteChanged();
                SummarizeCommand.RaiseCanExecuteChanged();
            }
        }

        private void IntermediateSummaryFetched(string sectionName, string summary, bool isSuccess)
        {
            switch (sectionName.ToLower())
            {
                case "allergies":
                    AllergiesSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    AllergiesSummary = RemoveIncompleteSentences(summary);
                    break;
                case "medications":
                    MedicationsSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    MedicationsSummary = RemoveIncompleteSentences(summary);
                    break;
                case "conditions":
                    ConditionsSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ConditionsSummary = RemoveIncompleteSentences(summary);
                    break;
                case "care plans":
                    CareplansSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    CareplansSummary = RemoveIncompleteSentences(summary);
                    break;
                case "reports":
                    ReportsSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ReportsSummary = RemoveIncompleteSentences(summary);
                    break;
                case "observations":
                    ObservationsSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ObservationsSummary = RemoveIncompleteSentences(summary);
                    break;
                case "procedures":
                    ProceduresSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ProceduresSummary = RemoveIncompleteSentences(summary);
                    break;
                case "immunizations":
                    ImmunizationsSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ImmunizationsSummary = RemoveIncompleteSentences(summary);
                    break;
                case "encounters":
                    EncountersSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    EncountersSummary = RemoveIncompleteSentences(summary);
                    break;
                case "imaging studies":
                    ImagingStudiesSummaryStatus = isSuccess ? CheckStatus.Success : CheckStatus.Failure;
                    ImagingStudiesSummary = RemoveIncompleteSentences(summary);
                    break;
                default:
                    break;
            }
        }

        private void FinalSummaryFetched(string summary)
        {
            if (string.IsNullOrEmpty(PatientSummary))
            {
                summary = summary.TrimStart();
            }
            PatientSummary += summary;
        }

        private void OnShowIntermediateSummary(string sectionName)
        {
            SectionName = sectionName;
            switch (sectionName.ToLower())
            {
                case "allergies":
                    PatientIntermediateSummary = string.IsNullOrEmpty(AllergiesSummary) ? "No summary available." : AllergiesSummary;
                    break;
                case "medications":
                    PatientIntermediateSummary = string.IsNullOrEmpty(MedicationsSummary) ? "No summary available." : MedicationsSummary;
                    break;
                case "conditions":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ConditionsSummary) ? "No summary available." : ConditionsSummary;
                    break;
                case "care plans":
                    PatientIntermediateSummary = string.IsNullOrEmpty(CareplansSummary) ? "No summary available." : CareplansSummary;
                    break;
                case "reports":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ReportsSummary) ? "No summary available." : ReportsSummary;
                    break;
                case "observations":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ObservationsSummary) ? "No summary available." : ObservationsSummary;
                    break;
                case "procedures":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ProceduresSummary) ? "No summary available." : ProceduresSummary;
                    break;
                case "immunizations":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ImmunizationsSummary) ? "No summary available." : ImmunizationsSummary;
                    break;
                case "encounters":
                    PatientIntermediateSummary = string.IsNullOrEmpty(EncountersSummary) ? "No summary available." : EncountersSummary;
                    break;
                case "imaging studies":
                    PatientIntermediateSummary = string.IsNullOrEmpty(ImagingStudiesSummary) ? "No summary available." : ImagingStudiesSummary;
                    break;
                default:
                    break;
            }
            IsPopupOpen = true;
        }

        private void OnCloseIntermediateSummary()
        {
            IsPopupOpen = false;
        }

        private void OnResourceHyperlink(string url)
        {
            Process.Start(url);
        }

        private string RemoveIncompleteSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Split text into sentences based on common sentence terminators
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var completeSentences = sentences
                .Where(IsCompleteSentence)
                .ToList();

            return string.Join(" ", completeSentences);
        }

        private bool IsCompleteSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;

            sentence = sentence.Trim();

            // Check if sentence ends with proper punctuation
            if (!Regex.IsMatch(sentence, @"[.!?]$"))
                return false;

            // Check minimum length (at least a few characters)
            if (sentence.Length < 3)
                return false;

            // Check if sentence has at least one word character
            if (!Regex.IsMatch(sentence, @"\w+"))
                return false;

            // Check for common incomplete patterns
            if (Regex.IsMatch(sentence, @"^(and|or|but|because|however|therefore)\s",
                RegexOptions.IgnoreCase))
                return false;

            return true;
        }

        private void ResetSummaryStatuses()
        {
            AllergiesSummaryStatus = CheckStatus.Loading;
            MedicationsSummaryStatus = CheckStatus.Loading;
            ConditionsSummaryStatus = CheckStatus.Loading;
            CareplansSummaryStatus = CheckStatus.Loading;
            ReportsSummaryStatus = CheckStatus.Loading;
            ObservationsSummaryStatus = CheckStatus.Loading;
            ProceduresSummaryStatus = CheckStatus.Loading;
            ImmunizationsSummaryStatus = CheckStatus.Loading;
            EncountersSummaryStatus = CheckStatus.Loading;
            ImagingStudiesSummaryStatus = CheckStatus.Loading;
        }

        private void FailSummaryStatuses()
        {
            AllergiesSummaryStatus = CheckStatus.Failure;
            MedicationsSummaryStatus = CheckStatus.Failure;
            ConditionsSummaryStatus = CheckStatus.Failure;
            CareplansSummaryStatus = CheckStatus.Failure;
            ReportsSummaryStatus = CheckStatus.Failure;
            ObservationsSummaryStatus = CheckStatus.Failure;
            ProceduresSummaryStatus = CheckStatus.Failure;
            ImmunizationsSummaryStatus = CheckStatus.Failure;
            EncountersSummaryStatus = CheckStatus.Failure;
            ImagingStudiesSummaryStatus = CheckStatus.Failure;
        }
    }
}
