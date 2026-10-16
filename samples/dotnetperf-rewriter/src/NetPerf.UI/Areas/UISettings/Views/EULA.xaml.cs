using MetadataDashboard.NetperfUI.Infrastructure;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Data;


namespace MetadataDashboard.NetperfUI.Areas.UISettings.Views {
	/// <summary>
	/// Interaction logic for EULA.xaml
	/// </summary>
	public partial class EULA : NPUserControl {
		
		public EULA() {
			DataContextChanged += new DependencyPropertyChangedEventHandler(EULA_DataContextChanged);
			InitializeComponent();
		}

		void EULA_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
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
			DependencyProperty.Register("EULAStream", typeof(System.IO.Stream), typeof(EULA), new UIPropertyMetadata(null, OnEULAFilePathPropertyChanged));


		private static void OnEULAFilePathPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var stream = (System.IO.Stream)e.NewValue;
			try {
				if (stream == null || !stream.CanRead) {
					return;
				}

				var doc = new FlowDocument();
				var range = new TextRange(doc.ContentStart, doc.ContentStart);

				range.Load(stream, DataFormats.Rtf);
				((EULA)d).rtfEULA.Document = doc;
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
