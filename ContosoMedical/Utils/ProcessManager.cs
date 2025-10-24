using System.Diagnostics;
using System.Threading.Tasks;

namespace PatientSummaryTool.Utils
{
    public class ProcessManager : IProcess
    {
        public virtual int StartAndWaitForExit(Process process)
        {
            process.Start();
            process.EnableRaisingEvents = true;
            process.WaitForExit();
            if (process.HasExited)
            {
                return process.ExitCode;
            }
            return 1;
        }

        public virtual void Start(ProcessStartInfo processStartInfo)
        {
            Process.Start(processStartInfo);
        }
    }
}
