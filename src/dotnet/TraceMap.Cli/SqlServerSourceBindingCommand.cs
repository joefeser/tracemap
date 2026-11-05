using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Cli;

internal static class SqlServerSourceBindingCommand
{
    internal const string Help = """
        tracemap sqlserver-source-binding compare --input <binding.local.json> --source-root <source-folder> --sql-input <selection.local.json> --sql-source-root <original-sql-folder> --out <new-folder>

        Local-only syntax profile candidate over explicit source methods and separately admitted selected SQL.
        No scanner/index mutation, code execution, database access or compiler/runtime terminal binding.
        Preserves dispatch, external PetaPoco transport, symbol, result and hydration gaps.
        """;
    internal static async Task<int> RunAsync(string[] args, TextWriter output, CancellationToken token)
    {
        if (args.Length == 0 || args[0] != "compare") throw new ArgumentException(Help);
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
            if (i + 1 >= args.Length || args[i] is not ("--input" or "--source-root" or "--sql-input" or "--sql-source-root" or "--out")
                || args[i + 1].StartsWith("--", StringComparison.Ordinal) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException(Help);
        if (options.Count != 5) throw new ArgumentException(Help);
        var destination = SqlServerLocalDefinitionsCommand.PhysicalPath(options["--out"]);
        var source = SqlServerLocalDefinitionsCommand.PhysicalPath(options["--source-root"]);
        var sqlSource = SqlServerLocalDefinitionsCommand.PhysicalPath(options["--sql-source-root"]);
        var inputPath = Path.GetFullPath(options["--input"]); var sqlPath = Path.GetFullPath(options["--sql-input"]);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new ArgumentException("sql-source-binding-output-must-be-new");
        foreach (var root in new[] { source, sqlSource })
            if (Within(destination, root) || Within(root, destination)) throw new ArgumentException("sql-source-binding-output-overlaps-input");
        foreach (var path in new[] { inputPath, sqlPath })
            if (Within(SqlServerLocalDefinitionsCommand.PhysicalPath(path), destination)) throw new ArgumentException("sql-source-binding-output-overlaps-input");
        var snapshot = Inspect();
        await VerifySourceCommit(snapshot.Report.Input).ConfigureAwait(false);
        SqlServerSchemaCommand.PublishNewDirectory(destination, staging =>
        {
            Recheck();
            var json = SqlServerSchemaEvidence.Json(snapshot.Report);
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "binding.local.json"), json + "\n");
            WriteIndex(Path.Combine(staging, "source-sql-bindings.sqlite"), snapshot.Report, json, token);
            SqlServerSchemaCommand.PublishNewFile(Path.Combine(staging, "report.md"), Report(snapshot.Report));
            Recheck(); token.ThrowIfCancellationRequested();
        }, requiredFiles: ["binding.local.json", "source-sql-bindings.sqlite", "report.md"]);
        await output.WriteLineAsync($"Source/SQL candidate: {snapshot.Report.Status}; parameter candidates: {snapshot.Report.Parameters.Count}; coverage=partial; terminalBinding=unavailable; {destination}");
        return 0;

