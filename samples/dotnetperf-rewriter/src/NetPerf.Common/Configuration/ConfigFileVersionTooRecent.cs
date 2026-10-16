using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Common.Configuration {

	[Serializable]
	public class ConfigFileVersionTooRecent : NotSupportedException {

		public readonly int SupportedVersion;
		public readonly int ConfigFileVersion;

		public ConfigFileVersionTooRecent(int supportedVersion, int configFileVersion, string message = null, Exception innerException = null)
			: base(message, innerException) {
			
				this.SupportedVersion = supportedVersion;
				this.ConfigFileVersion = configFileVersion;
		}
	}
}