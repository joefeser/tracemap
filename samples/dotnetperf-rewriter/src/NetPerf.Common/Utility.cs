using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace NetPerf.Common {
	public static class Utility {
		public static string ToHexString(byte[] bytes) {
			Contract.Ensures(Contract.Result<string>() != null);
			Contract.Ensures(Contract.Result<string>().Length == (bytes == null ? 0 : bytes.Length << 1));

			if (bytes == null || bytes.Length == 0)
				return string.Empty;

			return bytes.Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:X2}", b))
						.ToString();
		}
	}
}
