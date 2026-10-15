using System;
using System.Windows;
using System.Windows.Data;

namespace MetadataDashboard.NetperfUI.Infrastructure.Converters {

	internal class ValueEqualsParameterConverter : IValueConverter {


		public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value == null || parameter == null)
				return DependencyProperty.UnsetValue;

			return value.ToString() == parameter.ToString();
		}

		public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is bool && (bool)value && parameter != null) {
				return Enum.Parse(targetType, parameter.ToString());
			}

			return DependencyProperty.UnsetValue;
		}
	}

}