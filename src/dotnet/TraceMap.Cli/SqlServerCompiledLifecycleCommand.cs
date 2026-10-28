using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

internal static class SqlServerCompiledLifecycleCommand
{
    internal const string Help = "tracemap sqlserver-compiled-lifecycle <compare|compare-owner> --declarations <result-declarations.json> --schema <result-export.json> --bindings <lifecycle-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>; or produce-owner-schema --declarations <owner-declarations.json> --out <new-file> (owner artifacts are private/local and conditional, not deployed-schema proof)";
    private static readonly string[] Options = ["--declarations", "--schema", "--bindings", "--facts", "--repo", "--revision", "--out"];
    private static readonly string[] Files = ["compiled-lifecycle-comparison.json", "report.md"];

    internal static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0 || args[0] is not ("compare" or "compare-owner" or "produce-owner-schema")) throw new ArgumentException(Help);
        var produce = args[0] == "produce-owner-schema";
        string[] required = produce ? ["--declarations", "--out"] : Options;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !required.Contains(args[i], StringComparer.Ordinal)
                || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        if (required.Any(o => !options.ContainsKey(o))) throw new ArgumentException(Help);
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        string Read(string key) => SqlServerDdlCommand.ReadBounded(options[key], SqlServerSchemaEvidence.MaxBytes);
        if (produce)
        {
            var export = SqlServerOwnerResultEvidence.Produce(Read("--declarations"));
            SqlServerSchemaCommand.PublishNewFile(destination, SqlServerSchemaEvidence.Json(export) + "\n");
            output.WriteLine(destination);
            return 0;
        }
        var owner = args[0] == "compare-owner";
        var schema = owner
            ? SqlServerOwnerResultEvidence.Admit(Read("--schema"), Read("--declarations"), options["--repo"], options["--revision"])
            : SqlServerResultEvidence.Admit(Read("--schema"), Read("--declarations"), options["--repo"], options["--revision"]);
        var bindings = SqlServerSchemaEvidence.Read<SqlServerLifecycleBindings>(Read("--bindings"));
        var facts = SqlServerDdlCommand.ReadBounded(options["--facts"], SqlServerDataTableCommand.MaxFactsBytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var report = owner
            ? SqlServerCompiledLifecycleEvidence.CompareOwnerDeclared(schema, bindings, facts, options["--repo"], options["--revision"])
            : SqlServerCompiledLifecycleEvidence.Compare(schema, bindings, facts, options["--repo"], options["--revision"]);
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, Files[0]), SqlServerSchemaEvidence.Json(report) + "\n");
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, Files[1]), SqlServerCompiledLifecycleReportWriter.Build(report));
        }, requiredFiles: Files);
        output.WriteLine(destination);
        return 0;
    }
}
