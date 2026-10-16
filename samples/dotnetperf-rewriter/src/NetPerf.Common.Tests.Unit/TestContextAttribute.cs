using System;
using System.Runtime.Remoting.Contexts;
using System.Runtime.Remoting.Messaging;
using System.Linq;

namespace NetPerf.Common.Tests {
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
	public abstract class TestContextAttribute : ContextAttribute, IContributeObjectSink, IMessageSink {

		public TestContextAttribute()
			: base("junk data throw away") {

		}

		public override bool IsNewContextOK(Context newCtx) {
			return true;
		}

		public override void Freeze(Context newContext) {

		}

		#region IContributeObjectSink implementation

		public IMessageSink GetObjectSink(MarshalByRefObject obj, IMessageSink nextSink) {
			NextSink = nextSink;
			return this;
		}

		#endregion IContributeObjectSink implementation

		#region IMessageSink implementation

		public IMessageCtrl AsyncProcessMessage(IMessage msg, IMessageSink replySink) {
			throw new InvalidOperationException();
		}

		public IMessageSink NextSink {
			get;
			private set;
		}

		public IMessage SyncProcessMessage(IMessage msg) {
			if (msg == null)
				throw new ArgumentNullException("msg");

			var attr = IsAttributeOnMessage(msg);
			if (attr != null) {

				BeforeTestExecutes(attr);

				try {
					// execute the unit test
					var returnMessage = NextSink.SyncProcessMessage(msg);
					return returnMessage;
				}
				finally {
					AfterTestExecutes(attr);
				}
			}

			return NextSink.SyncProcessMessage(msg);
		}

		private TestContextAttribute IsAttributeOnMessage(IMessage msg) {
			var typeName = (string)msg.Properties["__TypeName"];
			var methodName = (string)msg.Properties["__MethodName"];

			var typeObj = Type.GetType(typeName);
			var methodImpl = typeObj.GetMethod(methodName)
				.GetCustomAttributes(true)
				.Where(attr => attr.GetType() == this.GetType())
				.FirstOrDefault();

			return (TestContextAttribute)methodImpl;
		}

		#endregion IMessageSink implementation

		protected abstract void AfterTestExecutes(TestContextAttribute dataContext);

		protected abstract void BeforeTestExecutes(TestContextAttribute dataContext);
	}
}
