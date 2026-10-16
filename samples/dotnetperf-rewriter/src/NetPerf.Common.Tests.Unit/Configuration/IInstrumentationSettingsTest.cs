using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Configuration;
using NetPerf.Common.Validation;

namespace NetPerf.Common.Tests.Configuration {

	[TestClass]
	public class InstrumentationSettingsTest : IInstrumentationSettingsTest {

		protected override IInstrumentationSettings GetSetting() {
			return new InstrumentationSettings();
		}
	}

	[TestClass]
	public abstract class IInstrumentationSettingsTest : TestClassBase {

		protected abstract IInstrumentationSettings GetSetting();

		[TestMethod]
		public void ApplicationToRun_CannotBeNull() {
			var sett = GetSetting();
			sett.AssertCheckedForEmpty(s => s.ApplicationToRun);
		}

		[TestMethod]
		public void ApplicationToRun_ValidatesFileMustExist() {
			var sett = GetSetting();
			sett.AssertHasRule(s => s.AssembliesToInstrument, typeof(FileExistsValidator));
		}

		[TestMethod]
		public void StrongNameKeyFile_ValidatesFileMustExist() {
			var sett = GetSetting();
			sett.AssertHasRule(s => s.AssembliesToInstrument, typeof(FileExistsValidator));
		}

		[TestMethod]
		public void AssembliesToInstrument_ValidatesCannotBeEmpty() {
			var sett = GetSetting();
			sett.AssertCheckedForEmpty(s => s.AssembliesToInstrument);
		}

		[TestMethod]
		public void AssembliesToInstrument_ValidatesFileMustExist() {
			var sett = GetSetting();
			sett.AssertHasRule(s => s.AssembliesToInstrument, typeof(FileExistsValidator));
		}

		[TestMethod]
		public void SummaryReport_ValidatesReportSetting() {
			var sett = GetSetting();
			sett.AssertHasRule(s => s.SummaryReport, typeof(IReportSettingValidator));
		}

		[TestMethod]
		public void ExtractReport_ValidatesReportSetting() {
			var sett = GetSetting();
			sett.AssertHasRule(s => s.ExtractReport, typeof(IReportSettingValidator));
		}
	}
}
