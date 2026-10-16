using System.Xml.Serialization;
using System;

namespace NetPerf.Common.Configuration {

	[Obsolete("not supporting config serialization at this time")]
	public interface IConfig {
		FilePath FileLocation {
			get;
		}

		bool HasFileLocation {
			get;
		}

		int Version {
			get;
		}

		/// <summary>
		/// Saves the configuration data to the location specified in the <see cref="FileLocation"/> property. (If it exists).
		/// </summary>
		ErrorTuple Save();

		/// <summary>
		/// Saves the configuration data to the location specified by the <paramref name="pathToFile"/> property.
		/// </summary>
		/// <param name="pathToFile"></param>
		ErrorTuple Save(FilePath pathToFile);
	}
}
