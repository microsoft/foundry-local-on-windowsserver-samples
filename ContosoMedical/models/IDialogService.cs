namespace PatientSummaryTool.Models
{
    internal interface IDialogService
    {
        string OpenFile(string filter, string initialDirectory);
        string OpenFolder(string initialDirectory);
    }
}
