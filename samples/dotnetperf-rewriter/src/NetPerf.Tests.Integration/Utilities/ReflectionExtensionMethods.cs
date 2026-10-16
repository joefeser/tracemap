using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NetPerf.Tests.Integration {

    /// <summary>
    /// extension methods designed to work with the integration tests.
    /// </summary>
    public static class ReflectionExtensionMethods {

        //public static void NotifyOfPropertyChange<TProperty>(Expression<Func<TProperty>> property) {

        //    MemberInfo method = null;

        //    var lambda = (LambdaExpression)property;
        //    MemberExpression memberExpression;
        //    if (lambda.Body is UnaryExpression) {
        //        var unaryExpression = (UnaryExpression)lambda.Body;
        //        memberExpression = (MemberExpression)unaryExpression.Operand;
        //        method = memberExpression.Member;
        //        //todo field info
        //        var type = (method as PropertyInfo).AsNetPerfSignature(true);
        //    }
        //    else if (lambda.Body is MethodCallExpression) {
        //        var methodCall = (lambda.Body as MethodCallExpression);
        //        method = methodCall.Method;
        //        var type = (method as MethodBase).AsNetPerfSignature();

        //        //var func = property.Compile();
        //        //var success = func.Invoke();

        //        //object[] arguments = methodCall.Arguments.OfType<ConstantExpression>().Select(p => p.Value).ToArray();
        //        //(method as MethodBase).Invoke(item, arguments);
        //        //var val =  (method as MethodBase).Invoke()
        //        //memberExpression = method;
        //    }
        //    else {
        //        memberExpression = (MemberExpression)lambda.Body;
        //        method = memberExpression.Member as PropertyInfo;
        //        //todo field info
        //        var type = (method as PropertyInfo).AsNetPerfSignature(true);
        //    }

        //    var name = method.Name;
        //}

        //var blah = "hey Now";
        //NotifyOfPropertyChange(() => blah.ToUpper(System.Threading.Thread.CurrentThread.CurrentCulture));
        //NotifyOfPropertyChange(() => blah.ToUpper());
        //NotifyOfPropertyChange(() => blah.Length);
        //NotifyOfPropertyChange(() => string.Empty);

        /// <summary>
        /// Convert a methodinfo to a string that we would expect for the NetPerf Signature String.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="get">Get or set</param>
        /// <returns></returns>
        public static string AsNetPerfSignature(this PropertyInfo method, bool get) {

            if (method == null) {
                return null;
            }

            MethodBase process = null;

            if (get) {
                process = method.GetGetMethod();
            }
            else {
                process = method.GetSetMethod();
            }
            if (process != null) {
                return AsNetPerfSignature(process);
            }
            return null;
        }

        /// <summary>
        /// Convert a methodinfo to a string that we would expect for the NetPerf Signature String.
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string AsNetPerfSignature(this MethodBase method) {
            //build out the fully qualified type replacing + with / and
            // return type Signature :: Method ( Params )
            var sb = new StringBuilder();
            if (method is MethodInfo) {
                var miType = (method as MethodInfo).ReturnType;
                ProcessForGenericType(sb, miType);
                sb.Append(" ");
            }
            else {
                sb.Append("System.Void ");
            }

            //todo try to understand why this is the case. I don't know why the FullName is null but this code works.
            if (method.DeclaringType.IsGenericType && method.DeclaringType.FullName == null) {
                sb.Append(method.DeclaringType.Namespace).Append(".").Append(method.DeclaringType.Name.Replace("+", "/"));
            }
            else {
                sb.Append(method.DeclaringType.FullName.Replace("+", "/"));
            }

            sb.Append("::");
            sb.Append(method.Name).Append("(");
            var paramExists = false;
            foreach (var item in method.GetParameters()) {
                ProcessForGenericType(sb, item.ParameterType);
                sb.Append(",");
                //sb.Append(item.ParameterType.FullName).Append(",");
                paramExists = true;
            }
            if (paramExists) {
                sb.Length -= 1;
            }
            sb.Append(")");
            var retVal = sb.ToString();
            return retVal;
        }

        /// <summary>
        /// Determine if the item is compiler generated
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static bool IsCompilerGenerated(this MethodBase method) {
            var compilerGenerated = method.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).FirstOrDefault();
            return compilerGenerated != null;
        }

        private static void ProcessForGenericType(StringBuilder sb, Type miType) {
            if (miType.IsGenericType) {
                sb.Append(miType.Namespace).Append(".").Append(miType.Name);
                sb.Append("<");
                foreach (var ga in miType.GetGenericArguments()) {
                    sb.Append(ga.Name).Append(",");
                }
                sb.Length -= 1;
                sb.Append(">");
            }
            else if (miType.IsGenericParameter) {
                sb.Append(miType.Name);
            }
            else {
                sb.Append(miType.FullName);
            }
        }

    }
}
