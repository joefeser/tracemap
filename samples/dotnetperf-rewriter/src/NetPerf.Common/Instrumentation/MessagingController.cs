using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NetPerf.Common.Instrumentation {
	internal class MessagingController : IMessagingController {

		public bool WantsMethodEnterMessages {
			get {
				return true;
			}
		}

		public bool WantsMethodExitMessages {
			get {
				return true;
			}
		}

		public bool WantsSeqPointEndMessages {
			get {
				return true;
			}
		}
	}
}
