// #856 stage 2b: shims for the dotnetperf dependency surface. The era sources
// (Adapted/ILInstrumenter.cs / Adapted/ILValidator.cs) are compiled from adapted,
// diff-pinned copies of the byte-pinned tree; these types fill the missing
// NetPerf.Common / NetPerf.Client surface in matching namespaces, mirroring the
// corpus member shapes. Nothing here executes in production scans — it exists so
// the era rewriter can produce genuine before/after assemblies for the
// rewrite-evidence oracle.
namespace NetPerf.Common
{
    public sealed class FilePath
    {
        public FilePath(string fullPath) => FullPath = fullPath;
        public string FullPath { get; }
        public bool IsAbsolute => Path.IsPathRooted(FullPath);
    }

    public enum HashAlgorithm
    {
        SHA512,
    }

    public static class HashAlgorithmExtensions
    {
        public static string ComputeHash(this HashAlgorithm algorithm, Stream stream)
        {
            using var sha = System.Security.Cryptography.SHA512.Create();
            return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }
    }

    public sealed class ErrorTuple
    {
        public ErrorTuple() => ErrorMessage = string.Empty;
        public ErrorTuple(string errorMessage) => ErrorMessage = errorMessage;
        public string ErrorMessage { get; }
    }

    public static class EraCollectionExtensions
    {
        public static bool IsEmpty<T>(this IEnumerable<T> items) => !items.Any();
        public static bool IsNotEmpty<T>(this IEnumerable<T> items) => items.Any();
    }

    public static class FileStub
    {
        public static Stream OpenRead(string path) => File.OpenRead(path);
        public static FileStream Open(string path, FileMode mode, FileAccess access, FileShare share) => new(path, mode, access, share);
        public static FileStream Open(string path, FileMode mode) => new(path, mode, FileAccess.Read, FileShare.Read);
        public static FileStream Open(string path, FileMode mode, FileAccess access) => new(path, mode, access, FileShare.Read);
    }
}

namespace NetPerf.Common.Instrumentation
{
    // Mirrors corpus src/NetPerf.Common/Instrumentation/IInstrumentFactory.cs member
    // for member (the corpus contract-class and Code Contracts attributes are dead
    // tooling on modern .NET). GetMetadataForAssembly returns the string payload the
    // instrumenter bakes into the NetPerfMetadata.InstrumentationData literal —
    // returning anything but a string corrupts the constant table on write.
    public interface IInstrumentFactory
    {
        IMessagingController MessageController { get; }
        string GetMetadataForAssembly(int assemblyId, out ErrorTuple error);
        int LookupSourceFile(int methodId, string uri);
        int RegisterAssembly(string assemblyIdentity, string preInstrumentedHash, HashAlgorithm preInstrumentedHashAlgorithm);
        int RegisterModule(int assemblyId, string moduleName);
        int RegisterType(int moduleId, string signature, string[] customAttributes = null, string[] genericArguments = null);
        int RegisterEvent(int typeId, string signature, int? addMethodId, int? removeMethodId,
                          int? fireMethodId = null, int[] otherMethodIds = null, string[] customAttributes = null);
        int RegisterMethod(int typeId, string signature, byte[] rawMethodBytes = null, string[] customAttributes = null, int[] sourceFileIds = null, string[] genericArguments = null);
        int RegisterPoint(int methodId, int? sourceFileId = null, int? startLine = null, int? startColumn = null, int? endLine = null, int? endColumn = null);
        int RegisterProperty(int typeId, string signature, int? getMethodId, int? setMethodId,
                             int[] otherMethodIds = null, string[] customAttributes = null);
        int RegisterSourceFile(int moduleId, string uri, byte[] hash, string hashAlgorithm, string sourceLanguage);
    }

    // Mirrors corpus src/NetPerf.Common/Instrumentation/IMessagingController.cs.
    public interface IMessagingController
    {
        bool WantsMethodEnterMessages { get; }
        bool WantsMethodExitMessages { get; }
        bool WantsSeqPointEndMessages { get; }
    }
}

namespace NetPerf.Client
{
    // The runtime messaging surface the instrumenter rewrites calls toward (corpus
    // src/NetPerf.Client/Messaging.cs — parameter signatures only; the era named-
    // pipe plumbing is not executed in the oracle). These members never execute
    // here; the instrumented assembly carries memberrefs to this shape, which is
    // exactly what the rewrite-evidence oracle observes.
    public static class Messaging
    {
        public static void EnterMethod(int moduleId, int methodId) { }
        public static void ExitMethod(int moduleId, int methodId) { }
        public static void EnterGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) { }
        public static void ExitGenericMethod(int moduleId, int methodId, string[] classTypes, string[] methodTypes) { }
        public static void InstrumentationPointHit(int moduleId, int pointId) { }
    }
}
