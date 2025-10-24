using System;
using System.Collections;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Resources;

namespace PatientSummaryTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                PrepopulateLocalDataDirectory();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to load the application because Startup failed: {ex.Message}", "Error");
                Shutdown();
            }
        }
        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            try
            {
                DeleteLocalDataDirectory();
            }
            catch (Exception)
            {
                // Ignore any exceptions during cleanup
            }
        }

        private static void DeleteLocalDataDirectory()
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string localDataDirectory = configFile.AppSettings.Settings["LocalDataDirectory"].Value;

            if (!string.IsNullOrEmpty(localDataDirectory) && Directory.Exists(localDataDirectory))
            {
                Directory.Delete(localDataDirectory, true);
            }
        }

        private static void PrepopulateLocalDataDirectory()
        {
            var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            string localDataDirectory = configFile.AppSettings.Settings["LocalDataDirectory"].Value;

            if (!string.IsNullOrEmpty(localDataDirectory) && !Directory.Exists(localDataDirectory))
            {
                Directory.CreateDirectory(localDataDirectory);
            }

            Assembly assembly = Assembly.GetExecutingAssembly();

            // The compiled resource name — e.g., "PatientSummaryTool.g.resources"
            string resourceName = assembly.GetName().Name + ".g.resources";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Resource stream '{resourceName}' not found."))
            using (ResourceReader reader = new ResourceReader(stream))
            {
                foreach (DictionaryEntry entry in reader)
                {
                    string key = (string)entry.Key; // example: "assets/marc_marquez.txt"
                    if (key.Split('/').Length > 1 && key.Split('/')[0] == "defaultdataassets" && key.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        CopyApplicationResourcesToLocal(key, Path.Combine(localDataDirectory, Path.GetFileName(key)));
                    }
                }
            }
        }

        private static void CopyApplicationResourcesToLocal(string resourcePath, string localFilePath)
        {
            // Construct the pack URI for the resource
            Uri resourceUri = new Uri($"pack://application:,,/{resourcePath}", UriKind.Absolute);

            // Get the resource stream
            StreamResourceInfo sri = GetResourceStream(resourceUri);

            if (sri != null && sri.Stream != null)
            {
                // Copy the stream to the local file
                using (FileStream fs = new FileStream(localFilePath, FileMode.Create, FileAccess.Write))
                {
                    sri.Stream.CopyTo(fs);
                }
                Console.WriteLine($"File copied successfully from {resourcePath} to {localFilePath}");
            }
            else
            {
                Console.WriteLine($"Resource not found or stream is null for: {resourcePath}");
            }
        }

        private static void ReadApplicationResource(string resourcePath, string localFilePath)
        {
            Uri resourceUri = new Uri($"pack://application:,,/{resourcePath}", UriKind.Absolute);
            var resourceStream = Application.GetResourceStream(resourceUri);
            using (var reader = new StreamReader(resourceStream.Stream))
            {
                string text = reader.ReadToEnd();
            }
        }

    }
}
