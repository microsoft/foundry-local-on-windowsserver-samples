using PatientSummaryTool.Models;
using PatientSummaryTool.Models.Objects;
using PatientSummaryTool.Utils;
using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;

namespace PatientSummaryTool.Services
{
    public class PatientsRepository : BindableBase, IPatientsRepository
    {
        private readonly IConfigurationManager configurationManager;
        private readonly IFile file;
        private readonly Selections selections;
        private readonly ITranslator translator;
        private readonly ISummarizer summarizer;
        private readonly ITranslationRequestRepository translationRequestRepository;

        public event Action<string, string, bool> IntermediateSummaryFetched = delegate { };
        public event Action<string> FinalSummaryFetched = delegate { };
        public event Action TranslationCompleted = delegate { };

        private SectionSummary intermediateSummary;
        public SectionSummary IntermediateSummary
        {
            get { return intermediateSummary; }
            set
            {
                SetProperty(ref intermediateSummary, value);
                OnIntermediateSummaryFetched(intermediateSummary.SectionName, intermediateSummary.Summary, intermediateSummary.IsSuccess);
            }
        }

        private string finalSummary;
        public string FinalSummary
        {
            get { return finalSummary; }
            set
            {
                SetProperty(ref finalSummary, value);
                OnFinalSummaryFetched(finalSummary);
            }
        }

        public PatientsRepository(IFile _file, Selections _selections, IConfigurationManager _configurationManager, ITranslator _translator, ISummarizer _summarizer, ITranslationRequestRepository _translationRequestRepository)
        {
            file = _file;
            selections = _selections;
            configurationManager = _configurationManager;
            translator = _translator;
            summarizer = _summarizer;
            translationRequestRepository = _translationRequestRepository;
        }

        public ObservableCollection<Patient> GetPatients()
        {
            var patientData = new ObservableCollection<Patient>();

            Assembly assembly = Assembly.GetExecutingAssembly();

            // The compiled resource name — e.g., "PatientSummaryTool.g.resources"
            string resourceName = assembly.GetName().Name + ".g.resources";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Resource stream '{resourceName}' not found."))
            using (ResourceReader reader = new ResourceReader(stream))
            {
                foreach (DictionaryEntry entry in reader)
                {
                    string key = (string)entry.Key;
                    if (key.Split('/').Length > 1 && key.Split('/')[0] == "defaultdataassets" && key.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(key);
                        var nameParts = fileName.Split('_');

                        if (nameParts.Length >= 2)
                        {
                            string firstName = nameParts[0];
                            string lastName = nameParts[1];
                            patientData.Add(new Patient { Id = patientData.Count + 1, FirstName = firstName, LastName = lastName, IsTranslationCompleted = true, SourceLanguage = "English" });
                        }
                    }
                }
            }

            return patientData;
        }

        public async Task<string> GetPatientDetails(Patient patient)
        {
            var details = await file.ReadAllText(patient);

            return details;
        }

        public async Task<string> GetPatientSummary(Patient patient)
        {
            var details = await file.ReadAllText(patient);

            IProgress<SectionSummary> intermediateSummaryProgress = new Progress<SectionSummary>(s => IntermediateSummary = s);
            IProgress<string> finalSummaryProgress = new Progress<string>(s => FinalSummary = s);
            string summary = await summarizer.RunAsync(details, intermediateSummaryProgress, finalSummaryProgress);

            return summary;
        }

        public async Task GetPatientTranslation(Patient patient)
        {
            try
            {
                translationRequestRepository.AddTranslationRequest(patient);
                var details = await file.ReadAllText(patient);

                string translation = await translator.RunAsync(details, patient.SourceLanguage);

                selections.Patients[patient.Id - 1].IsTranslationCompleted = true;

                await file.WriteAllText(translation, patient);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                translationRequestRepository.RemoveTranslationRequest(patient);
            }
        }

        public void AddPatientDetails(string firstName, string lastName, string sourceLanguage, string filePath)
        {
            int id = selections.Patients.Count + 1;
            Patient newPatient = new Patient { Id = id, FirstName = firstName, LastName = lastName, SourceLanguage = string.IsNullOrEmpty(sourceLanguage) ? "English" : sourceLanguage, IsTranslationCompleted = string.IsNullOrEmpty(sourceLanguage) ? true : false };
            selections.Patients.Add(newPatient);
            selections.PatientSelected = newPatient;

            file.UploadFile(newPatient, filePath);
        }

        public virtual void OnIntermediateSummaryFetched(string sectionName, string summary, bool isSuccess)
        {
            IntermediateSummaryFetched(sectionName, summary, isSuccess);
        }

        public virtual void OnFinalSummaryFetched(string summary)
        {
            FinalSummaryFetched(summary);
        }
    }
}
