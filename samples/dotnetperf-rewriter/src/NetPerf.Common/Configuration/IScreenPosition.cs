using System.Diagnostics.Contracts;

namespace NetPerf.Common.Configuration {
	[ContractClass(typeof(IScreenPositionContract))]
	public interface IScreenPosition {

		double ScreenLeft {
			get;
			set;
		}

		double ScreenTop {
			get;
			set;
		}

		double ScreenHeight {
			get;
			set;
		}

		double ScreenWidth {
			get;
			set;
		}

		int ScreenState {
			get;
			set;
		}
	}

	[ContractClassFor(typeof(IScreenPosition))]
	abstract class IScreenPositionContract : IScreenPosition {

		public double ScreenLeft {
			get;
			set;
		}

		public double ScreenTop {
			get;
			set;
		}

		public double ScreenHeight {
			get;
			set;
		}

		public double ScreenWidth {
			get;
			set;
		}

		public int ScreenState {
			get {
				Contract.Ensures(Contract.Result<int>() >= 0 && Contract.Result<int>() <= 2);
				return default(int);
			}
			set {
				Contract.Requires(value >= 0 && value <= 2);
			}
		}
	}

}
