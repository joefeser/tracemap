using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System;
using System.Text;

namespace NetPerf.Common.Instrumentation {

	public class InstrumentFactory : IInstrumentFactory, IInstrumentationData {

		readonly Dictionary<string, IAssemblyRecord> _assemblies;

		public ReadOnlyCollection<IAssemblyRecord> Assemblies {
			get {
				return _assemblies.Values.ToList().AsReadOnly();
			}
		}

		private string _instanceHash;

		public string InstanceHash {
			get {
				return _instanceHash;
			}
			set {
				if (value == null) {
					throw new ArgumentNullException("value");
				}
				_instanceHash = value;
			}
		}

		public IMessagingController MessageController {
			get;
			private set;
		}

		public InstrumentFactory() {
			Contract.Ensures(_assemblies != null);
			Contract.Ensures(MessageController != null);
			//Contract.Ensures(InstanceHash != null);

			MessageController = new MessagingController();
			_assemblies = new Dictionary<string, IAssemblyRecord>(StringComparer.OrdinalIgnoreCase);
			InstanceHash = Guid.NewGuid().ToString() + "|" + Guid.NewGuid().ToString();
		}

		public int LookupSourceFile(int methodId, string uri) {
			var method = (from apair in _assemblies
						  from module in apair.Value.Modules
						  from t in module.Types
						  from m in t.Methods
						  where m.Id == methodId
						  select m)
						  .Single();

			var possibleIds = method.SourceFileIds;

			var x = (from apair in _assemblies
					 from m in apair.Value.Modules
					 from file in m.SourceFiles
					 where possibleIds.Contains(file.Id) && file.Signature == uri
					 select file)
					.SingleOrDefault();

			return x == null ? -1 : x.Id;
		}

		public int RegisterAssembly(string assemblyIdentity, string preInstrumentedHash, HashAlgorithm preInstrumentedHashAlgorithm) {

			return _assemblies.AtomicAdd(assemblyIdentity,
				() => new AssemblyRecord(UniqueIdGenerator.NextId(typeof(AssemblyRecord)), assemblyIdentity, preInstrumentedHash, preInstrumentedHashAlgorithm),
				record => record.Id);
		}

		public int RegisterModule(int assemblyid, string moduleName) {
			var assembly = (from pair in _assemblies
							where pair.Value.Id == assemblyid
							select pair.Value)
							.Single();

			return assembly.AddModule(moduleName).Id;
		}

		public int RegisterType(int moduleId, string signature, string[] customAttributes = null, string[] genericArguments = null) {
			var module = (from apair in _assemblies
						  from mpair in apair.Value.Modules
						  where mpair.Id == moduleId
						  select mpair)
						   .Single();

			return module.AddType(signature, customAttributes, genericArguments).Id;
		}

		public int RegisterEvent(int typeId, string signature, int? addMethodId, int? removeMethodId,
						  int? fireMethodId = null, int[] otherMethodIds = null, string[] customAttributes = null) //
		{
			var type = (from apair in _assemblies
						from mpair in apair.Value.Modules
						from t in mpair.Types
						where t.Id == typeId
						select t)
						.Single();

			return type.AddEvent(signature, addMethodId, removeMethodId, fireMethodId, otherMethodIds, customAttributes).Id;
		}

		public int RegisterMethod(int typeId, string signature, byte[] rawMethodBytes = null, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArguments = null) {
			var type = (from apair in _assemblies
						from mpair in apair.Value.Modules
						from t in mpair.Types
						where t.Id == typeId
						select t)
						.Single();

			var hashAlgo = HashAlgorithm.SHA512;
			var hash = (rawMethodBytes == null || rawMethodBytes.Length < 1) ? string.Empty : hashAlgo.ComputeHash(rawMethodBytes);

			// compute the hash of the method from the instructions
			return type.AddMethod(signature, hash, hashAlgo, customAttributes, sourceFileIds, genericArguments).Id;
		}

		public int RegisterPoint(int methodId, int? sourceFileId = null, int? startLine = null, int? startColumn = null, int? endLine = null, int? endColumn = null) {
			var method = (from apair in _assemblies
						  from module in apair.Value.Modules
						  from t in module.Types
						  from m in t.Methods
						  where m.Id == methodId
						  select m)
						  .Single();

			return method.AddPoint(sourceFileId, startLine, startColumn, endLine, endColumn).Id;
		}

		public int RegisterProperty(int typeId, string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds = null, string[] customAttributes = null) {
			var type = (from apair in _assemblies
						from mpair in apair.Value.Modules
						from t in mpair.Types
						where t.Id == typeId
						select t)
						.Single();

			return type.AddProperty(signature, getMethodId, setMethodId, otherMethodIds, customAttributes).Id;
		}

		public int RegisterSourceFile(int moduleId, string uri, byte[] hash, string hashAlgorithm, string sourceLanguage) {
			var module = (from apair in _assemblies
						  from mpair in apair.Value.Modules
						  where mpair.Id == moduleId
						  select mpair)
						   .Single();

			return module.AddSourceFile(uri, Utility.ToHexString(hash), hashAlgorithm, sourceLanguage).Id;
		}

		public ErrorTuple Save(FilePath toFile) {
			return InstrumentationDataSerializer.Write(toFile, this);
		}

		public string GetMetadataForAssembly(int assemblyId, out ErrorTuple error) {

			//we need to just include the metadata for this assembly and not the entire run.
			var toSerialize = new InstrumentFactory();

			var assembly = Assemblies.FirstOrDefault(item => item.Id == assemblyId);
			toSerialize._assemblies.Add(assembly.Signature, assembly);

			string retVal = null;
			var tempError = InstrumentationDataSerializer.WriteAsString(out retVal, toSerialize);
			error = tempError;
			return retVal;
		}

	}
}
