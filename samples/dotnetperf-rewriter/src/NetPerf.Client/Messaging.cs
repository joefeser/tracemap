using System;
using System.Collections.Generic;
//using Autofac;
//using Autofac.Builder;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using NetPerf.Core;
using NetPerf.Core.Interfaces;

namespace NetPerf.Client {

	[CLSCompliant(false)]
	public static class Messaging {

		private static bool EventsWiredUp = false;

		private static IClientMessageService _service = null;

		public static IClientMessageService Service {
			get {
				//if we do not have a service, create one
				if (_service == null) {
					////register the default.
					//var builder = new ContainerBuilder();
					//Debug.WriteLine("new ContainerBuilder");
					//builder.RegisterType<NamedPipesIMessageProcessorSenderV1>().As<IMessageProcessor>().ExternallyOwned().SingleInstance();
					//builder.RegisterType<CoreClientMessageService>().As<IClientMessageService>().ExternallyOwned().SingleInstance();
					//Debug.WriteLine("RegisterType NamedPipesIMessageProcessorSenderV1 as IClientMessageService");

					//var container = builder.Build();

					//Debug.WriteLine("Resolve IClientMessageService");
					//_service = container.Resolve<IClientMessageService>();
					//SetupService(null, _service, true);
					//Debug.WriteLine("Registerd Service: IClientMessageService" + Messaging.Service != null ? Messaging.Service.GetType().FullName : "<none>");
					//container.Dispose();
					Debug.WriteLine("new Default IClientMessageService Start");
					_service = new CoreClientMessageService(new NamedPipesIMessageProcessorSenderV1());
					Debug.WriteLine("new Default IClientMessageService Complete");
					SetupService(null, _service, true);
				}

				return _service;
			}
			set {
				var oldService = _service;
				_service = value;
				SetupService(oldService, value, false);
			}
		}

		static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args) {
			ProcessAssemblyForMetadata(Service, args.LoadedAssembly);
		}

		static void CurrentDomain_ProcessExit(object sender, EventArgs e) {
			Debug.WriteLine("process exit start");
			if (_service == null) {
				return;
			}
			Service.Stop();
			Debug.WriteLine("process exit end");
		}

		static void CurrentDomain_DomainUnload(object sender, EventArgs e) {
			Debug.WriteLine("appdomain unload start");
			if (_service == null) {
				return;
			}
			Service.Stop();
			Debug.WriteLine("appdomain unload end");
		}

		public static void EnterMethod(int moduleId, int methodId) {
			if (moduleId == 0 || Service == null) {
				return;
			}
			Service.EnterMethod(moduleId, methodId);
		}

		public static void EnterGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
			if (moduleId == 0 || Service == null) {
				return;
			}
			Service.EnterGenericMethod(moduleId, methodId, classTypes, methodTypes);
		}

		public static void ExitMethod(int moduleId, int methodId) {
			if (moduleId == 0 || Service == null) {
				return;
			}
			Service.ExitMethod(moduleId, methodId);
		}

		public static void InstrumentationPointHit(int moduleId, int pointId) {
			if (moduleId == 0 || Service == null) {
				return;
			}
			Service.InstrumentationPointHit(moduleId, pointId);
		}

		public static void ExitGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) {
			if (moduleId == 0 || Service == null) {
				return;
			}
			Service.ExitGenericMethod(moduleId, methodId, classTypes, methodTypes);
		}

		public static void Stop() {
			Debug.WriteLine("Messaging.Stop");
			if (Service != null) {
				Service.Stop();
			}
		}

		private static void ProcessAssemblyForMetadata(IClientMessageService service, Assembly assem) {
			Debug.WriteLine(string.Format("Messaging.ProcessAssemblyForMetadata for assembly:{0} - Start", assem.FullName));
			foreach (var module in assem.GetModules()) {
				//TODO, this should only be in the main module so it may not exist every time
				var data = module.InstrumentationDataString();

				int moduleId = module.ModuleIdentifier();
				if (data != null && moduleId > 0) {
					//TODO determine if it needs to be changed since we may have a collision.
					service.RegisterMetadata(assem.FullName, data);
					//we would set the field back if the module had a collision.
				}
			}
			Debug.WriteLine(string.Format("Messaging.ProcessAssemblyForMetadata for assembly:{0} - End", assem.FullName));
		}

		private static void SetupService(IClientMessageService oldService, IClientMessageService newService, bool initialize) {
			Debug.WriteLine("Messaging.SetupService - Start");
			//disable the old service before we continue
			//DO NOT TOUCH. IT MUST BE LOWER CASE
			if (!initialize && oldService != null) {
				Debug.WriteLine("Service.Stop()");
				//perform on a different thread so it does not get stuck
				var thread = new Thread(new ThreadStart(oldService.Stop));
				thread.IsBackground = true;
				thread.Start();
			}

			if (!EventsWiredUp) {
				Debug.WriteLine("EventsWiredUp");
				AppDomain.CurrentDomain.DomainUnload += new EventHandler(CurrentDomain_DomainUnload);
				AppDomain.CurrentDomain.ProcessExit += new EventHandler(CurrentDomain_ProcessExit);
				AppDomain.CurrentDomain.AssemblyLoad += new AssemblyLoadEventHandler(CurrentDomain_AssemblyLoad);
				EventsWiredUp = true;
			}

			var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

			foreach (var assem in loadedAssemblies) {
				ProcessAssemblyForMetadata(newService, assem);
			}
			Debug.WriteLine("Messaging.SetupService - End");
		}

	}
}
