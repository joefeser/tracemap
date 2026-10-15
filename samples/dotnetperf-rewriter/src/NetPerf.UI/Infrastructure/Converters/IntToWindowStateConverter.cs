using System;
using System.Windows;
using System.Windows.Data;

namespace MetadataDashboard.NetperfUI.Infrastructure.Converters {
	[ValueConversion(typeof(int), typeof(WindowState))]
	public class IntToWindowStateConverter : IValueConverter {

		/// <summary>
		/// Converts from a <see cref="System.Int32"/> to a <see cref="WindowState"/>
		/// </summary>
		/// <param name="value">The <see cref="System.Int32"/> to be converted</param>
		/// <param name="targetType">Should be <see cref="System.Int32"/></param>
		/// <param name="parameter">Additional information needed for the conversion.</param>
		/// <param name="culture">The culture to use for the conversion.</param>
		/// <returns>The <see cref="WindowState"/> enum for the <see cref="System.Int32"/> <paramref name="value"/>,
		/// or <see cref="WindowState.Normal"/> if there is no conversion.</returns>
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {

			if (Enum.IsDefined(typeof(WindowState), value)) {
				return (WindowState)(int)value;
			}

			return WindowState.Normal;
		}

		/// <summary>
		/// Converts from a <see cref="WindowState"/> to a <see cref="System.Int32"/>.
		/// </summary>
		/// <param name="value">The <see cref="WindowState"/> to be converted</param>
		/// <param name="targetType">Should be <see cref="WindowState"/></param>
		/// <param name="parameter">Additional information needed for the conversion.</param>
		/// <param name="culture">The culture to use for the conversion.</param>
		/// <returns>The <see cref="System.Int32"/> value for the <see cref="WindowState"/> <paramref name="value"/>,
		/// or the value of <seealso cref="WindowState.Normal"/> if there is no conversion.</returns>
		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {

			if (Enum.IsDefined(typeof(WindowState), value)) {
				return (int)value;
			}

			return (int)WindowState.Normal;
		}
	}
}
