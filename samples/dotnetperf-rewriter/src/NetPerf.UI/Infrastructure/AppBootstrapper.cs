using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Autofac;
using Caliburn.Micro;
using NetPerf.Common.Configuration;


namespace MetadataDashboard.NetperfUI.Infrastructure {
	internal class AppBootstrapper : Bootstrapper<AppController>, IHandle<DowngradeUIConfigMessage> {

		private readonly ILog _logger = LogManager.GetLog(typeof(AppBootstrapper));
		private Autofac.IContainer m_container;
		private IAppConfig _config;
		private AppController _rootController;

		private bool _saveConfigOnExit;

		public void Handle(DowngradeUIConfigMessage message) {
			if (message != null) {
				_saveConfigOnExit = message.DowngradeConfig;
			}
		}

		protected override void PrepareApplication() {
			base.PrepareApplication();

			// use explicit shutdown so that we can show splash screens/configuration screens before the main shell opens
			Application.ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
		}

		protected override void OnStartup(object sender, System.Windows.StartupEventArgs e) {

			IoC.Get<IEventAggregator>().Subscribe(this);

			var result = AppConfigParser.Read(Utility.ApplicationConfigFilePath);
			_config = result.HasError
				? AppConfigParser.DefaultConfig
				: result.Data;

			// have to call this after the application configuration is loaded
			// or the resolution of its interface will make the application crash.
			DisplayRootView(result.ErrorMessage.StartsWith("Unsupported User Interface configuration file version"));
		}

		private void DisplayRootView(bool handleNewerVersion) {
			// default to FALSE when a newer configuration file has already been saved. 
			// if the user wants to save over it we will get a message that tells us to save the configuration file
			_saveConfigOnExit = !handleNewerVersion;

			_rootController = new AppController(IoC.Get<IWindowManagerEx>(), IoC.Get<IEventAggregator>(), () => {
				Application.Shutdown();
			});
			_rootController.StartApplication(handleNewerVersion);
		}

		protected override void OnExit(object sender, EventArgs e) {
			// Note: this method is called as a result of the "onApplicationExit" delegate passed to the AppController
			//save the application configuration file
			if (_saveConfigOnExit) {
				AppConfigParser.Write(Utility.ApplicationConfigFilePath, _config);
			}

			base.OnExit(sender, e);
		}

		protected override void Configure() { //  configure container

			var builder = new ContainerBuilder();

			//  register view models
			builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())

				//  must be a type that ends with VM
				.Where(type => type.Name.EndsWith("VM"))

				//  must be in a namespace ending with ViewModels
				.Where(type => !(type.Namespace.IsNullOrWhitespace()) && type.Namespace.Contains(".ViewModels"))

				//  must implement INotifyPropertyChanged (deriving from PropertyChangedBase will satisfy this)
				.Where(type => type.GetInterface(typeof(INotifyPropertyChanged).Name) != null)

				//  registered as self
				.AsSelf()

				//  always create a new one
				.InstancePerDependency();

			//  register views
			builder.RegisterAssemblyTypes(AssemblySource.Instance.ToArray())

				//  must be a type that ends with View
				//.Where(type => type.Name.EndsWith("View"))

				//  must be in a namespace that ends in Views
				.Where(type => !type.Namespace.IsNullOrWhitespace() && type.Namespace.Contains(".Views"))

				//  registered as self
				.AsSelf()

				//  always create a new one
				.InstancePerDependency();

			builder.Register(c => _config)
				.AsSelf()
				.InstancePerLifetimeScope();


			// SingleInstance() not InstancePerLifetimeScope() is appropriate for these objects
			// (although either would work w/ the way Netperf.UI uses them), because these objects
			// truly should be shared across all scopes.
			builder.Register(c => new WindowManager()).As<IWindowManager>().SingleInstance();
			builder.Register(c => new WindowManagerEx()).As<IWindowManagerEx>().SingleInstance();
			builder.Register(c => new EventAggregator()).As<IEventAggregator>().SingleInstance();

			ConfigureContainer(builder);

			m_container = builder.Build();

			ConfigureCaliburn();
		}

		protected virtual void ConfigureContainer(ContainerBuilder builder) {

		}

		private void ConfigureCaliburn() {
			ConventionManager.ApplyValidation = (bind, typ, info) => {
				bind.ValidatesOnExceptions = true;

			#if DEBUG
				// Note: comment out the following line if you don't want to Debug WPF Bindings. Turning on the Trace does slow down the app.
				//System.Diagnostics.PresentationTraceSources.SetTraceLevel(bind, System.Diagnostics.PresentationTraceLevel.High);
			#endif
				if (typeof(IDataErrorInfo).IsAssignableFrom(typ))
					bind.ValidatesOnDataErrors = true;
			};


			// the convention is that ViewModels end in VM, and Views end in nothing.
			ViewLocator.LocateForModelType = (modelType, displayLocation, context) => {
				var viewTypeName = modelType.FullName.Replace("Model", string.Empty);
				if (viewTypeName.EndsWith("VM"))
					viewTypeName = viewTypeName.Remove(viewTypeName.Length - 2);

				if (context != null) {
					// when a context is provided, look for a view that starts with
					// the name of the ViewModel, but ends w/ the name of the context
					// we're using the underscore b/c C# does not allow periods in class names
					viewTypeName = viewTypeName + "_" + context.ToString();
				}

				var viewType = (from assmebly in AssemblySource.Instance
								from type in assmebly.GetExportedTypes()
								where type.FullName == viewTypeName
								select type).FirstOrDefault();

				return viewType == null
					? new TextBlock {
						Text = string.Format("{0} not found.", viewTypeName)
					}
					: ViewLocator.GetOrCreateViewType(viewType);
			};
		}

		protected override object GetInstance(Type serviceType, string key) {

			if (key.IsNullOrWhitespace() && m_container.IsRegistered(serviceType)) {
				return m_container.Resolve(serviceType);
			}
			else if (m_container.IsRegisteredWithKey(key, serviceType)) {
				return m_container.ResolveNamed(key, serviceType);
			}

			throw new Exception(string.Format("Could not locate any instances of contract {0}.", key ?? serviceType.Name));

		}

		protected override IEnumerable<object> GetAllInstances(Type serviceType) {
			return m_container.Resolve(typeof(IEnumerable<>).MakeGenericType(serviceType)) as IEnumerable<object>;
		}

		protected override void BuildUp(object instance) {
			m_container.InjectProperties(instance);
		}

	}
}
