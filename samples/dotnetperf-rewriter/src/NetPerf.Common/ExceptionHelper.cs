using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;

namespace NetPerf.Common {
	public static class ExceptionHelper {

		private static Type[] FileOperationExceptions = new[] {
					typeof(PathTooLongException), typeof(DirectoryNotFoundException),
					typeof(IOException),typeof(NotSupportedException),
					typeof(UnauthorizedAccessException),typeof(FileNotFoundException),
					typeof(SecurityException),typeof(PlatformNotSupportedException),
					typeof(DriveNotFoundException),typeof(SystemException)
				};

		public static XmlException NewUnrecognizedXmlElement(string parentName, string elementName) {
			Contract.Requires<ArgumentException>(!String.IsNullOrEmpty(parentName));
			Contract.Requires<ArgumentException>(!String.IsNullOrEmpty(elementName));

			Contract.Ensures(Contract.Result<XmlException>() != null);

			return new XmlException(string.Format(
				"Unrecognized child element '{0}' within element '{1}",
				elementName,
				parentName));
		}

		public static ErrorTuple WrapFileOperation(Action executeLogic) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple>() != null);

			return Wrap(executeLogic, FileOperationExceptions);
		}

		public static ErrorTuple<TResult> WrapFileOperation<TResult>(Func<TResult> executeLogic) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple<TResult>>() != null);

			return Wrap<TResult>(executeLogic, FileOperationExceptions);
		}

		public static ErrorTuple Wrap(Action executeLogic, params Type[] caughtExceptions) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple>() != null);

			return Wrap(executeLogic, null, caughtExceptions);
		}

		public static ErrorTuple Wrap(Action executeLogic, Action finallyLogic = null, params Type[] caughtExceptions) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple>() != null);

			try {
				executeLogic();
				return new ErrorTuple();
			}
			catch (Exception ex) {
				var handleException = caughtExceptions.IsEmpty() || caughtExceptions.Contains(ex.GetType());

				if (!handleException)
					throw;

				var message = ex.ToString();
				Trace.WriteLine(message);
				return new ErrorTuple(message);
			}
			finally {
				if (finallyLogic != null)
					finallyLogic();
			}
		}

		public static ErrorTuple<TResult> Wrap<TResult>(Func<TResult> executeLogic, params Type[] caughtExceptions) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple<TResult>>() != null);

			return Wrap(executeLogic, null, caughtExceptions);
		}

		public static ErrorTuple<TResult> Wrap<TResult>(Func<TResult> executeLogic, Action finallyLogic = null, params Type[] caughtExceptions) {
			Contract.Requires<ArgumentNullException>(executeLogic != null, "executeLogic");
			Contract.Ensures(Contract.Result<ErrorTuple<TResult>>() != null);

			try {
				var result = executeLogic();
				return ErrorTuple.Create(result);
			}
			catch (Exception ex) {
				var handleException = caughtExceptions.IsEmpty() || caughtExceptions.Contains(ex.GetType());

				if (!handleException)
					throw;

				var message = ex.ToString();
				Trace.WriteLine(message);
				return new ErrorTuple<TResult>(message);
			}
			finally {
				if (finallyLogic != null)
					finallyLogic();
			}
		}

	}
}
