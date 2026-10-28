using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

internal static class SqlServerCompiledLifecycleCommand
{
    internal const string Help = "tracemap sqlserver-compiled-lifecycle compare --declarations <public-result-declarations.json> --schema <result-export.json> --bindings <lifecycle-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    private static readonly string[] Options = ["--declarations", "--schema", "--bindings", "--facts", "--repo", "--revision", "--out"];
    private static readonly string[] Files = ["compiled-lifecycle-comparison.json", "report.md"];

    internal static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0 || args[0] != "compare") throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !Options.Contains(args[i], StringComparer.Ordinal)
                || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        if (Options.Any(o => !options.ContainsKey(o))) throw new ArgumentException(Help);
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        string Read(string key) => SqlServerDdlCommand.ReadBounded(options[key], SqlServerSchemaEvidence.MaxBytes);
        var schema = SqlServerResultEvidence.Admit(Read("--schema"), Read("--declarations"), options["--repo"], options["--revision"]);
        var bindings = SqlServerSchemaEvidence.Read<SqlServerLifecycleBindings>(Read("--bindings"));
        var facts = SqlServerDdlCommand.ReadBounded(options["--facts"], SqlServerDataTableCommand.MaxFactsBytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var report = SqlServerCompiledLifecycleEvidence.Compare(schema, bindings, facts, options["--repo"], options["--revision"]);
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, Files[0]), SqlServerSchemaEvidence.Json(report) + "\n");
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, Files[1]), SqlServerCompiledLifecycleReportWriter.Build(report));
        }, requiredFiles: Files);
        output.WriteLine(destination);
        return 0;
    }
}
