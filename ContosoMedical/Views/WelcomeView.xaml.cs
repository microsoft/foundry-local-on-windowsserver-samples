using PatientSummaryTool.Utils;
using PatientSummaryTool.ViewModels;
using System.Windows.Controls;
using Unity;

namespace PatientSummaryTool.Views
{
    /// <summary>
    /// Interaction logic for WelcomeView.xaml
    /// </summary>
    public partial class WelcomeView : UserControl
    {
        public WelcomeView()
        {
            InitializeComponent();
            this.DataContext = ContainerHelper.Container.Resolve<WelcomeViewModel>();
        }
    }
}
