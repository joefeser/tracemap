using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.Views {
	/// <summary>
	/// Interaction logic for InstrumentationConfig.xaml
	/// </summary>
	public partial class InstrumentationConfig : NPUserControl {
		public InstrumentationConfig() {
			InitializeComponent();
		}

		private void IncludedAssemblies_Drop(object sender, System.Windows.DragEventArgs e) {
			var context = this.DataContext as IHandleFileDrop;
			if (context != null && e.Data.GetDataPresent("FileDrop")) {
				var files = e.Data.GetData("FileDrop") as string[];
				context.AcceptFileDrop(files);
			}
		}

		private void IncludedAssemblies_DragEnter(object sender, System.Windows.DragEventArgs e) {
			var context = this.DataContext as IHandleFileDrop;
			if (context != null && e.Data.GetDataPresent("FileDrop")) {
				var files = e.Data.GetData("FileDrop") as string[];
				if (context.CanAcceptFileDrop(files)) {
					e.Effects = System.Windows.DragDropEffects.Copy;
				}
				
			}

			e.Effects = System.Windows.DragDropEffects.None;
		}

	}
}
