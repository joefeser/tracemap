using System;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using NetPerf.Common.Instrumentation;
using CallGraphTestClasses;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    //TODO modify tests to use the generic parameter when we decide we will be reporting based on the generic type.

    /*
     
    Hand verified Call Graphs
     
    */

    [CLSCompliant(false)]
    [TestClass]
    public class NormalClassGenericChildClassTests : CallGraphValidateBaseClass {


        [TestMethod]
        public void NormalClassGenericChildClass_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
            //VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoReadOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoReadOnlyProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass::VoidPrivateMethodTwoLines()")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass::set_IntAutoWriteOnlyProperty(System.Int32)")
            .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassGenericChildClass_IntMethodReturnOneLineCallPrivateMethod() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethodReturnOneLineCallPrivateMethod();
            //IntMethodReturnOneLineCallPrivateMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass::IntMethodReturnOneLineCallPrivateMethod()")
                .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass::IntMethodReturnOneLineCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalAndGenericChildClass_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
            //VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoReadOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoReadOnlyProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::VoidPrivateMethodTwoLines()")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::set_IntAutoWriteOnlyProperty(System.Int32)")
            .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalAndGenericChildClass_IntMethodReturnOneLineCallPrivateMethod() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethodReturnOneLineCallPrivateMethod();
            //IntMethodReturnOneLineCallPrivateMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::IntMethodReturnOneLineCallPrivateMethod()")
                .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass::IntMethodReturnOneLineCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalAndGenericChildClass_T_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
            //VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoReadOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoReadOnlyProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::VoidPrivateMethodTwoLines()")

                .Call("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::set_IntAutoWriteOnlyProperty(System.Int32)")
            .Exit("System.Void CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalAndGenericChildClass_T_IntMethodReturnOneLineCallPrivateMethod() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethodReturnOneLineCallPrivateMethod();
            //IntMethodReturnOneLineCallPrivateMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::IntMethodReturnOneLineCallPrivateMethod()")
                .Call("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.NormalClassGenericChildClass/NormalAndGenericChildClass`1::IntMethodReturnOneLineCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        #region genclass

        //[TestMethod]
        //public void Main() {
        //    MainCallGraphTestClasses_NormalClassGenericChildClass();
        //    MainCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();
        //    MainCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T();
        //} //main

        //public void MainCallGraphTestClasses_NormalClassGenericChildClass() {
        //    var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass();
        //    var type = instance.GetType();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalClassGenericChildClass_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.IntMethodReturnOneLineCallPrivateMethod();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalClassGenericChildClass_IntMethodReturnOneLineCallPrivateMethod() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.IntMethodReturnOneLineCallPrivateMethod();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("IntMethodReturnOneLineCallPrivateMethod")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        //public void MainCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass() {
        //    var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();
        //    var type = instance.GetType();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalAndGenericChildClass_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.IntMethodReturnOneLineCallPrivateMethod();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalAndGenericChildClass_IntMethodReturnOneLineCallPrivateMethod() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.IntMethodReturnOneLineCallPrivateMethod();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("IntMethodReturnOneLineCallPrivateMethod")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        //public void MainCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T() {
        //    var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T();
        //    var type = instance.GetType();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalAndGenericChildClass`1_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass`1();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.IntMethodReturnOneLineCallPrivateMethod();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalAndGenericChildClass`1_IntMethodReturnOneLineCallPrivateMethod() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass`1();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.IntMethodReturnOneLineCallPrivateMethod();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("IntMethodReturnOneLineCallPrivateMethod")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method
        #endregion genclass

        protected CallGraphTestClasses.NormalClassGenericChildClass GetInstanceCallGraphTestClasses_NormalClassGenericChildClass() {
            return new CallGraphTestClasses.NormalClassGenericChildClass();
        }


        protected CallGraphTestClasses.NormalClassGenericChildClass.NormalAndGenericChildClass GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass() {
            return new CallGraphTestClasses.NormalClassGenericChildClass.NormalAndGenericChildClass();
        }


        protected CallGraphTestClasses.NormalClassGenericChildClass.NormalAndGenericChildClass<string> GetInstanceCallGraphTestClasses_NormalClassGenericChildClass_NormalAndGenericChildClass_T() {
            return new CallGraphTestClasses.NormalClassGenericChildClass.NormalAndGenericChildClass<string>();
        }
    } //class
} //ns
