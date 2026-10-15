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
    public class RecursiveClassTests : CallGraphValidateBaseClass {

        [TestMethod]
        public void RecursiveClassBase_MethodTwoEnter() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoEnter();
            //MethodTwoEnter
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassBase_MethodTwoExit() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoExit();
            //MethodTwoExit
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassBase_TwoLevelsSameMethod() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.TwoLevelsSameMethod();
            //TwoLevelsSameMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::TwoLevelsSameMethod()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::TwoLevelsSameMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassBase_RecursiveMethodCountCallMethodTwo() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.RecursiveMethodCountCallMethodTwo();
            //RecursiveMethodCountCallMethodTwo
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")

                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")

                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")

                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassCallBase_MethodTwoEnter() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoEnter();
            //MethodTwoEnter
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassCallBase_MethodTwoExit() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoExit();
            //MethodTwoExit
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassCallBase_TwoLevelsSameMethod() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.TwoLevelsSameMethod();
            //TwoLevelsSameMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::TwoLevelsSameMethod()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCount(System.Int32,System.Int32)")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::TwoLevelsSameMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassCallBase_RecursiveMethodCountCallMethodTwo() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.RecursiveMethodCountCallMethodTwo();
            //RecursiveMethodCountCallMethodTwo
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::RecursiveMethodCountCallMethodTwo()")
                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")

                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                                .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")

                                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
                                    .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")
                                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoEnter()")
                                    .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoEnter()")

                                    .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                                    .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                                .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                                    .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                            .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                                .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                        .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                            .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                            .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")

                    .Call("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                        .Call("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                        .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::MethodTwoExit()")
                    .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::MethodTwoExit()")
                .Exit("System.Void CallGraphTestClasses.RecursiveClassBase::RecursiveMethodCountCallMethodTwo(System.Int32,System.Int32)")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassCallBase::RecursiveMethodCountCallMethodTwo()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassInherited_MethodTwoEnter() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassInherited();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoEnter();
            //MethodTwoEnter
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassInherited::MethodTwoEnter()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassInherited::MethodTwoEnter()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RecursiveClassInherited_MethodTwoExit() {
            var instance = GetInstanceCallGraphTestClasses_RecursiveClassInherited();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.MethodTwoExit();
            //MethodTwoExit
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.RecursiveClassInherited::MethodTwoExit()")
            .Exit("System.Void CallGraphTestClasses.RecursiveClassInherited::MethodTwoExit()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        protected CallGraphTestClasses.RecursiveClassBase GetInstanceCallGraphTestClasses_RecursiveClassBase() {
            return new CallGraphTestClasses.RecursiveClassBase();
        }


        protected CallGraphTestClasses.RecursiveClassCallBase GetInstanceCallGraphTestClasses_RecursiveClassCallBase() {
            return new CallGraphTestClasses.RecursiveClassCallBase();
        }


        protected CallGraphTestClasses.RecursiveClassInherited GetInstanceCallGraphTestClasses_RecursiveClassInherited() {
            return new CallGraphTestClasses.RecursiveClassInherited();
        }

        #region codegen

        //[TestMethod]
        //public void Main() {
        //    MainCallGraphTestClasses_RecursiveClassBase();
        //    MainCallGraphTestClasses_RecursiveClassCallBase();
        //    MainCallGraphTestClasses_RecursiveClassInherited();
        //} //main

        //public void MainCallGraphTestClasses_RecursiveClassBase() {
        //    var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoEnter();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassBase_MethodTwoEnter() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoEnter();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoEnter"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoExit();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassBase_MethodTwoExit() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoExit();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoExit"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.TwoLevelsSameMethod();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassBase_TwoLevelsSameMethod() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.TwoLevelsSameMethod();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("TwoLevelsSameMethod"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.RecursiveMethodCountCallMethodTwo();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassBase_RecursiveMethodCountCallMethodTwo() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.RecursiveMethodCountCallMethodTwo();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("RecursiveMethodCountCallMethodTwo"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        //public void MainCallGraphTestClasses_RecursiveClassCallBase() {
        //    var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoEnter();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassCallBase_MethodTwoEnter() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoEnter();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoEnter"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoExit();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassCallBase_MethodTwoExit() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoExit();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoExit"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.TwoLevelsSameMethod();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassCallBase_TwoLevelsSameMethod() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.TwoLevelsSameMethod();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("TwoLevelsSameMethod"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.RecursiveMethodCountCallMethodTwo();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassCallBase_RecursiveMethodCountCallMethodTwo() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassCallBase();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.RecursiveMethodCountCallMethodTwo();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("RecursiveMethodCountCallMethodTwo"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        //public void MainCallGraphTestClasses_RecursiveClassInherited() {
        //    var instance = GetInstanceCallGraphTestClasses_RecursiveClassInherited();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoEnter();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassInherited_MethodTwoEnter() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassInherited();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoEnter();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoEnter"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.MethodTwoExit();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void RecursiveClassInherited_MethodTwoExit() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_RecursiveClassInherited();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.MethodTwoExit();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("MethodTwoExit"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        #endregion

    } //class
} //ns
