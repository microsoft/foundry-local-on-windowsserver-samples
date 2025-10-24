using PatientSummaryTool.Utils;
using System.Windows;
using Unity;

namespace PatientSummaryTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = ContainerHelper.Container.Resolve<MainWindowViewModel>();
        }
    }
}
