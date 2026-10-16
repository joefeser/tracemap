using System;
using System.Windows;
using System.Windows.Data;

namespace MetadataDashboard.NetperfUI.Infrastructure.Converters {
	/// <summary>
	/// Converts Boolean values to the Visibility enumeration.
	/// True values are converted to Visibility.Visible and 
	/// False value are converted to Visibility.Collapsed
	/// </summary>
	public class BooleanToCollapsedConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			if (value is bool) {
				return (bool)value ? Visibility.Visible : Visibility.Collapsed;
			}

			return DependencyProperty.UnsetValue;
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotImplementedException("The " + this.GetType()  + " is a one-way conversion.");
		}
	}
}
