// #856 stage 2b: shims for the dotnetperf dependency surface. The corpus sources
// (ILInstrumenter.cs / ILValidator.cs) are compiled UNMODIFIED from the byte-pinned
// tree; these types fill the missing NetPerf.Common / NetPerf.Client surface in
// matching namespaces. Nothing here executes in production scans — it exists so the
// era rewriter can produce genuine before/after assemblies for the rewrite-evidence
// oracle. Member shapes follow the instrumenter's call sites exactly.
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
        public ErrorTuple(string message) => Message = message;
        public string Message { get; }
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
    public interface IInstrumentFactory
    {
        NetPerf.Client.IMessageController MessageController { get; }
        int RegisterAssembly(string assemblyName, string hash, NetPerf.Common.HashAlgorithm hashAlgorithm);
        int GetMetadataForAssembly(int assemblyId, out NetPerf.Common.ErrorTuple error);
        int RegisterModule(int assemblyId, string moduleName);
        int RegisterType(int moduleId, string fullName, string[] customAttributes, string[] genericParameters);
        int RegisterMethod(int typeId, string fullName, byte[] rawMethodBytes, string[] customAttributes, int[] sourceFileIds, string[] genericTypes);
        void RegisterEvent(int typeId, string fullName, int? addMethodId, int? removeMethodId, int? otherMethodId, int[] otherMethodIds, string[] customAttributes);
        void RegisterProperty(int typeId, string fullName, int? getterId, int? setterId, int[] otherMethodIds, string[] customAttributes);
        int RegisterSourceFile(int moduleId, string url, byte[] hash, string hashAlgorithm, string language);
        int LookupSourceFile(int methodId, string url);
        int RegisterPoint(int methodId);
        int RegisterPoint(int methodId, int documentId, int startLine, int startColumn, int endLine, int endColumn);
    }
}

namespace NetPerf.Client
{
    public interface IMessageController
    {
        bool WantsMethodEnterMessages { get; }
        bool WantsMethodExitMessages { get; }
        bool WantsSeqPointEndMessages { get; }
    }

    // The runtime messaging surface the instrumenter rewrites calls toward. These
    // members never execute here; the instrumented assembly carries memberrefs to
    // this shape, which is exactly what the rewrite-evidence oracle observes.
    public static class Messaging
    {
        public static void EnterMethod(int moduleIdentifier, int methodIdentifier) { }
        public static void ExitMethod(int moduleIdentifier, int methodIdentifier) { }
        public static void EnterGenericMethod(int moduleIdentifier, int methodIdentifier, string[] genericTypeNames) { }
        public static void ExitGenericMethod(int moduleIdentifier, int methodIdentifier, string[] genericTypeNames) { }
        public static void InstrumentationPointHit(int moduleIdentifier, int pointIdentifier) { }
    }
}
