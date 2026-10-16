using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Xml;

namespace NetPerf.Common.Configuration {
	public static class InstrumentationSettingsParser {

		const int CurrentVersion = 1;
		const string RootElementName = "netperfsettings";

		public static IInstrumentationSettings DefaultSettings {
			get {
				return new InstrumentationSettings();
			}
		}

		public static IReportSetting DefaultReport {
			get {
				return new ReportSetting();
			}
		}

		public static ErrorTuple<IInstrumentationSettings> Read(FilePath fromFile) {
			Contract.Requires(fromFile != null);

			Contract.Ensures(Contract.Result<ErrorTuple<IInstrumentationSettings>>() != null);

			return ExceptionHelper.WrapFileOperation<IInstrumentationSettings>(() => {
				var sett = new XmlReaderSettings() {
					IgnoreComments = true,
					IgnoreWhitespace = true
				};

				using (var reader = XmlReader.Create(fromFile, sett)) {
					reader.MoveToContent();
					return Read(reader);
				}
			});

		}

		private static IInstrumentationSettings Read(XmlReader reader) {
			Contract.Requires(reader != null);

			return reader.ReadElement(RootElementName,
				(bag) => new InstrumentationSettings(),
				(r, elementName, data) => {
					switch (elementName) {
						case "application":
							var path = r.ReadElementString();
							data.ApplicationToRun = FilePath.Parse(path);
							break;
						case "applicationparameters":
							data.ApplicationParameters = r.ReadElementString();
							break;
						case "assembliesToInstrument":
							var list = r.ReadElementList(rr => FilePath.Parse(rr.ReadElementString()));
							data.AssembliesToInstrument = list;
							break;
						case "exclusions":
							data.InstrumentationExclusions = r.ReadElementList(rr => rr.ReadElementString());
							break;
						case "extractReport":
							data.ExtractReport = ReadReport(r, "extractReport", NetperfReportType.Extract);
							break;
						case "keyfile":
							var kf = r.ReadElementString();
							data.StrongNameKeyFile = FilePath.Parse(kf);
							break;
						case "summaryReport":
							data.SummaryReport = ReadReport(r, "summaryReport", NetperfReportType.Summary);
							break;
						case "workingFolder":
							var workingFolder = r.ReadElementString();
							data.WorkingFolder = DirectoryPath.Parse(workingFolder);
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}
				});
		}

		private static IReportSetting ReadReport(XmlReader reader, string elementName, NetperfReportType reportType) {
			Contract.Requires(reader != null);

			return reader.ReadElement(elementName,
				(bag) => {
					return new ReportSetting {
						IsActive = bool.Parse((string)bag["active"]),
						OutputType = reportType,
						OutputFile = bag.ContainsKey("outputFile") ? FilePath.Parse((string)bag["outputFile"]) : null
					};
				}
			);
		}

		public static ErrorTuple Write(FilePath toFile, IInstrumentationSettings settings) {
			Contract.Requires(toFile != null);
			Contract.Requires(settings != null);

			Contract.Ensures(Contract.Result<ErrorTuple>() != null);

			return ExceptionHelper.WrapFileOperation(() => {
				var sett = new XmlWriterSettings() {
					CloseOutput = true,
					Indent = true,
					IndentChars = " ",
					OmitXmlDeclaration = false,
					CheckCharacters = true
				};

				if (!DirectoryStub.Exists(toFile.ParentDirectory)) {
					DirectoryStub.CreateDirectory(toFile.ParentDirectory);
				}

				using (var writer = XmlWriter.Create(toFile, sett)) {
					Write(writer, settings);
				}
			});
		}

		private static void Write(XmlWriter writer, IInstrumentationSettings settings) {
			Contract.Requires(writer != null);
			Contract.Requires(settings != null);

			writer.WriteStartElement(RootElementName);
			writer.WriteAttributeString("version", CurrentVersion);

			if (settings.ApplicationToRun != null) {
				writer.WriteElementString("application", settings.ApplicationToRun.FullPath);
			}

			if (!settings.ApplicationParameters.IsNullOrWhitespace()) {
				writer.WriteElementString("applicationparameters", settings.ApplicationParameters);
			}

			writer.WriteElementList(settings.AssembliesToInstrument, "assembliesToInstrument", (w, assem) => w.WriteElementString("assembly", assem.FullPath));
			writer.WriteElementList(settings.InstrumentationExclusions, "exclusions", (w, e) => w.WriteElementString("exclusion", e));

			WriteReport(writer, settings.ExtractReport, "extractReport");

			if (settings.StrongNameKeyFile != null) {
				writer.WriteElementString("keyfile", settings.StrongNameKeyFile.FullPath);
			}

			WriteReport(writer, settings.SummaryReport, "summaryReport");

			if (settings.WorkingFolder != null) {
				writer.WriteElementString("workingFolder", settings.WorkingFolder);
			}

			writer.WriteEndElement();
		}

		private static void WriteReport(XmlWriter writer, IReportSetting report, string elementName) {
			Contract.Requires(writer != null);
			Contract.Requires(!String.IsNullOrEmpty(elementName));

			if (report != null) {
				writer.WriteStartElement(elementName);
				writer.WriteAttributeString("active", report.IsActive.ToString());

				if (report.OutputFile != null) {
					writer.WriteAttributeString("outputFile", report.OutputFile.FullPath);
				}
				writer.WriteEndElement();
			}
		}

	}
}
