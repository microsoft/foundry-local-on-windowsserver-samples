using Microsoft.Win32;
using PatientSummaryTool.Models;
using System.Windows.Forms; // Requires reference to System.Windows.Forms.dll

namespace PatientSummaryTool.Utils
{

    public class DialogService : IDialogService
    {
        public string OpenFile(string filter, string initialDirectory)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }
            return null;
        }

        public string OpenFolder(string initialDirectory)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                SelectedPath = initialDirectory
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
            return null;
        }
    }
}