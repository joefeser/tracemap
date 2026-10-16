using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using MetadataDashboard.NetperfUI.Infrastructure;

namespace MetadataDashboard.NetperfUI.Areas.UISettings.Views {
	/// <summary>
	/// Interaction logic for StartupEULA.xaml
	/// </summary>
	public partial class StartupEULA : NPUserControl {

		public StartupEULA() {
			DataContextChanged += new DependencyPropertyChangedEventHandler(StartupEULA_DataContextChanged);
			InitializeComponent();
		}

		void StartupEULA_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
			var b = new Binding();
			b.Source = this.DataContext;
			b.Path = new PropertyPath("EULAFileStream");
			this.SetBinding(EULAFilePathProperty, b);
		}

		//
		// The RichTextBox.Document property is not exposed as a dependancy property, but all the code in this code-behind
		// simulates binding to that property
		//.

		public System.IO.Stream EULAStream {
			get {
				return (System.IO.Stream)GetValue(EULAFilePathProperty);
			}
			set {
				SetValue(EULAFilePathProperty, value);
			}
		}

		// Using a DependencyProperty as the backing store for EULAFilePath.  This enables animation, styling, binding, etc...
		public static readonly DependencyProperty EULAFilePathProperty =
			DependencyProperty.Register("EULAStream", typeof(System.IO.Stream), typeof(StartupEULA), new UIPropertyMetadata(null, OnEULAFilePathPropertyChanged));


		private static void OnEULAFilePathPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var stream = (System.IO.Stream)e.NewValue;
			try {
				if (stream == null || !stream.CanRead) {
					return;
				}

				var doc = new FlowDocument();
				var range = new TextRange(doc.ContentStart, doc.ContentStart);

				range.Load(stream, DataFormats.Rtf);
				((StartupEULA)d).rtfEULA.Document = doc;
			}
			finally {
				if (stream != null) {
					stream.Dispose();
					stream = null;
				}
			}

		}
	}
}
