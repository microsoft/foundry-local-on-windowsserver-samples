using System.Diagnostics;
using System.Threading.Tasks;

namespace PatientSummaryTool.Utils
{
    public interface IProcess
    {
        int StartAndWaitForExit(Process process);
        void Start(ProcessStartInfo processStartInfo);
    }
}
