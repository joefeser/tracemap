using System;
using System.Collections.Generic;
using System.Threading;

namespace NetPerf.Common.Instrumentation {
	internal static class UniqueIdGenerator {
		static Dictionary<Type, int> _previousIds = new Dictionary<Type, int>();
		static object AccessLock = new object();

		public static int NextId(Type forType) {
			lock (AccessLock) {
				int val;
				if (_previousIds.TryGetValue(forType, out val)) {
					_previousIds[forType] = val + 1;
					return val + 1;
				}

				_previousIds.Add(forType, 1);
				return 1;
			}
		}
	}
}

