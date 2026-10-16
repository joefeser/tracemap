using System;
using System.Diagnostics.Contracts;
using Caliburn.Micro;

namespace MetadataDashboard.NetperfUI.Infrastructure.Utilities {
	/// <summary>
	/// yes the name sucks. This class provides an implementation of caliburn.micro's IResult
	/// whose functionality is provided by a delegate
	/// </summary>
	internal class DelegateResult : IResult {


		private readonly Action<ActionExecutionContext> _execute;

		public event EventHandler<ResultCompletionEventArgs> Completed;

		public void Execute(ActionExecutionContext context) {
			_execute(context);

			var ee = Completed;
			if (ee != null) {
				ee(this, new ResultCompletionEventArgs());
			}
		}

		public DelegateResult(Action<ActionExecutionContext> doWork) {
			Contract.Requires(doWork != null);

			_execute = doWork;
		}
	}
}
