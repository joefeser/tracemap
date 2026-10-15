using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.Profiling.Views {
	/// <summary>
	/// Interaction logic for InstrumentWizardStep3.xaml
	/// </summary>
	public partial class InstrumentWizardStep3 : NPUserControl {
		public InstrumentWizardStep3() {
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
