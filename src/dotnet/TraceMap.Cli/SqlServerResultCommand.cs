using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

/// <summary>Opt-in first-result-set column routes; separate from the parameter-only sqlserver-schema/sqlserver-ddl routes.</summary>
internal static class SqlServerResultCommand
{
    internal const string SchemaHelp = "tracemap sqlserver-result-schema produce --declarations <public-result-declarations.json> --out <new.json> | compare --declarations <public-result-declarations.json> --schema <result-export.json> --bindings <result-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    internal const string DdlHelp = "tracemap sqlserver-ddl-result produce --input <public-result-manifest.json> --source-root <public-synthetic-root> --out <new.json> | compare --input <public-result-manifest.json> --source-root <public-synthetic-root> --schema <ddl-result-export.json> --bindings <result-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    internal const string DdlTableHelp = "tracemap sqlserver-ddl-table-result produce --input <public-table-result-manifest.json> --source-root <public-synthetic-root> --out <new.json> | compare --input <public-table-result-manifest.json> --source-root <public-synthetic-root> --schema <ddl-table-result-export.json> --bindings <result-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    private static readonly string[] ReportFiles = ["result-column-comparison.json", "report.md"];

    internal static int RunSchema(string[] args, TextWriter output)
    {
        var options = Options(args, SchemaHelp, ["--declarations"]);
        var declarations = Read(options, "--declarations");
        if (args[0] == "produce")
            SqlServerSchemaCommand.PublishNewFile(options["--out"], SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(declarations)) + "\n");
        else
        {
            var schema = SqlServerResultEvidence.Admit(Read(options, "--schema"), declarations, options["--repo"], options["--revision"]);
            Publish(options["--out"], SqlServerResultEvidence.Compare(schema, Bindings(options), Facts(options), options["--repo"], options["--revision"]),
                r => SqlServerResultReportWriter.Build(r));
        }
        output.WriteLine(options["--out"]);
        return 0;
    }

    internal static int RunDdl(string[] args, TextWriter output)
    {
        var options = Options(args, DdlHelp, ["--input", "--source-root"]);
        var inputJson = Read(options, "--input");
        var input = SqlServerDdlResultEvidence.ReadInput(inputJson);
        var sources = SqlServerDdlCommand.ReadSources(options["--source-root"], input);
        if (args[0] == "produce")
            SqlServerSchemaCommand.PublishNewFile(options["--out"], SqlServerSchemaEvidence.Json(SqlServerDdlResultEvidence.Produce(inputJson, sources)) + "\n");
        else
        {
            var report = SqlServerDdlResultEvidence.Compare(Read(options, "--schema"), inputJson, sources, Bindings(options), Facts(options),
                options["--repo"], options["--revision"]);
            Publish(options["--out"], report, SqlServerResultReportWriter.Build);
        }
        output.WriteLine(options["--out"]);
        return 0;
    }

    internal static int RunDdlTable(string[] args, TextWriter output)
    {
        var options = Options(args, DdlTableHelp, ["--input", "--source-root"]);
        var inputJson = Read(options, "--input");
        var input = SqlServerDdlTableResultEvidence.ReadInput(inputJson);
        var sources = SqlServerDdlCommand.ReadSources(options["--source-root"], input);
        if (args[0] == "produce")
            SqlServerSchemaCommand.PublishNewFile(options["--out"], SqlServerSchemaEvidence.Json(SqlServerDdlTableResultEvidence.Produce(inputJson, sources)) + "\n");
        else
        {
            var report = SqlServerDdlTableResultEvidence.Compare(Read(options, "--schema"), inputJson, sources, Bindings(options), Facts(options),
                options["--repo"], options["--revision"]);
            Publish(options["--out"], report, SqlServerResultReportWriter.Build);
        }
        output.WriteLine(options["--out"]);
        return 0;
    }

    private static Dictionary<string, string> Options(string[] args, string help, string[] origin)
    {
        if (args.Length == 0) throw new ArgumentException(help);
        string[] allowed = args[0] == "produce" ? [.. origin, "--out"]
            : args[0] == "compare" ? [.. origin, "--schema", "--bindings", "--facts", "--repo", "--revision", "--out"] : throw new ArgumentException(help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i], StringComparer.Ordinal) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(help);
        if (allowed.Any(a => !options.ContainsKey(a))) throw new ArgumentException(help);
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        return options;
    }

    private static string Read(Dictionary<string, string> options, string key) => SqlServerDdlCommand.ReadBounded(options[key], SqlServerSchemaEvidence.MaxBytes);
    private static SqlServerResultBindings Bindings(Dictionary<string, string> options) => SqlServerSchemaEvidence.Read<SqlServerResultBindings>(Read(options, "--bindings"));
    private static CodeFact[] Facts(Dictionary<string, string> options)
        => Read(options, "--facts").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();

    private static void Publish<T>(string destination, T report, Func<T, string> markdown)
        => SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, ReportFiles[0]), SqlServerSchemaEvidence.Json(report) + "\n");
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, ReportFiles[1]), markdown(report));
        }, requiredFiles: ReportFiles);
}
