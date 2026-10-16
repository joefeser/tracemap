using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NetPerf.Common.Tests.Test_References {
	internal class DirectoryPath_Privates {

		public static string RemoveLocalDirectoryPaths(string path) {
			var obj = new PrivateType(typeof(DirectoryPath));
			return (string)obj.InvokeStatic("RemoveLocalDirectoryPaths", path);

		}

	}
}
