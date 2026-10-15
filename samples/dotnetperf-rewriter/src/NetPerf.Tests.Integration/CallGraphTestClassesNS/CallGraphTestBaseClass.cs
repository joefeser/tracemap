using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NetPerf.Common.Instrumentation;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    /// <summary>
    /// base class for some test items.
    /// </summary>
    [CLSCompliant(false)]
    [TestClass]
    public class CallGraphTestBaseClass {

        protected static IInstrumentationData InstrumentationData {
            get;
            set;
        }

        protected const string ASSEMBLY_DLL_NAME = "CallGraphTestClasses.dll";

        protected static Assembly Assembly {
            get;
            set;
        }

        [ClassInitialize()]
        public static void LoadInstrumentationData(TestContext testContext) {
            InitializeTestClassBase();
        }

        protected static void InitializeTestClassBase() {
            if (Assembly == null) {
                Assembly = System.Reflection.Assembly.Load("CallGraphTestClasses");
            }
            if (InstrumentationData == null) {
                InstrumentationData = Assembly.InstrumentationData();
            }
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyModuleIdIsSet() {
            var value = Assembly.ModuleIdentifier();
            Assert.IsTrue(value != 0);
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyInstrumentationDataExist() {
            Assert.IsNotNull(InstrumentationData, "Instrumentation data did not load");
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyTestAssembliesExist() {
            Assert.IsNotNull(Assembly, "Test Assembly did not load");
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyInstrumentationAssemblyDataExist() {
            Assert.IsNotNull(GetTestAssembly(), "Test Assembly not found in instrumentation data");
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyModulesExist() {
            var ilModule = GetTestModule();
            var assemModule = Assembly.GetLoadedModules().First(item => item.Name.Equals(ASSEMBLY_DLL_NAME));
            Assert.AreEqual(assemModule.Name, ilModule.Signature, "Signature did not match");
        }


        protected static void AssertFailedList<T>(List<T> failedList, Func<T, string> selector) {
            if (failedList.Count > 0) {
                var sb = new StringBuilder();
                sb.Append("Signatures were not found:");
                foreach (var item in failedList) {
                    sb.Append(selector(item));
                    sb.AppendLine(";");
                }
                Assert.Fail(sb.ToString());
            }
        }

        protected static List<ITypeRecord> __instrumentedTypes = null;

        protected List<ITypeRecord> GetInstrumentedTypes() {
            if (__instrumentedTypes == null) {
                __instrumentedTypes = GetTestModule().Types.Where(item => item.Methods.Any()).ToList();
            }
            return __instrumentedTypes;
        }

        protected static List<Type> __assemblyTypes = null;

        protected List<Type> GetAssemblyTypes() {
            if (__assemblyTypes == null) {
                __assemblyTypes = Assembly.GetTypes().Where(item => item.IsClass).ToList(); //ignore interfaces;
            }
            return __assemblyTypes;
        }

        protected IModuleRecord GetTestModule() {
            var retVal = GetTestAssembly().Modules.First(item => item.Signature.Equals(ASSEMBLY_DLL_NAME, StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(retVal, "Test Module not found in instrumentation data");
            return retVal;
        }

        protected IAssemblyRecord GetTestAssembly() {
            var retVal = InstrumentationData.Assemblies.First(item => item.Signature.StartsWith("CallGraphTestClasses", StringComparison.OrdinalIgnoreCase));
            Assert.IsNotNull(retVal, "Test Assembly not found in instrumentation data");
            return retVal;
        }

    }

}
