using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using NetPerf.Common.Instrumentation;
using NetPerf.Client;
using NetPerf.Core;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    [TestClass]
    public class VerifyMultipleAssemblies {

        protected const string ASSEMBLY_DLL_NAME_NORMAL = "CallGraphTestClasses.dll";

        protected const string ASSEMBLY_DLL_NAME_DUPLICATES = "CallGraphTestClassesDuplicates.dll";

        protected static Assembly CallGraphTestClasses {
            get;
            private set;
        }

        protected static Assembly CallGraphTestClassesDuplicates {
            get;
            private set;
        }

        public static Dictionary<Assembly, IInstrumentationData> InstrumentationData {
            get;
            private set;
        }

        static VerifyMultipleAssemblies() {
            if (CallGraphTestClasses == null) {
                CallGraphTestClasses = System.Reflection.Assembly.Load("CallGraphTestClasses");
            }
            if (CallGraphTestClassesDuplicates == null) {
                CallGraphTestClassesDuplicates = System.Reflection.Assembly.Load("CallGraphTestClassesDuplicates");
            }

            InstrumentationData = new Dictionary<Assembly, IInstrumentationData>();

            InstrumentationData[CallGraphTestClasses] = CallGraphTestClasses.InstrumentationData();
            InstrumentationData[CallGraphTestClassesDuplicates] = CallGraphTestClassesDuplicates.InstrumentationData();
        }

        [ClassInitialize()]
        public static void MyClassInitialize(TestContext testContext) {
            IntegrationTestIClientMessageService.RegisterWithIOC();
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyBothAssembliesLoaded() {
            Assert.IsNotNull(CallGraphTestClasses, "CallGraphTestClasses did not load");
            Assert.IsNotNull(CallGraphTestClassesDuplicates, "CallGraphTestClassesDuplicates did not load");
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyModulesExistNormal() {
            var ilModule = GetTestModule(CallGraphTestClasses);
            var assemModule = CallGraphTestClasses.GetLoadedModules().First(item => item.Name.Equals(ASSEMBLY_DLL_NAME_NORMAL));
            Assert.AreEqual(assemModule.Name, ilModule.Signature, "Signature did not match");
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyModulesExistDuplicates() {
            var ilModule = GetTestModule(CallGraphTestClassesDuplicates);
            var assemModule = CallGraphTestClassesDuplicates.GetLoadedModules().FirstOrDefault(item => item.Name.Equals(ASSEMBLY_DLL_NAME_DUPLICATES));
            Assert.AreEqual(assemModule.Name, ilModule.Signature, "Signature did not match");
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyMethodCallsNormal() {
            ValidateCallsForTypeCtor(CallGraphTestClasses);
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyMethodCallsDuplicates() {
            ValidateCallsForTypeCtor(CallGraphTestClassesDuplicates);
        }

        [TestMethod]
        public void VerifyMultipleAssemblies_VerifyMethodCallsBoth() {
            //clear
            IntegrationTestIClientMessageService.Instance.ClearCalls();

            //set up a location to place all of the calls made in the order they exist.
            var methodCalls = new List<TimingMethod>();
            var pointCalls = new List<TimingPoint>();

            //for each item, call the items and place in master collection.

            var instance = CreateNormalPublicClass(CallGraphTestClasses);
            instance = CreateNormalPublicClass(CallGraphTestClassesDuplicates);

            methodCalls.AddRange(IntegrationTestIClientMessageService.Instance.MethodEvents);
            pointCalls.AddRange(IntegrationTestIClientMessageService.Instance.PointEvents);

            //verify the first half of the calls
            VerifyCallAndPointForCtor(CallGraphTestClasses, methodCalls.Take(methodCalls.Count / 2), pointCalls.Take(pointCalls.Count / 2));

            //verify the second half of the calls
            VerifyCallAndPointForCtor(CallGraphTestClassesDuplicates, methodCalls.Skip(methodCalls.Count / 2), pointCalls.Skip(pointCalls.Count / 2));
        }

        private void ValidateCallsForTypeCtor(Assembly assembly) {
            IntegrationTestIClientMessageService.Instance.ClearCalls();
            var instance = CreateNormalPublicClass(assembly);
            VerifyCallAndPointForCtor(assembly, IntegrationTestIClientMessageService.Instance.MethodEvents, IntegrationTestIClientMessageService.Instance.PointEvents);
        }

        private void VerifyCallAndPointForCtor(Assembly assembly, IEnumerable<TimingMethod> methods, IEnumerable<TimingPoint> points) {
            var classType = GetNormalPublicClassType(assembly);
            var ctor = classType.GetConstructor(new Type[] { }); //get the default ctor
            var typeRecord = GetTypeRecord(classType);
            Assert.IsNotNull(typeRecord, "Type record was not found");
            var methodRecord = typeRecord.Methods.FirstOrDefault(item => item.Signature == ctor.AsNetPerfSignature());
            Assert.IsNotNull(methodRecord, "ctor record was not found" + ctor.AsNetPerfSignature());
            //Validate the methodRecords id is the first and last call.

            Assert.AreEqual<int>(methodRecord.Id, methods.First().MethodId);
            Assert.AreEqual<int>(methodRecord.Id, methods.Last().MethodId);

            //now verify all of the points were hit
            Assert.AreEqual<int>(methodRecord.Points.Count, points.Count());

            for (int i = 0; i < methodRecord.Points.Count; i++) {
                Assert.AreEqual<int>(methodRecord.Points[i].Id, points.Skip(i).First().PointId);
            }
        }

        private static Type GetNormalPublicClassType(Assembly assembly) {
            return assembly.GetType("CallGraphTestClasses.NormalPublicClass");
        }

        private static object CreateNormalPublicClass(Assembly assembly) {
            var testType = GetNormalPublicClassType(assembly);
            Assert.IsNotNull(testType, "Test Type Could Not be found");
            return Activator.CreateInstance(testType);
        }

        protected IModuleRecord GetTestModule(Assembly assembly) {
            var modName = assembly.ManifestModule.Name;
            var retVal = GetTestAssembly(assembly).Modules.FirstOrDefault(item =>
                item.Signature.Equals(modName, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(retVal, "Test Module not found in instrumentation data");
            return retVal;
        }

        protected IAssemblyRecord GetTestAssembly(Assembly assembly) {
            IInstrumentationData query = null;
            if (InstrumentationData.TryGetValue(assembly, out query)) {
                var retVal = query
                    .Assemblies
                    .FirstOrDefault(item => item.Signature.Equals(assembly.FullName));
                Assert.IsNotNull(retVal, "Test Assembly not found in instrumentation data");
                return retVal;
            }
            return null;
        }

        protected ITypeRecord GetTypeRecord(Type type) {
            var assembly = GetTestAssembly(type.Assembly);
            if (assembly == null) {
                return null;
            }

            var actualSignature = type.FullName.Replace("+", "/");

            ITypeRecord retVal = (from m in assembly.Modules
                                  from t in m.Types
                                  where t.Signature == actualSignature
                                  select t).FirstOrDefault();

            return retVal;
        }

    }
}
