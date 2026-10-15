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

    /*
     
    Hand verified Call Graphs
     
    */

    [CLSCompliant(false)]
    [TestClass]
    public class NormalClassInheritedTests : CallGraphValidateBaseClass {

        [TestMethod]
        public void MainCallGraphTestClasses_NormalClassInheritedOverrideProperties() {

            IntegrationTestIClientMessageService.Instance.ClearCalls();

            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverrideProperties();

            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::.ctor()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoProperty(System.Int32)")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoProperty(System.Int32)")

                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoReadOnlyProperty(System.Int32)")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoReadOnlyProperty(System.Int32)")

                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoWriteOnlyProperty(System.Int32)")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverrideProperties::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void NormalClassInheritedOverridesAndCallsBasePublicMethods_VoidMethodTwoLines() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoLines();
            //VoidMethodTwoLines
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods::VoidMethodTwoLines()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods::VoidMethodTwoLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesAndCallsBasePublicMethods_IntMethodReturnOneLine() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethodReturnOneLine();
            //IntMethodReturnOneLine
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods::IntMethodReturnOneLine()")
                .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLine()")
                .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::IntMethodReturnOneLine()")
            .Exit("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods::IntMethodReturnOneLine()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesCtorsForwardCtorDefault() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalClassInheritedOverridesCtorsForward();
            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward::.ctor()")

                .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor())

            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesCtorsForwardCtorThreeParms() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalClassInheritedOverridesCtorsForward(1, 2, 3);
            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward::.ctor(System.Int32,System.Int32,System.Int32)")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")

                .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())

                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward::.ctor(System.Int32,System.Int32,System.Int32)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesCtorsSetDefault() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalClassInheritedOverridesCtorsSet();
            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet::.ctor()")

            .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor())

            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesCtorsSetThreeParams() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalClassInheritedOverridesCtorsSet(1, 2, 3);
            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet::.ctor(System.Int32,System.Int32,System.Int32)")

            .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor())

            .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())

            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet::.ctor(System.Int32,System.Int32,System.Int32)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesPublicAndPrivateMethods_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
            //VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)")
                //This line changes the call graph and we can't use the append
                .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::VoidPrivateMethodTwoLines()")

                .Call("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesPublicAndPrivateMethods_IntMethodReturnOneLineCallPrivateMethod() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethodReturnOneLineCallPrivateMethod();
            //IntMethodReturnOneLineCallPrivateMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::IntMethodReturnOneLineCallPrivateMethod()")
                .Call("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods::IntMethodReturnOneLineCallPrivateMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesPublicCallsBasePublic_VoidMethodTwoLines() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoLines();
            //VoidMethodTwoLines
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublic::VoidMethodTwoLines()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublic::VoidMethodTwoLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate_VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines();
            //VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()")

                .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate::VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void NormalClassInheritedOverridesPublicMethods_VoidMethodTwoLines() {
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethodTwoLines();
            //VoidMethodTwoLines
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicMethods::VoidMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedOverridesPublicMethods::VoidMethodTwoLines()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void MainCallGraphTestClasses_NormalClassInheritedShadowsProperties() {

            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedShadowsProperties();

            //.ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::.ctor()")
                .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoProperty(System.Int32)")

                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoReadOnlyProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoReadOnlyProperty(System.Int32)")

                    .Call("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoWriteOnlyProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::set_IntAutoWriteOnlyProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
            .Exit("System.Void CallGraphTestClasses.NormalClassInheritedShadowsProperties::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        #region gencode

        ////[TestMethod]
        ////public void Main() {
        ////    MainCallGraphTestClasses_NormalClassInheritedOverrideProperties();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsForwardDefault();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsForwardThreeParms();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsSet();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsSetThreeParams();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate();
        ////    MainCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods();
        ////    MainCallGraphTestClasses_NormalClassInheritedShadowsProperties();
        ////} //main

        //[TestMethod]
        //public void Main2() {
        //    MainCallGraphTestClasses_NormalClassInheritedOverrideProperties();
        //    MainCallGraphTestClasses_NormalClassInheritedShadowsProperties();
        //} //main


        //public void MainCallGraphTestClasses_NormalClassInheritedOverrideProperties() {

        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverrideProperties();

        //    Debug.WriteLine("//TODO MainCallGraphTestClasses_NormalClassInheritedOverrideProperties");

        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void MainCallGraphTestClasses_NormalClassInheritedOverrideProperties() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverrideProperties();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.VoidMethodTwoLines();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(""));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");


        //} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods() {
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();
        ////    


        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.VoidMethodTwoLines();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesAndCallsBasePublicMethods_VoidMethodTwoLines() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.VoidMethodTwoLines();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoLines")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");



        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.IntMethodReturnOneLine();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesAndCallsBasePublicMethods_IntMethodReturnOneLine() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.IntMethodReturnOneLine();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("IntMethodReturnOneLine")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsForwardDefault() {

        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();

        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesCtorsForward();
        ////    

        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesCtorsForwardCtorDefault() {");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("var instance = new NormalClassInheritedOverridesCtorsForward();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(".ctor"));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsForwardThreeParms() {

        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();

        ////    var instance = new NormalClassInheritedOverridesCtorsForward(1, 2, 3);

        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesCtorsForwardCtorThreeParms() {");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("var instance = new NormalClassInheritedOverridesCtorsForward(1,2,3);");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(".ctor"));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method


        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsSet() {
        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesCtorsSet();

        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesCtorsSet_.ctor() {");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("var instance = new NormalClassInheritedOverridesCtorsSet(enterhere);");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(".ctor"));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesCtorsSetThreeParams() {
        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    var instance = new NormalClassInheritedOverridesCtorsSet(1, 2, 3);
        ////    

        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesCtorsSet_.ctor() {");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("var instance = new NormalClassInheritedOverridesCtorsSet(1,2,3);");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(".ctor"));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method


        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods() {
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();
        ////    


        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesPublicAndPrivateMethods_VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoPropertiesCallPrivateMethodThenOneProperty")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");



        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.IntMethodReturnOneLineCallPrivateMethod();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesPublicAndPrivateMethods_IntMethodReturnOneLineCallPrivateMethod() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.IntMethodReturnOneLineCallPrivateMethod();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("IntMethodReturnOneLineCallPrivateMethod")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic() {
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic();
        ////    


        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.VoidMethodTwoLines();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesPublicCallsBasePublic_VoidMethodTwoLines() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.VoidMethodTwoLines();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoLines")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate() {
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate();
        ////    


        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate_VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoLinesCallPrivateTwoLinesThenTwoMoreLines")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        ////public void MainCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods() {
        ////    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods();
        ////    


        ////    IntegrationTestIClientMessageService.Instance.ClearCalls();
        ////    instance.VoidMethodTwoLines();
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("[TestMethod]");
        ////    Debug.WriteLine("public void NormalClassInheritedOverridesPublicMethods_VoidMethodTwoLines() {");
        ////    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods();");
        ////    Debug.WriteLine("");
        ////    Debug.WriteLine("//clear after creating.");
        ////    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        ////    Debug.WriteLine("instance.VoidMethodTwoLines();");
        ////    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("VoidMethodTwoLines")));

        ////    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        ////    Debug.WriteLine("}");
        ////    Debug.WriteLine("");

        ////} //method

        //public void MainCallGraphTestClasses_NormalClassInheritedShadowsProperties() {

        //    IntegrationTestIClientMessageService.Instance.ClearCalls();

        //    var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedShadowsProperties();

        //    Debug.WriteLine("//TODO MainCallGraphTestClasses_NormalClassInheritedShadowsProperties");

        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void MainCallGraphTestClasses_NormalClassInheritedShadowsProperties() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalClassInheritedShadowsProperties();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.VoidMethodTwoLines();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(".ctor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");


        //} //method

        #endregion gencode

        protected CallGraphTestClasses.NormalClassInheritedOverrideProperties GetInstanceCallGraphTestClasses_NormalClassInheritedOverrideProperties() {
            return new CallGraphTestClasses.NormalClassInheritedOverrideProperties();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesAndCallsBasePublicMethods() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesAndCallsBasePublicMethods();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesCtorsForward() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesCtorsForward();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesCtorsSet() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesCtorsSet();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicAndPrivateMethods() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesPublicAndPrivateMethods();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublic GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublic() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublic();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesPublicCallsBasePublicPrivateAndPrivate();
        }


        protected CallGraphTestClasses.NormalClassInheritedOverridesPublicMethods GetInstanceCallGraphTestClasses_NormalClassInheritedOverridesPublicMethods() {
            return new CallGraphTestClasses.NormalClassInheritedOverridesPublicMethods();
        }


        protected CallGraphTestClasses.NormalClassInheritedShadowsProperties GetInstanceCallGraphTestClasses_NormalClassInheritedShadowsProperties() {
            return new CallGraphTestClasses.NormalClassInheritedShadowsProperties();
        }

    } //class
} //ns
