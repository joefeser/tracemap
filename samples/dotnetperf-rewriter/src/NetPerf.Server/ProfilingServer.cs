using System;
using System.ServiceModel;
using NetPerf.Client;
using NetPerf.Common.Instrumentation;
using NetPerf.Core.Interfaces;

namespace NetPerf.Server {

	[CLSCompliant(false)]
	public static class ProfilingServer {

		private static ServiceHost _host;
		private static IMessagingContract _processor;

		//TODO add support for Start that takes in a PrintStringMessageProcessor 

		//public static void Start(Action<string> printMessage, IInstrumentationData data) {
		//    Start(new PrintStringMessageProcessor(printMessage, data));
		//}

		public static void Start(IMessageProcessor proc) {

			_processor = new MessageReciever(proc);

			_host = new ServiceHost(_processor);
			_host.AddServiceEndpoint(typeof(IMessagingContract), new NetNamedPipeBinding(),
				new Uri(Constants.NamedPipeServerUri));

			_host.Open();
		}

		public static bool IsRunning {
			get {
				return _host != null && _host.State == CommunicationState.Opened;
			}
		}

		public static void Stop() {
			//TODO add a timeout and handle the exception that
			//can happen if the host isn't closed by the timeout
			_host.Close();
			_host = null;
			//TODO need some way for the IMessageProcessor to know if it
			//has pending requests and some way for it to cancel them
			//or at least not blow up if it recieves messages after the
			//service is closed
			_processor = null;
		}
	}
}
