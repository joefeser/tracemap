using System.Windows;
using MetadataDashboard.NetperfUI.Areas.ViewModels;

namespace MetadataDashboard.NetperfUI.Areas.Views {
	/// <summary>
	/// Interaction logic for Shell.xaml
	/// </summary>
	public partial class Shell : Window {
		public Shell() {
			InitializeComponent();

			this.DataContextChanged += new DependencyPropertyChangedEventHandler(Shell_DataContextChanged);
		}

		void Shell_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
			var newvm = e.NewValue as ShellVM;
			if (newvm != null) {
				newvm.SetAutoUpdate(new AutoUpdateAdaptor(autoUpdate));
			}
		}

		
	}
}
