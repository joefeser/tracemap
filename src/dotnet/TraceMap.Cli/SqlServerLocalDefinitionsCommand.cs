using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Cli;

internal sealed record SqlLocalCollectionInput(string ManifestPath, string SourceRoot, string ManifestSha256,
    SqlLocalExport Export, IReadOnlyList<SqlServerDdlFile> SourceFiles);

internal static class SqlServerLocalDefinitionsCommand
{
    internal const string Help = """
        tracemap sqlserver-definitions verify --input <local-manifest.json> --source-root <offline-definition-folder>
        tracemap sqlserver-definitions collect --input <local-manifest.json> --source-root <offline-definition-folder> --out <new-folder>

        Local-only schema-definition inventory, parameters, dependencies and bounded result shapes.
        No database connection or execution. No publicSynthetic assertion, body/literal output or automatic source-call binding.
        Input is an explicit hashed file list; context repository/commit is not the database export revision.
        The separate local definition-selection input accepts whole batches from larger originals with explicit owner/qualifier values.
        """;

    internal static int Run(string[] args, TextWriter output, CancellationToken token)
    {
        if (args.Length == 0 || args[0] is not ("verify" or "collect")) throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || args[i] is not ("--input" or "--source-root" or "--out")
                || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException(Help);
        }
        if (!options.ContainsKey("--input") || !options.ContainsKey("--source-root")
            || (args[0] == "collect") != options.ContainsKey("--out")) throw new ArgumentException(Help);
        var input = Inspect(options["--input"], options["--source-root"], token);
        if (args[0] == "collect")
        {
            var destination = PhysicalPath(options["--out"]);
            var source = PhysicalPath(input.SourceRoot);
            if (Within(destination, source) || Within(source, destination) || Within(PhysicalPath(input.ManifestPath), destination))
                throw new ArgumentException("sql-local-output-overlaps-input");
            Publish(destination, input.Export, token, () => Recheck(input, token));
        }
        output.WriteLine($"Local SQL definitions: {input.Export.Objects.Count}; references: {input.Export.Dependencies.Count}; unknowns: {input.Export.Gaps.Count}; coverage=partial; callBinding=not-established");
        return 0;
    }

    internal static SqlLocalCollectionInput Inspect(string manifestPath, string sourceRoot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        manifestPath = Path.GetFullPath(manifestPath); sourceRoot = Path.GetFullPath(sourceRoot);
        if ((File.GetAttributes(manifestPath) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("sql-local-input-link-refused");
        var json = SqlServerDdlCommand.ReadBounded(manifestPath, SqlServerSchemaEvidence.MaxBytes);
        using var document = JsonDocument.Parse(json);
        SqlLocalExport export;
        IReadOnlyList<SqlServerDdlFile> files;
        if (document.RootElement.GetProperty("version").GetString() == SqlServerDefinitionSelection.InputVersion)
        {
            var input = SqlServerDefinitionSelection.ReadInput(json);
            files = input.Files;
            var sources = SqlServerDdlCommand.ReadSourceFiles(sourceRoot, files,
                SqlServerDefinitionSelection.MaxFileBytes, SqlServerDefinitionSelection.MaxTotalBytes);
            export = SqlServerDefinitionSelection.Produce(json, sources, token);
        }
        else
        {
            var input = SqlServerLocalDefinitions.ReadInput(json);
            files = input.Files;
            var sources = SqlServerDdlCommand.ReadSourceFiles(sourceRoot, files);
            export = SqlServerLocalDefinitions.Produce(json, sources, token);
        }
        return new(manifestPath, sourceRoot, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(json)), export, files);
    }

    internal static void Recheck(SqlLocalCollectionInput input, CancellationToken token)
    {
        var current = Inspect(input.ManifestPath, input.SourceRoot, token);
        if (current.ManifestSha256 != input.ManifestSha256 || SqlServerSchemaEvidence.Json(current.Export) != SqlServerSchemaEvidence.Json(input.Export))
            throw new ArgumentException("sql-local-input-changed");
    }

    internal static void Publish(string destination, SqlLocalExport export, CancellationToken token, Action? recheck = null)
    {
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("sql-local-output-must-be-new");
        token.ThrowIfCancellationRequested();
        recheck?.Invoke();
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            var json = SqlServerSchemaEvidence.Json(export);
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "definitions.local.json"), json + "\n");
            WriteIndex(Path.Combine(staging, "sql-evidence.sqlite"), export, json, token);
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "report.md"), Report(export));
            recheck?.Invoke();
            token.ThrowIfCancellationRequested();
        }, requiredFiles: ["definitions.local.json", "sql-evidence.sqlite", "report.md"]);
    }

    private static void WriteIndex(string path, SqlLocalExport export, string json, CancellationToken token)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            create table sql_collection_metadata (schema_version text not null, visibility text not null, generator_sha256 text not null, bounded_input_sha256 text not null, payload_json text not null);
            create table sql_objects (object_id text primary key, kind text not null, schema_name text not null, object_name text not null, payload_json text not null);
            create table sql_parameters (object_id text not null, ordinal integer not null, payload_json text not null, primary key(object_id, ordinal));
            create table sql_result_shapes (object_id text primary key, completeness text not null, payload_json text not null);
            create table sql_dependencies (ordinal integer primary key, source_id text not null, target_id text, resolution text not null, payload_json text not null);
            create table sql_unknowns (ordinal integer primary key, kind text not null, payload_json text not null);
            """;
        command.ExecuteNonQuery();
        Insert("insert into sql_collection_metadata values ($a,$b,$c,$d,$e)", "sqlserver-local-definition-index/1", "local-only",
            HashFile(typeof(SqlServerLocalDefinitionsCommand).Assembly.Location), SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(json)), json);
        foreach (var item in export.Objects)
        {
            token.ThrowIfCancellationRequested();
            Insert("insert into sql_objects values ($a,$b,$c,$d,$e)", item.Id, item.Kind, item.Schema, item.Name, SqlServerSchemaEvidence.Json(item));
            for (var i = 0; i < item.Parameters.Count; i++)
                Insert("insert into sql_parameters values ($a,$b,$c)", item.Id, i, SqlServerSchemaEvidence.Json(item.Parameters[i]));
            if (item.ResultShape is { } shape)
                Insert("insert into sql_result_shapes values ($a,$b,$c)", item.Id, shape.Completeness, SqlServerSchemaEvidence.Json(shape));
        }
        for (var i = 0; i < export.Dependencies.Count; i++)
        {
            var item = export.Dependencies[i];
            Insert("insert into sql_dependencies values ($a,$b,$c,$d,$e)", i, item.SourceId, item.TargetId, item.Resolution, SqlServerSchemaEvidence.Json(item));
        }
        for (var i = 0; i < export.Gaps.Count; i++)
            Insert("insert into sql_unknowns values ($a,$b,$c)", i, export.Gaps[i].Kind, SqlServerSchemaEvidence.Json(export.Gaps[i]));
        token.ThrowIfCancellationRequested(); transaction.Commit();
        return;

        void Insert(string sql, params object?[] values)
        {
            command.CommandText = sql; command.Parameters.Clear();
            for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$" + (char)('a' + i), values[i] ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    private static string Report(SqlLocalExport export)
    {
        var text = new StringBuilder("# Local SQL definition evidence\n\nCoverage: partial. No aggregate clean verdict. SQL was parsed, never executed.\n\n");
        text.AppendLine("Application context is not the database export revision. Source calls are not automatically bound.\n");
        if (export.Selection is { } selection)
        {
            text.AppendLine("## Explicit definition selection\n");
            text.AppendLine($"Settings origin: {Escape(selection.Input.Template.ValuesOrigin)}. These values do not establish deployment.\n");
            foreach (var item in selection.Projections)
                text.AppendLine($"- {Escape(item.OriginalSpan.FilePath)}:{item.OriginalSpan.StartLine}-{item.OriginalSpan.EndLine}: {Escape(item.Schema)}.{Escape(item.Name)}; projected SHA-256 {item.ProjectedSha256}.");
            text.AppendLine("\nOriginal hashes and explicit settings are in definitions.local.json. Projected bodies are not stored.\n");
        }
        text.AppendLine("| Kind | Object | Parameters | First result | Evidence |\n| --- | --- | --- | --- | --- |");
        foreach (var item in export.Objects)
        {
            text.AppendLine($"| {Escape(item.Kind)} | {Escape(item.Schema)}.{Escape(item.Name)} | {item.Parameters.Count} | {Escape(item.ResultShape?.Completeness ?? "not-modeled")} | {Escape(item.Span.FilePath)}:{item.Span.StartLine}-{item.Span.EndLine} |");
        }
        text.AppendLine("\n## Unknowns\n");
        foreach (var gap in export.Gaps) text.AppendLine($"- {Escape(gap.Kind)} — {Escape(gap.Span.FilePath)}:{gap.Span.StartLine}-{gap.Span.EndLine}; {gap.RuleId}; {gap.Tier}");
        text.AppendLine("\n## Retained artifacts\n\n- definitions.local.json: definitions, parameters, result shapes, references, gaps and provenance.\n- sql-evidence.sqlite: queryable SQL sidecar; not a TraceMap raw scan index.\n");
        foreach (var limitation in export.Limitations) text.AppendLine("- " + limitation);
        return text.ToString();
    }

    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("|", "&#124;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal);
    internal static string HashFile(string path) { using var stream = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(stream)); }
    internal static string PhysicalPath(string path) => Resolve(path, 0);
    private static string Resolve(string path, int depth)
    {
        if (depth > 32) throw new ArgumentException("sql-local-path-link-limit");
        var full = Path.GetFullPath(path);
        var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is not null)
            {
                var target = info.ResolveLinkTarget(true)?.FullName ?? throw new ArgumentException("sql-local-path-link-unresolved");
                // A link's target can itself traverse an aliased ancestor (macOS /var).
                current = Resolve(target, depth + 1);
            }
        }
        return Path.TrimEndingDirectorySeparator(current);
    }
    private static bool Within(string path, string root) => path.Equals(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
