using PatientSummaryTool.Models;
using PatientSummaryTool.Models.Objects;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PatientSummaryTool.Utils
{
    public class FileManager : IFile
    {
        private readonly IConfigurationManager configurationManager;

        public FileManager(IConfigurationManager _configurationManager)
        {
            configurationManager = _configurationManager;
        }

        public virtual bool Exists(string path)
        {
            return File.Exists(path);
        }

        public virtual async Task<string> ReadAllText(Patient patient)
        {
            string readFolderPath = configurationManager.GetAppSetting("LocalDataDirectory");
            string readFileName = patient.IsTranslationCompleted ? $"{patient.FirstName}_{patient.LastName}.txt" : $"{patient.FirstName}_{patient.LastName}_{patient.SourceLanguage}.txt";
            try
            {
                using (StreamReader streamReader = new StreamReader(Path.Combine(readFolderPath, readFileName), Encoding.UTF8))
                {
                    return await streamReader.ReadToEndAsync();
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"The file could not be read: {ex.Message}");
                throw;
            }
        }

        public virtual void UploadFile(Patient patient, string filePath)
        {
            string destinationFolderPath = configurationManager.GetAppSetting("LocalDataDirectory");
            string newFileName = string.Equals(patient.SourceLanguage, "English", StringComparison.OrdinalIgnoreCase) ? $"{patient.FirstName}_{patient.LastName}.txt" : $"{patient.FirstName}_{patient.LastName}_{patient.SourceLanguage}.txt";
            string destinationFilePath = Path.Combine(destinationFolderPath, newFileName);
            if (!Directory.Exists(destinationFolderPath))
            {
                Directory.CreateDirectory(destinationFolderPath);
            }
            try
            {
                File.Copy(filePath, destinationFilePath, true); // true to overwrite if exists
            }
            catch (IOException ex)
            {
                Console.WriteLine($"The file could not be copied: {ex.Message}");
                throw;
            }
        }

        public virtual async Task WriteAllText(string text, Patient patient)
        {
            string destinationFolderPath = configurationManager.GetAppSetting("LocalDataDirectory");
            string newFileName = $"{patient.FirstName}_{patient.LastName}.txt";
            string destinationFilePath = Path.Combine(destinationFolderPath, newFileName);
            if (!Directory.Exists(destinationFolderPath))
            {
                Directory.CreateDirectory(destinationFolderPath);
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(destinationFilePath, false))
                {
                    await writer.WriteAsync(text);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"The file could not be written: {ex.Message}");
                throw;
            }
        }
    }
}
