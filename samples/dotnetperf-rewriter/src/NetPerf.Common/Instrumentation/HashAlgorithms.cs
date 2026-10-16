using System.IO;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

namespace NetPerf.Common.Instrumentation {

	[ContractClass(typeof(HashAlgorithmContract))]
	public abstract class HashAlgorithm {

		public abstract string ComputeHash(Stream stream);
		public abstract string ComputeHash(byte[] bytes);

		public static HashAlgorithm FromString(string hashAlgorithmName) {
			Contract.Ensures(Contract.Result<HashAlgorithm>() != null);

			if (SHA512.ToString() == hashAlgorithmName)
				return SHA512;

			return None;
		}

		protected static string ToHexString(byte[] bytes) {
			Contract.Ensures(Contract.Result<string>() != null);
			Contract.Ensures(Contract.Result<string>().Length == (bytes == null ? 0 : bytes.Length << 1));

			if (bytes == null || bytes.Length == 0)
				return string.Empty;

			return bytes.Aggregate(new StringBuilder(), (sb, b) => sb.AppendFormat("{0:X2}", b)).ToString();
		}

		public static readonly HashAlgorithm None = new NullAlgorithm();
		public static readonly HashAlgorithm SHA512 = new SHA512Algorithm();

		private class NullAlgorithm : HashAlgorithm {

			public override string ComputeHash(Stream stream) {
				return string.Empty;
			}

			public override string ComputeHash(byte[] bytes) {
				return string.Empty;
			}
		}

		private class SHA512Algorithm : HashAlgorithm {

			public override string ComputeHash(Stream stream) {
				Contract.Ensures(Contract.Result<string>().Length == 128);

				var hasher = System.Security.Cryptography.SHA512Managed.Create();
				var hash = hasher.ComputeHash(stream);

				return ToHexString(hash);
			}

			public override string ComputeHash(byte[] bytes) {
				Contract.Ensures(Contract.Result<string>().Length == 128);

				var hasher = System.Security.Cryptography.SHA512Managed.Create();
				var hash = hasher.ComputeHash(bytes);

				return ToHexString(hash);
			}

			public override string ToString() {
				return "SHA512";
			}
		}
	}

	[ContractClassFor(typeof(HashAlgorithm))]
	internal abstract class HashAlgorithmContract : HashAlgorithm {

		public override string ComputeHash(Stream stream) {
			Contract.Requires(stream != null && stream.Length > 0);
			Contract.Ensures(Contract.Result<string>() != null);

			return default(string);
		}

		public override string ComputeHash(byte[] bytes) {
			Contract.Requires(bytes != null && bytes.Length > 0);
			Contract.Ensures(Contract.Result<string>() != null);

			return default(string);
		}
	}
}