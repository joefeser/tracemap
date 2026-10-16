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
    public class ChildClassesTests : CallGraphValidateBaseClass {

        [TestMethod]
        public void TopLevelClassWithChildrenBase_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_TopLevelClassWithChildrenBase_CallProperties());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void TopLevelClassWithChildrenBase_CallMethods() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallMethods();
            //CallMethods
            var tester = new CallGraphTester(InstrumentationData)

            .Append(Calls_TopLevelClassWithChildrenBase_CallMethods());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void TopLevelClassWithChildrenBase_VoidMethod() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.VoidMethod();
            //VoidMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidMethod()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidPrivateMethodTwoLines()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidPrivateMethodTwoLines()")
            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void TopLevelClassWithChildrenBase_IntMethod() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.IntMethod();
            //IntMethod
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntMethod()")
                .Call("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntPrivateMethodTwoLines()")
                .Exit("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntPrivateMethodTwoLines()")
            .Exit("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntMethod()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void TopLevelClassWithChildrenBase_StringMethodOneParam() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.StringMethodOneParam("Test");
            //StringMethodOneParam
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::StringMethodOneParam(System.String)")
            .Exit("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::StringMethodOneParam(System.String)");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RegularChildClass_CreateParentClass() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_RegularChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/RegularChildClass::CreateParentClass()")
                .Append(Calls_TopLevelClassWithChildren_ctor())

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

                .Append(Calls_TopLevelClassWithChildrenBase_CallMethods())

                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/RegularChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void SecondChildClass_CreateParentClass() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::CreateParentClass()")
                .Append(Calls_TopLevelClassWithChildren_ctor())

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

                .Append(Calls_TopLevelClassWithChildrenBase_CallMethods())

                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ThirdChildClass_CreateParentClass() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass_ThirdChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass/ThirdChildClass::CreateParentClass()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::.ctor()")
                    .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()")
                    .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

                .Append(Calls_TopLevelClassWithChildrenBase_CallMethods())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass/ThirdChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ForthChildClass_CreateParentClass() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass_ThirdChildClass_ForthChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass/ThirdChildClass/ForthChildClass::CreateParentClass()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::.ctor()")
                    .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()")
                    .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

                .Append(Calls_TopLevelClassWithChildrenBase_CallMethods())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildren/SecondChildClass/ThirdChildClass/ForthChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void TopLevelClassWithChildrenNoBase_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)

            .Append(Calls_TopLevelClassWithChildrenNoBase_CallProperties());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void TopLevelClassWithChildrenNoBase_CallRegularChildClassProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallRegularChildClassProperties();
            //CallRegularChildClassProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::CallRegularChildClassProperties()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::.ctor()")

                .Append(Calls_RegularChildClass_CallProperties())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::CallRegularChildClassProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void RegularChildClass_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_RegularChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_RegularChildClass_CallProperties());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void RegularChildClass_CreateParentClass2() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_RegularChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::CreateParentClass()")
                .Append(Calls_TopLevelClassWithChildren_ctor())

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void SecondChildClass_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)

            .Append(Calls_TopLevelClassWithChildrenNoBase_SecondChildClass_CallProperties());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }

        [TestMethod]
        public void SecondChildClass_CreateParentClass2() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CreateParentClass()")
                .Append(Calls_TopLevelClassWithChildren_ctor())

                .Append(Calls_TopLevelClassWithChildrenBase_CallProperties())

                .Append(Calls_TopLevelClassWithChildrenBase_CallMethods())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void SecondChildClass_CreateParentAndChild() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentAndChild();
            //CreateParentAndChild
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CreateParentAndChild()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenNoBase_CallProperties())

                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenNoBase_ThirdChildClass_CallProperties())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CreateParentAndChild()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ThirdChildClass_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Append(Calls_TopLevelClassWithChildrenNoBase_ThirdChildClass_CallProperties());

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ThirdChildClass_CreateParentClass2() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::CreateParentClass()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenNoBase_SecondChildClass_CallProperties())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ForthChildClass_CallProperties() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass_ForthChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CallProperties();
            //CallProperties
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::CallProperties()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::set_IntProperty(System.Int32)")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::set_IntProperty(System.Int32)")
                
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::set_StringProperty(System.String)")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::set_StringProperty(System.String)")
                
                .Call("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::get_IntProperty()")
                .Exit("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::get_IntProperty()")
                
                .Call("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::get_StringProperty()")
                .Exit("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::get_StringProperty()")
            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::CallProperties()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        [TestMethod]
        public void ForthChildClass_CreateParentClass2() {
            var instance = GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass_ForthChildClass();

            //clear after creating.
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            instance.CreateParentClass();
            //CreateParentClass
            var tester = new CallGraphTester(InstrumentationData)
            .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::CreateParentClass()")
                .Call("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::.ctor()")
                .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::.ctor()")

                .Append(Calls_TopLevelClassWithChildrenNoBase_SecondChildClass_CallProperties())

            .Exit("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass/ForthChildClass::CreateParentClass()");

            IntegrationTestIClientMessageService.ValidateCallGraph(tester);
        }


        protected CallGraphTestClasses.TopLevelClassWithChildrenBase GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenBase() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenBase();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildren GetInstanceCallGraphTestClasses_TopLevelClassWithChildren() {
            return new CallGraphTestClasses.TopLevelClassWithChildren();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildren.RegularChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_RegularChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildren.RegularChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass.ThirdChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass_ThirdChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass.ThirdChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass.ThirdChildClass.ForthChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildren_SecondChildClass_ThirdChildClass_ForthChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildren.SecondChildClass.ThirdChildClass.ForthChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildrenNoBase GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenNoBase();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildrenNoBase.RegularChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_RegularChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenNoBase.RegularChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass.ThirdChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass.ThirdChildClass();
        }

        protected CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass.ThirdChildClass.ForthChildClass GetInstanceCallGraphTestClasses_TopLevelClassWithChildrenNoBase_SecondChildClass_ThirdChildClass_ForthChildClass() {
            return new CallGraphTestClasses.TopLevelClassWithChildrenNoBase.SecondChildClass.ThirdChildClass.ForthChildClass();
        }

        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildrenBase_CallProperties() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::CallProperties()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::set_IntProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::set_IntProperty(System.Int32)"),

                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::set_StringProperty(System.String)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::set_StringProperty(System.String)"),

                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::get_IntProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::get_IntProperty()"),

                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::get_StringProperty()"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::get_StringProperty()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::CallProperties()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildrenBase_CallMethods() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::CallMethods()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidMethod()"),
                        CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidPrivateMethodTwoLines()"),
                        CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidPrivateMethodTwoLines()"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::VoidMethod()"),

                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntMethod()"),
                        CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntPrivateMethodTwoLines()"),
                        CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntPrivateMethodTwoLines()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenBase::IntMethod()"),

                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::StringMethodOneParam(System.String)"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenBase::StringMethodOneParam(System.String)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::CallMethods()")
            };

            return retVal;
        }


        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildrenNoBase_CallProperties() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::CallProperties()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::set_IntProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::set_IntProperty(System.Int32)"),

                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::set_StringProperty(System.String)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::set_StringProperty(System.String)"),
                
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase::get_IntProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase::get_IntProperty()"),
                
                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase::get_StringProperty()"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase::get_StringProperty()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase::CallProperties()")
            };

            return retVal;
        }


        private IEnumerable<CallGraphMethod> Calls_RegularChildClass_CallProperties() {
            var retVal = new List<CallGraphMethod>() {
               CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::CallProperties()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::set_IntProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::set_IntProperty(System.Int32)"),
                    
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::set_StringProperty(System.String)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::set_StringProperty(System.String)"),
                    
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::get_IntProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::get_IntProperty()"),
                    
                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::get_StringProperty()"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::get_StringProperty()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/RegularChildClass::CallProperties()")
            };

            return retVal;
        }


        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildrenNoBase_SecondChildClass_CallProperties() {
            var retVal = new List<CallGraphMethod>() {
               CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CallProperties()"),
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::set_IntProperty(System.Int32)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::set_IntProperty(System.Int32)"),

                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::set_StringProperty(System.String)"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::set_StringProperty(System.String)"),
                
                CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::get_IntProperty()"),
                CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::get_IntProperty()"),
                
                CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::get_StringProperty()"),
                CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::get_StringProperty()"),
            CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass::CallProperties()")
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildrenNoBase_ThirdChildClass_CallProperties() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::CallProperties()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::set_IntProperty(System.Int32)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::set_IntProperty(System.Int32)"),
                
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::set_StringProperty(System.String)"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::set_StringProperty(System.String)"),
                    
                    CallGraphMethod.EnterMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::get_IntProperty()"),
                    CallGraphMethod.ExitMethod("System.Int32 CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::get_IntProperty()"),
                    
                    CallGraphMethod.EnterMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::get_StringProperty()"),
                    CallGraphMethod.ExitMethod("System.String CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::get_StringProperty()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenNoBase/SecondChildClass/ThirdChildClass::CallProperties()"),
            };

            return retVal;
        }

        private IEnumerable<CallGraphMethod> Calls_TopLevelClassWithChildren_ctor() {
            var retVal = new List<CallGraphMethod>() {
                CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildren::.ctor()"),
                    CallGraphMethod.EnterMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()"),
                    CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildrenBase::.ctor()"),
                CallGraphMethod.ExitMethod("System.Void CallGraphTestClasses.TopLevelClassWithChildren::.ctor()")
             };

            return retVal;
        }
    
    }
}
