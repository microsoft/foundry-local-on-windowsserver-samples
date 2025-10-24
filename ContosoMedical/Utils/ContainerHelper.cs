using PatientSummaryTool.Models;
using PatientSummaryTool.Services;
using PatientSummaryTool.Utils.Events;
using Unity;
using Unity.Lifetime;

namespace PatientSummaryTool.Utils
{
    public static class ContainerHelper
    {
        private readonly static IUnityContainer _container;

        static ContainerHelper()
        {
            _container = new UnityContainer();
            _container.RegisterType<Selections>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ChildToMainViewModelEvent>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IProcess, ProcessManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IFile, FileManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IPatientsRepository, PatientsRepository>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ITranslationRequestRepository, TranslationRequestRepository>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ISummarizer, Summarizer>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ITranslator, Translator>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IConfigurationManager, ConfigurationManager>(new ContainerControlledLifetimeManager());
            _container.RegisterType<IDialogService, DialogService>(new ContainerControlledLifetimeManager());
        }

        public static IUnityContainer Container
        {
            get { return _container; }
        }
    }
}
