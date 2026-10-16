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
    public class InterfaceTestClassesTests : CallGraphValidateBaseClass {

        [TestMethod]
        public void ConcreteSimplePropertyInterfaceAuto_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAuto();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAuto::CallProperties()")
                .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAuto::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterfaceAutoOverRide_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAutoOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::CallProperties()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterfaceAutoOverRideCallBase_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAutoOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::CallProperties()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::set_IntProperty(System.Int32)")
                    .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_IntProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::set_StringProperty(System.String)")
                    .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_StringProperty(System.String)")
                    .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::get_IntProperty()")
                    .Call("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_IntProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::get_StringProperty()")
                    .Call("System.String CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_StringProperty()")
                    .Exit("System.String CallGraphTestClasses.AbstractSimplePropertyInterfaceAuto::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterface_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterface::CallProperties()")
                .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterface::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterface::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.AbstractSimplePropertyInterface::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.AbstractSimplePropertyInterface::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterface::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterfaceOverRide_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::CallProperties()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterfaceOverRideCallBase_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::CallProperties()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::set_IntProperty(System.Int32)")
                    .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_IntProperty(System.Int32)")
                    .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::set_StringProperty(System.String)")
                    .Call("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_StringProperty(System.String)")
                    .Exit("System.Void CallGraphTestClasses.AbstractSimplePropertyInterface::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::get_IntProperty()")
                    .Call("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterface::get_IntProperty()")
                    .Exit("System.Int32 CallGraphTestClasses.AbstractSimplePropertyInterface::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::get_StringProperty()")
                    .Call("System.String CallGraphTestClasses.AbstractSimplePropertyInterface::get_StringProperty()")
                    .Exit("System.String CallGraphTestClasses.AbstractSimplePropertyInterface::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimplePropertyInterfaceAbstract_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::CallProperties()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void ConcreteSimpleMethodInterface_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterface::CallMethods()")
                .Append(Calls_AbstractSimpleMethodInterface_VoidMethod())

                .Append(Calls_AbstractSimpleMethodInterface_IntMethod())

                .Call("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterface::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRide_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRide_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRide_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRide_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRideCallBase_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::VoidMethod()")
                    .Append(Calls_AbstractSimpleMethodInterface_VoidMethod())
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::IntMethod()")
                    .Append(Calls_AbstractSimpleMethodInterface_IntMethod())
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
                    .Call("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
                    .Exit("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRideCallBase_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::IntMethod()")
                .Append(Calls_AbstractSimpleMethodInterface_IntMethod())
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRideCallBase_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
                .Call("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.AbstractSimpleMethodInterface::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceOverRideCallBase_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::VoidMethod()")
                .Append(Calls_AbstractSimpleMethodInterface_VoidMethod())
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceAbstract_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceAbstract_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceAbstract_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodInterfaceAbstract_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterface_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterface::CallMethods()")
                .Append(Calls_AbstractSimpleCombinedInterface_VoidMethod())

                .Append(Calls_AbstractSimpleCombinedInterface_IntMethod())

                .Call("System.String CallGraphTestClasses.AbstractSimpleCombinedInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.AbstractSimpleCombinedInterface::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterface::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverride_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverride();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
                //WATCH CASE
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverride_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverride();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverride_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverride();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverride_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverride();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverrideCallBase_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverrideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::CallMethods()")

                .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_VoidMethod())

                .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_IntMethod())

                .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_StringMethodOneParam())

            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverrideCallBase_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverrideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_IntMethod());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverrideCallBase_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverrideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_StringMethodOneParam());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverrideCallBase_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverrideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_VoidMethod());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverRide_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
                //WATCH CASE
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::CallMethods()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidMethod()")
                        .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidPrivateMethodTwoLines()")
                        .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidMethod()")

                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntMethod()")
                        .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntPrivateMethodTwoLines()")
                        .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntMethod()")

                    .Call("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::StringMethodOneParam(System.String)")
                    .Exit("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::StringMethodOneParam(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverRide_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverRide_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleCombinedInterfaceOverRide_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterface_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterface::CallMethods()")
                    .Append(Calls_AbstractSimpleInheritedInterface_VoidMethod())

                    .Append(Calls_AbstractSimpleInheritedInterface_IntMethod())

                    .Call("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
                    .Exit("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterface::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRide_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRide_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRide_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRide_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRide();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::VoidMethod()")
                    .Append(Calls_AbstractSimpleInheritedInterface_VoidMethod())
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::IntMethod()")
                    .Append(Calls_AbstractSimpleInheritedInterface_IntMethod())
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
                    .Call("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
                    .Exit("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::IntMethod()")
                .Append(Calls_AbstractSimpleInheritedInterface_IntMethod())
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::StringMethodOneParam(System.String)")
                .Call("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.AbstractSimpleInheritedInterface::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::VoidMethod()")
                .Append(Calls_AbstractSimpleInheritedInterface_VoidMethod())
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceAbstract_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceAbstract_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceAbstract_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleInheritedInterfaceAbstract_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceAbstract();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void SameInterfaceOnBaseAndInhertedClassInherited_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_SameInterfaceOnBaseAndInhertedClassInherited();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassInherited::CallProperties()")
                .Call("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::set_IntProperty(System.Int32)")

                .Call("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::set_StringProperty(System.String)")

                .Call("System.Int32 CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::get_IntProperty()")

                .Call("System.String CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassBase::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassInherited::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodPropertyInterface_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodPropertyInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::CallMethods()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidMethod()")
                    .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidPrivateMethodTwoLines()")
                    .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidMethod()")

                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntMethod()")
                    .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntPrivateMethodTwoLines()")
                    .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntMethod()")

                .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::StringMethodOneParam(System.String)")
                .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::StringMethodOneParam(System.String)")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::CallMethods()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodPropertyInterface_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodPropertyInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodPropertyInterface_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodPropertyInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ConcreteSimpleMethodPropertyInterface_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_ConcreteSimpleMethodPropertyInterface();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceAuto GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAuto() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceAuto();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAutoOverRide() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRide();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAutoOverRideCallBase() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceAutoOverRideCallBase();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterface GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterface() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterface();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceOverRide() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRide();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceOverRideCallBase() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceOverRideCallBase();
        }


        protected CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract GetInstanceCallGraphTestClasses_ConcreteSimplePropertyInterfaceAbstract() {
            return new CallGraphTestClasses.ConcreteSimplePropertyInterfaceAbstract();
        }


        protected CallGraphTestClasses.ConcreteSimpleMethodInterface GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterface() {
            return new CallGraphTestClasses.ConcreteSimpleMethodInterface();
        }


        protected CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRide() {
            return new CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRide();
        }


        protected CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceOverRideCallBase() {
            return new CallGraphTestClasses.ConcreteSimpleMethodInterfaceOverRideCallBase();
        }


        protected CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract GetInstanceCallGraphTestClasses_ConcreteSimpleMethodInterfaceAbstract() {
            return new CallGraphTestClasses.ConcreteSimpleMethodInterfaceAbstract();
        }


        protected CallGraphTestClasses.ConcreteSimpleCombinedInterface GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterface() {
            return new CallGraphTestClasses.ConcreteSimpleCombinedInterface();
        }


        protected CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverride() {
            return new CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverride();
        }


        protected CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverrideCallBase() {
            return new CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase();
        }


        protected CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide GetInstanceCallGraphTestClasses_ConcreteSimpleCombinedInterfaceOverRide() {
            return new CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverRide();
        }


        protected CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterface GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterface() {
            return new CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterface();
        }


        protected CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRide() {
            return new CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRide();
        }


        protected CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase() {
            return new CallGraphTestClasses.ConcreteSimpleInheritedInterfaceInterfaceOverRideCallBase();
        }


        protected CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract GetInstanceCallGraphTestClasses_ConcreteSimpleInheritedInterfaceAbstract() {
            return new CallGraphTestClasses.ConcreteSimpleInheritedInterfaceAbstract();
        }


        protected CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassInherited GetInstanceCallGraphTestClasses_SameInterfaceOnBaseAndInhertedClassInherited() {
            return new CallGraphTestClasses.SameInterfaceOnBaseAndInhertedClassInherited();
        }


        protected CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface GetInstanceCallGraphTestClasses_ConcreteSimpleMethodPropertyInterface() {
            return new CallGraphTestClasses.ConcreteSimpleMethodPropertyInterface();
        }

        private IEnumerable<CallGraphMethod> Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_IntMethod() {
            var retVal = new List<CallGraphMethod>();
            retVal.Add(CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::IntMethod()"));
            retVal.AddRange(Calls_AbstractSimpleCombinedInterface_IntMethod());
            retVal.Add(CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::IntMethod()"));

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleCombinedInterface_IntMethod() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleCombinedInterface::IntMethod()"),
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleCombinedInterface::IntPrivateMethodTwoLines()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleCombinedInterface::IntPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleCombinedInterface::IntMethod()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_StringMethodOneParam() {
            var retVal = new List<CallGraphMethod>() {
		        CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::StringMethodOneParam(System.String)"),
                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.AbstractSimpleCombinedInterface::StringMethodOneParam(System.String)"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.AbstractSimpleCombinedInterface::StringMethodOneParam(System.String)"),
                CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::StringMethodOneParam(System.String)"),
            };

            return retVal;
        }


        private IEnumerable<CallGraphMethod> Calls_ConcreteSimpleCombinedInterfaceOverrideCallBase_VoidMethod() {
            var retVal = new List<CallGraphMethod>();
            retVal.Add(CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::VoidMethod()"));
            retVal.AddRange(Calls_AbstractSimpleCombinedInterface_VoidMethod());
            retVal.Add(CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.ConcreteSimpleCombinedInterfaceOverrideCallBase::VoidMethod()"));

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleCombinedInterface_VoidMethod() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleCombinedInterface::VoidMethod()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleCombinedInterface::VoidPrivateMethodTwoLines()"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleCombinedInterface::VoidPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleCombinedInterface::VoidMethod()")
             };

            return retVal;
        }


        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleMethodInterface_VoidMethod() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleMethodInterface::VoidMethod()"),
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleMethodInterface::VoidPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleMethodInterface::VoidPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleMethodInterface::VoidMethod()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleMethodInterface_IntMethod() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleMethodInterface::IntMethod()"),
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleMethodInterface::IntPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleMethodInterface::IntPrivateMethodTwoLines()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleMethodInterface::IntMethod()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleInheritedInterface_VoidMethod() {
            var retVal = new List<CallGraphMethod>() {
		        CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleInheritedInterface::VoidMethod()"),
		            CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.AbstractSimpleInheritedInterface::VoidPrivateMethodTwoLines()"),
		            CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleInheritedInterface::VoidPrivateMethodTwoLines()"),
		        CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.AbstractSimpleInheritedInterface::VoidMethod()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_AbstractSimpleInheritedInterface_IntMethod() {
            var retVal = new List<CallGraphMethod>() {
		        CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleInheritedInterface::IntMethod()"),
		            CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.AbstractSimpleInheritedInterface::IntPrivateMethodTwoLines()"),
		            CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleInheritedInterface::IntPrivateMethodTwoLines()"),
		        CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.AbstractSimpleInheritedInterface::IntMethod()")
            };

            return retVal;
        }

    } //class
} //ns
