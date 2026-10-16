
namespace NetPerf.Common.Configuration {
	public enum UpdateOption {
		/// <summary
		/// Netperf checks for updates, but tells the user before downloading or installing them.
		/// </summary>
		CheckOnly = 0,

		/// <summary>
		/// Netperf never checks for updates
		/// </summary>
		Never = 1,

		/// <summary>
		/// Netperf checks for and downloads updates, then tells the user when they are ready to be installed
		/// </summary>
		CheckAndDownload = 2,
		
		/// <summary>
		/// Netperf automatically installs updates whenever they are available
		/// </summary>
		AutomaticInstall = 3
	}
}
