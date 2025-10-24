using PatientSummaryTool.Models;

namespace PatientSummaryTool.Utils
{
    public class ConfigurationManager : IConfigurationManager
    {
        public virtual string GetAppSetting(string key)
        {
            return System.Configuration.ConfigurationManager.AppSettings[key];
        }
    }
}
