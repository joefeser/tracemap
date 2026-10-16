using System.Collections.Generic;
using System;

namespace NetPerf.Common.Configuration {
	public sealed class PropertyBag : Dictionary<string, object> {

		public PropertyBag()
			: base(StringComparer.OrdinalIgnoreCase) {

		}
	}
}
