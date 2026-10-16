
namespace NetPerf.Common.Instrumentation {
	public interface IMessagingController {

		bool WantsMethodEnterMessages {
			get;
		}

		bool WantsMethodExitMessages {
			get;
		}

		bool WantsSeqPointEndMessages {
			get;
		}
	}
}
