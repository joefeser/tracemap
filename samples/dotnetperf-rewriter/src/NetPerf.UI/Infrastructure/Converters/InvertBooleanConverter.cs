using System.Windows.Data;

namespace MetadataDashboard.NetperfUI.Infrastructure.Converters {

	internal class InvertBooleanConverter : IValueConverter {

		public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is bool) {
				return !(bool)value;
			}

			return false;
		}

		public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is bool) {
				return !(bool)value;
			}

			return true;
		}
	}

}