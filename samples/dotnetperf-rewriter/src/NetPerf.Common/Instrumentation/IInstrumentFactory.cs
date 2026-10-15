using System.Diagnostics.Contracts;
using System;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(IInstrumentFactoryContract))]
	public interface IInstrumentFactory {

		IMessagingController MessageController {
			get;
		}

		//TODO we need to fix this bug where every assembly does not have the full metadata.
		string GetMetadataForAssembly(int assemblyId, out ErrorTuple error);

		//TODO add a way to specify an overall context
		//something that will be unique to an instrumentation set

		int LookupSourceFile(int methodId, string uri);

		int RegisterAssembly(string assemblyIdentity, string preInstrumentedHash, HashAlgorithm preInstrumentedHashAlgorithm);
		int RegisterModule(int assemblyId, string moduleName);
		int RegisterType(int moduleId, string signature, string[] customAttributes = null, string[] genericArguments = null);
		int RegisterEvent(int typeId, string signature, int? addMethodId, int? removeMethodId,
						  int? fireMethodId = null, int[] otherMethodIds = null, string[] customAttributes = null);
		int RegisterMethod(int typeId, string signature, byte[] rawMethodBytes = null, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArguments = null);
		int RegisterPoint(int methodId, int? sourceFileId = null, int? startLine = null, int? startColumn = null, int? endLine = null, int? endColumn = null);
		int RegisterProperty(int typeId, string signature, int? getMethodId, int? setMethodId,
							 int[] otherMethodIds = null, string[] customAttributes = null);
		int RegisterSourceFile(int moduleId, string uri, byte[] hash, string hashAlgorithm, string sourceLanguage);
	}

	[ContractClassFor(typeof(IInstrumentFactory))]
	abstract class IInstrumentFactoryContract : IInstrumentFactory {

		string IInstrumentFactory.GetMetadataForAssembly(int assemblyId, out ErrorTuple error) {
			error = new ErrorTuple();
			return string.Empty;
		}

		IMessagingController IInstrumentFactory.MessageController {
			get {
				Contract.Ensures(Contract.Result<IMessagingController>() != null);

				return default(IMessagingController);
			}
		}

		int IInstrumentFactory.LookupSourceFile(int methodId, string uri) {
			Contract.Requires(methodId > 0);
			Contract.Requires(!String.IsNullOrEmpty(uri));

			return default(int);
		}

		int IInstrumentFactory.RegisterAssembly(string assemblyIdentity, string preInstrumentedHash, HashAlgorithm preInstrumentedHashAlgorithm) {
			Contract.Requires(!String.IsNullOrEmpty(assemblyIdentity));
			Contract.Requires(!String.IsNullOrEmpty(preInstrumentedHash));
			Contract.Requires(preInstrumentedHashAlgorithm != null);
			Contract.Ensures(Contract.Result<int>() != 0);
			return default(int);
		}

		int IInstrumentFactory.RegisterModule(int assemblyId, string moduleName) {
			Contract.Requires(assemblyId != 0);
			Contract.Requires(!String.IsNullOrEmpty(moduleName));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterType(int moduleId, string signature, string[] customAttributes, string[] genericArguments) {
			Contract.Requires(moduleId > 0);
			Contract.Requires(!String.IsNullOrEmpty(signature));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterEvent(int typeId, string signature, int? addMethodId, int? removeMethodId,
						  int? fireMethodId, int[] otherMethodIds, string[] customAttributes) {
			Contract.Requires(typeId > 0);
			Contract.Requires(!string.IsNullOrEmpty(signature));
			Contract.Requires((addMethodId == null && removeMethodId == null) || (addMethodId > 0 && removeMethodId > 0));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterMethod(int typeId, string signature, byte[] rawMethodBytes, string[] customAttributes, int[] sourceFileIds, string[] genericArguments) {
			Contract.Requires(typeId > 0);
			Contract.Requires(!String.IsNullOrEmpty(signature));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterPoint(int methodId, int? sourceFileId, int? startLine, int? startColumn, int? endLine, int? endColumn) {
			Contract.Requires(methodId > 0);
			Contract.Requires(sourceFileId == null || sourceFileId > 0);
			Contract.Requires(startLine == null || startLine >= 0);
			Contract.Requires(startColumn == null || startColumn >= 0);
			Contract.Requires(endLine == null || endLine >= 0);
			Contract.Requires(endColumn == null || endColumn >= 0);

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterProperty(int typeId, string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds, string[] customAttributes) {
			Contract.Requires(typeId > 0);
			Contract.Requires(!String.IsNullOrEmpty(signature));
			Contract.Requires((getMethodId.HasValue && getMethodId > 0) || (setMethodId.HasValue && setMethodId > 0));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

		int IInstrumentFactory.RegisterSourceFile(int moduleId, string uri, byte[] hash, string hashAlgorithm, string sourceLanguage) {
			Contract.Requires(moduleId > 0);
			Contract.Requires(!String.IsNullOrEmpty(uri));

			Contract.Ensures(Contract.Result<int>() > 0);

			return default(int);
		}

	}
}
