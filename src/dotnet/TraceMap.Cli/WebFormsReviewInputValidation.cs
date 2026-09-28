using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Cli;

internal sealed record WebFormsReviewValidatedInputs(
    WebFormsReviewPreflightManifest Preflight,
    CompiledInputProvenance CompiledProvenance,
    ScanManifest? ParentManifest,
    long ParentFactCount,
    string SourceState,
    IReadOnlyList<string> Gaps);

/// <summary>
/// Internal execution gate. It neither executes a scan nor changes a run/parent.
/// Receipt classifications come exclusively from the existing Core policy.
/// </summary>
internal static class WebFormsReviewInputValidation
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 32
    };

    internal static async Task<WebFormsReviewValidatedInputs> ValidateAsync(
        WebFormsReviewPreflightManifest preflight,
        CancellationToken cancellationToken = default)
    {
        await RecheckAsync(preflight, cancellationToken);
        var config = preflight.Configuration;
        var git = GitMetadataProvider.Detect(config.SourceRoot);
        if (git.CommitSha != config.SourceCommitSha || string.IsNullOrWhiteSpace(git.GitRootPath))
            throw Fail("SOURCE_IDENTITY_CHANGED");
        var budgets = config.Budgets;
        var options = new ScanOptions(config.SourceRoot, "unused-preflight-output",
            CompiledInputPaths: Paths("primary-assembly"),
            CompiledDependencyPaths: Paths("dependency-assembly"),
            CompiledBindingReceiptPaths: Paths("binding-receipt"),
            CompiledInputLimits: new(MaxArtifactCount: budgets.MaxInputFiles,
                MaxFileSizeBytes: budgets.MaxAssemblyBytes,
                MaxTextLength: budgets.MetadataMaxText,
                MaxTotalWorkUnits: budgets.MetadataMaxWork));
        var inspection = ManagedMetadataExtractor.InspectInputs(options, config.SourceCommitSha, cancellationToken);
        var compiled = inspection.Provenance
            ?? throw Fail("COMPILED_INPUTS_UNAVAILABLE");
        var gaps = new SortedSet<string>(inspection.GapKinds, StringComparer.Ordinal);
        foreach (var outcome in compiled.Outcomes)
            foreach (var gap in outcome.GapKinds) gaps.Add(gap);
        if (compiled.OmittedInputCount > 0) gaps.Add("LimitArtifactCountExceeded");
        if (compiled.CoverageState != "compiled-metadata-complete") gaps.Add("CompiledMetadataCoverageReduced");
        // These are distinct from receipt classification. Later scan phases
        // establish an actual source-byte snapshot and map/PDB reconciliation.
        gaps.Add("BuildAuthenticityNotEstablished");
        gaps.Add("SourceLineIdentityNotEstablished");
        ScanManifest? parent = null;
        long parentFacts = 0;
        if (config.ParentScanRoot is not null)
        {
            try { (parent, parentFacts) = await ValidateParentAsync(preflight, git, cancellationToken); }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 9 && cancellationToken.IsCancellationRequested)
            { throw new OperationCanceledException(cancellationToken); }
            gaps.Add("RetainedSourceSnapshotNotCurrentSourceValidation");
        }
        await RecheckAsync(preflight, cancellationToken);
        var gitAfter = GitMetadataProvider.Detect(config.SourceRoot);
        if (gitAfter.CommitSha != git.CommitSha || gitAfter.RemoteUrl != git.RemoteUrl ||
            gitAfter.GitRootPath != git.GitRootPath || gitAfter.ScanRootRelativePath != git.ScanRootRelativePath)
            throw Fail("SOURCE_IDENTITY_CHANGED");
        return new(preflight, compiled, parent, parentFacts,
            parent is null ? "current-source-snapshot-pending" : "retained-parent-identity-validated",
            gaps.ToArray());

        string[] Paths(string role) => preflight.Inputs.Where(input => input.Role == role).Select(input => input.Path).ToArray();
    }

    internal static async Task RecheckAsync(WebFormsReviewPreflightManifest preflight, CancellationToken token)
    {
        foreach (var input in preflight.Inputs)
        {
            token.ThrowIfCancellationRequested();
            if (WebFormsReviewPreflightCommand.PhysicalPath(input.Path) != input.Path) throw Fail("INPUT_LOCATOR_CHANGED");
            var observed = await WebFormsReviewPreflightCommand.HashAsync(input.Role, input.Path, input.Bytes, token);
            if (observed.Bytes != input.Bytes || observed.Sha256 != input.Sha256) throw Fail("INPUT_CHANGED");
        }
    }

    private static async Task<(ScanManifest Manifest, long Facts)> ValidateParentAsync(
        WebFormsReviewPreflightManifest preflight, GitMetadata git, CancellationToken token)
    {
        var config = preflight.Configuration;
        var index = Input("parent-index.sqlite");
        var manifestInput = Input("parent-scan-manifest.json");
        var factsInput = Input("parent-facts.ndjson");
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(manifestInput.Path, 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var manifest = JsonSerializer.Deserialize<ScanManifest>(bytes, JsonOptions) ?? throw Fail("PARENT_MANIFEST_INVALID");
        if (manifest.ScanId != preflight.ParentScanId || manifest.CommitSha != config.SourceCommitSha ||
            manifest.RepoName != git.RepoName || manifest.RemoteUrl != git.RemoteUrl ||
            manifest.ScanRootRelativePath != (string.IsNullOrEmpty(git.ScanRootRelativePath) ? "." : git.ScanRootRelativePath.Replace('\\', '/')) ||
            manifest.ScanRootPathHash != FactFactory.Hash(Path.GetFullPath(config.SourceRoot), 32) ||
            manifest.GitRootHash != FactFactory.Hash(Path.GetFullPath(git.GitRootPath!), 32) ||
            !IsDigest(manifest.SourceSnapshotDigest) || string.IsNullOrWhiteSpace(manifest.ScannerVersion))
            throw Fail("PARENT_IDENTITY_MISMATCH");
        RejectSidecars(index.Path);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            // Pinned, sidecar-free bytes are immutable for this inspection.
            // The URI flag also prevents read-only WAL databases from creating
            // shared-memory/WAL sidecars in the retained parent directory.
            DataSource = new Uri(index.Path).AbsoluteUri + "?immutable=1",
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        connection.Open();
        using var interrupt = InterruptOnCancellation(connection, token);
        token.ThrowIfCancellationRequested();
        using (var cardinality = connection.CreateCommand())
        {
            cardinality.CommandText = "select count(*) from scan_manifest";
            if ((long)cardinality.ExecuteScalar()! != 1) throw Fail("PARENT_INDEX_MANIFEST_MISMATCH");
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "pragma integrity_check";
            using var result = command.ExecuteReader();
            if (!result.Read() || result.GetString(0) != "ok" || result.Read()) throw Fail("PARENT_INDEX_INTEGRITY");
        }
        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select scan_id, repo, commit_sha, scanner_version, analysis_level, build_status, scanned_at, manifest_json
                from scan_manifest
                where length(cast(manifest_json as blob)) <= 4194304
                  and length(scan_id) <= 4096 and length(repo) <= 4096
                  and length(commit_sha) <= 64 and length(scanner_version) <= 4096
                  and length(analysis_level) <= 4096 and length(build_status) <= 4096
                  and length(scanned_at) <= 64
                """;
            using var result = command.ExecuteReader();
            if (!result.Read() || result.GetString(0) != manifest.ScanId || result.GetString(1) != manifest.RepoName ||
                result.GetString(2) != manifest.CommitSha || result.GetString(3) != manifest.ScannerVersion ||
                result.GetString(4) != manifest.AnalysisLevel || result.GetString(5) != manifest.BuildStatus ||
                result.GetString(6) != manifest.ScannedAt.ToString("O"))
                throw Fail("PARENT_INDEX_MANIFEST_MISMATCH");
            var embeddedBytes = Encoding.UTF8.GetBytes(result.GetString(7));
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(embeddedBytes);
            using var embedded = JsonDocument.Parse(embeddedBytes);
            using var retained = JsonDocument.Parse(bytes);
            if (!JsonElement.DeepEquals(embedded.RootElement, retained.RootElement) || result.Read())
                throw Fail("PARENT_INDEX_MANIFEST_MISMATCH");
        }
        long expectedFacts;
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "select count(*) from facts";
            expectedFacts = (long)command.ExecuteScalar()!;
            if (expectedFacts <= 0 || expectedFacts > config.Budgets.MaxParentFacts) throw Fail("PARENT_FACT_COUNT_LIMIT");
        }
        // Main remains read-only. SQLite's file-backed, bounded-cache temporary
        // table detects duplicate NDJSON IDs without a full managed-memory set.
        // Temporary rows contain IDs only and vanish when this connection closes.
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText = "pragma temp_store=FILE; pragma temp.cache_size=-2048; create temp table seen_fact_ids (fact_id text primary key) without rowid";
            setup.ExecuteNonQuery();
        }
        using var seen = connection.CreateCommand();
        seen.CommandText = "insert or ignore into temp.seen_fact_ids values ($id)";
        seen.Parameters.Add("$id", SqliteType.Text);
        using var lookup = connection.CreateCommand();
        lookup.CommandText = """
            select scan_id, repo, commit_sha, project_path, fact_type, rule_id,
                   evidence_tier, source_symbol, target_symbol, contract_element,
                   file_path, start_line, end_line, snippet_hash, extractor_id,
                   extractor_version, properties_json from facts where fact_id=$id
              and coalesce(length(scan_id),0)+coalesce(length(repo),0)+coalesce(length(commit_sha),0)
                +coalesce(length(project_path),0)+coalesce(length(fact_type),0)+coalesce(length(rule_id),0)
                +coalesce(length(evidence_tier),0)+coalesce(length(source_symbol),0)+coalesce(length(target_symbol),0)
                +coalesce(length(contract_element),0)+coalesce(length(file_path),0)+coalesce(length(snippet_hash),0)
                +coalesce(length(extractor_id),0)+coalesce(length(extractor_version),0)+coalesce(length(properties_json),0) <= $max_chars
            """;
        lookup.Parameters.Add("$id", SqliteType.Text);
        lookup.Parameters.AddWithValue("$max_chars", config.Budgets.MaxFactLineChars);
        long count = 0;
        await foreach (var line in ReadLinesAsync(factsInput, config.Budgets.MaxFactLineChars, token))
        {
            if (++count > config.Budgets.MaxParentFacts) throw Fail("PARENT_FACT_COUNT_LIMIT");
            var factBytes = Encoding.UTF8.GetBytes(line);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(factBytes);
            var fact = JsonSerializer.Deserialize<CodeFact>(factBytes, JsonOptions) ?? throw Fail("PARENT_FACT_INVALID");
            if (string.IsNullOrWhiteSpace(fact.FactId) || fact.ScanId != manifest.ScanId || fact.Repo != manifest.RepoName ||
                fact.CommitSha != manifest.CommitSha || string.IsNullOrWhiteSpace(fact.RuleId) || fact.Evidence is null ||
                fact.Properties is null || fact.EvidenceTier is not (EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural or
                    EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown)) throw Fail("PARENT_FACT_INVALID");
            seen.Parameters["$id"].Value = fact.FactId;
            if (seen.ExecuteNonQuery() != 1) throw Fail("PARENT_FACT_DUPLICATE");
            lookup.Parameters["$id"].Value = fact.FactId;
            using var row = lookup.ExecuteReader();
            if (!row.Read() || !SameFact(row, fact) || row.Read()) throw Fail("PARENT_FACT_INDEX_MISMATCH");
        }
        if (count != expectedFacts) throw Fail("PARENT_FACT_COUNT_MISMATCH");
        RejectSidecars(index.Path);
        return (manifest, count);

        WebFormsReviewInput Input(string role) => preflight.Inputs.Single(input => input.Role == role);
    }

    private static bool SameFact(SqliteDataReader row, CodeFact fact)
    {
        string? Text(int ordinal) => row.IsDBNull(ordinal) ? null : row.GetString(ordinal);
        var expected = new[] { fact.ScanId, fact.Repo, fact.CommitSha, fact.ProjectPath, fact.FactType,
            fact.RuleId, fact.EvidenceTier, fact.SourceSymbol, fact.TargetSymbol, fact.ContractElement,
            fact.Evidence.FilePath };
        for (var i = 0; i < expected.Length; i++) if (Text(i) != expected[i]) return false;
        if (row.GetInt32(11) != fact.Evidence.StartLine || row.GetInt32(12) != fact.Evidence.EndLine ||
            Text(13) != fact.Evidence.SnippetHash || Text(14) != fact.Evidence.ExtractorId || Text(15) != fact.Evidence.ExtractorVersion)
            return false;
        var bytes = Encoding.UTF8.GetBytes(row.GetString(16));
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(bytes, JsonOptions);
        return properties is not null && properties.Count == fact.Properties.Count &&
            properties.All(pair => fact.Properties.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(WebFormsReviewInput input, int maximumChars,
        [EnumeratorCancellation] CancellationToken token)
    {
        await using var stream = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65_536, FileOptions.SequentialScan | FileOptions.Asynchronous);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var buffer = new char[16_384];
        var line = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            if (stream.Length != input.Bytes) throw Fail("INPUT_CHANGED");
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] == '\n')
                {
                    if (line.Length > 0 && line[^1] == '\r') line.Length--;
                    if (line.Length == 0) throw Fail("PARENT_FACT_EMPTY_LINE");
                    yield return line.ToString();
                    line.Clear();
                }
                else
                {
                    if (line.Length >= maximumChars) throw Fail("PARENT_FACT_LINE_LIMIT");
                    line.Append(buffer[i]);
                }
            }
        }
        if (line.Length > 0)
        {
            if (line[^1] == '\r') line.Length--;
            if (line.Length == 0) throw Fail("PARENT_FACT_EMPTY_LINE");
            yield return line.ToString();
        }
    }

    private static bool IsDigest(string? value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static CancellationTokenRegistration InterruptOnCancellation(SqliteConnection connection, CancellationToken token) =>
        token.Register(static state =>
        {
            var database = (SqliteConnection)state!;
            if (database.Handle is { } handle) SQLitePCL.raw.sqlite3_interrupt(handle);
        }, connection);
    private static void RejectSidecars(string index)
    {
        if (new[] { "-wal", "-shm", "-journal" }.Any(suffix => File.Exists(index + suffix)))
            throw Fail("PARENT_INDEX_UNCHECKPOINTED");
    }
    private static InvalidOperationException Fail(string suffix) => new("WEBFORMS_REVIEW_" + suffix);
}
