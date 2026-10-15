using System;
using System.Diagnostics.Contracts;
using System.Windows.Input;
using Caliburn.Micro;
using MetadataDashboard.NetperfUI.Infrastructure.Aspects;
using MetadataDashboard.NetperfUI.Infrastructure.Utilities;

namespace MetadataDashboard.NetperfUI.Areas.ViewModels {
	[IgnorePropertyChanged]
	public class MenuItemVM : BindableCollection<MenuItemVM> {

		public bool IsSeparator {
			get;
			private set;
		}

		public string Text {
			get;
			private set;
		}

		public object Tag {
			get;
			private set;
		}

		public string ToolTipText {
			get;
			private set;
		}

		public ICommand ClickCommand {
			get;
			private set;
		}

		public MenuItemVM() {
			IsSeparator = true;
		}

		public MenuItemVM(string text, string tooltip = null, object tag = null, Action<object> onClick = null, Func<object, bool> canClick = null) {
			Contract.Requires(!String.IsNullOrEmpty(text));

			Text = text;
			ToolTipText = tooltip;
			Tag = tag;
			if (onClick != null) {
				ClickCommand = new DelegateCommand(onClick, canClick);
			}
		}
	}
}
