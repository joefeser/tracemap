using System;

namespace NetPerf.Common.SystemStubs {
	[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class MockForClass : Attribute {
		private readonly Type _classMocked;
		public Type ClassMocked {
			get {
				return _classMocked;
			}
		}

		public MockForClass(Type classType) {
			_classMocked = classType;
		}
	}
}
