
namespace NetPerf.Common.Configuration {
	internal class ScreenPosition : IScreenPosition {
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
			get;
			set;
		}

		public ScreenPosition() {
			ScreenHeight = 0.0;
			ScreenWidth = 0.0;
			ScreenLeft = double.NaN;
			ScreenTop = double.NaN;
			ScreenState = 0;
		}
	}
}
