using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Diagnostics;

namespace NetPerf.Common.Instrumentation {


	[ContractClass(typeof(ITypeRecordContract))]
	public interface ITypeRecord : IChildRecordWithGenericArguments {

		ReadOnlyCollection<IEventRecord> Events {
			get;
		}

		ReadOnlyCollection<IMethodRecord> Methods {
			get;
		}

		ReadOnlyCollection<IPropertyRecord> Properties {
			get;
		}

		IEventRecord AddEvent(string signature, int? addMethodId, int? removeMethodId, int? fireMethodId = null, int[] otherMethodIds = null, string[] customAttributes = null);
		IMethodRecord AddMethod(string signature, string hash, HashAlgorithm hashAlgorithm, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArgumetns = null);
		IPropertyRecord AddProperty(string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds = null, string[] customAttributes = null);
	}

	[DebuggerDisplay("{Signature}")]
	[ContractClassFor(typeof(ITypeRecord))]
	abstract class ITypeRecordContract : ITypeRecord {

		ReadOnlyCollection<IEventRecord> ITypeRecord.Events {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IEventRecord>>() != null);
				return default(ReadOnlyCollection<IEventRecord>);
			}
		}

		ReadOnlyCollection<IMethodRecord> ITypeRecord.Methods {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IMethodRecord>>() != null);
				return default(ReadOnlyCollection<IMethodRecord>);
			}
		}

		ReadOnlyCollection<IPropertyRecord> ITypeRecord.Properties {
			get {
				Contract.Ensures(Contract.Result<ReadOnlyCollection<IPropertyRecord>>() != null);

				return default(ReadOnlyCollection<IPropertyRecord>);
			}
		}

		IEventRecord ITypeRecord.AddEvent(string signature, int? addMethodId, int? removeMethodId,
			int? fireMethodId, int[] otherMethodIds, string[] customAttributes) //
		{
			Contract.Requires(!string.IsNullOrEmpty(signature));
			Contract.Requires((addMethodId == null && removeMethodId == null) || (addMethodId > 0 && removeMethodId > 0));

			Contract.Ensures(Contract.Result<IEventRecord>() != null);
			return default(IEventRecord);
		}

		IMethodRecord ITypeRecord.AddMethod(string signature, string hash, HashAlgorithm hashAlgorithm, string[] customAttributes, int[] sourceFileIds, string[] genericArguments) {
			Contract.Requires(!String.IsNullOrEmpty(signature));
			Contract.Requires(hash != null);
			Contract.Requires(hashAlgorithm != null);

			Contract.Ensures(Contract.Result<IMethodRecord>() != null);
			return default(IMethodRecord);
		}

		IPropertyRecord ITypeRecord.AddProperty(string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds, string[] customAttributes) {
			Contract.Requires(!String.IsNullOrEmpty(signature));
			Contract.Requires((getMethodId.HasValue && getMethodId > 0) || (setMethodId.HasValue && setMethodId > 0));

			Contract.Ensures(Contract.Result<IPropertyRecord>() != null);
			return default(IPropertyRecord);
		}

		ReadOnlyCollection<string> IChildRecordWithGenericArguments.GenericArguments {
			get {
				return default(ReadOnlyCollection<string>);
			}
		}

		ReadOnlyCollection<string> IChildRecordWithCustomAttributes.CustomAttributes {
			get {
				return default(ReadOnlyCollection<string>);
			}
		}

		int IChildRecord.ParentId {
			get {
				return default(int);
			}
		}

		int IRecord.Id {
			get {
				return default(int);
			}
		}

		string IRecord.Signature {
			get {
				return default(string);
			}
		}
	}
}
