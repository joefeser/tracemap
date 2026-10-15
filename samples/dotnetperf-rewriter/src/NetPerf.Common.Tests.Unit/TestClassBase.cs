using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.SystemStubs;

namespace NetPerf.Common.Tests {

	[TestClass]
	[TestClassContext]
	public abstract class TestClassBase : ContextBoundObject {

		private const string FieldName = "_interfaceStub";
		private static ConcurrentDictionary<Type, StubField> _mapper;

		static TestClassBase() {
			_mapper = new ConcurrentDictionary<Type, StubField>();
		}

		/// <summary>
		///Gets or sets the test context which provides
		///information about and functionality for the current test run.
		///</summary>
		public TestContext TestContext {
			get;
			set;
		}

		[TestCleanup]
		public void TestCleanup() {
			RestoreAllStubs(TestContext.TestName);

			AfterEachTest();
		}

		[TestInitialize]
		public void TestInitialization() {
			BeforeEachTest();
		}

		protected virtual void AfterEachTest() {

		}

		protected virtual void BeforeEachTest() {

		}

		internal void ReplaceStubWith(IConsoleStub stub) {
			Replace(stub, typeof(IConsoleStub), TestContext.TestName);
		}

		internal void ReplaceStubWith(IDirectoryStub stub) {
			Replace(stub, typeof(IDirectoryStub), TestContext.TestName);
		}

		internal void ReplaceStubWith(IEnvironmentStub stub) {
			Replace(stub, typeof(IEnvironmentStub), TestContext.TestName);
		}

		internal void ReplaceStubWith(IFileStub stub) {
			Replace(stub, typeof(IFileStub), TestContext.TestName);
		}

		internal void ReplaceStubWith(IPathStub stub) {
			Replace(stub, typeof(IPathStub), TestContext.TestName);
		}

		private static void Replace(object replacement, Type replacementType, string testName) {
			if (replacement == null)
				throw new ArgumentNullException();

			StubField data;
			if (!_mapper.TryGetValue(replacementType, out data))
				data = PopulateCacheForType(replacementType);

			while (!TryObtainStubUsage(data, testName)) {
				System.Threading.Thread.Sleep(TimeSpan.FromMilliseconds(250));
			}

			data.Field.SetValue(data.ClassToStub, replacement);
		}

		private static bool TryObtainStubUsage(StubField data, string testName) {
			if (!string.IsNullOrWhiteSpace(data.TestNameUsingStub))
				return false;

			lock (data.LockIsStubInUse) {
				if (!string.IsNullOrWhiteSpace(data.TestNameUsingStub))
					return false;

				data.TestNameUsingStub = testName;
			}

			return true;
		}

		private static void RestoreAllStubs(string testName) {
			var fields = GetFieldFromTestName(testName);
			if (fields.IsEmpty())
				return;

			foreach (var field in fields) {
				if (testName.Equals(field.TestNameUsingStub)) {
					lock (field.LockIsStubInUse) {
						if (testName.Equals(field.TestNameUsingStub)) {
							field.Field.SetValue(field.ClassToStub, null);
							field.TestNameUsingStub = null;
						}
					}
				}
			}
		}

		private static IList<StubField> GetFieldFromTestName(string testName) {
			var result = from v in _mapper.Values
						 where testName.Equals(v.TestNameUsingStub)
						 select v;

			return result.ToList();
		}

		private static StubField PopulateCacheForType(Type interfaceType) {
			var attrs = interfaceType.GetCustomAttributes(typeof(MockForClass), false);
			if (attrs.IsEmpty()) {
				throw new InvalidOperationException();
			}

			var classType = ((MockForClass)attrs.First()).ClassMocked;

			var field = classType.GetField(FieldName, BindingFlags.Static | BindingFlags.NonPublic);
			if (field == null) {
				throw new InvalidOperationException(string.Format(
					"Cannot find a private static field named '{0}' of type '{1}' on the '{2}' class.",
					FieldName,
					interfaceType,
					classType));
			}

			if (field.FieldType != interfaceType) {
				throw new InvalidOperationException(string.Format(
					"The private static field named '{0}' on the '{2}' class is not of type '{1}'",
					FieldName,
					interfaceType,
					classType));
			}

			var storage = new StubField(classType, field);
			if (_mapper.TryAdd(interfaceType, storage))
				return storage;

			_mapper.TryGetValue(interfaceType, out storage);
			return storage;
		}
		private class StubField {
			private readonly Type _class;
			private readonly FieldInfo _interfaceStubField;
			private readonly object _lockObject;

			public Type ClassToStub {
				get {
					return _class;
				}
			}

			public FieldInfo Field {
				get {
					return _interfaceStubField;
				}
			}

			public object LockIsStubInUse {
				get {
					return _lockObject;
				}
			}

			public string TestNameUsingStub {
				get;
				set;
			}

			public StubField(Type toStub, FieldInfo stubField) {
				_class = toStub;
				_interfaceStubField = stubField;
				_lockObject = new object();
			}
		}


	}
}
