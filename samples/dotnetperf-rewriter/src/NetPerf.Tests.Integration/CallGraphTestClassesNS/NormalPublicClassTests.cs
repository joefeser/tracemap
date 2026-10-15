using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using NetPerf.Common.Instrumentation;
using CallGraphTestClasses;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    /*
     
    Hand verified Call Graphs
     
    */

    [TestClass]
    [CLSCompliant(false)]
    public class NormalPublicClassTests : CallGraphValidateBaseClass {

        [TestMethod]
        public void NormalPublicClassTests_VerifyDefaultCtor() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = GetInstance();

            //Console.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements());

            var tester = new CallGraphTester(InstrumentationData)
                        .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VerifyCtor3() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalPublicClass(3, 5, 7);

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                            .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_IntMethodReturnOneLine() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            var value = instance.IntMethodReturnOneLine();

            var tester = new CallGraphTester(InstrumentationData)
                    .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLine()")
                    .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLine()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_IntMethodReturnOneLineCallPrivateMethod() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            var value = instance.IntMethodReturnOneLineCallPrivateMethod();

            var tester = new CallGraphTester(InstrumentationData)
                         .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLineCallPrivateMethod()")
                             .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::IntPrivateMethodTwoLines()")
                             .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::IntPrivateMethodTwoLines()")
                         .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLineCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_IntMethodTwoPropertiesCallPrivateMethodThenOneProperty() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            var value = instance.IntMethodTwoPropertiesCallPrivateMethodThenOneProperty();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                            .Append(Calls_NormalPublicClass_set_IntAutoPropertyAndVoidMethodTwoLines())    

                            .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoWriteOnlyProperty()")
                            .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoWriteOnlyProperty()")
                        .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodLockBlockCallPrivateMethod() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodLockBlockCallPrivateMethod();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodLockBlockCallPrivateMethod()")
                            .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                            .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodLockBlockCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodLockBlockInstanceField() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodLockBlockInstanceField();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodLockBlockInstanceField()")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodLockBlockInstanceField()");
            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodTwoLines() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodTwoLines();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodTwoLinesCallPrivateTwoLines() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodTwoLinesCallPrivateTwoLines();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLines()")
                            .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                            .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLines()");
            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()")
                            .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                            .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalPublicClassTests_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {

            var instance = GetInstance();
            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();

            var tester = new CallGraphTester(InstrumentationData)
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                            .Append(Calls_NormalPublicClass_set_IntAutoPropertyAndVoidMethodTwoLines())
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        protected NormalPublicClass GetInstance() {
            return new NormalPublicClass();
        }

        public static IEnumerable<CallGraphMethod> Calls_NormalPublicClass_Ctor() {
            var retVal = new List<CallGraphMethod>();
            retVal.Add(CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()"));
            retVal.AddRange(Calls_NormalPublicClass_set_IntAutoProperty());
            retVal.Add(CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()"));

            return retVal;
        }

        public static IEnumerable<CallGraphMethod> Calls_NormalPublicClass_set_IntAutoProperty() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
                    
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
                    
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")
             };

            return retVal;
        }

        public static IEnumerable<CallGraphMethod> Calls_NormalPublicClass_set_IntAutoPropertyAndVoidMethodTwoLines() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),

                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),

                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()"),

                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")

            };

            return retVal;
        }



    }
}
