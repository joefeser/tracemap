using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {

	[DebuggerDisplay("{Signature}")]
	internal class TypeRecord : AChildRecordWithGenericArguments, ITypeRecord {

		readonly Dictionary<string, IMethodRecord> _methods;
		readonly Dictionary<string, IPropertyRecord> _properties;
		readonly Dictionary<string, IEventRecord> _events;

		public ReadOnlyCollection<IEventRecord> Events {
			get {
				return _events.Values.ToList().AsReadOnly();
			}
		}

		public ReadOnlyCollection<IMethodRecord> Methods {
			get {
				return _methods.Values.ToList().AsReadOnly();
			}
		}

		public ReadOnlyCollection<IPropertyRecord> Properties {
			get {
				return _properties.Values.ToList().AsReadOnly();
			}
		}

		public IEventRecord AddEvent(string signature, int? addMethodId, int? removeMethodId,
			int? fireMethodId = null, int[] otherMethodIds = null, string[] customAttributes = null) //
		{
			return _events.AtomicAdd(signature,
				() => new EventRecord(UniqueIdGenerator.NextId(typeof(EventRecord)), Id, signature, addMethodId,
					removeMethodId, fireMethodId, otherMethodIds, customAttributes),
				record => record);
		}

		public IMethodRecord AddMethod(string signature, string hash, HashAlgorithm hashAlgorithm, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArguments = null) {
			return _methods.AtomicAdd(signature,
				() => new MethodRecord(UniqueIdGenerator.NextId(typeof(MethodRecord)), Id, signature, hash, hashAlgorithm, customAttributes, sourceFileIds, genericArguments),
				record => record);
		}

		public IPropertyRecord AddProperty(string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds = null, string[] customAttributes = null) {
			return _properties.AtomicAdd(signature,
				() => new PropertyRecord(UniqueIdGenerator.NextId(typeof(PropertyRecord)), Id, signature, getMethodId, setMethodId, otherMethodIds, customAttributes),
				record => record);
		}

		public void LinkEvents(IEnumerable<IEventRecord> events) {
			_events.AddUniqueChildRecords(events,
				evnt => evnt.ParentId == Id,
				(left, right) => left.Signature == right.Signature,
				evnt => evnt.Signature);
		}

		public void LinkMethods(IEnumerable<IMethodRecord> methods) {
			_methods.AddUniqueChildRecords(methods,
				method => method.ParentId == Id,
				(left, right) => left.Signature == right.Signature,
				method => method.Signature);
		}

		public void LinkProperties(IEnumerable<IPropertyRecord> properties) {
			_properties.AddUniqueChildRecords(properties,
				property => property.ParentId == Id,
				(left, right) => left.Signature == right.Signature,
				property => property.Signature);
		}

		public TypeRecord(int id, int moduleId, string signature, string[] customAttributes = null, string[] genericArguments = null)
			: base(id, moduleId, signature, customAttributes, genericArguments) //
		{
			Contract.Requires(id > 0);
			Contract.Requires(moduleId > 0);
			Contract.Requires(!String.IsNullOrEmpty(signature));

			Contract.Ensures(_methods != null);
			Contract.Ensures(_properties != null);
			Contract.Ensures(_events != null);

			_methods = new Dictionary<string, IMethodRecord>();
			_properties = new Dictionary<string, IPropertyRecord>();
			_events = new Dictionary<string, IEventRecord>();
		}

	}
}
