using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;
using NetPerf.Common;
using System.Reflection;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    /// <summary>
    /// Summary description for VerifyInstrumentationData
    /// </summary>
    [CLSCompliant(false)]
    [TestClass]
    public class VerifyInstrumentationData : CallGraphTestBaseClass {

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext {
            get {
                return testContextInstance;
            }
            set {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [ClassInitialize()]
        public static new void LoadInstrumentationData(TestContext testContext) {
            InitializeTestClassBase();
        }

        //TODO find a way to test the file names of the assembly.

        [TestMethod]
        public void CallGraphTestClasses_VerifyTypesExist_SourceIsMetadata() {
            var failedList = new List<ITypeRecord>();
            var instrumentedTypes = GetInstrumentedTypes();
            var assemblyTypes = GetAssemblyTypes();
            foreach (var type in instrumentedTypes) {
                //obtain the type from the assembly
                var actualSignature = type.Signature.Replace("/", "+");
                var assemblyType = assemblyTypes.FirstOrDefault(item => item.FullName == actualSignature);
                if (assemblyType == null) {
                    failedList.Add(type);
                }
            }
            AssertFailedList<ITypeRecord>(failedList, (item) => item.Signature);
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyTypesExist_SourceIsAssemblyReflection() {
            var failedList = new List<Type>();
            var instrumentedTypes = GetInstrumentedTypes();
            var assemblyTypes = GetAssemblyTypes().Where(item => !item.IsAbstract); //ensure non abstract types are counted.
            foreach (var type in assemblyTypes) {
                //obtain the type from the assembly
                var actualSignature = type.FullName.Replace("+", "/");
                var instrumentedType = instrumentedTypes.FirstOrDefault(item => item.Signature == actualSignature);
                if (instrumentedType == null) {
                    failedList.Add(type);
                }
            }
            AssertFailedList<Type>(failedList, (item) => item.FullName);
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyMethodsExist_SourceIsMetadata() {
            var failedList = new List<IMethodRecord>();
            var instrumentedTypes = GetInstrumentedTypes();
            var assemblyTypes = Assembly.GetTypes().ToList();
            foreach (var type in instrumentedTypes) {
                //obtain the type from the assembly
                var actualSignature = type.Signature.Replace("/", "+");
                var assemblyType = assemblyTypes.FirstOrDefault(item => item.FullName == actualSignature);
                if (assemblyType != null) {
                    var typeMethods = new List<MethodBase>();
                    typeMethods.AddRange(assemblyType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                    typeMethods.AddRange(assemblyType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
                    foreach (var method in type.Methods) {
                        var assemblyMethod = typeMethods.FirstOrDefault(item => item.AsNetPerfSignature() == method.Signature);
                        if (assemblyMethod == null) {
                            failedList.Add(method);
                        }
                    }
                }
            }
            AssertFailedList<IMethodRecord>(failedList, (item) => item.Signature);
        }

        [TestMethod]
        public void CallGraphTestClasses_VerifyMethodsExist_SourceIsAssemblyReflection() {
            var failedList = new List<MethodBase>();
            var instrumentedTypes = GetInstrumentedTypes();
            var assemblyTypes = GetAssemblyTypes();
            foreach (var type in assemblyTypes) {
                //obtain the type from the assembly
                var actualSignature = type.FullName.Replace("+", "/");
                var instrumentedType = instrumentedTypes.FirstOrDefault(item => item.Signature == actualSignature);
                if (instrumentedType != null) {
                    var typeMethods = new List<MethodBase>();
                    typeMethods.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                        .Where(item => !item.IsAbstract && !item.IsCompilerGenerated()));
                    typeMethods.AddRange(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                                        .Where(item => !item.IsAbstract && !item.IsCompilerGenerated()));

                    foreach (var method in typeMethods) {
                        var instrumentedRecord = instrumentedType.Methods.FirstOrDefault(item => method.AsNetPerfSignature() == item.Signature);
                        if (instrumentedRecord == null) {
                            failedList.Add(method);
                        }
                    }
                }
            }
            AssertFailedList<MethodBase>(failedList, (item) => item.AsNetPerfSignature());
        }
    }
}
