using NetPerf.Common;
using NetPerf.Common.Configuration;
using System.IO;
using System;
using NetPerf.Core.Interfaces;
using NetPerf.Core;
using System.Diagnostics;
using System.Xml.Xsl;
using System.Xml;
using System.Xml.XPath;

namespace NetPerf {

	/// <summary>
	/// Not meant to be exposed or accessible from the command line
	/// </summary>
	internal class CreateReportCommand {

		public FilePath OutputFile {
			get;
			set;
		}

		public NetperfReportType TypeOfReport {
			get;
			set;
		}

		/// <summary>
		/// Not the best thing but we only want to generate the xml once.
		/// Whatever report hits it first will set the variable.
		/// </summary>
		private static MemoryStream XmlReportData {
			get;
			set;
		}

		public void CreateReport(IInstrumentationDataMapAndInstances map, TimingDataInformation data) {

			try {
				if (FileStub.Exists(OutputFile)) {
					FileStub.Delete(OutputFile);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine("Error deleting report output file:" + OutputFile + " " + ex.ToString());
			}

			using (var stream = FileStub.Open(OutputFile, FileMode.OpenOrCreate, FileAccess.Write)) {
				switch (TypeOfReport) {
					case NetperfReportType.Summary:
						CreateSummaryReport(map, data, stream);
						break;
					case NetperfReportType.Extract:
						CreateExtractReport(map, data, stream);
						break;
					default:
						throw new InvalidOperationException("Unrecognized enumeration '" + TypeOfReport + "'");
				}
			}
		}

		private void CreateSummaryReport(IInstrumentationDataMapAndInstances map, TimingDataInformation data, Stream outputStream) {
			EnsureMemoryStream(map, data);

			var path = PathStub.Combine(NetperfHelpers.NetperfFolder(), "netperfmainreport.xsl");

			if (!FileStub.Exists(path)) {
				throw new InvalidOperationException("Xslt File is missing '" + path + "'");
			}

			var xslt = new XslCompiledTransform(true);
			xslt.Load(path);
			var xmlreader = XmlReader.Create(XmlReportData, default(XmlReaderSettings));

			// Clone its output settings and turn character checking off
			XmlWriterSettings ws = xslt.OutputSettings.Clone();
			ws.CheckCharacters = false;
			var xmlWriter = XmlWriter.Create(outputStream, ws);

			xslt.Transform(xmlreader, default(XsltArgumentList), xmlWriter, new XmlUrlResolver());

		}

		private void CreateExtractReport(IInstrumentationDataMapAndInstances map, TimingDataInformation data, Stream outputStream) {
			EnsureMemoryStream(map, data);

			XmlReportData.Position = 0;
			XmlReportData.WriteTo(outputStream);

		}

		private static void EnsureMemoryStream(IInstrumentationDataMapAndInstances map, TimingDataInformation data) {
			if (XmlReportData == null) {
				XmlReportData = new MemoryStream();
				data.ToXmlReport(map, XmlReportData);
			}
			XmlReportData.Position = 0;
		}

	}
}
