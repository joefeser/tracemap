using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;

namespace NetPerf.UnitTests {

	/// <summary>
	/// Used only so we have a type that we can use for the assembly for the bootstrapper.
	/// </summary>
	public class TestPlaceHolder {

	}

	internal static class ILHelper {

		private const string ILAsmPath = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319\ilasm.exe";

		public static Moq.Mock<IMessagingController> GetMessageController(bool wantsEnterMessages, bool wantsExitMessages, bool wantsSeqPts = false) {
			var controller = new Moq.Mock<IMessagingController>();
			controller.Setup(c => c.WantsMethodEnterMessages).Returns(wantsEnterMessages);
			controller.Setup(c => c.WantsMethodExitMessages).Returns(wantsExitMessages);
			controller.Setup(c => c.WantsSeqPointEndMessages).Returns(wantsSeqPts);

			return controller;
		}

		private static string CompileTestIL(string testFileName) {
			testFileName = Path.GetFullPath(testFileName);

			var tempFileName = Path.GetTempFileName();
			var arguments = string.Format("/NOLOGO /DLL /DEBUG /OPTIMIZE /OUTPUT=\"{0}\" \"{1}\"", tempFileName, testFileName);
			var procInfo = new ProcessStartInfo(ILAsmPath, arguments) {
				CreateNoWindow = true,
				UseShellExecute = false, // to get rid of the flashing console window
				RedirectStandardOutput = true
			};

			var p = Process.Start(procInfo);

			p.WaitForExit(30 * 1000);
			var data = p.StandardOutput.ReadToEnd();

			Assert.AreEqual(0, p.ExitCode, data);

			return tempFileName;
		}

		/// <summary>
		/// Ostensibly, clones an IL method. In reality, the clone only works if the instructions in the
		/// method are either nops or rets.
		/// </summary>
		/// <param name="original"></param>
		/// <returns></returns>
		public static Mono.Cecil.Cil.MethodBody CloneMethod(Mono.Cecil.Cil.MethodBody original) {
			var p = original;

			var method = new Mono.Cecil.Cil.MethodBody(p.Method);

			var x = method.GetILProcessor();

			for (int i = 0; i < p.Instructions.Count; i++) {
				var instr = p.Instructions[i];
				var ni = Mono.Cecil.Cil.Instruction.Create(instr.OpCode);
				ni.Offset = instr.Offset;
				ni.SequencePoint = instr.SequencePoint;
				x.Append(ni);
			}

			return method;
		}

		public static Mono.Cecil.Cil.MethodBody GetMethodBody(string testFileName, string methodName) {
			return GetMethodBody(GetAssembly(testFileName), methodName);
		}

		public static Mono.Cecil.Cil.MethodBody GetMethodBody(Mono.Cecil.AssemblyDefinition assembly, string methodName) {
			var methods = from m in assembly.Modules
						  from t in m.Types
						  from mm in t.Methods
						  where mm.Name == methodName || mm.FullName == methodName
						  select mm;

			var method = methods.Single();
			return method.Body;
		}

		public static Mono.Cecil.AssemblyDefinition GetAssembly(string testFileName) {
			var assemblyName = CompileTestIL(testFileName);

			var pdbFileName = Path.ChangeExtension(assemblyName, "pdb");
			var symbolStream = new MemoryStream(File.ReadAllBytes(pdbFileName));

			var readerParams = new Mono.Cecil.ReaderParameters(Mono.Cecil.ReadingMode.Immediate) {
				ReadSymbols = true,
				SymbolStream = symbolStream,
				SymbolReaderProvider = new Mono.Cecil.Pdb.PdbReaderProvider()
			};

			var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyName, readerParams);

			// keeps the temporary files directory from filling up
			File.Delete(assemblyName);
			File.Delete(pdbFileName);

			return assembly;
		}

		public static Mono.Cecil.PropertyDefinition GetPropertyBody(string testFileName, string propertyName) {
			var assemblyName = CompileTestIL(testFileName);

			var items = from m in Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyName).Modules
						from t in m.Types
						from mm in t.Properties
						where mm.Name == propertyName || mm.FullName == propertyName
						select mm;

			var item = items.Single();
			return item;
		}

		public static Mono.Cecil.EventDefinition GetEvent(string testFileName, string eventName) {
			var assemblyName = CompileTestIL(testFileName);

			var items = from m in Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyName).Modules
						from t in m.Types
						from mm in t.Events
						where mm.Name == eventName || mm.FullName == eventName
						select mm;

			var item = items.Single();
			return item;
		}

		public static Mono.Cecil.TypeDefinition GetType(string testFileName, string typeName) {
			var assemblyName = CompileTestIL(testFileName);

			var types = from m in Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyName).Modules
						from t in m.Types
						where t.Name == typeName || t.FullName == typeName
						select t;

			var type = types.Single();
			return type;
		}

		public static Mono.Cecil.ModuleDefinition GetModule(string testFileName, string moduleName) {
			var assemblyName = CompileTestIL(testFileName);

			var modules = from m in Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyName).Modules
						  where m.Name == moduleName
						  select m;

			var module = modules.Single();
			return module;
		}

	}
}
