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
  
    //TODO Verify the generic methods including the generic calls.

    [CLSCompliant(false)]
    [TestClass]
    public class GenericMethodTests : CallGraphValidateBaseClass {

        //[TestMethod]
        //public void Main() {
        //    MainCallGraphTestClasses_GenericMethodsExecutor();
        //} //main

        //public void MainCallGraphTestClasses_GenericMethodsExecutor() {
        //    var instance = GetInstanceCallGraphTestClasses_GenericMethodsExecutor();
        //    var type = instance.GetType();


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    instance.ExecuteAll();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void GenericMethodsExecutor_ExecuteAll() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_GenericMethodsExecutor();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.ExecuteAll();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements(type.GetMethod("ExecuteAll")));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method


        [TestMethod]
        public void GenericMethodsExecutor_ExecuteAll() {
            var instance = GetInstanceCallGraphTestClasses_GenericMethodsExecutor();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.ExecuteAll();
            //ExecuteAll
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.GenericMethodsExecutor::ExecuteAll()")
                .Call("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass::.ctor()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method1(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__0(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__0(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method1(T,System.Func`2<T,R>)")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method1(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__1(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__1(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method1(T,System.Func`2<T,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method2()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method2()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method3(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__2(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__2(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method3(T,System.Func`2<T,R>)")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method3(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__3(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__3(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method3(T,System.Func`2<T,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass::Method4()")
                
                .Call("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::.ctor()")
                .Exit("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::.ctor()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__4(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__4(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                    .Call("System.Int32 CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__5(System.Int32)")
                    .Exit("System.Int32 CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__5(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method2()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method2()")
                
                .Call("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::.ctor()")
                .Exit("System.Void CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::.ctor()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__6(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__6(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                    .Call("System.Int64 CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__7(System.Int64)")
                    .Exit("System.Int64 CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__7(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method1(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method2()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method2()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method3(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__8(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__8(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method3(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method4()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method3(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__9(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__9(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method3(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinInstanceClass`1::Method4()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinStaticClass::Method3(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__a(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__a(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinStaticClass::Method3(T,System.Func`2<T,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass::Method4()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinStaticClass::Method3(T,System.Func`2<T,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__b(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__b(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinStaticClass::Method3(T,System.Func`2<T,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass::Method4()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method3(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__c(System.Int32)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__c(System.Int32)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method3(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method4()")
                
                .Call("R CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method3(K,System.Func`2<K,R>)")
                    .Call("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__d(System.Int64)")
                    .Exit("System.String CallGraphTestClasses.GenericMethodsExecutor::<ExecuteAll>b__d(System.Int64)")
                .Exit("R CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method3(K,System.Func`2<K,R>)")
                
                .Call("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method4()")
                .Exit("System.Int32 CallGraphTestClasses.GenericMethodsWithinStaticClass`1::Method4()")
            .Exit("System.Void CallGraphTestClasses.GenericMethodsExecutor::ExecuteAll()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }
        
        protected CallGraphTestClasses.GenericMethodsExecutor GetInstanceCallGraphTestClasses_GenericMethodsExecutor() {
            return new CallGraphTestClasses.GenericMethodsExecutor();
        }
    } //class
}
