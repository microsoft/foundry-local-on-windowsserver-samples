using PatientSummaryTool.Utils;
using PatientSummaryTool.Utils.Events;
using PatientSummaryTool.ViewModels;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using Unity;

namespace PatientSummaryTool
{
    public class MainWindowViewModel : BindableBase
    {
        private readonly WelcomeViewModel welcomeViewModel;
        private readonly PatientViewModel patientViewModel;
        private readonly AddPatientViewModel addPatientViewModel;
        private readonly ChildToMainViewModelEvent childToMainViewModelEvent;
        private string title;
        private readonly BindableBase[] viewModelsList;
        private int currentViewModelCount = 0;
        private BindableBase currentViewModel;
        private ObservableCollection<Brush> foregroundColors;
        private ObservableCollection<Brush> backgroundColors;


        public BindableBase CurrentViewModel
        {
            get { return currentViewModel; }
            set { SetProperty(ref currentViewModel, value); }
        }
        public string Title
        {
            get { return title; }
            set { SetProperty(ref title, value); }
        }

        public ObservableCollection<Brush> ForegroundColors
        {
            get { return foregroundColors; }
            set { SetProperty(ref foregroundColors, value); }
        }

        public ObservableCollection<Brush> BackgroundColors
        {
            get { return backgroundColors; }
            set { SetProperty(ref backgroundColors, value); }
        }

        private bool isPopupOpen;
        public bool IsPopupOpen
        {
            get { return isPopupOpen; }
            set { SetProperty(ref isPopupOpen, value); }
        }

        public DelegateCommand OpenDisclaimerPopupCommand { get; set; }
        public DelegateCommand CloseDisclaimerPopupCommand { get; set; }

        public MainWindowViewModel(ChildToMainViewModelEvent _childToMainViewModelEvent)
        {
            welcomeViewModel = ContainerHelper.Container.Resolve<WelcomeViewModel>();
            patientViewModel = ContainerHelper.Container.Resolve<PatientViewModel>();
            addPatientViewModel = ContainerHelper.Container.Resolve<AddPatientViewModel>();
            childToMainViewModelEvent = _childToMainViewModelEvent;

            childToMainViewModelEvent.LoadTitle += LoadTitle;
            childToMainViewModelEvent.Back += Back;
            childToMainViewModelEvent.AddNewPatient += AddNewPatient;
            childToMainViewModelEvent.PatientLookup += PatientLookup;

            OpenDisclaimerPopupCommand = new DelegateCommand(OnOpenDisclaimer);
            CloseDisclaimerPopupCommand = new DelegateCommand(OnCloseDisclaimer);

            viewModelsList = new BindableBase[] { welcomeViewModel, patientViewModel, addPatientViewModel };
        }

        public void LoadMain()
        {
            currentViewModelCount = 0;
            CurrentViewModel = viewModelsList[currentViewModelCount];

            ForegroundColors = new ObservableCollection<Brush>(new Brush[viewModelsList.Length]);
            BackgroundColors = new ObservableCollection<Brush>(new Brush[viewModelsList.Length]);

            for (int i = 0; i < viewModelsList.Length; i++)
            {
                SetLabelDefaultColor(i);
            }

            SetLabelSelectedColor(currentViewModelCount);
        }

        private void LoadTitle(string pageTitle)
        {
            Title = pageTitle;
        }

        private void SetLabelSelectedColor(int _currentViewModelCount)
        {
            ForegroundColors[_currentViewModelCount] = Brushes.Black;
            BackgroundColors[_currentViewModelCount] = (Brush)new BrushConverter().ConvertFrom("#F5F5F5");
        }

        private void SetLabelDefaultColor(int _currentViewModelCount)
        {
            ForegroundColors[_currentViewModelCount] = SystemColors.GrayTextBrush;
            BackgroundColors[_currentViewModelCount] = SystemColors.WindowBrush;
        }

        private void PatientLookup()
        {
            SetLabelDefaultColor(currentViewModelCount);
            currentViewModelCount = (currentViewModelCount + 1) % viewModelsList.Length;
            CurrentViewModel = viewModelsList[currentViewModelCount];
            SetLabelSelectedColor(currentViewModelCount);
        }

        private void AddNewPatient()
        {
            currentViewModelCount = (currentViewModelCount + 1) % viewModelsList.Length;
            CurrentViewModel = viewModelsList[currentViewModelCount];
        }

        private void Back()
        {
            currentViewModelCount = (currentViewModelCount - 1) % viewModelsList.Length;
            CurrentViewModel = viewModelsList[currentViewModelCount];
        }

        private void OnOpenDisclaimer()
        {
            IsPopupOpen = true;
        }

        private void OnCloseDisclaimer()
        {
            IsPopupOpen = false;
        }
    }
}
