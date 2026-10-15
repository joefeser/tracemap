using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.Instrumentation;
using Mono.Cecil;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Tests.Test_References {
	internal class ILInstrumenter_Privates {

		public static void InstrumentModule(Mono.Cecil.ModuleDefinition module, int assemblyId, IInstrumentFactory factory) {
			var obj = new PrivateType(typeof(ILInstrumenter));
			obj.InvokeStatic("InstrumentModule", module, assemblyId, factory);
		}

		public static ILInstrumenter.MessagingImports CreateImports(Mono.Cecil.ModuleDefinition module, int moduleId) {
			return new ILInstrumenter.MessagingImports(module, moduleId);
		}

		public static int? InstrumentMethod(MethodDefinition method, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, ILInstrumenter.MessagingImports imports, bool forceInstrument = false) {
			var hel = new PrivateType(typeof(ILInstrumenter));

			return (int?)hel.InvokeStatic("InstrumentMethod", method, typeId, moduleId, assemblyId, factory, imports, forceInstrument);
		}

		public static void InstrumentProperty(PropertyDefinition property, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, ILInstrumenter.MessagingImports imports) {
			var hel = new PrivateType(typeof(ILInstrumenter));
			hel.InvokeStatic("InstrumentProperty", property, typeId, moduleId, assemblyId, factory, imports);
		}

		public static void InstrumentEvent(EventDefinition evnt, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, ILInstrumenter.MessagingImports imports) {
			var hel = new PrivateType(typeof(ILInstrumenter));
			hel.InvokeStatic("InstrumentEvent", evnt, typeId, moduleId, assemblyId, factory, imports);
		}

		public static void InstrumentType(TypeDefinition type, int moduleId, int assemblyId, IInstrumentFactory factory, ILInstrumenter.MessagingImports imports) {
			var hel = new PrivateType(typeof(ILInstrumenter));
			hel.InvokeStatic("InstrumentType", type, moduleId, assemblyId, factory, imports);
		}

		public static void InsertInstrumentationCallbacks(int methodId, int moduleId, int assemblyId, Mono.Cecil.Cil.MethodBody body, ILInstrumenter.MessagingImports imports, IInstrumentFactory factory) {
			var hel = new PrivateType(typeof(ILInstrumenter));
			hel.InvokeStatic("InsertInstrumentationCallbacks", methodId, moduleId, assemblyId, body, imports, factory);
		}
	}
}
