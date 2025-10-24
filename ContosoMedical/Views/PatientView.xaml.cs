using PatientSummaryTool.Utils;
using PatientSummaryTool.ViewModels;
using System.Windows.Controls;
using Unity;

namespace PatientSummaryTool.Views
{
    /// <summary>
    /// Interaction logic for PatientView.xaml
    /// </summary>
    public partial class PatientView : UserControl
    {
        public PatientView()
        {
            InitializeComponent();
            this.DataContext = ContainerHelper.Container.Resolve<PatientViewModel>();
        }
    }
}
