namespace PatientSummaryTool.Models
{
    public interface IConfigurationManager
    {
        string GetAppSetting(string key);
    }
}
