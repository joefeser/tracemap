using System.Text;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

internal static class SqlServerDdlCommand
{
    internal const string Help = "tracemap sqlserver-ddl produce --input <public-manifest.json> --source-root <public-synthetic-root> --out <new.json> | compare --input <public-manifest.json> --source-root <public-synthetic-root> --schema <ddl-export.json> --bindings <bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    internal static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0) throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var allowed = args[0] == "produce" ? new[] { "--input", "--source-root", "--out" }
            : args[0] == "compare" ? new[] { "--input", "--source-root", "--schema", "--bindings", "--facts", "--repo", "--revision", "--out" } : throw new ArgumentException(Help);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i], StringComparer.Ordinal) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        if (allowed.Any(a => !options.ContainsKey(a))) throw new ArgumentException(Help);
        string Read(string key) => ReadBounded(options[key], SqlServerSchemaEvidence.MaxBytes);
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        var inputJson = Read("--input");
        var input = SqlServerDdlEvidence.ReadInput(inputJson);
        var sources = ReadSources(options["--source-root"], input);
        if (args[0] == "produce")
            SqlServerSchemaCommand.PublishNewFile(destination, SqlServerSchemaEvidence.Json(SqlServerDdlEvidence.Produce(inputJson, sources)) + "\n");
        else
        {
            var bindings = SqlServerSchemaEvidence.Read<SqlServerBindings>(Read("--bindings"));
            var facts = Read("--facts").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
            var report = SqlServerDdlEvidence.Compare(Read("--schema"), inputJson, sources, bindings, facts, options["--repo"], options["--revision"]);
            SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
            {
                SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "parameter-comparison.json"), SqlServerSchemaEvidence.Json(report) + "\n");
                SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "report.md"), SqlServerParameterReportWriter.Build(report));
            });
        }
        output.WriteLine(destination);
        return 0;
    }

    internal static string ReadBounded(string path, int limit)
    {
        // Read at most limit+1 bytes even if a file grows after its size check.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > limit) throw new ArgumentException("input-size-limit");
        using var bytes = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = stream.Read(buffer, 0, Math.Min(buffer.Length, limit + 1 - (int)bytes.Length))) > 0)
        {
            bytes.Write(buffer, 0, count);
            if (bytes.Length > limit) throw new ArgumentException("input-size-limit");
        }
        return new UTF8Encoding(false, true).GetString(bytes.ToArray());
    }

    /// <summary>Reads only the manifest's explicit files under the trusted root; links at or below it are refused.</summary>
    internal static Dictionary<string, string> ReadSources(string sourceRoot, SqlServerDdlInput input)
    {
        var root = Path.GetFullPath(sourceRoot);
        // The explicitly supplied root is trusted; reject links at that root and
        // below it. System ancestors (for example macOS /tmp) may be symlinks.
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("ddl-source-link-refused");
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        var total = 0;
        foreach (var file in input.Files)
        {
            var path = root;
            foreach (var component in file.Path.Split('/'))
            {
                path = Path.Combine(path, component);
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("ddl-source-link-refused");
            }
            var text = ReadBounded(path, SqlServerDdlEvidence.MaxFileBytes);
            total += Encoding.UTF8.GetByteCount(text);
            if (total > SqlServerDdlEvidence.MaxTotalBytes) throw new ArgumentException("ddl-input-size-limit");
            sources.Add(file.Path, text);
        }
        return sources;
    }
}
