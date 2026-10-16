using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Xml;

namespace NetPerf.Common.Configuration {
	public static class AppConfigParser {

		const int CurrentVersion = 1;
		const string RootElementName = "NetPerfConfiguration";

		public static IAppConfig DefaultConfig {
			get {
				return new AppConfig() {
					HasFinishedFirstStartup = false,
					ParticipateInCEIP = true, // this is an opt-out option
					AutoUpdateOption = UpdateOption.CheckAndDownload,
					MainWindowPosition = new ScreenPosition() {
						ScreenHeight = 550,
						ScreenLeft = double.NaN, // so that the window starts in the center of the screen the first time it shows
						ScreenTop = double.NaN, // so that the window starts in the center of the screen the first time it shows
						ScreenWidth = 600
					},
					ProblemReportingOptions = new ProblemReportingOptions {
						CanContactForInfo = true,
						ContactOption = ReportOnError.Always,
						UserEmail = null
					}
				};
			}
		}

		public static ErrorTuple<IAppConfig> Read(FilePath fromFile) {
			Contract.Requires(fromFile != null);

			Contract.Ensures(Contract.Result<ErrorTuple<IAppConfig>>() != null);
			try {
				return ExceptionHelper.WrapFileOperation<IAppConfig>(() => {
					var sett = new XmlReaderSettings() {
						IgnoreComments = true,
						IgnoreWhitespace = true
					};

					using (var reader = XmlReader.Create(fromFile, sett)) {
						reader.MoveToContent();
						return ReadAppConfig(reader);
					}
				});
			}
			catch (ConfigFileVersionTooRecent ex) {
				return new ErrorTuple<IAppConfig>("Unsupported config file version {0}. The supported version is {1} or earlier", ex.ConfigFileVersion, ex.SupportedVersion);
			}
		}

		private static IAppConfig ReadAppConfig(XmlReader reader) {
			Contract.Requires(reader != null);

			Contract.Ensures(Contract.Result<IAppConfig>() != null);

			return reader.ReadElement(RootElementName,
				 (bag) => {
					 return new AppConfig();
				 },
				(rdr, elementName, appConfig) => {
					switch (elementName) {
						case "ceip":
							appConfig.ParticipateInCEIP = reader.ReadElementContentAsBoolean();
							break;
						case "finishedStartup":
							appConfig.HasFinishedFirstStartup = reader.ReadElementContentAsBoolean();
							break;
						case "autoUpdate":
							appConfig.AutoUpdateOption = reader.ReadElementAsEnum<UpdateOption>();
							break;
						case "mainWindowPosition":
							appConfig.MainWindowPosition = ReadScreenPosition(reader);
							break;
						case "recentSettingFiles":
							var filePaths = reader.ReadElementList(r => FilePath.Parse(r.ReadElementString()));
							appConfig.RecentSettingFiles.AddRange(filePaths);
							break;
						case "problemOptions":
							var options = ReadProblemReportingOptions(reader);
							appConfig.ProblemReportingOptions.CanContactForInfo = options.CanContactForInfo;
							appConfig.ProblemReportingOptions.ContactOption = options.ContactOption;
							appConfig.ProblemReportingOptions.UserEmail = options.UserEmail;
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}
				},
				(bag, rdr, attribName) => {
					switch (attribName) {
						case "version":
							var fileVersion = rdr.ReadAttributeAsInt();
							if (fileVersion > CurrentVersion) {
								var msg = string.Format("Unable to read Netperf UI configuration file. Expected version {0} of the file but encountered version {1}.", CurrentVersion, fileVersion);
								throw new ConfigFileVersionTooRecent(CurrentVersion, fileVersion, msg);
							}
							break;
					}
				});
		}

		private static IScreenPosition ReadScreenPosition(XmlReader reader) {
			Contract.Requires(reader != null);

			Contract.Ensures(Contract.Result<IScreenPosition>() != null);

			return reader.ReadElement("mainWindowPosition",
				(bag) => {
					return new ScreenPosition {
						ScreenTop = ((string)bag["top"]).ParseDoubleStorageString(),
						ScreenLeft = ((string)bag["left"]).ParseDoubleStorageString(),
						ScreenWidth = ((string)bag["width"]).ParseDoubleStorageString(),
						ScreenHeight = ((string)bag["height"]).ParseDoubleStorageString(),
						ScreenState = ((string)bag["state"]).ParseStorageString()
					};
				});
		}

		private static IProblemReportingOptions ReadProblemReportingOptions(XmlReader reader) {
			Contract.Requires(reader != null);
			Contract.Ensures(Contract.Result<IProblemReportingOptions>() != null);

			return reader.ReadElement("problemOptions",
				(bag) => new ProblemReportingOptions(),
				(rdr, elementName, options) => {
					switch (elementName) {
						case "canContact":
							options.CanContactForInfo = reader.ReadElementContentAsBoolean();
							break;
						case "contactWhen":
							options.ContactOption = reader.ReadElementAsEnum<ReportOnError>();
							break;
						case "contactEmail":
							options.UserEmail = reader.ReadElementContentAsString();
							break;
						default:
							throw ExceptionHelper.NewUnrecognizedXmlElement(elementName, reader.LocalName);
					}

				}
				);
		}

		public static ErrorTuple Write(FilePath toFile, IAppConfig config) {
			Contract.Requires(toFile != null);
			Contract.Requires(config != null);

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
					Write(writer, config);
				}
			});

		}

		private static void Write(XmlWriter writer, IAppConfig config) {
			Contract.Requires(writer != null);
			Contract.Requires(config != null);

			writer.WriteStartElement(RootElementName);

			writer.WriteAttributeString("version", CurrentVersion.ToStorageString());

			writer.WriteElementString("ceip", config.ParticipateInCEIP.ToString().ToLowerInvariant());
			writer.WriteElementString("finishedStartup", config.HasFinishedFirstStartup.ToString().ToLowerInvariant());
			writer.WriteElementString("autoUpdate", config.AutoUpdateOption.ToString());
			Write(writer, config.MainWindowPosition);

			writer.WriteElementList(config.RecentSettingFiles, "recentSettingFiles", (w, item) => w.WriteElementString("setting", item.FullPath));

			Write(writer, config.ProblemReportingOptions);

			writer.WriteEndElement();
		}

		private static void Write(XmlWriter writer, IScreenPosition mainWindowPosition) {
			Contract.Requires(writer != null);
			Contract.Requires(mainWindowPosition != null);

			writer.WriteStartElement("mainWindowPosition");
			writer.WriteAttributeString("top", mainWindowPosition.ScreenTop.ToStorageString());
			writer.WriteAttributeString("left", mainWindowPosition.ScreenLeft.ToStorageString());
			writer.WriteAttributeString("width", mainWindowPosition.ScreenWidth.ToStorageString());
			writer.WriteAttributeString("height", mainWindowPosition.ScreenHeight.ToStorageString());

			writer.WriteAttributeString("state", mainWindowPosition.ScreenState.ToStorageString());

			writer.WriteEndElement();
		}

		private static void Write(XmlWriter writer, IProblemReportingOptions options) {
			Contract.Requires(writer != null);
			Contract.Requires(options != null);

			writer.WriteStartElement("problemOptions");
			writer.WriteElementString("canContact", options.CanContactForInfo.ToString().ToLowerInvariant());
			writer.WriteElementString("contactWhen", options.ContactOption.ToString());
			writer.WriteElementString("contactEmail", options.UserEmail);
			writer.WriteEndElement();
		}
	}
}
