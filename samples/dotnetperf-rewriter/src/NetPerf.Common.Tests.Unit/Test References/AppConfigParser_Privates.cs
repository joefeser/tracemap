using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Configuration;

namespace NetPerf.Common.Tests.Test_References {
	internal class AppConfigParser_Privates {

		private static readonly PrivateType _privates = new PrivateType(typeof(AppConfigParser));

		public static IAppConfig ReadAppConfig(XmlReader reader) {
			return (IAppConfig)_privates.InvokeStatic("ReadAppConfig", reader);
		}

		public static IScreenPosition ReadScreenPosition(XmlReader reader) {
			return (IScreenPosition)_privates.InvokeStatic("ReadScreenPosition", reader);
		}

		public static IProblemReportingOptions ReadProblemReportingOptions(XmlReader reader) {
			return (IProblemReportingOptions)_privates.InvokeStatic("ReadProblemReportingOptions", reader);
		}

		public static void Write(XmlWriter writer, IAppConfig config) {
			_privates.InvokeStatic("Write", writer, config);
		}

		public static void Write(XmlWriter writer, IScreenPosition mainWindowPosition) {
			_privates.InvokeStatic("Write", writer, mainWindowPosition);
		}


		public static void Write(XmlWriter writer, IProblemReportingOptions options) {
			_privates.InvokeStatic("Write", writer, options);
		}

	}
}
