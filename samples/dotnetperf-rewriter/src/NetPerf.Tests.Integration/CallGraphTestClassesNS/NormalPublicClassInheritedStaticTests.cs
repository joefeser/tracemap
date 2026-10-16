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

    [TestClass]
    public class NormalPublicClassInheritedStaticTests : CallGraphValidateBaseClass {
        //TODO locks up the server Determine why this causes a loop condition.
        [Ignore]
        //[TestMethod]
        public void NormalClassGenericChildClass_NormalPublicClassInheritedStatic() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalPublicClassInheritedStatic();

            //NOTE this test will fail in vs.net because the other tests exercise first causing a lifecycle issue and the .cctor does not execute

            //..ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalPublicClassInheritedStatic::.cctor()")
            .Exit("System.Void CallGraphTestClasses.NormalPublicClassInheritedStatic::.cctor()")

            .Call("System.Void CallGraphTestClasses.NormalPublicClassInheritedStatic::.ctor()")
            .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor())
            .Exit("System.Void CallGraphTestClasses.NormalPublicClassInheritedStatic::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        //TODO locks up the server Determine why this causes a loop condition.
        [Ignore]//locks up the server
        //[TestMethod]
        public void NormalClassGenericChildClass_NormalPublicClassInheritedStaticNoDefaultStaticCtor() {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = new NormalPublicClassInheritedStaticNoDefaultStaticCtor();

            //NOTE this test will fail in vs.net because the other tests exercise first causing a lifecycle issue and the .cctor does not execute

            //..ctor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.NormalPublicClassInheritedStaticNoDefaultStaticCtor::.cctor()")
            .Exit("System.Void CallGraphTestClasses.NormalPublicClassInheritedStaticNoDefaultStaticCtor::.cctor()")

            .Call("System.Void CallGraphTestClasses.NormalPublicClassInheritedStaticNoDefaultStaticCtor::.ctor()")

            .Append(NormalPublicClassTests.Calls_NormalPublicClass_Ctor())

            .Exit("System.Void CallGraphTestClasses.NormalPublicClassInheritedStaticNoDefaultStaticCtor::.ctor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        #region gencode

        //[TestMethod]
        //public void Main() {
        //    NormalClassGenericChildClass_NormalPublicClassInheritedStatic();

        //} //main


        //public void NormalClassGenericChildClass_NormalPublicClassInheritedStatic() {
        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    var instance = GetInstanceCallGraphTestClasses_NormalPublicClassInheritedStatic();

        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalClassGenericChildClass_NormalPublicClassInheritedStatic() {");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_NormalPublicClassInheritedStatic();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("..ctor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method


        //[TestMethod]
        //public void Main() {
        //    NormalClassGenericChildClass_NormalPublicClassInheritedStatic2();

        //} //main


        //public void NormalClassGenericChildClass_NormalPublicClassInheritedStatic2() {
        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    var instance = new NormalPublicClassInheritedStaticNoDefaultStaticCtor();

        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void NormalClassGenericChildClass_NormalPublicClassInheritedStaticNoDefaultStaticCtor() {");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("var instance = new NormalPublicClassInheritedStaticNoDefaultStaticCtor();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("..ctor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        #endregion gencode

    } //class
} //ns
