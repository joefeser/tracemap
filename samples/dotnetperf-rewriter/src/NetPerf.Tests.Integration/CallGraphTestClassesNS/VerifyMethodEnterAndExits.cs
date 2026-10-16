using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NetPerf.Common.Instrumentation;
using System.Reflection;
using NetPerf.Common;
using System.Runtime.CompilerServices;

namespace NetPerf.Tests.Integration.CallGraphTestClassesNS {

    /// <summary>
    /// Summary description for VerifyMethodEnterAndExits
    /// </summary>
    [CLSCompliant(false)]
    [TestClass]
    public class VerifyMethodEnterAndExits : CallGraphTestBaseClass {

        private Dictionary<string, TypeHolder> _typeSignatures = null;

        private class TypeHolder {

            public ITypeRecord Type {
                get;
                set;
            }

            public IMethodRecord Method {
                get;
                set;
            }

            public string TypeSignature {
                get {
                    return this.Type.Signature;
                }
            }

            private string _reflectionTypeSignature = null;

            public string ReflectionTypeSignature {
                get {
                    if (_reflectionTypeSignature == null) {
                        _reflectionTypeSignature = this.Type.Signature.Replace("/", "+");
                    }
                    return _reflectionTypeSignature;
                }
            }

            public string MethodSignature {
                get {
                    return this.Method.Signature;
                }
            }

        }

        private TypeHolder GetIMethodRecordForSignature(string signature) {

            if (_typeSignatures == null) {
                _typeSignatures = (from assemblies in InstrumentationData.Assemblies
                                   from modules in assemblies.Modules
                                   from types in modules.Types
                                   from method in types.Methods
                                   select new TypeHolder {
                                       Method = method,
                                       Type = types
                                   }).ToDictionary(item => item.MethodSignature);
            }

            TypeHolder retVal = null;

            if (_typeSignatures.TryGetValue(signature, out retVal)) {
                return retVal;
            }

            return null;
        }

        public VerifyMethodEnterAndExits() {
          
        }

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

        [TestInitialize()]
        public void MyTestInitialize() {
            IntegrationTestIClientMessageService.RegisterWithIOC();
        }

        /// <summary>
        /// This method will be called for every method in the file.
        /// </summary>
        [DeploymentItem("NetPerf.Tests.Integration\\TestAssemblies\\CallGraphTestClasses.xml"), DataSource("Microsoft.VisualStudio.TestTools.DataSource.XML", "|DataDirectory|\\CallGraphTestClasses.xml", "method", DataAccessMethod.Sequential),
        TestMethod]
        public void TestMethodEnterAndExit() {

            var signature = TestContext.DataRow["signature"].ToString();

            var metaData = GetIMethodRecordForSignature(signature);

            Assert.IsNotNull(metaData, string.Format("Mapping file did not contain the signature: {0}", signature));

            var executeType = GetAssemblyTypes().FirstOrDefault(item => item.FullName == metaData.ReflectionTypeSignature);

            Assert.IsNotNull(executeType, string.Format("No types exist in assembly for the signature: {0}", metaData.ReflectionTypeSignature));

            //now we want to find the method.

            var method = executeType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                         .FirstOrDefault(item => item.AsNetPerfSignature() == signature) as MethodBase;

            //see if the item is a ctor
            if (method == null) {
                method = executeType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                            .FirstOrDefault(item => item.AsNetPerfSignature() == signature) as MethodBase;
            }

            Assert.IsNotNull(method, string.Format("No method exist in assembly for the signature: {0}", metaData.MethodSignature));

            //we can't create an abstract class.
            if (executeType.IsAbstract) {
                return;
            }

            //see if the class is compiler generated and if it is, we are not going to create it for now.
            //TODO determine how we can test the IL for a compiler generated type

            var compilerGenerated = executeType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).FirstOrDefault();

            if (compilerGenerated != null) {
                return;
            }

            object instance = null;

            Type genericType = null;

            if (executeType.IsGenericType) {
                var ga = executeType.GetGenericArguments();
                var gaParams = new Type[ga.Length];

                for (int i = 0; i < gaParams.Length; i++) {
                    gaParams[i] = typeof(int);
                }

                genericType = executeType.MakeGenericType(gaParams);
                instance = Activator.CreateInstance(genericType);
            }
            else {
                instance = Activator.CreateInstance(executeType);
            }

            if (instance != null) {

                //we can't clear until after the ctor is called so we do not fail because the ctor executes.
                IntegrationTestIClientMessageService.Instance.ClearCalls();

                var parameterList = method.GetParameters();
                if (parameterList.Length > 0) {

                    if (method.IsGenericMethod) {
                        //TODO support generic methods of generic types
                        //http://msdn.microsoft.com/en-us/library/system.type.isgenerictype.aspx
                        return;
                        //Assert.Inconclusive("method.IsGenericMethod");
                    }
                    else if (method.IsGenericMethodDefinition) {
                        //TODO support generic methods of generic types
                        //http://msdn.microsoft.com/en-us/library/system.type.isgenerictype.aspx
                        return;
                        //Assert.Inconclusive("method.IsGenericMethodDefinition");
                    }
                    else {
                        var executeParms = new object[parameterList.Length];

                        for (int i = 0; i < parameterList.Length; i++) {
                            var paramType = parameterList[i].ParameterType;
                            executeParms[i] = ReflectionDefaultValues.DefaultValueForType(paramType);
                        }
                        //TODO support generic methods of generic types
                        //http://msdn.microsoft.com/en-us/library/system.type.isgenerictype.aspx
                        if (executeType.IsGenericType) {
                            return;
                        }

                        //call the method
                        method.Invoke(instance, executeParms);
                    }
                }
                else {
                    //call the method with no parameters
                    if (method.ContainsGenericParameters) {
                        //TODO support generic methods of generic types
                        //http://msdn.microsoft.com/en-us/library/system.type.isgenerictype.aspx
                        if (executeType.IsGenericType) {
                            return;
                        }
                        Assert.Inconclusive("method.IsGenericMethodDefinition");

                        //method.Invoke(instance, null);

                        //var ga = executeType.GetGenericArguments();
                        //var gaParams = new Type[ga.Length];

                        //for (int i = 0; i < gaParams.Length; i++) {
                        //    gaParams[i] = typeof(int);
                        //}

                        //genericType.InvokeMember(,,,,,,,)

                        //var genericMethod = (method as MethodInfo).MakeGenericMethod(gaParams);//.Invoke({target}, new object[] {o}); 

                        //genericMethod.Invoke(instance, null);
                    }
                    else {
                        method.Invoke(instance, null);
                    }
                }

                //now we want to verify that we have entered and existed the method.

                var me = IntegrationTestIClientMessageService.Instance.MethodEvents;

                Assert.IsTrue(me.Count >= 2, "An Enter and Exit was not registered with the method call.");

                Assert.AreEqual(me.First().MethodId, metaData.Method.Id, "The Method Id does not match");
                Assert.IsTrue(me.First().Enter, "The First Event must be an enter event");

                Assert.AreEqual(me.Last().MethodId, metaData.Method.Id, "The Method Id does not match");
                Assert.IsFalse(me.Last().Enter, "The Last Event must NOT be an enter event");
            }
        }

    }
}
