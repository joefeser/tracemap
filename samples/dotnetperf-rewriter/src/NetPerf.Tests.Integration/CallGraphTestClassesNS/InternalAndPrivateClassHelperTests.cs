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
    public class InternalAndPrivateClassHelperTests : CallGraphValidateBaseClass {


        [TestMethod]
        public void InternalAndPrivateClassHelper_CallPrivateClassInheritsPropertiesDefault() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallPrivateClassInheritsPropertiesDefault();
            //CallPrivateClassInheritsPropertiesDefault
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassInheritsPropertiesDefault()")
                .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRideProperties()")
                    .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor()")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                            .Append(Calls_PrivateClassInheritedOverRideProperties())
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                    .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor()")
                .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRideProperties()")

                .Call("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoProperty()")
                    .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                .Exit("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoProperty()")

                .Call("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()")
                    .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                .Exit("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()")
            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassInheritsPropertiesDefault()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void InternalAndPrivateClassHelper_CallPrivateClassInheritsPropertiesOverRideCtor() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallPrivateClassInheritsPropertiesOverRideCtor();
            //CallPrivateClassInheritsPropertiesOverRideCtor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassInheritsPropertiesOverRideCtor()")
                .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRidePropertiesOverRideCtor()")
                    .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                            .Append(Calls_PrivateClassInheritedOverRideProperties())
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")
                .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRidePropertiesOverRideCtor()")

                .Call("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoProperty()")
                    .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                .Exit("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoProperty()")

                .Call("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()")
                    .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                .Exit("System.Int32 CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()")
            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassInheritsPropertiesOverRideCtor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void InternalAndPrivateClassHelper_CallPrivateClassOverridesMethodsDefault() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallPrivateClassOverridesMethodsDefault();
            //CallPrivateClassOverridesMethodsDefault
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassOverridesMethodsDefault()")
                .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverridesPublicMethods()")
                    .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::.ctor()")
                        .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                            .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())
                        .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                    .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::.ctor()")
                .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverridesPublicMethods()")

                .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")

                .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")

                .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::VoidMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::VoidMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallPrivateClassOverridesMethodsDefault()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void InternalAndPrivateClassHelper_CallInternalClassInheritsPropertiesDefault() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallInternalClassInheritsPropertiesDefault();
            //CallInternalClassInheritsPropertiesDefault
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassInheritsPropertiesDefault()")
                .Call("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                        .Append(Calls_ProtectedClassInheritedOverRideProperties_set_IntAutoProperty())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::.ctor()")

                .Append(Calls_ProtectedClassInheritedOverRideProperties_get_IntAutoProperty())

            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassInheritsPropertiesDefault()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void InternalAndPrivateClassHelper_CallInternalClassInheritsPropertiesOverRideCtor() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallInternalClassInheritsPropertiesOverRideCtor();
            //CallInternalClassInheritsPropertiesOverRideCtor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassInheritsPropertiesOverRideCtor()")
                .Call("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                        .Append(Calls_ProtectedClassInheritedOverRideProperties_set_IntAutoProperty())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")

                .Append(Calls_ProtectedClassInheritedOverRideProperties_get_IntAutoProperty())
                
            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassInheritsPropertiesOverRideCtor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void InternalAndPrivateClassHelper_CallInternalClassOverridesMethodsDefault() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            InternalAndPrivateClassHelper.CallInternalClassOverridesMethodsDefault();
            //CallInternalClassOverridesMethodsDefault
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassOverridesMethodsDefault()")
                .Call("System.Void CallGraphTestClasses.ProtectedClassInheritedOverridesPublicMethods::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                        .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.ProtectedClassInheritedOverridesPublicMethods::.ctor()")

                .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")
                .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()")

                .Call("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")
                .Exit("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()")

                .Call("System.Void CallGraphTestClasses.ProtectedClassInheritedOverridesPublicMethods::VoidMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ProtectedClassInheritedOverridesPublicMethods::VoidMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.InternalAndPrivateClassHelper::CallInternalClassOverridesMethodsDefault()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void PrivateClassWrapper_GetPrivateClassInheritedOverRideProperties() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            PrivateClassWrapper.GetPrivateClassInheritedOverRideProperties();
            //GetPrivateClassInheritedOverRideProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRideProperties()")
                .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                        .Append(Calls_PrivateClassInheritedOverRideProperties())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor()")
            .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRideProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void PrivateClassWrapper_GetPrivateClassInheritedOverRidePropertiesOverRideCtor() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            PrivateClassWrapper.GetPrivateClassInheritedOverRidePropertiesOverRideCtor();
            //GetPrivateClassInheritedOverRidePropertiesOverRideCtor
            var tester = new CallGraphTester(InstrumentationData)
            .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRidePropertiesOverRideCtor()")
                .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                        .Append(Calls_PrivateClassInheritedOverRideProperties())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor(System.Int32,System.Int32,System.Int32)")
                .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::.ctor(System.Int32,System.Int32,System.Int32)")
            .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverRidePropertiesOverRideCtor()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void PrivateClassWrapper_GetPrivateClassInheritedOverridesPublicMethods() {

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            PrivateClassWrapper.GetPrivateClassInheritedOverridesPublicMethods();
            //GetPrivateClassInheritedOverridesPublicMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverridesPublicMethods()")
                .Call("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::.ctor()")
                    .Call("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                        .Append(NormalPublicClassTests.Calls_NormalPublicClass_set_IntAutoProperty())
                    .Exit("System.Void CallGraphTestClasses.NormalPublicClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverridesPublicMethods::.ctor()")
            .Exit("CallGraphTestClasses.NormalPublicClass CallGraphTestClasses.PrivateClassWrapper::GetPrivateClassInheritedOverridesPublicMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        private IEnumerable<CallGraphMethod> Calls_PrivateClassInheritedOverRideProperties() {
            var retVal = new List<CallGraphMethod>() {
		        CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoProperty(System.Int32)"),
		            CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
		            CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
		        CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoProperty(System.Int32)"),

		        CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoReadOnlyProperty(System.Int32)"),
		            CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
		            CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
		        CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoReadOnlyProperty(System.Int32)"),

		        CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoWriteOnlyProperty(System.Int32)"),
		            CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
		            CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
		        CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.PrivateClassWrapper/PrivateClassInheritedOverRideProperties::set_IntAutoWriteOnlyProperty(System.Int32)")
	        };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_ProtectedClassInheritedOverRideProperties_set_IntAutoProperty() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoProperty(System.Int32)"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoProperty(System.Int32)"),
                        
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoReadOnlyProperty(System.Int32)"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoReadOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoReadOnlyProperty(System.Int32)"),
                        
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoWriteOnlyProperty(System.Int32)"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.NormalPublicClass::set_IntAutoWriteOnlyProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::set_IntAutoWriteOnlyProperty(System.Int32)")	
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_ProtectedClassInheritedOverRideProperties_get_IntAutoProperty() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::get_IntAutoProperty()"),
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoProperty()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::get_IntAutoProperty()"),
                
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()"),
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.NormalPublicClass::get_IntAutoReadOnlyProperty()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.ProtectedClassInheritedOverRideProperties::get_IntAutoReadOnlyProperty()")
            };

            return retVal;
        }



        #region codegen
        //[TestMethod]
        //public void Main() {
        //    MainCallGraphTestClasses_InternalAndPrivateClassHelper();
        //    MainCallGraphTestClasses_PrivateClassWrapper();
        //} //main

        //public void MainCallGraphTestClasses_InternalAndPrivateClassHelper() {


        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallPrivateClassInheritsPropertiesDefault();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallPrivateClassInheritsPropertiesDefault() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallPrivateClassInheritsPropertiesDefault();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallPrivateClassInheritsPropertiesDefault"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallPrivateClassInheritsPropertiesOverRideCtor();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallPrivateClassInheritsPropertiesOverRideCtor() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallPrivateClassInheritsPropertiesOverRideCtor();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallPrivateClassInheritsPropertiesOverRideCtor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallPrivateClassOverridesMethodsDefault();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallPrivateClassOverridesMethodsDefault() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallPrivateClassOverridesMethodsDefault();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallPrivateClassOverridesMethodsDefault"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallInternalClassInheritsPropertiesDefault();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallInternalClassInheritsPropertiesDefault() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallInternalClassInheritsPropertiesDefault();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallInternalClassInheritsPropertiesDefault"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallInternalClassInheritsPropertiesOverRideCtor();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallInternalClassInheritsPropertiesOverRideCtor() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallInternalClassInheritsPropertiesOverRideCtor();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallInternalClassInheritsPropertiesOverRideCtor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    InternalAndPrivateClassHelper.CallInternalClassOverridesMethodsDefault();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void InternalAndPrivateClassHelper_CallInternalClassOverridesMethodsDefault() {");
        //    Debug.WriteLine("var instance = GetInstanceCallGraphTestClasses_InternalAndPrivateClassHelper();");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.CallInternalClassOverridesMethodsDefault();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("CallInternalClassOverridesMethodsDefault"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method

        //public void MainCallGraphTestClasses_PrivateClassWrapper() {

        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    PrivateClassWrapper.GetPrivateClassInheritedOverRideProperties();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void PrivateClassWrapper_GetPrivateClassInheritedOverRideProperties() {");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.GetPrivateClassInheritedOverRideProperties();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("GetPrivateClassInheritedOverRideProperties"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    PrivateClassWrapper.GetPrivateClassInheritedOverRidePropertiesOverRideCtor();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void PrivateClassWrapper_GetPrivateClassInheritedOverRidePropertiesOverRideCtor() {");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.GetPrivateClassInheritedOverRidePropertiesOverRideCtor();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("GetPrivateClassInheritedOverRidePropertiesOverRideCtor"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");



        //    IntegrationTestIClientMessageService.Instance.ClearCalls();
        //    PrivateClassWrapper.GetPrivateClassInheritedOverridesPublicMethods();
        //    Debug.WriteLine("");
        //    Debug.WriteLine("[TestMethod]");
        //    Debug.WriteLine("public void PrivateClassWrapper_GetPrivateClassInheritedOverridesPublicMethods() {");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("");
        //    Debug.WriteLine("//clear after creating.");
        //    Debug.WriteLine("IntegrationTestIClientMessageService.Instance.ClearCalls();");
        //    Debug.WriteLine("instance.GetPrivateClassInheritedOverridesPublicMethods();");
        //    Debug.WriteLine(IntegrationTestIClientMessageService.ToTraceStatements("GetPrivateClassInheritedOverridesPublicMethods"));

        //    Debug.WriteLine("IntegrationTestIClientMessageService.ValidateCallGraph(tester);");
        //    Debug.WriteLine("}");
        //    Debug.WriteLine("");

        //} //method
        #endregion codegen



    } //class
} //ns
