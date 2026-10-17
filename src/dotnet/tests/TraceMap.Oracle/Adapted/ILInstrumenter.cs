// ADAPTED COPY of samples/dotnetperf-rewriter/src/NetPerf/ILInstrumenter.cs at
// pinned corpus SHA db8c3359 (PROVENANCE.md). Adaptations for modern Mono.Cecil
// are enumerated and pinned by OracleAdaptationPinTests; the corpus tree itself
// is never modified.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common;
using System.Diagnostics.Contracts;
using Mono.Cecil;
using System.IO;
using NetPerf.Client;
using NetPerf.Common.Instrumentation;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Diagnostics;

namespace NetPerf {
	// #856 stage 2b ADAPTATION of the byte-pinned corpus source (see
	// OracleAdaptationPinTests for the exact diff pin): modern Cecil moved
	// sequence points from Instruction to MethodDefinition.DebugInformation;
	// this bridge routes the era reads.
	internal static class SeqPointBridge
	{
		internal static SequencePoint SeqPoint(MethodBody body, Instruction instruction)
			=> body.Method.DebugInformation.GetSequencePoint(instruction);
	}


	internal static class ILInstrumenter {

		private const string MetaDataModule = "NetPerf";
		private const string MetaDataType = "NetPerfMetadata";
		private const string MetaDataFullName = MetaDataModule + "." + MetaDataType;

		/// <summary>
		/// This is temporary until we can totally support it on the command line.
		/// </summary>
		public static bool InstrumentSequencePoints {
			get;
			set;
		}

		public static bool IsAssemblyInstrumented(FilePath assembly, FilePath assemblySymbols) {
			if (!(assembly != null)) throw new ArgumentNullException("assembly");
			if (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));
			try {
				var readParams = GetAssemblyReaderParams(assemblySymbols);
				var assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes
				if (assemDef != null) {

					//determine if our type is in the assembly
					var type = (from m in assemDef.Modules
								from t in m.Types
								where t.FullName == MetaDataFullName
								select t).FirstOrDefault();

					if (type != null) {
						//we still want the IL data stored in the factory.
						return true;
					}
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(string.Format("Is your PDB out of sync with your DLL? Error AssemblyIsInstrumented:{0}. Reason:{1}", assembly.FullPath, ex.Message));
				Console.WriteLine(string.Format("Is your PDB out of sync with your DLL? Error AssemblyIsInstrumented:{0}. Reason:{1}", assembly.FullPath, ex.Message));
				return true;
			}

			return false;
		}

