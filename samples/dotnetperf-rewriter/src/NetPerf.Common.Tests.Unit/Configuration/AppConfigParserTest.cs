using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Configuration;
using System.Xml;
using System.IO;
using NetPerf.Common.UnitTests;
using NetPerf.Common.Tests.Test_References;

namespace NetPerf.Common.Tests.Configuration {
	[TestClass]
	public class AppConfigParserTest {

		private IAppConfig ReadApp(string xml) {
			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				return AppConfigParser_Privates.ReadAppConfig(reader);
			}
		}

		private IScreenPosition ReadScreenPosition(string xml) {
			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				return AppConfigParser_Privates.ReadScreenPosition(reader);
			}
		}

		[TestMethod]
		public void ReadAppConfig_ReadsAllAttributes() {
			var xml = @"<NetPerfConfiguration>"
				+ "<ceip>false</ceip>"
				+ "<finishedStartup>true</finishedStartup>"
				+ "<autoUpdate>" + UpdateOption.CheckAndDownload.ToString() + "</autoUpdate>"
				+ "</NetPerfConfiguration>";

			var config = ReadApp(xml);

			Assert.IsFalse(config.ParticipateInCEIP);
			Assert.IsTrue(config.HasFinishedFirstStartup);
			Assert.AreEqual(UpdateOption.CheckAndDownload, config.AutoUpdateOption);
		}

		[TestMethod]
		public void ReadAppConfig_ReadsScreenPosition() {
			var xml = @"<NetPerfConfiguration>"
				+ "<mainWindowPosition top=\"5\" left=\"0\" width=\"0\" height=\"0\" state=\"0\" />"
				+ "</NetPerfConfiguration>";

			var config = ReadApp(xml);

			// only checking one property b/c we just want to 
			// see if the screen position was deserialized, not
			// if all the properties were deserialized
			Assert.AreEqual(5, config.MainWindowPosition.ScreenTop);
		}

		[TestMethod]
		public void ReadAppConfig_ReadsRecentSettingFiles() {
			var xml = @"<NetPerfConfiguration>"
				+ "<recentSettingFiles>"
				+ @"<setting>C:\hello world</setting>"
				+ @"<setting>is\this\america</setting>"
				+ @"<setting>nicities.txt</setting>"
				+ "</recentSettingFiles>"
				+ "</NetPerfConfiguration>";

			var config = ReadApp(xml);

			var file = FilePath.Parse(@"C:\hello world");
			var setting = config.RecentSettingFiles.FirstOrDefault(f => f == file);
			Assert.IsNotNull(setting);

			file = FilePath.Parse(@"is\this\america");
			setting = config.RecentSettingFiles.FirstOrDefault(f => f == file);
			Assert.IsNotNull(setting);

			file = FilePath.Parse(@"nicities.txt");
			setting = config.RecentSettingFiles.FirstOrDefault(f => f == file);
			Assert.IsNotNull(setting);
		}

		[TestMethod]
		public void ReadAppConfig_ReadsProblemReportingOptions() {
			var emailAddress = "abcdefg@abcdefg.com";
			var xml = @"<NetPerfConfiguration>"
				+ "<problemOptions>"
				+ "<contactEmail>" + emailAddress + "</contactEmail>"
				+ "</problemOptions>"
				+ "</NetPerfConfiguration>";

			var config = ReadApp(xml);

			Assert.AreEqual(emailAddress, config.ProblemReportingOptions.UserEmail);
		}

		[TestMethod]
		public void ReadScreenPosition_ReadsAllAttributes() {
			var xml = "<mainWindowPosition "
				+ "top=\"1\" "
				+ "left=\"2\" "
				+ "width=\"3\" "
				+ "height=\"4\" "
				+ "state=\"0\" "
				+ " />";

			var screen = ReadScreenPosition(xml);

			Assert.AreEqual(1, screen.ScreenTop);
			Assert.AreEqual(2, screen.ScreenLeft);
			Assert.AreEqual(3, screen.ScreenWidth);
			Assert.AreEqual(4, screen.ScreenHeight);
			Assert.AreEqual(0, screen.ScreenState);
		}

		private IProblemReportingOptions ReadOptions(string xml) {
			using (var reader = XmlReader.Create(new StringReader(xml))) {
				reader.MoveToContent();
				return AppConfigParser_Privates.ReadProblemReportingOptions(reader);
			}
		}

		[TestMethod]
		public void ReadProblemReportingOptions_ReadsUserEmail() {
			var emailAddress = "nowhere@oz.com";
			var xml = "<problemOptions>"
				+ "<contactEmail>" + emailAddress + "</contactEmail>"
				+ "</problemOptions>";

			var options = ReadOptions(xml);

			Assert.AreEqual(emailAddress, options.UserEmail);
		}

		[TestMethod]
		public void ReadProblemReportingOptions_ReadsCanContact() {
			var xml = "<problemOptions>"
				+ "<canContact>true</canContact>"
				+ "</problemOptions>";

			var options = ReadOptions(xml);

			Assert.AreEqual(true, options.CanContactForInfo);

			xml = "<problemOptions>"
				+ "<canContact>false</canContact>"
				+ "</problemOptions>";

			options = ReadOptions(xml);

			Assert.AreEqual(false, options.CanContactForInfo);
		}

		[TestMethod]
		public void ReadProblemReportingOptions_ReadsContactOption() {
			var reportWhen = ReportOnError.Always;
			var xml = "<problemOptions>"
				+ "<contactWhen>" + reportWhen.ToString() + "</contactWhen>"
				+ "</problemOptions>";

			var options = ReadOptions(xml);

			Assert.AreEqual(reportWhen, options.ContactOption);

			reportWhen = ReportOnError.Ask;
			xml = "<problemOptions>"
				+ "<contactWhen>" + reportWhen.ToString() + "</contactWhen>"
				+ "</problemOptions>";

			options = ReadOptions(xml);

			Assert.AreEqual(reportWhen, options.ContactOption);

			reportWhen = ReportOnError.Never;
			xml = "<problemOptions>"
				+ "<contactWhen>" + reportWhen.ToString() + "</contactWhen>"
				+ "</problemOptions>";

			options = ReadOptions(xml);

			Assert.AreEqual(reportWhen, options.ContactOption);
		}

		//
		// write testss
		//

		private string WriteHelper(AppConfig configToWrite) {
			var sb = new StringBuilder();

			var sett = new XmlWriterSettings {
				Indent = false,
				OmitXmlDeclaration = true
			};

			using (var writer = XmlWriter.Create(sb, sett)) {
				AppConfigParser_Privates.Write(writer, configToWrite);
			}

			return sb.ToString();
		}

		private string WriteHelper(IScreenPosition position) {
			var sb = new StringBuilder();

			var sett = new XmlWriterSettings {
				Indent = false,
				OmitXmlDeclaration = true
			};

			using (var writer = XmlWriter.Create(sb, sett)) {
				AppConfigParser_Privates.Write(writer, position);
			}

			return sb.ToString();
		}

		[TestMethod]
		public void WriteAppConfig_WritesAllAttributes() {
			var config = new AppConfig() {
				ParticipateInCEIP = true,
				HasFinishedFirstStartup = false,
				AutoUpdateOption= UpdateOption.AutomaticInstall 
			};

			var xml = WriteHelper(config);

			StringAssert.Contains(xml, "<NetPerfConfiguration");
			StringAssert.Contains(xml, "<ceip>true</ceip>");
			StringAssert.Contains(xml, "<finishedStartup>false</finishedStartup>");
			StringAssert.Contains(xml, "<autoUpdate>AutomaticInstall</autoUpdate>");
		}

		[TestMethod]
		public void WriteAppConfig_WritesScreenPosition() {
			var config = new AppConfig {
				MainWindowPosition = new ScreenPosition {
					ScreenHeight = 1,
					ScreenLeft = 2,
					ScreenTop = 3,
					ScreenWidth = 4
				}
			};

			var xml = WriteHelper(config);

			StringAssert.Contains(xml, "<mainWindowPosition ");
		}

		[TestMethod]
		public void WriteAppConfig_WritesRecentSettingFiles() {
			var config = new AppConfig();
			config.RecentSettingFiles.AddRange(new[] {
				FilePath.Parse(@"hello\there"),
				FilePath.Parse(@"i\is\file"),
				FilePath.Parse(@"C:\hello world")
			});

			var xml = WriteHelper(config);

			StringAssert.Contains(xml, @"<recentSettingFiles>");
			StringAssert.Contains(xml, @"<setting>hello\there</setting>");
			StringAssert.Contains(xml, @"<setting>i\is\file</setting>");
			StringAssert.Contains(xml, @"<setting>C:\hello world</setting>");
			StringAssert.Contains(xml, @"</recentSettingFiles>");
		}

		[TestMethod]
		public void WriteScreenPosition_WritesAllAttributes() {
			var position = new ScreenPosition {
				ScreenHeight = 1,
				ScreenLeft = 2,
				ScreenTop = 3,
				ScreenWidth = 4,
				ScreenState = 0
			};

			var xml = WriteHelper(position);

			StringAssert.Contains(xml, "<mainWindowPosition ");
			StringAssert.Contains(xml, "top=\"3\"");
			StringAssert.Contains(xml, "left=\"2\"");
			StringAssert.Contains(xml, "width=\"4\"");
			StringAssert.Contains(xml, "height=\"1\"");
			StringAssert.Contains(xml, "state=\"0\"");
		}
	}
}
