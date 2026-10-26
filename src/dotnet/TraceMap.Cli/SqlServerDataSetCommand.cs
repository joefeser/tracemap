using TraceMap.Core;
using TraceMap.Reporting;
namespace TraceMap.Cli;
/// <summary>Opt-in DataSet first-table comparison; shapes come only from the unchanged result-schema / ddl-result / ddl-table-result routes.</summary>
internal static class SqlServerDataSetCommand
{
    internal const string Help = "tracemap sqlserver-dataset-result compare --origin result-schema --declarations <public-result-declarations.json> | --origin <ddl-result|ddl-table-result> --input <public-manifest.json> --source-root <public-synthetic-root>; then --schema <shape-export.json> --bindings <dataset-bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    private static readonly string[] ReportFiles = ["dataset-column-comparison.json", "report.md"];
    private static readonly string[] Common = ["--origin", "--schema", "--bindings", "--facts", "--repo", "--revision", "--out"];
    internal static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0 || args[0] != "compare") throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        string[] origin = options.GetValueOrDefault("--origin") switch
        {
            "result-schema" => ["--declarations"],
            "ddl-result" or "ddl-table-result" => ["--input", "--source-root"],
            _ => throw new ArgumentException(Help)
        };
        string[] allowed = [.. Common, .. origin];
        if (options.Keys.Any(k => !allowed.Contains(k, StringComparer.Ordinal)) || allowed.Any(a => !options.ContainsKey(a))) throw new ArgumentException(Help);
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        var bindings = SqlServerSchemaEvidence.Read<SqlServerDataSetBindings>(Read(options, "--bindings"));
        // A whole scan's facts.ndjson is larger than the 4 MiB public-artifact bound; it keeps the DataTable route's explicit bound.
        var facts = SqlServerDdlCommand.ReadBounded(options["--facts"], SqlServerDataTableCommand.MaxFactsBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var schema = Read(options, "--schema");
        var (repo, revision) = (options["--repo"], options["--revision"]);
        SqlServerDataSetComparisonReport report;
        if (options["--origin"] == "result-schema")
            report = SqlServerDataSetEvidence.CompareResultSchema(schema, Read(options, "--declarations"), bindings, facts, repo, revision);
        else
        {
            var inputJson = Read(options, "--input");
            // Each DDL origin reads only its own manifest version; the other route's manifests are refused here.
            var input = options["--origin"] == "ddl-result" ? SqlServerDdlResultEvidence.ReadInput(inputJson) : SqlServerDdlTableResultEvidence.ReadInput(inputJson);
            var sources = SqlServerDdlCommand.ReadSources(options["--source-root"], input);
            report = options["--origin"] == "ddl-result"
                ? SqlServerDataSetEvidence.CompareDdlResult(schema, inputJson, sources, bindings, facts, repo, revision)
                : SqlServerDataSetEvidence.CompareDdlTableResult(schema, inputJson, sources, bindings, facts, repo, revision);
        }
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, ReportFiles[0]), SqlServerSchemaEvidence.Json(report) + "\n");
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, ReportFiles[1]), SqlServerDataSetReportWriter.Build(report));
        }, requiredFiles: ReportFiles);
        output.WriteLine(destination);
        return 0;
    }
    private static string Read(Dictionary<string, string> options, string key) => SqlServerDdlCommand.ReadBounded(options[key], SqlServerSchemaEvidence.MaxBytes);
}
