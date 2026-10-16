using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.Instrumentation;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.Tests.Test_References {
	internal class InstrumentationDataSerializer_Privates {

		private static readonly PrivateType _privates = new PrivateType(typeof(InstrumentationDataSerializer));

		public static IAssemblyRecord ReadAssemblyRecord(XmlReader reader) {
			return (IAssemblyRecord)_privates.InvokeStatic("ReadAssemblyRecord", reader);
		}

		public static IModuleRecord ReadModuleRecord(XmlReader reader, int assemblyId) {
			return (IModuleRecord)_privates.InvokeStatic("ReadModuleRecord", reader, assemblyId);
		}

		public static ISourceFileRecord ReadSourceFileRecord(XmlReader reader, int moduleId) {
			return (ISourceFileRecord)_privates.InvokeStatic("ReadSourceFileRecord", reader, moduleId);
		}

		public static ITypeRecord ReadTypeRecord(XmlReader reader, int moduleId) {
			return (ITypeRecord)_privates.InvokeStatic("ReadTypeRecord", reader, moduleId);
		}

		public static IEventRecord ReadEventRecord(XmlReader reader, int typeId) {
			return (IEventRecord)_privates.InvokeStatic("ReadEventRecord", reader, typeId);
		}

		public static IMethodRecord ReadMethodRecord(XmlReader reader, int typeId) {
			return (IMethodRecord)_privates.InvokeStatic("ReadMethodRecord", reader, typeId);
		}

		public static IPointRecord ReadPointRecord(XmlReader reader, int methodId) {
			return (IPointRecord)_privates.InvokeStatic("ReadPointRecord", reader, methodId);
		}

		public static IPropertyRecord ReadPropertyRecord(XmlReader reader, int typeId) {
			return (IPropertyRecord)_privates.InvokeStatic("ReadPropertyRecord", reader, typeId);
		}

		public static void Write(XmlWriter writer, IInstrumentationData data) {
			_privates.InvokeStatic("Write", writer, data);
		}

		public static void WriteAssemblyRecord(XmlWriter writer, IAssemblyRecord record) {
			_privates.InvokeStatic("WriteAssemblyRecord", writer, record);
		}

		public static void WriteModuleRecord(XmlWriter writer, IModuleRecord record) {
			_privates.InvokeStatic("WriteModuleRecord", writer, record);
		}

		public static void WriteSourceFileRecord(XmlWriter writer, ISourceFileRecord record) {
			_privates.InvokeStatic("WriteSourceFileRecord", writer, record);
		}

		public static void WriteTypeRecord(XmlWriter writer, ITypeRecord record) {
			_privates.InvokeStatic("WriteTypeRecord", writer, record);
		}

		public static void WriteEventRecord(XmlWriter writer, IEventRecord record) {
			_privates.InvokeStatic("WriteEventRecord", writer, record);
		}

		public static void WriteMethodRecord(XmlWriter writer, IMethodRecord record) {
			_privates.InvokeStatic("WriteMethodRecord", writer, record);
		}

		public static void WritePointRecord(XmlWriter writer, IPointRecord record) {
			_privates.InvokeStatic("WritePointRecord", writer, record);
		}

		public static void WritePropertyRecord(XmlWriter writer, IPropertyRecord record) {
			_privates.InvokeStatic("WritePropertyRecord", writer, record);
		}

	}
}