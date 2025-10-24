using PatientSummaryTool.Utils;
using PatientSummaryTool.ViewModels;
using System.Windows.Controls;
using Unity;

namespace PatientSummaryTool.Views
{
    /// <summary>
    /// Interaction logic for AddPatientView.xaml
    /// </summary>
    public partial class AddPatientView : UserControl
    {
        public AddPatientView()
        {
            InitializeComponent();
            this.DataContext = ContainerHelper.Container.Resolve<AddPatientViewModel>();
        }
    }
}