        (SqlSourceBindingReport Report, string InputHash, string SqlHash) Inspect()
        {
            token.ThrowIfCancellationRequested();
            foreach (var path in new[] { inputPath, sqlPath })
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("sql-source-binding-input-link-refused");
            var input = SqlServerDdlCommand.ReadBounded(inputPath, SqlServerSchemaEvidence.MaxBytes);
            var contract = SqlServerSourceBinding.ReadInput(input);
            var files = contract.Methods.GroupBy(method => method.Path, StringComparer.Ordinal).Select(group => new SqlServerDdlFile(group.Key, group.First().Sha256)).ToArray();
            var sources = SqlServerDdlCommand.ReadSourceFiles(source, files, SqlServerSourceBinding.MaxFileBytes, SqlServerSourceBinding.MaxTotalBytes);
            var sqlInput = SqlServerDdlCommand.ReadBounded(sqlPath, SqlServerSchemaEvidence.MaxBytes);
            var sqlContract = SqlServerDefinitionSelection.ReadInput(sqlInput);
            var sqlSources = SqlServerDdlCommand.ReadSourceFiles(sqlSource, sqlContract.Files,
                SqlServerDefinitionSelection.MaxFileBytes, SqlServerDefinitionSelection.MaxTotalBytes);
            return (SqlServerSourceBinding.Produce(input, sources, sqlInput, sqlSources, token),
                SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(input)), SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(sqlInput)));
        }
        void Recheck()
        {
            VerifySourceCommit(snapshot.Report.Input).GetAwaiter().GetResult();
            var current = Inspect();
            if (current.InputHash != snapshot.InputHash || current.SqlHash != snapshot.SqlHash
                || SqlServerSchemaEvidence.Json(current.Report) != SqlServerSchemaEvidence.Json(snapshot.Report))
                throw new ArgumentException("sql-source-binding-input-changed");
        }
        async Task VerifySourceCommit(SqlSourceBindingInput contract)
        {
            if ((await Git(source, token, "rev-parse", "--verify", "HEAD").ConfigureAwait(false)).Trim() != contract.CommitSha)
                throw new ArgumentException("sql-source-binding-git-head-mismatch");
            var prefix = (await Git(source, token, "rev-parse", "--show-prefix").ConfigureAwait(false)).TrimEnd('\r', '\n');
            foreach (var file in contract.Methods.GroupBy(method => method.Path, StringComparer.Ordinal).Select(group => group.First()))
            {
                var locator = contract.CommitSha + ":" + prefix + file.Path;
                var size = (await Git(source, token, "cat-file", "-s", locator).ConfigureAwait(false)).Trim();
                if (!long.TryParse(size, out var count) || count < 0 || count > SqlServerSourceBinding.MaxFileBytes)
                    throw new ArgumentException("sql-source-binding-committed-file-size");
                if (await CommittedHash(source, locator, token).ConfigureAwait(false) != file.Sha256)
                    throw new ArgumentException("sql-source-binding-source-not-at-declared-commit");
            }
            if ((await Git(source, token, "rev-parse", "--verify", "HEAD").ConfigureAwait(false)).Trim() != contract.CommitSha)
                throw new ArgumentException("sql-source-binding-git-head-changed");
        }
    }

    private static async Task<string> CommittedHash(string source, string locator, CancellationToken token)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-C", source, "show", locator }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("sql-source-binding-git-unavailable");
        var error = process.StandardError.ReadToEndAsync(token);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[8192]; var total = 0;
        try
        {
            int count;
            while ((count = await process.StandardOutput.BaseStream.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                total += count;
                if (total > SqlServerSourceBinding.MaxFileBytes) throw new ArgumentException("sql-source-binding-committed-file-size");
                hash.AppendData(buffer, 0, count);
            }
            await process.WaitForExitAsync(token).ConfigureAwait(false); await error.ConfigureAwait(false);
            if (process.ExitCode != 0) throw new ArgumentException("sql-source-binding-committed-file-unavailable");
            return Convert.ToHexStringLower(hash.GetHashAndReset());
        }
        catch { if (!process.HasExited) process.Kill(true); throw; }
    }

    private static async Task<string> Git(string source, CancellationToken token, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-C", source }.Concat(arguments)) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("sql-source-binding-git-unavailable");
        var output = process.StandardOutput.ReadToEndAsync(token); var error = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
        var text = await output.ConfigureAwait(false); await error.ConfigureAwait(false);
        if (process.ExitCode != 0) throw new ArgumentException("sql-source-binding-git-head-unavailable");
        return text;
    }

    private static void WriteIndex(string path, SqlSourceBindingReport report, string json, CancellationToken token)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open(); using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = """
            create table binding_metadata (schema_version text not null, visibility text not null, generator_sha256 text not null, bounded_input_sha256 text not null, source_commit_verified integer not null, payload_json text not null);
            create table binding_observations (role text primary key, status text not null, payload_json text not null);
            create table binding_parameters (ordinal integer primary key, sql_object_id text not null, status text not null, payload_json text not null);
            create table binding_gaps (ordinal integer primary key, kind text not null, payload_json text not null);
            """;
        command.ExecuteNonQuery();
        Insert("insert into binding_metadata values ($a,$b,$c,$d,$e,$f)", "sqlserver-source-binding-index/1", "local-only",
            SqlServerLocalDefinitionsCommand.HashFile(typeof(SqlServerSourceBindingCommand).Assembly.Location),
            SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(json)), 1, json);
        foreach (var item in report.Observations) Insert("insert into binding_observations values ($a,$b,$c)", item.Role, item.Status, SqlServerSchemaEvidence.Json(item));
        for (var i = 0; i < report.Parameters.Count; i++)
            Insert("insert into binding_parameters values ($a,$b,$c,$d)", i, report.Parameters[i].SqlObjectId, report.Parameters[i].Status, SqlServerSchemaEvidence.Json(report.Parameters[i]));
        for (var i = 0; i < report.Gaps.Count; i++) Insert("insert into binding_gaps values ($a,$b,$c)", i, report.Gaps[i].Kind, SqlServerSchemaEvidence.Json(report.Gaps[i]));
        token.ThrowIfCancellationRequested(); transaction.Commit();
        return;
        void Insert(string sql, params object[] values)
        {
            token.ThrowIfCancellationRequested(); command.CommandText = sql; command.Parameters.Clear();
            for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue("$" + (char)('a' + i), values[i]);
            command.ExecuteNonQuery();
        }
    }
    private static string Report(SqlSourceBindingReport report)
    {
        var text = new StringBuilder("# Source-to-SQL conditional candidate\n\n");
        text.AppendLine($"Status: {report.Status}. Coverage: partial. No aggregate clean verdict.\n");
        text.AppendLine("No compiler/runtime terminal binding is established. Owner/qualifier and selected dispatch remain explicit assumptions.\n");
        text.AppendLine("| Role | Shape | Source |\n| --- | --- | --- |");
        foreach (var observation in report.Observations)
            text.AppendLine($"| {observation.Role} | {observation.Status} | {(observation.Span is { } span ? span.FilePath + ":" + span.StartLine + "-" + span.EndLine : "unavailable")} |");
        text.AppendLine("\n## Conditional positional association\n");
        foreach (var parameter in report.Parameters)
            text.AppendLine($"- Source {parameter.SourceName} (ordinal {parameter.SourceOrdinal}) → {parameter.SqlName} (SQL ordinal {parameter.SqlOrdinal}); {parameter.Status}; {parameter.RuleId}; {parameter.Tier}.");
        text.AppendLine("\n## Unresolved boundaries\n");
        foreach (var gap in report.Gaps) text.AppendLine("- " + gap.Kind);
        text.AppendLine("\n## Limitations\n");
        foreach (var limitation in report.Limitations) text.AppendLine("- " + limitation);
        return text.ToString();
    }
    private static bool Within(string path, string root) => path.Equals(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
        || path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
