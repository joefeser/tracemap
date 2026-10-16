using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace MetadataDashboard.NetperfUI.Infrastructure {
	/// <summary>
	/// Forces the validation of all children in the UserControl when the control is first shown,
	/// so that errors are immediatly obvious.
	/// </summary>
	public class NPUserControl : UserControl  {
		/// <summary>
		/// Localizazed cache that keeps track of the dependancy properties on the different kinds of FrameworkElements.
		/// </summary>
		private static Dictionary<Type, FieldInfo[]> _dependancyPropertiesFor;

		static NPUserControl() {
			_dependancyPropertiesFor = new Dictionary<Type, FieldInfo[]>();

			//Note: we're not overriding the MetadataStyleKey b/c we just want to inject some behaviour
			// into the UserControl class, we don't actually want to create a new control.
			//DefaultStyleKeyProperty.OverrideMetadata(typeof(NPUserControl), new FrameworkPropertyMetadata(typeof(NPUserControl)));
		}

		public NPUserControl() {
			this.DataContextChanged += new DependencyPropertyChangedEventHandler(NPUserControl_DataContextChanged);
			this.Loaded += new RoutedEventHandler(NPUserControl_Loaded);
		}

		void NPUserControl_Loaded(object sender, RoutedEventArgs e) {
			ForceValidation(this);
		}

		void NPUserControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
			if (IsLoaded) {
				ForceValidation(this);
			}
		}

		protected void ForceValidation(object item) {
			return; // commented out for now because wrapping screens in <AdornerDecorator></AdornerDecorator> gives more consistent results when the screen first loads

			if (item == null)
				return;

			var fe = item as FrameworkElement;
			if (fe != null) {
				// show validation errors on the object itself first
				var dbProps = GetDependancyProperties(item);

				for (int i = 0; i < dbProps.Length; i++) {
					var expr = fe.GetBindingExpression((DependencyProperty)dbProps[i].GetValue(fe));
					if (expr != null) {
						expr.UpdateSource();
					}
				}
				
				// then show validation errors on the children
				var iter = LogicalTreeHelper.GetChildren(fe);
				foreach (var kid in iter) {
					ForceValidation(kid);
				}
			}
		}

		private FieldInfo[] GetDependancyProperties(object item) {
			if (item == null)
				return new FieldInfo[0];

			var itmType = item.GetType();
			if (_dependancyPropertiesFor.ContainsKey(itmType)) {
				return _dependancyPropertiesFor[itmType];
			}

			var dbProps = itmType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
					.Where(prop => typeof(DependencyProperty).IsAssignableFrom(prop.FieldType))
					.ToArray();

			_dependancyPropertiesFor[itmType] = dbProps;
			return dbProps;
		}

	}
}
