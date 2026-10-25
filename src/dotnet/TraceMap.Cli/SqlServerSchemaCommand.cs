using System.Text;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

internal static class SqlServerSchemaCommand
{
    internal const string Help = "tracemap sqlserver-schema produce --declarations <public.json> --out <new.json> | compare --declarations <public.json> --schema <export.json> --bindings <bindings.json> --facts <facts.ndjson> --repo <logical-repo> --revision <sha> --out <new-directory>";
    internal static int Run(string[] args, TextWriter output)
    {
        if (args.Length == 0) throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var allowed = args[0] == "produce" ? new[] { "--declarations", "--out" }
            : args[0] == "compare" ? new[] { "--declarations", "--schema", "--bindings", "--facts", "--repo", "--revision", "--out" } : throw new ArgumentException(Help);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || !allowed.Contains(args[i], StringComparer.Ordinal) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        if (allowed.Any(a => !options.ContainsKey(a))) throw new ArgumentException(Help);
        string Read(string key)
        {
            var path = options[key];
            if (new FileInfo(path).Length > SqlServerSchemaEvidence.MaxBytes) throw new ArgumentException("input-size-limit");
            return File.ReadAllText(path, new UTF8Encoding(false, true));
        }
        var destination = options["--out"];
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("output-must-be-new");
        var declarations = Read("--declarations");
        if (args[0] == "produce") File.WriteAllText(destination, SqlServerSchemaEvidence.Json(SqlServerSchemaEvidence.Produce(declarations)) + "\n", new UTF8Encoding(false));
        else
        {
            var schema = SqlServerSchemaEvidence.Admit(Read("--schema"), declarations, options["--repo"], options["--revision"]);
            var bindings = SqlServerSchemaEvidence.Read<SqlServerBindings>(Read("--bindings"));
            var facts = Read("--facts").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
            var report = SqlServerSchemaEvidence.Compare(schema, bindings, facts, options["--repo"], options["--revision"]);
            Directory.CreateDirectory(destination);
            File.WriteAllText(Path.Combine(destination, "parameter-comparison.json"), SqlServerSchemaEvidence.Json(report) + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(destination, "report.md"), SqlServerParameterReportWriter.Build(report), new UTF8Encoding(false));
        }
        output.WriteLine(destination);
        return 0;
    }
}
