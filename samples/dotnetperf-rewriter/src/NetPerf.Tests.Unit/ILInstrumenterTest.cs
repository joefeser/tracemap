using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Cecil = Mono.Cecil.Cil;
using System.Diagnostics;
using System.IO;
using NetPerf.Common.Instrumentation;
using Mono.Cecil.Cil;
using Mono.Cecil;
using NetPerf.Tests.Test_References;

namespace NetPerf.UnitTests {

	public class ILInstrumenterTest {

		private static int Mock_Module_Id = 235235235;

		[TestClass]
		public class TypeInstrumentationTests {
			[TestMethod]
			public void InstrumentsNestedClasses() {
				var module = ILHelper.GetModule("nestedclass.il.txt", "NestedClass.exe");

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												"NestedClass.Program",
												Moq.It.IsAny<string[]>(),
												Moq.It.IsAny<string[]>()))
					   .Returns(1)
					   .Verifiable();

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												"NestedClass.Program/MyNestedClass1",
												Moq.It.IsAny<string[]>(),
												Moq.It.IsAny<string[]>()))
					   .Returns(2)
					   .Verifiable();
				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												"NestedClass.Program/MyNestedClass1/MyNestedClass2",
												Moq.It.IsAny<string[]>(),
												Moq.It.IsAny<string[]>()))
					   .Returns(3)
					   .Verifiable();
				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												"NestedClass.Program/MyNestedClass1/MyNestedClass2/MyNestedClass3",
												Moq.It.IsAny<string[]>(),
												Moq.It.IsAny<string[]>()))
					   .Returns(4)
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentModule(module, 1, factory.Object);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringAnInstanceGenericType_PassesTypesGenericParameters() {
				var module = ILHelper.GetModule("generictypesandmethods.il.txt", "generictypesandmethods.exe");

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												  "generictypesandmethods.GenericClass`2",
												  Moq.It.IsAny<string[]>(),
												  new[] { "T", "K" }))
					   .Returns(1)
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentModule(module, 1, factory.Object);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringAStaticGenericType_PassesTypesGenericParameters() {
				var module = ILHelper.GetModule("generictypesandmethods.il.txt", "generictypesandmethods.exe");

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												  "generictypesandmethods.StaticGenericClass`2",
												  Moq.It.IsAny<string[]>(),
												  new[] { "T", "K" }))
					   .Returns(1)
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentModule(module, 1, factory.Object);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringANonGenericInstanceType_PassesNoGenericParameters() {
				var module = ILHelper.GetModule("generictypesandmethods.il.txt", "generictypesandmethods.exe");

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												  "generictypesandmethods.InstanceClass",
												  Moq.It.IsAny<string[]>(),
												  new string[0]))
					   .Returns(1)
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentModule(module, 1, factory.Object);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringANonGenericStaticType_PassesNoGenericParameters() {
				var module = ILHelper.GetModule("generictypesandmethods.il.txt", "generictypesandmethods.exe");

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												  "generictypesandmethods.StaticClass",
												  Moq.It.IsAny<string[]>(),
												  new string[0]))
					   .Returns(1)
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentModule(module, 1, factory.Object);

				factory.Verify();
			}
		}


		[TestClass]
		public class SourceFileInstrumentTests {

			[TestMethod]
			public void PassesCorrectModuleIdWhenRegisteringASourceFile() {
				var body = ILHelper.GetMethodBody("sourcefile.il.txt", "Main");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				factory.Setup(f => f.RegisterSourceFile(1,
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<byte[]>(),
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<string>()))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void PassesCorrectFilePathWhenRegisteringASourceFile() {
				var body = ILHelper.GetMethodBody("sourcefile.il.txt", "Main");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				factory.Setup(f => f.RegisterSourceFile(Moq.It.IsAny<int>(),
														Path.GetFullPath("sourcefile.il.txt"),
														Moq.It.IsAny<byte[]>(),
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<string>()))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringASourceFile_PassesTheCorrectAlgorithmAndLanguage() {
				var body = ILHelper.GetMethodBody("sourcefile.il.txt", "Main");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				factory.Setup(f => f.RegisterSourceFile(Moq.It.IsAny<int>(),
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<byte[]>(),
														"None",
														"Cil"))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}
		}



		[TestClass]
		public class MethodInstrumentationTests {

			[TestMethod]
			public void WhenRegisteringAMethod_PassesSourceFileIds() {
				var body = ILHelper.GetMethodBody("sourcefile.il.txt", "Main");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				int expectedSourceFileId = 1;
				factory.Setup(f => f.RegisterSourceFile(Moq.It.IsAny<int>(),
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<byte[]>(),
														Moq.It.IsAny<string>(),
														Moq.It.IsAny<string>()))
					   .Returns(expectedSourceFileId)
					   .Verifiable();

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													Moq.It.IsAny<byte[]>(),
													Moq.It.IsAny<string[]>(),
													new[] { expectedSourceFileId },
													Moq.It.IsAny<string[]>()))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void WhenRegisteringAMethod_PassesRawMethodBytes() {
				var body = ILHelper.GetMethodBody("sourcefile.il.txt", "Main");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				var expectedRawBytes = new byte[] {
					2,23,63,3,40,2,23,89,3,40,0,42
				};

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													expectedRawBytes,
													Moq.It.IsAny<string[]>(),
													Moq.It.IsAny<int[]>(),
													Moq.It.IsAny<string[]>()))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void NonGenericMethodInNonGenericType_PassesNoGenericParameters() {

				var methodNames = new[] {
					"System.Int32 generictypesandmethods.InstanceClass::Method1(System.Int32)",
					"System.Int32 generictypesandmethods.InstanceClass::Method2(System.Int32)",
					"System.Int32 generictypesandmethods.StaticClass::Method2(System.Int32)"
				};

				foreach (var methodName in methodNames) {

					var body = ILHelper.GetMethodBody("generictypesandmethods.il.txt", methodName);
					var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

					var factory = new Moq.Mock<IInstrumentFactory>();
					factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

					factory.Setup(f => f.RegisterMethod(
							Moq.It.IsAny<int>(),
							Moq.It.IsAny<string>(),
							Moq.It.IsAny<byte[]>(),
							Moq.It.IsAny<string[]>(),
							Moq.It.IsAny<int[]>(),
							new string[0]))
						.Returns(1)
						.Verifiable();

					ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

					factory.Verify();
				}
			}

			[TestMethod]
			public void NonGenericMethodInGenericType_PassesNoGenericParameters() {
				var methodNames = new[] {
					"T generictypesandmethods.GenericClass`2::Method1(K,System.Func`2<K,T>)",
					"T generictypesandmethods.GenericClass`2::Method2(K,System.Func`2<K,T>)",
					"T generictypesandmethods.StaticGenericClass`2::Method2(K,System.Func`2<K,T>)"
				};

				foreach (var methodName in methodNames) {

					var body = ILHelper.GetMethodBody("generictypesandmethods.il.txt", methodName);
					var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

					var factory = new Moq.Mock<IInstrumentFactory>();
					factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

					factory.Setup(f => f.RegisterMethod(
							Moq.It.IsAny<int>(),
							Moq.It.IsAny<string>(),
							Moq.It.IsAny<byte[]>(),
							Moq.It.IsAny<string[]>(),
							Moq.It.IsAny<int[]>(),
							new string[0]))
						.Returns(1)
						.Verifiable();

					ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

					factory.Verify();
				}
			}

			[TestMethod]
			public void GenericMethodInGenericType_PassesMethodsGenericParameters() {
				var methodNames = new[] {
					"R generictypesandmethods.GenericClass`2::Method3(K,T,System.Func`3<K,T,R>)",
					"R generictypesandmethods.GenericClass`2::Method4(K,T,System.Func`3<K,T,R>)",
					"R generictypesandmethods.StaticGenericClass`2::Method4(K,T,System.Func`3<K,T,R>)"
				};

				foreach (var methodName in methodNames) {

					var body = ILHelper.GetMethodBody("generictypesandmethods.il.txt", methodName);
					var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

					var factory = new Moq.Mock<IInstrumentFactory>();
					factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

					factory.Setup(f => f.RegisterMethod(
							Moq.It.IsAny<int>(),
							Moq.It.IsAny<string>(),
							Moq.It.IsAny<byte[]>(),
							Moq.It.IsAny<string[]>(),
							Moq.It.IsAny<int[]>(),
							new[] { "R" }))
						.Returns(1)
						.Verifiable();

					ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

					factory.Verify();
				}
			}

			[TestMethod]
			public void GenericMethodInNonGenericType_PassesMethodsGenericParameters() {
				var methodName = "R generictypesandmethods.InstanceClass::Method3(K,T,System.Func`3<K,T,R>)";

				var body = ILHelper.GetMethodBody("generictypesandmethods.il.txt", methodName);
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(false, false).Object);

				factory.Setup(f => f.RegisterMethod(
						Moq.It.IsAny<int>(),
						Moq.It.IsAny<string>(),
						Moq.It.IsAny<byte[]>(),
						Moq.It.IsAny<string[]>(),
						Moq.It.IsAny<int[]>(),
						new[] { "T", "K", "R" }))
					.Returns(1)
					.Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void ShortBranchesAreConvertedToLongBranches() {
				var body = ILHelper.GetMethodBody("shortbranches_to_longbranches.il.txt", "ContainsAllShortBranches");
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, true).Object);

				factory.Setup(f => f.RegisterMethod(
							Moq.It.IsAny<int>(),
							Moq.It.IsAny<string>(),
							Moq.It.IsAny<byte[]>(),
							Moq.It.IsAny<string[]>(),
							Moq.It.IsAny<int[]>(),
							new string[0]))
						.Returns(1);

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				var shortInstructions = new[] {
					OpCodes.Beq_S, OpCodes.Bge_S, OpCodes.Bge_Un_S, OpCodes.Bgt_Un_S, OpCodes.Ble_Un_S,
					OpCodes.Blt_Un_S, OpCodes.Bne_Un_S, OpCodes.Br_S, OpCodes.Brfalse_S, OpCodes.Brtrue_S,
					OpCodes.Leave_S
				};
				// get all the short branch instructions still in the method body
				// - there should be none, so if we find any it is an error
				var badInstructions = body.Instructions.Where(i => shortInstructions.Any(si => si == i.OpCode));

				var errorMessage = string.Join(",", badInstructions.Select(bi => bi.Offset + " " + bi.OpCode.ToString()));
				Assert.IsTrue(badInstructions.IsEmpty(), "some short branch instructions were not converted: " + errorMessage);
			}

		}


		[TestClass]
		public class PropertyInstrumentTests {

			[TestMethod]
			public void AllPropertyMethodsInstrument() {
				var property = ILHelper.GetPropertyBody("propertyinstrument.il.txt", "PropertyWithMethods");
				var imports = ILInstrumenter_Privates.CreateImports(property.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													Moq.It.IsAny<byte[]>(),
													Moq.It.IsAny<string[]>(),
													Moq.It.IsAny<int[]>(),
													Moq.It.IsAny<string[]>()))
					   .Returns(1);

				ILInstrumenter_Privates.InstrumentProperty(property, 1, 1, Mock_Module_Id, factory.Object, imports);

				Assert.AreEqual(4, property.GetMethod.Body.Instructions.Count);
				Assert.AreEqual(4, property.SetMethod.Body.Instructions.Count);

				foreach (var other in property.OtherMethods) {
					Assert.AreEqual(4, other.Body.Instructions.Count);
				}
			}
		}

		[TestClass]
		public class EventInstrumentTests {

			[TestMethod]
			public void AllEventMethodsInstrument() {
				var def = ILHelper.GetEvent("eventinstrument.il.txt", "EventWithMethods");
				var imports = ILInstrumenter_Privates.CreateImports(def.Module, Mock_Module_Id);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(ILHelper.GetMessageController(true, false).Object);

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													Moq.It.IsAny<byte[]>(),
													Moq.It.IsAny<string[]>(),
													Moq.It.IsAny<int[]>(),
													Moq.It.IsAny<string[]>()))
					   .Returns(1);

				ILInstrumenter_Privates.InstrumentEvent(def, 1, 1, Mock_Module_Id, factory.Object, imports);

				Assert.AreEqual(4, def.AddMethod.Body.Instructions.Count);
				Assert.AreEqual(4, def.RemoveMethod.Body.Instructions.Count);

				foreach (var other in def.OtherMethods) {
					Assert.AreEqual(4, other.Body.Instructions.Count);
				}
			}

		}


		[TestClass]
		public class CustomAttributeSavedTest {

			private void AssertMethodHasCustomAttributes(string methodName) {
				var body = ILHelper.GetMethodBody("customattributes.il.txt", methodName);
				var imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var controller = new Moq.Mock<IMessagingController>();
				controller.Setup(c => c.WantsMethodEnterMessages).Returns(false);
				controller.Setup(c => c.WantsMethodExitMessages).Returns(false);
				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController)
					.Returns(controller.Object);

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													Moq.It.IsAny<byte[]>(),
													new[] { "CustomAttributes.CustomAttribute1", "CustomAttributes.CustomAttribute1" },
													Moq.It.IsAny<int[]>(),
													Moq.It.IsAny<string[]>()))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentMethod(body.Method, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void MethodCustomAttributesAreSaved() {
				AssertMethodHasCustomAttributes("MethodWithCustomAttributes");
			}

			[TestMethod]
			public void ConstructorCustomAttributesAreSaved() {
				AssertMethodHasCustomAttributes("System.Void CustomAttributes.CustomAttributeExamples::.ctor()");
			}

			private void AssertEventHasCustomAttributes(string eventName) {
				var evnt = ILHelper.GetEvent("customattributes.il.txt", eventName);
				var imports = ILInstrumenter_Privates.CreateImports(evnt.Module, Mock_Module_Id);

				var controller = new Moq.Mock<IMessagingController>();
				controller.Setup(c => c.WantsMethodEnterMessages).Returns(false);
				controller.Setup(c => c.WantsMethodExitMessages).Returns(false);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController)
					.Returns(controller.Object);

				factory.Setup(f => f.RegisterEvent(Moq.It.IsAny<int>(),
													  Moq.It.IsAny<string>(),
													  Moq.It.IsAny<int?>(),
													  Moq.It.IsAny<int?>(),
													  Moq.It.IsAny<int?>(),
													  Moq.It.IsAny<int[]>(),
													  new[] { "CustomAttributes.CustomAttribute1", "CustomAttributes.CustomAttribute1" }))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentEvent(evnt, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();

			}

			[TestMethod]
			public void StructEventCustomAttributesAreSaved() {
				AssertEventHasCustomAttributes("System.EventHandler CustomAttributes.StructWithCustomAttributes::EventWithCustomAttributes");
			}

			[TestMethod]
			public void ClassEventCustomAttributesAreSaved() {
				AssertEventHasCustomAttributes("System.EventHandler CustomAttributes.CustomAttributeExamples::EventWithCustomAttributes");
			}

			private void AssertPropertyHasCustomAttributes(string propertyName) {
				var property = ILHelper.GetPropertyBody("customattributes.il.txt", propertyName);
				var imports = ILInstrumenter_Privates.CreateImports(property.Module, Mock_Module_Id);

				var controller = new Moq.Mock<IMessagingController>();
				controller.Setup(c => c.WantsMethodEnterMessages).Returns(false);
				controller.Setup(c => c.WantsMethodExitMessages).Returns(false);
				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController)
					.Returns(controller.Object);

				factory.Setup(f => f.RegisterProperty(Moq.It.IsAny<int>(),
													  Moq.It.IsAny<string>(),
													  Moq.It.IsAny<int?>(),
													  Moq.It.IsAny<int?>(),
													  Moq.It.IsAny<int[]>(),
													  new[] { "CustomAttributes.CustomAttribute1", "CustomAttributes.CustomAttribute1" }))
					   .Verifiable();

				ILInstrumenter_Privates.InstrumentProperty(property, 1, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();

			}

			[TestMethod]
			public void StructPropertyCustomAttributesAreSaved() {
				AssertPropertyHasCustomAttributes("System.Int32 CustomAttributes.StructWithCustomAttributes::PropertyWithCustomAttributes()");
			}

			[TestMethod]
			public void ClassPropertyCustomAttributesAreSaved() {
				AssertPropertyHasCustomAttributes("System.Collections.Generic.List`1<System.Int32> CustomAttributes.CustomAttributeExamples::PropertyWithCustomAttributes()");
			}

			private void AssertTypeHasCustomAttributes(string typeName) {
				var type = ILHelper.GetType("customattributes.il.txt", typeName);
				var imports = ILInstrumenter_Privates.CreateImports(type.Module, Mock_Module_Id);

				var controller = new Moq.Mock<IMessagingController>();
				controller.Setup(c => c.WantsMethodEnterMessages).Returns(false);
				controller.Setup(c => c.WantsMethodExitMessages).Returns(false);
				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController)
					.Returns(controller.Object);

				factory.Setup(f => f.RegisterType(Moq.It.IsAny<int>(),
												  Moq.It.IsAny<string>(),
												  new[] { "CustomAttributes.CustomAttribute1", "CustomAttributes.CustomAttribute1" },
												  Moq.It.IsAny<string[]>()))
					   .Verifiable();

				factory.Setup(f => f.RegisterMethod(Moq.It.IsAny<int>(),
													Moq.It.IsAny<string>(),
													Moq.It.IsAny<byte[]>(),
													Moq.It.IsAny<string[]>(),
													Moq.It.IsAny<int[]>(),
													Moq.It.IsAny<string[]>()));

				ILInstrumenter_Privates.InstrumentType(type, 1, Mock_Module_Id, factory.Object, imports);

				factory.Verify();
			}

			[TestMethod]
			public void ClassCustomAttributesAreSaved() {
				AssertTypeHasCustomAttributes("CustomAttributeExamples");
			}

			[TestMethod]
			public void StructCustomAttributesAreSaved() {
				AssertTypeHasCustomAttributes("StructWithCustomAttributes");
			}
		}

		[TestClass]
		public class MethodEntranceAndExitInstrumentationTest {

			private ILInstrumenter.MessagingImports  _imports;
			private Cecil.MethodBody _body;
			private int _origInstrCount;
			private const int _methodId = 1;

			private void InstrumentMethod(bool wantsEnterMsg, bool wantsExitMsg,
				string testFileName = "returns.il.txt", string methodName = "SingleReturn") //
			{
				_body = ILHelper.GetMethodBody(testFileName, methodName);
				_origInstrCount = _body.Instructions.Count;
				_imports = ILInstrumenter_Privates.CreateImports(_body.Method.Module, Mock_Module_Id);

				var controller = ILHelper.GetMessageController(wantsEnterMsg, wantsExitMsg);

				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(controller.Object);

				ILInstrumenter_Privates.InsertInstrumentationCallbacks(_methodId, 1, Mock_Module_Id, _body, _imports, factory.Object);
			}

			public void InstrumentsSimpleMethodEntranceWhenAsked() {
				InstrumentMethod(true, false);

				Assert.AreEqual(_origInstrCount + 2, _body.Instructions.Count);
				Assert.AreEqual(Cecil.OpCodes.Ldc_I4, _body.Instructions[0].OpCode);
				Assert.AreEqual(_methodId, _body.Instructions[0].Operand);

				Assert.AreEqual(Cecil.OpCodes.Call, _body.Instructions[1].OpCode);
				Assert.AreEqual(_imports.EnterMethodRef, _body.Instructions[1].Operand);
			}

			[TestMethod]
			public void InstrumentsSimpleMethodEntranceOrExitOnlyWhenAsked() {
				InstrumentMethod(false, false);

				Assert.AreEqual(_origInstrCount, _body.Instructions.Count);
			}

			private void AssertExitInstrumentedCorrectly(Cecil.Instruction retInstr) {
				var instr = retInstr;

				// assume the retInstr points to a return instruction
				// the previous instruction should call the exit message method
				instr = instr.Previous;

				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Call, instr.OpCode);
				Assert.AreEqual(_imports.ExitMethodRef, instr.Operand);

				// and the instruction before that should specify the methodId.
				instr = instr.Previous;
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Ldc_I4, instr.OpCode);
				Assert.AreEqual(_methodId, instr.Operand);
			}

			private void AssertJumpToExitInstrumentedCorrectly(Cecil.Instruction jmpInstr) {
				var instr = jmpInstr;

				Assert.IsNotNull(instr);
				// make sure the isntruction actually jumps to another instruction
				Assert.IsInstanceOfType(instr.Operand, typeof(Cecil.Instruction));

				instr = instr.Operand as Cecil.Instruction;

				// the jump instruction should jump to a load that loads the module id
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Ldsfld, instr.OpCode);
				Assert.AreEqual("System.Int32 NetPerf.NetPerfMetadata::ModuleIdentifier", instr.Operand.ToString());

				instr = instr.Next;
				// the next instruction should be the method
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Ldc_I4, instr.OpCode);
				Assert.AreEqual(_methodId, instr.Operand);

				// the next instruction should be a call to the exit message method
				instr = instr.Next;
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Call, instr.OpCode);
				Assert.AreEqual(_imports.ExitMethodRef, instr.Operand);

				// and the next instruction should be a return
				instr = instr.Next;
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Ret, instr.OpCode);
			}

			[TestMethod]
			public void InstrumentsSimpleMethodExitWhenAsked() {
				InstrumentMethod(false, true);

				Assert.AreEqual(_origInstrCount + 3, _body.Instructions.Count);

				var instr = _body.Instructions.Single(i => i.OpCode == Cecil.OpCodes.Ret);
				AssertExitInstrumentedCorrectly(instr);
			}

			[TestMethod]
			public void InstrumentsMethodExitsCloseToTheStartofTheMethod() {
				InstrumentMethod(false, true, "returns.il.txt", "ShortMethod");

				Assert.AreEqual(_origInstrCount + 3, _body.Instructions.Count);

				var instr = _body.Instructions.Single(i => i.OpCode == Cecil.OpCodes.Ret);
				AssertExitInstrumentedCorrectly(instr);
			}

			[TestMethod]
			public void MethodHasMultipleReturns_MethodExitInstrumentsCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "MultipleReturns");

				Assert.AreEqual(_origInstrCount + 6, _body.Instructions.Count);

				foreach (var retInstr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Ret)) {
					AssertExitInstrumentedCorrectly(retInstr);
				}
			}

			private void AssertTailCallInstrumentedCorrectly(Cecil.Instruction tailinstr) {

				AssertExitInstrumentedCorrectly(tailinstr);

				var instr = tailinstr;

				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Tail, instr.OpCode);

				instr = instr.Next;
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Call, instr.OpCode);

				instr = instr.Next;
				Assert.IsNotNull(instr);
				Assert.AreEqual(Cecil.OpCodes.Ret, instr.OpCode);
			}

			[TestMethod]
			public void MethodHasTailCall_TailCallInstrumentsCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "SingleTailCall");

				Assert.AreEqual(_origInstrCount + 3, _body.Instructions.Count);

				var instr = _body.Instructions.Single(i => i.OpCode == Cecil.OpCodes.Tail);
				AssertTailCallInstrumentedCorrectly(instr);
			}

			[TestMethod]
			public void MethodHasMultipleTailCalls_TailCallsInstrumentCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "MultipleTailCalls");

				Assert.AreEqual(_origInstrCount + 6, _body.Instructions.Count);

				foreach (var instr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Tail)) {
					AssertTailCallInstrumentedCorrectly(instr);
				}
			}

			[TestMethod]
			public void MethodHasTailCallAndReturn_BothInstrumentCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "TailCallAndReturn");

				Assert.AreEqual(_origInstrCount + 6, _body.Instructions.Count);

				foreach (var instr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Tail)) {
					AssertTailCallInstrumentedCorrectly(instr);
				}

				foreach (var retInstr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Ret)) {
					if (retInstr.Previous.Previous.OpCode == Cecil.OpCodes.Tail)
						continue;

					AssertExitInstrumentedCorrectly(retInstr);
				}
			}

			[TestMethod]
			public void MethodHasJmpInstr_InstrumentsCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "ExitMethodWithJmp");

				Assert.AreEqual(_origInstrCount + 6, _body.Instructions.Count);

				foreach (var instr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Jmp)) {
					AssertExitInstrumentedCorrectly(instr);
				}
			}

			[TestMethod]
			public void MethodHasJmpAndTailAndRetInstr_InstrumentedCorrectly() {
				InstrumentMethod(false, true, "returns.il.txt", "ExitMethodWithJmpAndTail");

				Assert.AreEqual(_origInstrCount + 9, _body.Instructions.Count);

				foreach (var instr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Jmp)) {
					AssertExitInstrumentedCorrectly(instr);
				}

				foreach (var instr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Tail)) {
					AssertTailCallInstrumentedCorrectly(instr);
				}

				foreach (var retInstr in _body.Instructions.Where(i => i.OpCode == Cecil.OpCodes.Ret)) {
					if (retInstr.Previous.Previous.OpCode == Cecil.OpCodes.Tail)
						continue;

					AssertExitInstrumentedCorrectly(retInstr);
				}
			}

			private void AssertAllBranchesJumpToExitInstrumentation(Cecil.MethodBody body) {
				_imports = ILInstrumenter_Privates.CreateImports(body.Method.Module, Mock_Module_Id);

				var controller = ILHelper.GetMessageController(true, true);
				var factory = new Moq.Mock<IInstrumentFactory>();
				factory.Setup(f => f.MessageController).Returns(controller.Object);

				var doesInstrJumpToRet = new Func<Cecil.Instruction, bool>(
					i => i.Operand is Cecil.Instruction && ((Cecil.Instruction)i.Operand).OpCode == Cecil.OpCodes.Ret);

				var jumpToRetInstructions = body.Instructions
					.Where(doesInstrJumpToRet)
					.ToArray();

				ILInstrumenter_Privates.InsertInstrumentationCallbacks(_methodId, 1, Mock_Module_Id, body, _imports, factory.Object);

				var anyJumpToRet = body.Instructions.Any(doesInstrJumpToRet);

				Assert.IsFalse(anyJumpToRet);

				foreach (var jmpInstr in jumpToRetInstructions) {
					AssertJumpToExitInstrumentedCorrectly(jmpInstr);
				}
			}

			[TestMethod]
			public void ExitInstrumentationReplacesBranchesToRetInstr_WithBranchesToExitInstrumentation() {
				var body = ILHelper.GetMethodBody("returns.il.txt", "SingleBranchToSingleReturn");
				AssertAllBranchesJumpToExitInstrumentation(body);
			}

			[TestMethod]
			public void MultipleBranchInstructionsToTheSameRetInstr_BranchesReplacedWithBranchesToExitInstrumentation() {
				var body = ILHelper.GetMethodBody("returns.il.txt", "MultipleBranchesToSingleReturn");
				AssertAllBranchesJumpToExitInstrumentation(body);
			}

			[TestMethod]
			public void MultipleBranchInstructionsToDifferentRetInstr_BranchesReplacedWithBranchesToCorrectExitInstrumentation() {
				var body = ILHelper.GetMethodBody("returns.il.txt", "MultipleBranchesToMultipleReturns");

				var retJmpTargets = body.Instructions
					.Where(i => i.Operand is Cecil.Instruction && ((Cecil.Instruction)i.Operand).OpCode == Cecil.OpCodes.Ret)
					.Select(i => new {
						Target = (Cecil.Instruction)i.Operand,
						Instr = i
					})
					.Aggregate(new Dictionary<Cecil.Instruction, List<Cecil.Instruction>>(
						new GenericEqualityComparer<Cecil.Instruction>((left, right) => left.Offset == right.Offset, left => left.Offset.GetHashCode())),
						(dict, item) => {
							if (dict.ContainsKey(item.Target))
								dict[item.Target].Add(item.Instr);
							else {
								dict[item.Target] = new List<Cecil.Instruction>();
								dict[item.Target].Add(item.Instr);
							}

							return dict;
						})
					.ToArray();


				AssertAllBranchesJumpToExitInstrumentation(body);

				// great, so now we know that all of the branches were correctly
				// moved two instructions prior to where they were before, and that
				// the 2nd isntruction past the one they point to is a return
				// statement, BUT is it the same return statement as before?
				foreach (var pair in retJmpTargets) {
					var retInstr = pair.Key;
					var jmpStatement = pair.Value;
					foreach (var jmp in jmpStatement) {
						var newRetInstr = ((Cecil.Instruction)jmp.Operand).Next.Next;

						Assert.AreEqual(Cecil.OpCodes.Ret, newRetInstr.OpCode);
						Assert.AreSame(retInstr, newRetInstr);
					}
				}

			}

			[TestMethod]
			public void MethodInGenericClass_EntranceInstrumentationIncludesClassTypeParams() {
				InstrumentMethod(true, false, "generictypesandmethods.il.txt", "T generictypesandmethods.GenericClass`2::Method1(K,System.Func`2<K,T>)");

				// make sure the right number of instructions were insterted
				Assert.AreEqual(_origInstrCount + 5, _body.Instructions.Count);

				// make sure the right instructions were inserted
				// assembly id
				Assert.AreEqual(OpCodes.Ldsfld, _body.Instructions[0].OpCode);
				Assert.AreEqual("System.Int32 NetPerf.NetPerfMetadata::ModuleIdentifier", _body.Instructions[0].Operand.ToString());

				// method id
				Assert.AreEqual(OpCodes.Ldc_I4, _body.Instructions[1].OpCode);
				Assert.AreEqual(_methodId, (int)_body.Instructions[1].Operand);

				// type's generic arguments
				Assert.AreEqual(OpCodes.Call, _body.Instructions[2].OpCode);
				Assert.AreEqual(
					"System.String[] generictypesandmethods.GenericClass`2<T,K>::<GenericClass`2>b__GenericArgs()",
					((MethodReference)_body.Instructions[2].Operand).FullName);

				// method's generic arguments
				Assert.AreEqual(OpCodes.Ldnull, _body.Instructions[3].OpCode);
				Assert.AreEqual(null, _body.Instructions[3].Operand);

				// the instrumentation method call
				Assert.AreEqual(OpCodes.Call, _body.Instructions[4].OpCode);
				Assert.AreEqual(
					"System.Void NetPerf.Client.Messaging::EnterGenericMethod(System.Int32,System.Int32,System.String[],System.String[])",
					((MethodReference)_body.Instructions[4].Operand).FullName);
			}

			[TestMethod]
			public void MethodInGenericClass_ExitInstrumentationIncludesClassTypeParams() {
				InstrumentMethod(false, true, "generictypesandmethods.il.txt", "T generictypesandmethods.GenericClass`2::Method1(K,System.Func`2<K,T>)");

				// make sure the right number of instructions were insterted
				Assert.AreEqual(_origInstrCount + 5, _body.Instructions.Count);

				var retInstr = _body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
				var instr = retInstr.Previous.Previous.Previous.Previous.Previous;

				// make sure the right instructions were inserted
				// module id
				Assert.AreEqual(OpCodes.Ldsfld, instr.OpCode);
				Assert.AreEqual("System.Int32 NetPerf.NetPerfMetadata::ModuleIdentifier", instr.Operand.ToString());

				// method id
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Ldc_I4, instr.OpCode);
				Assert.AreEqual(_methodId, (int)instr.Operand);

				// type's generic arguments
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Call, instr.OpCode);
				Assert.AreEqual(
					"System.String[] generictypesandmethods.GenericClass`2<T,K>::<GenericClass`2>b__GenericArgs()",
					((MethodReference)instr.Operand).FullName);

				// method's generic arguments
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Ldnull, instr.OpCode);
				Assert.AreEqual(null, instr.Operand);

				// the instrumentation method call
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Call, instr.OpCode);
				Assert.AreEqual(
					"System.Void NetPerf.Client.Messaging::ExitGenericMethod(System.Int32,System.Int32,System.String[],System.String[])",
					((MethodReference)instr.Operand).FullName);

				instr = instr.Next;
				Assert.AreEqual(OpCodes.Ret, instr.OpCode);
			}

			[TestMethod]
			public void GenericMethod_EntranceInstrumentationIncludesMethodTypeParams() {
				InstrumentMethod(true, false, "generictypesandmethods.il.txt", "T generictypesandmethods.InstanceClass::Method1(K,System.Func`2<K,T>)");

				var numExtraInstructions = 5 + 3 + (6 * 2);
				// make sure the right number of instructions were insterted
				Assert.AreEqual(_origInstrCount + numExtraInstructions, _body.Instructions.Count);

				// make sure the right instructions were inserted
				// module id
				Assert.AreEqual(OpCodes.Ldsfld, _body.Instructions[0].OpCode);
				Assert.AreEqual("System.Int32 NetPerf.NetPerfMetadata::ModuleIdentifier", _body.Instructions[0].Operand.ToString());

				// method id
				Assert.AreEqual(OpCodes.Ldc_I4, _body.Instructions[1].OpCode);
				Assert.AreEqual(_methodId, (int)_body.Instructions[1].Operand);

				// type's generic arguments
				Assert.AreEqual(OpCodes.Ldnull, _body.Instructions[2].OpCode);
				Assert.AreEqual(null, _body.Instructions[2].Operand);

				// method's generic arguments
				VerifyGenericMethodInstrumetation(_body.Instructions.Skip(3), _body.Variables.Count - 1, _body.Method.GenericParameters.Count);

				// the instrumentation method call
				Assert.AreEqual(OpCodes.Call, _body.Instructions[numExtraInstructions - 1].OpCode);
				Assert.AreEqual(
					"System.Void NetPerf.Client.Messaging::EnterGenericMethod(System.Int32,System.Int32,System.String[],System.String[])",
					((MethodReference)_body.Instructions[numExtraInstructions - 1].Operand).FullName);
			}

			private void VerifyGenericMethodInstrumetation(IEnumerable<Instruction> instructions, int localVarIndex, int numGenericParams, bool isExitInstrumentation = false) {
				var instrs = instructions.ToArray();

				var numInstructions = isExitInstrumentation
					? 1
					: 4 + (6 * numGenericParams);

				if (!isExitInstrumentation) {
					Assert.AreEqual(OpCodes.Ldc_I4, instrs[0].OpCode);
					Assert.AreEqual(numGenericParams, (int)instrs[0].Operand);

					Assert.AreEqual(OpCodes.Newarr, instrs[1].OpCode);
					Assert.AreEqual("System.String", ((TypeReference)instrs[1].Operand).FullName);

					Assert.AreEqual(OpCodes.Stloc, instrs[2].OpCode);
					Assert.AreEqual(localVarIndex, ((VariableReference)instrs[2].Operand).Index);

					for (int i = 0; i < numGenericParams; i++) {
						Assert.AreEqual(OpCodes.Ldloc, instrs[3 + (i * 6)].OpCode);
						Assert.AreEqual(localVarIndex, ((VariableReference)instrs[3 + (i * 6)].Operand).Index);

						Assert.AreEqual(OpCodes.Ldc_I4, instrs[4 + (i * 6)].OpCode);
						Assert.AreEqual(i, (int)instrs[4 + (i * 6)].Operand);

						Assert.AreEqual(OpCodes.Ldtoken, instrs[5 + (i * 6)].OpCode);
						Assert.AreEqual(i, ((GenericParameter)instrs[5 + (i * 6)].Operand).Position);

						Assert.AreEqual(OpCodes.Call, instrs[6 + (i * 6)].OpCode);
						Assert.AreEqual("System.Type System.Type::GetTypeFromHandle(System.RuntimeTypeHandle)", ((MethodReference)instrs[6 + (i * 6)].Operand).FullName);

						Assert.AreEqual(OpCodes.Callvirt, instrs[7 + (i * 6)].OpCode);
						Assert.AreEqual("System.String System.Type::get_FullName()", ((MethodReference)instrs[7 + (i * 6)].Operand).FullName);

						Assert.AreEqual(OpCodes.Stelem_Ref, instrs[8 + (i * 6)].OpCode);
						Assert.AreEqual(null, instrs[8 + (i * 6)].Operand);
					}
				}

				// make sure the local string[] variable w/ the method's generic arguments is loaded
				Assert.AreEqual(OpCodes.Ldloc, instrs[numInstructions - 1].OpCode);
				Assert.AreEqual(localVarIndex, ((VariableReference)instrs[numInstructions - 1].Operand).Index);
			}

			//ignored b/c the implementation this is supposed to test isn't up to snuff
			[TestMethod, Ignore]
			public void GenericMethod_ExitInstrumentationIncludesMethodTypeParams() {
				InstrumentMethod(false, true, "generictypesandmethods.il.txt", "T generictypesandmethods.InstanceClass::Method1(K,System.Func`2<K,T>)");

				var numExtraInstructions = 5;
				// make sure the right number of instructions were insterted
				Assert.AreEqual(_origInstrCount + numExtraInstructions, _body.Instructions.Count);

				var retInstr = _body.Instructions.Single(i => i.OpCode == OpCodes.Ret);
				var instr = retInstr.Previous.Previous.Previous.Previous.Previous;

				// make sure the right instructions were inserted
				// assembly id
				Assert.AreEqual(OpCodes.Ldc_I4, instr.OpCode);
				Assert.AreEqual(Mock_Module_Id, (int)instr.Operand);

				// method id
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Ldc_I4, instr.OpCode);
				Assert.AreEqual(_methodId, (int)instr.Operand);

				// type's generic arguments
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Ldnull, instr.OpCode);
				Assert.AreEqual(null, instr.Operand);

				// method's generic arguments
				instr = instr.Next;
				VerifyGenericMethodInstrumetation(_body.Instructions.SkipWhile(i => i != instr), _body.Variables.Count - 1, _body.Method.GenericParameters.Count, true);

				// the instrumentation method call
				instr = instr.Next;
				Assert.AreEqual(OpCodes.Call, _body.Instructions[numExtraInstructions - 1].OpCode);
				Assert.AreEqual(
					"System.Void NetPerf.Client.Messaging::EnterGenericMethod(System.Int32,System.Int32,System.String[],System.String[])",
					((MethodReference)_body.Instructions[numExtraInstructions - 1].Operand).FullName);
			}
		}
	}
}
