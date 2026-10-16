using System;
using System.Diagnostics.Contracts;

namespace NetPerf.Common {
	/// <summary>
	/// Strongly-typed class that permits a function to return a (string) error message w/o
	/// the yuckiness of making the calling function treat a null return value as the indication
	/// of a successful execution.
	/// </summary>
	[System.Diagnostics.DebuggerDisplay("{ErrorMessage}")]
	[Pure]
	public class ErrorTuple {
		/// <summary>
		/// A message describing the error that happened, or <see cref="String.Empty"/> if this object
		/// represents a successful execution.
		/// </summary>
		public string ErrorMessage {
			get;
			private set;
		}

		/// <summary>
		/// Whether this object represents an error or a non-error (successful) return value.
		/// True if it represents an error return value.
		/// </summary>
		public bool HasError {
			get {
				return !ErrorMessage.IsNullOrWhitespace();
			}
		}

		/// <summary>
		/// Whether this object represents an error or a successful execution.
		/// True if it represents a successful execution.
		/// </summary>
		public bool WasSuccess {
			get {
				return !HasError;
			}
		}

		/// <summary>
		/// Permits the implicit checking of the <see cref="WasSuccess"/> property.
		/// </summary>
		/// <param name="tuple">The <see cref="ErrorTuple"/> to convert to a <see cref="System.Boolean"/>.</param>
		/// <returns>True if the <paramref name="tuple"/> represents a successful execution, false otherwise.</returns>
		public static implicit operator bool(ErrorTuple tuple) {
			Contract.Requires(tuple != null);

			return tuple.WasSuccess;
		}

		/// <summary>
		/// Creates an <see cref="ErrorTuple"/> that represents a non-error (successful) return value.
		/// </summary>
		public ErrorTuple() {
			Contract.Ensures(ErrorMessage == string.Empty);
			Contract.Ensures(WasSuccess);
			Contract.Ensures(!HasError);

			ErrorMessage = string.Empty;
		}

		/// <summary>
		/// Creates an <see cref="ErrorTuple"/> that represents an error return value.
		/// The <see cref="ErrorMessage"/> of the created <see cref="ErrorTuple"/> will be constructed from
		/// the <paramref name="errorMessageFormat"/> and the <paramref name="formatArgs"/>.
		/// </summary>
		/// <param name="errorMessageFormat">A composite format string. Cannot be null or empty.</param>
		/// <param name="formatArgs">A <see cref="System.Object"/> array containing zero or more objects to format.</param>
		public ErrorTuple(string errorMessageFormat, params object[] formatArgs) {
			Contract.Requires(!errorMessageFormat.IsNullOrWhitespace());
			Contract.Ensures(!ErrorMessage.IsNullOrWhitespace());
			Contract.Ensures(!WasSuccess);
			Contract.Ensures(HasError);

			ErrorMessage = (formatArgs == null || formatArgs.Length < 1)
				? errorMessageFormat
				: string.Format(errorMessageFormat, formatArgs);
		}

		/// <summary>
		/// Creates an <see cref="ErrorTuple{TResult}"/> that represents a successful return value,
		/// and contains whatever <paramref name="value"/> the procedure wants to pass back to the caller.
		/// </summary>
		/// <typeparam name="TResult">The type of the contained <paramref name="value"/>.</typeparam>
		/// <param name="value">The value to pass back to the calling function.</param>
		/// <returns>A new <see cref="ErrorTuple{TResult}"/></returns>
		public static ErrorTuple<TResult> Create<TResult>(TResult value) {
			Contract.Ensures(Contract.Result<ErrorTuple<TResult>>() != null);
			Contract.Ensures(Contract.Result<ErrorTuple<TResult>>().WasSuccess);
			Contract.Ensures(!Contract.Result<ErrorTuple<TResult>>().HasError);

			return new ErrorTuple<TResult>(value);
		}
	}

	/// <summary>
	/// Strongly-typed class that permits a function to return a (string) error message w/o
	/// the yuckiness of making the calling function treat a null return value as the indication
	/// of a successful execution. Also permits the function to return data when it executes successfully.
	/// </summary>
	/// <typeparam name="T">The type of the data to return to the calling function.</typeparam>
	[System.Diagnostics.DebuggerDisplay("{ErrorMessage}")]
	[Pure]
	public class ErrorTuple<T> {

		/// <summary>
		/// The value returned when this object represents a successful execution, or
		/// default(T) if this object represents an error.
		/// </summary>
		public T Data {
			get;
			private set;
		}

		/// <summary>
		/// A message describing the error that happened, or <see cref="String.Empty"/> if this object
		/// represents a successful execution.
		/// </summary>
		public string ErrorMessage {
			get;
			private set;
		}

		/// <summary>
		/// Whether this object represents an error or a non-error (successful) execution.
		/// True if it represents an error execution.
		/// </summary>
		public bool HasError {
			get {
				return !ErrorMessage.IsNullOrWhitespace();
			}
		}

		/// <summary>
		/// Whether this object represents an error or a successful execution.
		/// True if it represents a successful execution.
		/// </summary>
		public bool WasSuccess {
			get {
				return !HasError;
			}
		}

		/// <summary>
		/// Permits the implicit checking of the <see cref="WasSuccess"/> property.
		/// </summary>
		/// <param name="tuple">The <see cref="ErrorTuple{T}"/> to convert to a <see cref="System.Boolean"/>.</param>
		/// <returns>True if the <paramref name="tuple"/> represents a successful execution, false otherwise.</returns>
		public static implicit operator bool(ErrorTuple<T> tuple) {
			Contract.Requires(tuple != null);

			return tuple.WasSuccess;
		}

		/// <summary>
		/// Creates an <see cref="ErrorTuple{T}"/> that represents an error return value.
		/// The <see cref="ErrorMessage"/> of the created <see cref="ErrorTuple{T}"/> will be constructed from
		/// the <paramref name="errorMessageFormat"/> and the <paramref name="formatArgs"/>.
		/// </summary>
		/// <param name="errorMessageFormat">A composite format string. Cannot be null or empty.</param>
		/// <param name="formatArgs">A <see cref="System.Object"/> array containing zero or more objects to format.</param>
		public ErrorTuple(string errorMessageFormat, params object[] formatArgs) {
			Contract.Requires(!errorMessageFormat.IsNullOrWhitespace());

			Contract.Ensures(!ErrorMessage.IsNullOrWhitespace());

			ErrorMessage = (formatArgs == null || formatArgs.Length < 1)
				? errorMessageFormat
				: string.Format(errorMessageFormat, formatArgs);

			Data = default(T);
		}

		/// <summary>
		/// Creates an <see cref="ErrorTuple{T}"/> that represents a successful execution.
		/// </summary>
		/// <param name="data">The value to return to the calling function.</param>
		public ErrorTuple(T data) {
			Contract.Ensures(ErrorMessage == string.Empty);

			ErrorMessage = string.Empty;
			Data = data;
		}
	}
}
