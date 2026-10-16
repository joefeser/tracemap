using System;
using System.Windows.Data;

namespace MetadataDashboard.NetperfUI.Infrastructure.Converters {
	/// <summary>
	/// Converts a string value to a boolean representation. True if the string is neither null nor empty, False if the string is either null or empty.
	/// </summary>
	public class StringNotEmptyConverter : IValueConverter {

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			System.Diagnostics.Debug.Print("Converting '{0}' to {1}", (value as string) ?? "", !string.IsNullOrEmpty(value as string));
			return !string.IsNullOrEmpty(value as string);
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
			throw new NotSupportedException("The 'StringNotEmptyConverter' Converter is a one way converter.");
		}
	}
}
