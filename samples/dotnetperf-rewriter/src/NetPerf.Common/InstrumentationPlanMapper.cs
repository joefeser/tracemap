using System;
using System.Linq;
using NetPerf.Common.Instrumentation;

namespace NetPerf.Common {

	/// <summary>
	/// bleh name. think of another one
	/// </summary>
	public class InstrumentationPlanMapper {

		private IInstrumentationData _data;

		public InstrumentationPlanMapper(IInstrumentationData data) {
			_data = data;
		}

		public string GetAssemblyName(int assemblyId) {
			return _data.Assemblies
				.Where(a => a.Id == assemblyId)
				.Select(a => a.Signature)
				.DefaultIfEmpty("No assembly w/ id: " + assemblyId.ToUserString())
				.FirstOrDefault();
		}

		public string GetMethodName(int methodId) {
			return _data.Assemblies
				.SelectMany(a => a.Modules)
				.SelectMany(m => m.Types)
				.SelectMany(t => t.Methods)
				.Where(m => m.Id == methodId)
				.Select(m => m.Signature)
				.DefaultIfEmpty("No method w/ id: " + methodId.ToUserString())
				.FirstOrDefault();
		}
	}
}
