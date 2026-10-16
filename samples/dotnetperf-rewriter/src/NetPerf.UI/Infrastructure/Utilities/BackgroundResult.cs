using System;
using System.ComponentModel;
using Caliburn.Micro;

namespace MetadataDashboard.NetperfUI.Infrastructure.Utilities {

	/// <summary>
	/// A <see cref="IResult"/> that runs on a background thread and marshals
	/// the result to the UI thread after it's done.
	/// No progress is reported and the process is not cancelable
	/// </summary>
	internal class BackgroundResult : IResult {

		private BackgroundWorker _worker;
		private Action<ActionExecutionContext> _execute;

		public event EventHandler<ResultCompletionEventArgs> Completed;

		public BackgroundResult(Action<ActionExecutionContext> doWork) {
			_worker = new BackgroundWorker() {
				WorkerReportsProgress = false,
				WorkerSupportsCancellation = false
			};

			_worker.DoWork += new DoWorkEventHandler(worker_DoWork);
			_worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(_worker_RunWorkerCompleted);


			_execute = doWork;
		}

		public void Execute(ActionExecutionContext context) {
			_worker.RunWorkerAsync(context);
		}

		void worker_DoWork(object sender, DoWorkEventArgs e) {
			var context = e.Argument as ActionExecutionContext;
			_execute(context);
		}

		void _worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			var ee = Completed;
			if (ee != null) {
				ee(this, new ResultCompletionEventArgs());
			}
		}
	}
}