		public static void InstrumentAssembly(FilePath assembly, FilePath assemblySymbols, FilePath keyFile, IInstrumentFactory factory) {
			if (!(assembly != null)) throw new ArgumentNullException("assembly");
			if (!(factory != null)) throw new ArgumentNullException("factory");
			if (!(factory.MessageController != null)) throw new ArgumentNullException("factory.MessageController");
			if (!(assembly.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));
			if (!(assemblySymbols == null || assemblySymbols.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));
			if (!(keyFile == null || keyFile.IsAbsolute)) throw new ArgumentException(nameof(ArgumentException));

			if (IsAssemblyInstrumented(assembly, assemblySymbols)) {
				return;
			}

			try {
				var readParams = GetAssemblyReaderParams(assemblySymbols);

				var assemDef = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(assembly.FullPath)), readParams); // ADAPTATION: in-memory read; modern Cecil patches bodies from the stream during same-path writes
				if (assemDef != null) {
					// TODO once we follow and resolve references to assemblies, we will have to apply exclusions to them
					// the assembly was passed in, so we already know that the user wants it instrumented

					string hash = string.Empty;

					var hashAlgo = HashAlgorithm.SHA512;
					using (var fs = FileStub.OpenRead(assembly.FullPath)) {
						hash = hashAlgo.ComputeHash(fs);
					}

					var assemblyId = factory.RegisterAssembly(assemDef.Name.FullName, hash, hashAlgo);

					foreach (var module in assemDef.Modules) {
						InstrumentModule(module, assemblyId, factory);
					}

					CreateTypeForMetadata(factory, assemDef, assemblyId);

					var writeParams = GetAssemblyWriterParams(assemblySymbols, keyFile);
					assemDef.Write(assembly.FullPath, writeParams);
				}
			}
			catch (Exception ex) {
				Debug.WriteLine(string.Format("Is your PDB out of sync with your DLL? Error InstrumentAssembly:{0}. Reason:{1}", assembly.FullPath, ex.Message));
				Console.WriteLine(string.Format("Is your PDB out of sync with your DLL? Error InstrumentAssembly:{0}. Reason:{1}", assembly.FullPath, ex.Message));
			}
		}

		/// <summary>
		/// We will be placing all of the metadata as helpers to serve the client for performance reasons.
		/// </summary>
		/// <param name="assemDef"></param>
		private static void CreateTypeForMetadata(IInstrumentFactory factory, AssemblyDefinition assemDef, int assemblyId) {
			var netperfModule = assemDef.MainModule; //TODO place in each module when we can support it.

			var netperfType = netperfModule.Types.FirstOrDefault(item => item.Namespace == MetaDataModule && item.Name == MetaDataType);

			//not obvious but this is how you obtain a type reference for a new type member.
			var stringReference = netperfModule.Import(typeof(string));
			var intReference = netperfModule.Import(typeof(int));

			var netperfField = new FieldDefinition("InstrumentationData", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, stringReference);
			netperfType.Fields.Add(netperfField);

			ErrorTuple error = null;
			//TODO determine if we should do something about the error.
			var value = factory.GetMetadataForAssembly(assemblyId, out error);

			netperfField.Constant = value;

		}

		private static ReaderParameters GetAssemblyReaderParams(FilePath assemblySymbols) {
			var symbolsAvailable = assemblySymbols != null;
			var readParams = new ReaderParameters(ReadingMode.Immediate) { // ADAPTATION: deferred + same-path write corrupts output in modern Cecil
				AssemblyResolver = new DefaultAssemblyResolver(),
				ReadSymbols = symbolsAvailable,
				SymbolStream = assemblySymbols == null ? null : new MemoryStream(File.ReadAllBytes(assemblySymbols.FullPath)),
				SymbolReaderProvider = assemblySymbols == null ? null : new Mono.Cecil.Pdb.PdbReaderProvider()
			};
			return readParams;
		}

		private static WriterParameters GetAssemblyWriterParams(FilePath assemblySymbols, FilePath keyFile) {
			var symbolsAvailable = assemblySymbols != null;
			// ADAPTATION: era behavior set a PdbWriterProvider unconditionally, which
			// corrupts output when no symbols were read (oracle path passes none).
			var writeParams = new WriterParameters() {
				SymbolStream = symbolsAvailable ? new MemoryStream() : null,
				WriteSymbols = symbolsAvailable,
				SymbolWriterProvider = symbolsAvailable ? new Mono.Cecil.Pdb.PdbWriterProvider() : null
			};

			if (keyFile != null) {
				using (var fs = FileStub.Open(keyFile.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					writeParams.StrongNameKeyPair = new System.Reflection.StrongNameKeyPair(fs);
				}
			}

			return writeParams;
		}

		private static MessagingImports CreateImports(ModuleDefinition module, int moduleId) {
			return new MessagingImports(module, moduleId);
		}

		private static void InstrumentModule(ModuleDefinition module, int assemblyId, IInstrumentFactory factory) {
			// optimistically, we're ignoring module data for now,
			// because it shouldn't be necessary for mapping IL to source

			if (ShouldInstrumentModule(module, factory)) {

				var moduleId = factory.RegisterModule(assemblyId, module.Name);
				var imports = CreateImports(module, moduleId);

				foreach (var modType in FilterTypes(module.Types)) {
					InstrumentType(modType, moduleId, assemblyId, factory, imports);
				}


				////create a ..ctor for the field

				//var netperfType = new TypeDefinition(MetaDataModule, MetaDataType, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);
				//netperfType.BaseType = module.Import(typeof(System.Object)); // TypeReference("System", "Object", null, false); //
				//module.Types.Add(netperfType);

				//var netperfField = new FieldDefinition("ModuleIdentifier", FieldAttributes.Public | FieldAttributes.Static, imports.IntTypeRef); //| FieldAttributes.HasDefault
				//netperfType.Fields.Add(netperfField);

				//var ctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				//    module.Import(typeof(void)));
				//ctor.CustomAttributes.Add(new CustomAttribute(imports.CompilerGeneratedAttributeRef));

				//ctor.Body.MaxStackSize = 3;
				//var proc = ctor.Body.GetILProcessor();
				//proc.Append(proc.Create(OpCodes.Ldc_I4, moduleId));
				//proc.Append(proc.Create(OpCodes.Stsfld, imports.ModuleMethodIdFieldRef));
				//proc.Append(proc.Create(OpCodes.Ret));
				//netperfType.Methods.Add(ctor);

				//ctor = new MethodDefinition(".ctor", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
				//module.Import(typeof(void)));

				//ctor.Body.MaxStackSize = 3;

				//proc = ctor.Body.GetILProcessor();
				//proc.Append(proc.Create(OpCodes.Ldarg_0));
				//proc.Append(proc.Create(OpCodes.Call, module.Import(typeof(System.Object).GetConstructor(new System.Type[] { }))));
				//proc.Append(proc.Create(OpCodes.Ret));
				//netperfType.Methods.Add(ctor);

			}
		}

		private static IEnumerable<TypeDefinition> FilterTypes(Mono.Collections.Generic.Collection<TypeDefinition> collection) {
			return collection.Where(t => t.IsClass || t.IsValueType);
		}

		private static bool ShouldInstrumentModule(ModuleDefinition module, IInstrumentFactory factory) {
			return true;
		}

		private static void InstrumentType(TypeDefinition type, int moduleId, int assemblyId, IInstrumentFactory factory, MessagingImports imports) {
			// TODO once attribute exclusions are supported, will have to check for
			// attributes on the type and possibly not instrument it

			if (ShouldInstrumentType(type, factory)) {
				var customAttributes = type.CustomAttributes
					.Select(ca => ca.AttributeType.FullName)
					.ToArray();

				var genericParamters = type.GenericParameters
					.Select(ga => ga.FullName)
					.ToArray();

				// get a new class/type record from the ilinstrumenter factory
				var typeId = factory.RegisterType(moduleId, type.FullName, customAttributes, genericParamters);

				foreach (var method in FilterMethods(type.Methods)) {
					InstrumentMethod(method, typeId, moduleId, assemblyId, factory, imports);
				}

				foreach (var property in FilterProperties(type.Properties)) {
					InstrumentProperty(property, typeId, moduleId, assemblyId, factory, imports);
				}

				//NOTE: placing the instrumentation of nested types here
				//means that it's impossible to instrument an inner type if
				//the containing type is not instrumented.
				//if you want to instrument nested types even when their parent type
				//isn't instrumented, then move this code to the InstrumentModule method,
				//and remember to handle deeply nested classes of nested classes.
				foreach (var nestedType in FilterTypes(type.NestedTypes)) {
					InstrumentType(nestedType, moduleId, assemblyId, factory, imports);
				}
			}
		}

		private static bool ShouldInstrumentType(TypeDefinition type, IInstrumentFactory factory) {
			//too many lifecyle issue, do not ever instrument the metadata
			if (type.FullName == MetaDataFullName) {
				return false;
			}
			return true;
		}

		private static IEnumerable<EventDefinition> FilterEvents(Mono.Collections.Generic.Collection<EventDefinition> collection) {
			return collection;
		}

		private static void InstrumentEvent(EventDefinition evnt, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, MessagingImports imports) {
			// TODO once attribute exclusions are supported, will have to check for
			// attributes on the event and possibly not instrument it

			if (ShouldInstrumentEvent(evnt, factory)) {
				var addId = evnt.AddMethod == null
					? null
					: InstrumentMethod(evnt.AddMethod, typeId, moduleId, assemblyId, factory, imports, true);
				var removeId = evnt.RemoveMethod == null
					? null
					: InstrumentMethod(evnt.RemoveMethod, typeId, moduleId, assemblyId, factory, imports, true);

				// Mono.Cecil doesn't implement the fire method

				var otherIds = from m in evnt.OtherMethods
							   select InstrumentMethod(m, typeId, moduleId, assemblyId, factory, imports, true).Value;

				var customAttributes = evnt.CustomAttributes
					.Select(ca => ca.AttributeType.FullName)
					.ToArray();

				factory.RegisterEvent(typeId, evnt.FullName, addId, removeId, null, otherIds.ToArray(), customAttributes);
			}
		}

		private static bool ShouldInstrumentEvent(EventDefinition type, IInstrumentFactory factory) {
			return true;
		}

		private static IEnumerable<PropertyDefinition> FilterProperties(Mono.Collections.Generic.Collection<PropertyDefinition> collection) {
			return collection.Where(item =>
				(item.GetMethod != null && item.GetMethod.HasBody)
				|| (item.SetMethod != null && item.SetMethod.HasBody)
				|| (item.OtherMethods != null && item.OtherMethods.Any(om => om.HasBody))
				);
		}

		private static void InstrumentProperty(PropertyDefinition property, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, MessagingImports imports) {


			if (ShouldInstrumentProperty(property, factory)) {

				var getterId = property.GetMethod == null || !property.GetMethod.HasBody
					? null
					: InstrumentMethod(property.GetMethod, typeId, moduleId, assemblyId, factory, imports, true);

				var setterId = property.SetMethod == null || !property.SetMethod.HasBody
					? null
					: InstrumentMethod(property.SetMethod, typeId, moduleId, assemblyId, factory, imports, true);

				var otherIds = from m in property.OtherMethods
							   where m.HasBody
							   select InstrumentMethod(m, typeId, moduleId, assemblyId, factory, imports, true).Value;

				var customAttributes = property.CustomAttributes
					.Select(ca => ca.AttributeType.FullName)
					.ToArray();

				factory.RegisterProperty(typeId, property.FullName, getterId, setterId, otherIds.ToArray(), customAttributes);
			}
		}

		private static bool ShouldInstrumentProperty(PropertyDefinition property, IInstrumentFactory factory) {
			// TODO once attribute exclusions are supported, will have to check for
			// attributes on the property and possibly not instrument it

			return true;
		}

		private static IEnumerable<MethodDefinition> FilterMethods(Mono.Collections.Generic.Collection<MethodDefinition> collection) {
			// have to force the collection to be non-lazy since we are going to add another method to the type
			// if it is a generic type or it has a generic method. not only do we not want to instrument the method,
			// but after we add that method, accessing the IEnumerable<T> returned by this method will crash the program
			return collection
				.Where(m => m.HasBody && !(m.IsAddOn || m.IsRemoveOn || m.IsFire || m.IsGetter || m.IsSetter || m.IsOther))
				.ToList();
		}

		private static int? InstrumentMethod(MethodDefinition method, int typeId, int moduleId, int assemblyId, IInstrumentFactory factory, MessagingImports imports, bool forceInstrument = false) {
			if (ShouldInstrumentMethod(method, factory)) {
				var paramStr = method.Parameters.Aggregate(new StringBuilder(), (sb, param) => sb.Append(param.Name).Append(", "));
				if (paramStr.Length > 0)
					paramStr.Length -= 2;

				var customAttributes = method.CustomAttributes
					.Select(ca => ca.AttributeType.FullName)
					.ToArray();

				var methodSourceFileIds = method.Body.Instructions
					.Where(i => SeqPointBridge.SeqPoint(method.Body, i) != null)
					.Select(i => SeqPointBridge.SeqPoint(method.Body, i).Document)
					.Select(doc => factory.RegisterSourceFile(moduleId, doc.Url, doc.Hash, doc.HashAlgorithm.ToString(), doc.Language.ToString()))
					.Distinct()
					.ToArray();

				var methodGenericTypes = method.GenericParameters
					.Select(ga => ga.FullName)
					.ToArray();


				var currMethodId = factory.RegisterMethod(typeId, method.FullName, GetRawMethodBytes(method), customAttributes, methodSourceFileIds, methodGenericTypes);

				InsertInstrumentationCallbacks(currMethodId, moduleId, assemblyId, method.Body, imports, factory);

				return currMethodId;
			}

			return null;
		}

		private static byte[] GetRawMethodBytes(MethodDefinition def) {
			Contract.Requires(def != null);

			if (!def.HasBody || def.Body.Instructions.Count == 0) {
				return new byte[0];
			}

			//TODO should include the opcode's parameters as part of the byte array
			//for now, we are just going to use the instruction's opcode (the type of instruction it is)
			//to re-construct the method body. This means that e.g. every auto-implemented property will have the same hash

			var instrs = def.Body.Instructions;
			var rawBytes = new List<byte>(instrs.Sum(i => i.OpCode.Size));

			for (int i = 0; i < instrs.Count; i++) {
				var instr = instrs[i];
				if (instr.OpCode.Size == 1) {
					rawBytes.Add(instr.OpCode.Op2);
				}
				else {
					rawBytes.Add(instr.OpCode.Op1);
					rawBytes.Add(instr.OpCode.Op2);
				}

				rawBytes.AddRange(GetOperandAsBytes(instr));
			}

			return rawBytes.ToArray();
		}

		private static byte[] GetOperandAsBytes(Instruction instr) {
			try {


				if (instr.Operand != null) {
					var instrType = instr.Operand.GetType();
					var utf8 = new UTF8Encoding();

					if (typeof(TypeReference).IsAssignableFrom(instrType)) {
						return utf8.GetBytes(((TypeReference)instr.Operand).Name);
					}
					else if (instrType == typeof(CallSite)) {
						return utf8.GetBytes(((CallSite)instr.Operand).Name);
					}
					else if (typeof(MethodReference).IsAssignableFrom(instrType)) {
						return utf8.GetBytes(((MethodReference)instr.Operand).Name);
					}
					else if (typeof(FieldReference).IsAssignableFrom(instrType)) {
						return utf8.GetBytes(((FieldReference)instr.Operand).Name);
					}
					else if (typeof(VariableReference).IsAssignableFrom(instrType)) {
						return utf8.GetBytes(((VariableDefinition)instr.Operand).Index.ToString(System.Globalization.CultureInfo.InvariantCulture));
					}
					else if (typeof(ParameterReference).IsAssignableFrom(instrType)) {
						return utf8.GetBytes(((ParameterReference)instr.Operand).Name);
					}
					else if (instrType == typeof(string)) {
						return utf8.GetBytes((string)instr.Operand);
					}
					else if (instrType == typeof(sbyte)) {
						return BitConverter.GetBytes((short)(sbyte)instr.Operand);
					}
					else if (instrType == typeof(byte)) {
						return new byte[1] { (byte)instr.Operand };
					}
					else if (instrType == typeof(int)) {
						return BitConverter.GetBytes((int)instr.Operand);
					}
					else if (instrType == typeof(long)) {
						return BitConverter.GetBytes((long)instr.Operand);
					}
					else if (instrType == typeof(float)) {
						return BitConverter.GetBytes((float)instr.Operand);
					}
					else if (instrType == typeof(double)) {
						return BitConverter.GetBytes((double)instr.Operand);
					}
					else if (instrType == typeof(Instruction)) {
						return BitConverter.GetBytes(((Instruction)instr.Operand).Offset);
					}
					else if (instrType == typeof(Instruction[])) {
						return ((Instruction[])instr.Operand).SelectMany(i => BitConverter.GetBytes(i.Offset)).ToArray();
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine(ex.ToString());
			}

			return new byte[0];
		}

		private static void InsertGenericMethodEnterExitCode(Instruction firstInstr, MethodBody body, MessagingImports imports,
													   MethodDefinition method, ILProcessor proc,
													   bool classIsGeneric, bool methodIsGeneric) { //

			// Messaging.EnterGenericMethod(nnn, *SettingThisPortion*, null);
			if (classIsGeneric) {
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Call, GenericParamsStringForClass(method.DeclaringType, imports)));
			}
			else {
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldnull));
			}

			if (methodIsGeneric) {
				// add a local string[] variable to hold the parameters for the method's generic parameters
				body.Variables.Add(new VariableDefinition(imports.StringArrayTypeRef));
				var index = body.Variables.Count - 1;

				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldc_I4, method.GenericParameters.Count));
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Newarr, imports.StringTypeRef));
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Stloc, index));

				for (int i = 0; i < method.GenericParameters.Count; i++) {
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldloc, index));
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldc_I4, i));
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldtoken, method.GenericParameters[i]));
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Call, imports.GetTypeFromHandleRef));
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Callvirt, imports.Type_get_FullNameRef));
					proc.InsertBefore(firstInstr, proc.Create(OpCodes.Stelem_Ref));
				}
				// Messaging.EnterGenericMethod(nnn, ClassName<T, P>.-238831742(), new string[] { typeof(T).FullName, typeof(R).FullName });
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldloc, index));
			}
			else {
				// Messaging.EnterGenericMethod(nnn, ClassName<T, P>.-238831742(), null);
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldnull));
			}
		}

		private static void InsertInstrumentationCallbacks(int methodId, int moduleId, int assemblyId, MethodBody body, MessagingImports imports, IInstrumentFactory factory) {
			var instrController = factory.MessageController;
			var wantsInstrumentation = instrController.WantsMethodEnterMessages || instrController.WantsMethodExitMessages;
			if (!wantsInstrumentation)
				return;

			// turn all short instructions into long instructions
			body.SimplifyMacros();

			// need to capture the original instruction list in an array
			// because the body of the loop will modify it
			var originalInstructions = body.Instructions.ToArray();
			var proc = body.GetILProcessor();

			//used by the call to CreateGenericMethodCallCode
			var method = body.Method;
			var classIsGeneric = method.DeclaringType.HasGenericParameters;
			var methodIsGeneric = method.HasGenericParameters;

			// ask if interested in method entrance
			if (instrController.WantsMethodEnterMessages) {
				InsertMethodEntranceInstrumentation(originalInstructions[0], methodId, assemblyId, body, imports);
			}

			foreach (var instr in originalInstructions) {

				Instruction newJumpTarget = null;

				// ask if interested in sq.pt. end
				if (instrController.WantsSeqPointEndMessages && ShouldInstrumentSequencePoint(body, instr)) {

					var point = SeqPointBridge.SeqPoint(body, instr);
					var docId = factory.LookupSourceFile(methodId, point.Document.Url);
					var pointId = docId == -1
						? factory.RegisterPoint(methodId) // couldn't find the document, so do the best possible
						: factory.RegisterPoint(methodId, docId, point.StartLine, point.StartColumn, point.EndLine, point.EndColumn);

					newJumpTarget = proc.Create(OpCodes.Ldsfld, imports.ModuleMethodIdFieldRef);
					proc.InsertBefore(instr, newJumpTarget);
					proc.InsertBefore(instr, proc.Create(OpCodes.Ldc_I4, pointId));
					proc.InsertBefore(instr, proc.Create(OpCodes.Call, imports.PointHit));

				}

				// ask if interested in catch block
				// ask if interested in filter block

				// ask if interested in method exit 
				if (instrController.WantsMethodExitMessages && IsMethodExitInstr(instr)) {
					if (newJumpTarget == null) {
						newJumpTarget = proc.Create(OpCodes.Ldsfld, imports.ModuleMethodIdFieldRef);
						proc.InsertBefore(instr, newJumpTarget);
					}
					else {

						proc.InsertBefore(instr, proc.Create(OpCodes.Ldsfld, imports.ModuleMethodIdFieldRef));
					}


					proc.InsertBefore(instr, proc.Create(OpCodes.Ldc_I4, methodId));

					if ((methodIsGeneric || classIsGeneric)) {
						//TODO need to update this instrumentation for method exits so that it doesn't re-create the local variable
						// that the method entrance instrumentation alread created - but that needs to happen only after 
						// it's not possible to ask for exit instrumentation but not entrance instrumentation
						InsertGenericMethodEnterExitCode(instr, body, imports, method, proc, classIsGeneric, methodIsGeneric);
						proc.InsertBefore(instr, proc.Create(OpCodes.Call, imports.ExitGenericMethodRef));
					}
					else {
						proc.InsertBefore(instr, proc.Create(OpCodes.Call, imports.ExitMethodRef));
					}

				}

				if (newJumpTarget != null) {
					// the current instruction was instrumented, so
					// we need to fix up any instructions that jump to it
					var instrsToFixUp = GetInstructionsThatJumpTo(instr, originalInstructions);
					foreach (var item in instrsToFixUp) {
						if (item.OpCode == OpCodes.Switch) {
							// switch instructions can have multiple target instructions.
							// mono.cecil represents this by making the operand an instruction array
							// so we need to update every instruction in the array that jumps to the
							// point we just instrumented
							var targets = (Instruction[])item.Operand;
							for (int i = 0; i < targets.Length; i++) {
								if (targets[i] == instr) {
									// instr was the previous jump target
									targets[i] = newJumpTarget;
								}
							}

						}
						else {
							item.Operand = newJumpTarget;
						}
					}
				}
			}
		}

		private static bool IsMethodExitInstr(Instruction methodEndInstr) {
			Contract.Requires(methodEndInstr != null);

			var priorIsTailCall = methodEndInstr.Previous != null && methodEndInstr.Previous.Previous != null
				&& methodEndInstr.Previous.Previous.OpCode == OpCodes.Tail;

			return methodEndInstr.OpCode == OpCodes.Jmp
				|| methodEndInstr.OpCode == OpCodes.Tail
				|| (methodEndInstr.OpCode == OpCodes.Ret && !priorIsTailCall);
		}

		private static void InsertMethodEntranceInstrumentation(Instruction firstInstr, int methodId, int assemblyId, MethodBody body, MessagingImports imports) {

			var method = body.Method;
			var proc = body.GetILProcessor();
			var classIsGeneric = method.DeclaringType.HasGenericParameters;
			var methodIsGeneric = method.HasGenericParameters;

			proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldsfld, imports.ModuleMethodIdFieldRef));
			proc.InsertBefore(firstInstr, proc.Create(OpCodes.Ldc_I4, methodId));

			if ((methodIsGeneric || classIsGeneric)) {
				if (body.MaxStackSize < 6) {
					// 1 for the assembly id
					// 1 for the method id
					// 1 for the class's generic parameter handling
					// 3 for the method's generic parameter handling
					body.MaxStackSize = 6;
				}

				InsertGenericMethodEnterExitCode(firstInstr, body, imports, method, proc, classIsGeneric, methodIsGeneric);
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Call, imports.EnterGenericMethodRef));
			}
			else {
				proc.InsertBefore(firstInstr, proc.Create(OpCodes.Call, imports.EnterMethodRef));
			}
		}

		private static MethodReference GenericParamsStringForClass(TypeDefinition forType, MessagingImports imports) {
			var genericRef = new GenericInstanceType(forType);

			for (int i = 0; i < forType.GenericParameters.Count; i++) {
				genericRef.GenericArguments.Add(forType.GenericParameters[i]);
			}

			var methodName = ClassGenericParamsMethodName(forType);
			var method = forType.Methods.SingleOrDefault(m => m.Name == methodName);
			if (method == null) {
				lock (forType) {
					method = forType.Methods.SingleOrDefault(m => m.Name == methodName);
					if (method == null) {

						var fieldAttrs = FieldAttributes.Static | FieldAttributes.Private;
						var field = new FieldDefinition("<" + methodName + ">k__BackingField", fieldAttrs, imports.StringArrayTypeRef);
						field.CustomAttributes.Add(new CustomAttribute(imports.CompilerGeneratedAttributeRef));
						forType.Fields.Add(field);

						var fieldRef = new FieldReference(field.Name, imports.StringArrayTypeRef);
						fieldRef.DeclaringType = genericRef;

						var methodAttrs = MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig;
						method = new MethodDefinition(methodName, methodAttrs, imports.StringArrayTypeRef);
						method.CustomAttributes.Add(new CustomAttribute(imports.CompilerGeneratedAttributeRef));
						method.Body.MaxStackSize = 3;

						var proc = method.Body.GetILProcessor();
						proc.Append(proc.Create(OpCodes.Ldnull));
						proc.Append(proc.Create(OpCodes.Ldsfld, fieldRef));
						proc.Append(proc.Create(OpCodes.Ceq));

						var targetWhenArrIsCreated = proc.Create(OpCodes.Ldsfld, fieldRef);
						proc.Append(proc.Create(OpCodes.Brfalse_S, targetWhenArrIsCreated));

						proc.Append(proc.Create(OpCodes.Ldc_I4, forType.GenericParameters.Count));
						proc.Append(proc.Create(OpCodes.Newarr, imports.StringTypeRef));
						proc.Append(proc.Create(OpCodes.Stsfld, fieldRef));

						for (int i = 0; i < forType.GenericParameters.Count; i++) {
							proc.Append(proc.Create(OpCodes.Ldsfld, fieldRef));
							proc.Append(proc.Create(OpCodes.Ldc_I4, i));
							proc.Append(proc.Create(OpCodes.Ldtoken, forType.GenericParameters[i]));
							proc.Append(proc.Create(OpCodes.Call, imports.GetTypeFromHandleRef));
							proc.Append(proc.Create(OpCodes.Callvirt, imports.Type_get_FullNameRef));
							proc.Append(proc.Create(OpCodes.Stelem_Ref));
						}

						proc.Append(targetWhenArrIsCreated);
						proc.Append(proc.Create(OpCodes.Ret));

						forType.Methods.Add(method);
					}
				}
			}

			if (method == null)
				return method;

			var methodRef = new MethodReference(method.Name, method.ReturnType);
			methodRef.DeclaringType = genericRef;

			return methodRef;
		}

		private static string ClassGenericParamsMethodName(TypeDefinition forType) {
			return "<" + forType.Name + ">" + "b__GenericArgs";
		}

		private static Instruction[] GetInstructionsThatJumpTo(Instruction target, IEnumerable<Instruction> instructionList) {
			return instructionList
				.Where(i => i.OpCode.FlowControl == FlowControl.Branch || i.OpCode.FlowControl == FlowControl.Cond_Branch)
				.Where(i => {
					return i.OpCode == OpCodes.Switch
						? ((Instruction[])i.Operand).Any(it => it == target)
						: ((Instruction)i.Operand) == target;
				})
				.ToArray();
		}


		private static void PrintMethodAndSequencePoints(MethodBody body) {
			using (var writer = new StreamWriter(FileStub.Open("planned_instrumentation.txt", FileMode.Append, FileAccess.Write))) {


				writer.WriteLine(body.Method.FullName);
				writer.WriteLine();

				for (int i = 0; i < body.Instructions.Count; i++) {
					var instr = body.Instructions[i];

					var line = "    " + instr.ToString();
					if (SeqPointBridge.SeqPoint(body, instr) != null) {
						var point = SeqPointBridge.SeqPoint(body, instr);
						line += string.Format(
							" // \"{0}\"  from {1},{2} to {3},{4}  {5}",
							point.Document.Url,
							point.StartLine,
							point.StartColumn,
							point.EndLine,
							point.EndColumn,
							ShouldInstrumentSequencePoint(body, instr) ? "(instrument)" : "(don't instrument)");
					}

					writer.WriteLine(line);
				}

				writer.WriteLine("// end method");
				writer.WriteLine();
				writer.WriteLine();
				writer.WriteLine();
			}
		}

		private static bool ShouldInstrumentSequencePoint(MethodBody body, Instruction current) {
			// this method takes advantage of the fact that
			// mono.cecil associates a sequence point w/ the first
			// instruction that the point represents, not all of the instructions

			if (current == null || SeqPointBridge.SeqPoint(body, current) == null)
				return false;

			// 0xFEEFEE is a special line number that tells debuggers
			// to ignore this sequence point, so we will ignore it as well
			// http://blogs.msdn.com/b/jmstall/archive/2005/06/19/feefee-sequencepoints.aspx
			if (SeqPointBridge.SeqPoint(body, current).StartLine == 0xFEEFEE)
				return false;

			// sequence points on nop instructions are there to make
			// a debugging experience friendly. we want to ignore them.
			if (current.OpCode == OpCodes.Nop)
				return false;

			if (!InstrumentSequencePoints)
				return false;

			return true;
		}

		private static bool ShouldInstrumentMethod(MethodDefinition method, IInstrumentFactory factory) {
			// TODO once attribute exclusions are supported, will have to check for
			// attributes on the method and possibly not instrument it
			return true;
		}

		internal class MessagingImports {

			public TypeReference MessagingRef {
				get;
				private set;
			}

			public MethodReference EnterGenericMethodRef {
				get;
				private set;
			}

			public MethodReference ExitGenericMethodRef {
				get;
				private set;
			}

			public MethodReference GetTypeFromHandleRef {
				get;
				private set;
			}

			public TypeReference IntTypeRef {
				get;
				private set;
			}

			/// <summary>
			/// What is the method for the current module?
			/// </summary>
			public FieldReference ModuleMethodIdFieldRef {
				get;
				private set;
			}

			public MethodReference Type_get_FullNameRef {
				get;
				private set;
			}

			public TypeReference StringArrayTypeRef {
				get;
				private set;
			}

			public TypeReference StringTypeRef {
				get;
				private set;
			}

			public MethodReference CompilerGeneratedAttributeRef {
				get;
				private set;
			}

			public MethodReference EnterMethodRef {
				get;
				private set;
			}

			public MethodReference ExitMethodRef {
				get;
				private set;
			}

			public MethodReference PointHit {
				get;
				private set;
			}

			public MessagingImports(ModuleDefinition module, int moduleId) {
				if (!(module != null)) throw new ArgumentNullException("moduleToAddReferencesTo");

				Contract.Ensures(MessagingRef != null);
				Contract.Ensures(EnterGenericMethodRef != null);
				Contract.Ensures(ExitGenericMethodRef != null);
				Contract.Ensures(GetTypeFromHandleRef != null);
				Contract.Ensures(IntTypeRef != null);
				Contract.Ensures(ModuleMethodIdFieldRef != null);
				Contract.Ensures(Type_get_FullNameRef != null);
				Contract.Ensures(StringArrayTypeRef != null);
				Contract.Ensures(StringTypeRef != null);
				Contract.Ensures(CompilerGeneratedAttributeRef != null);
				Contract.Ensures(EnterMethodRef != null);
				Contract.Ensures(ExitMethodRef != null);
				Contract.Ensures(PointHit != null);

				var bindingflags = System.Reflection.BindingFlags.Instance
					| System.Reflection.BindingFlags.Static
					| System.Reflection.BindingFlags.Public
					| System.Reflection.BindingFlags.NonPublic;

				IntTypeRef = module.Import(typeof(int));

				MessagingRef = module.Import(typeof(Messaging));
				EnterGenericMethodRef = module.Import(typeof(Messaging).GetMethod("EnterGenericMethod"));
				ExitGenericMethodRef = module.Import(typeof(Messaging).GetMethod("ExitGenericMethod"));

				GetTypeFromHandleRef = module.Import(typeof(Type).GetMethod("GetTypeFromHandle", bindingflags));
				Type_get_FullNameRef = module.Import(typeof(Type).GetProperty("FullName").GetGetMethod());
				StringArrayTypeRef = module.Import(typeof(string[]));
				StringTypeRef = module.Import(typeof(string));
				CompilerGeneratedAttributeRef = module.Import(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).GetConstructor(Type.EmptyTypes));

				EnterMethodRef = module.Import(typeof(Messaging).GetMethod("EnterMethod"));
				ExitMethodRef = module.Import(typeof(Messaging).GetMethod("ExitMethod"));
				PointHit = module.Import(typeof(Messaging).GetMethod("InstrumentationPointHit"));

				var netperfType = new TypeDefinition(MetaDataModule, MetaDataType, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.BeforeFieldInit);
				netperfType.BaseType = module.Import(typeof(System.Object)); // TypeReference("System", "Object", null, false); //
				module.Types.Add(netperfType);

				var netperfField = new FieldDefinition("ModuleIdentifier", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.HasDefault, IntTypeRef); //| FieldAttributes.HasDefault
				netperfType.Fields.Add(netperfField);
				netperfField.Constant = moduleId;

				this.ModuleMethodIdFieldRef = new FieldReference(netperfField.Name, IntTypeRef) {
					DeclaringType = netperfType //this is the key to making field references that work. http://www.mail-archive.com/mono-cecil@googlegroups.com/msg01568.html
				};

				//initialize the field. the constant did not seem to work.
				var ctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
					module.Import(typeof(void)));
				ctor.CustomAttributes.Add(new CustomAttribute(CompilerGeneratedAttributeRef));

				ctor.Body.MaxStackSize = 3;
				var proc = ctor.Body.GetILProcessor();
				proc.Append(proc.Create(OpCodes.Ldc_I4, moduleId));
				proc.Append(proc.Create(OpCodes.Stsfld, ModuleMethodIdFieldRef));
				proc.Append(proc.Create(OpCodes.Ret));
				netperfType.Methods.Add(ctor);
			}
		}
	}
}
