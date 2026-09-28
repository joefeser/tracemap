using System.Security.Cryptography;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Cli;

internal delegate Task WebFormsReviewAttachmentRunner(WebFormsReviewPreflightManifest plan,
    ScanManifest parent, string output, CancellationToken token);

internal static class WebFormsReviewAttachmentExecution
{
    internal static ScanOptions Options(WebFormsReviewPreflightManifest plan, string output)
    {
        var config = plan.Configuration; var budget = config.Budgets;
        return new(config.SourceRoot, output, CompiledInputPaths: Paths("primary-assembly"),
            CompiledDependencyPaths: Paths("dependency-assembly"), CompiledBindingReceiptPaths: Paths("binding-receipt"),
            CompiledInputLimits: new(MaxArtifactCount: budget.MaxInputFiles, MaxFileSizeBytes: budget.MaxAssemblyBytes,
                MaxTextLength: budget.MetadataMaxText, MaxTotalWorkUnits: budget.MetadataMaxWork),
            PdbInputPaths: Paths("pdb"), PdbInputLimits: new(MaxArtifactCount: budget.MaxInputFiles,
                MaxFileSizeBytes: budget.MaxAssemblyBytes), IlBodyEvidence: true,
            IlBodyLimits: new(MaxTextLength: budget.IlMaxText, MaxTotalWorkUnits: budget.IlMaxWork),
            WebFormsPublishReceiptPath: plan.Inputs.SingleOrDefault(item => item.Role == "publish-receipt")?.Path,
            WebFormsPublishedRootPath: config.PublishedRoot);
        string[] Paths(string role) => plan.Inputs.Where(item => item.Role == role)
            .OrderBy(item => item.Path, StringComparer.Ordinal).Select(item => item.Path).ToArray();
    }

    internal static async Task WriteAsync(WebFormsReviewPreflightManifest plan, ScanManifest parent,
        string output, CancellationToken token)
    {
        if (Directory.Exists(output) || File.Exists(output)) throw Fail("OUTPUT_ALREADY_EXISTS");
        var roster = await WebFormsReviewInputValidation.ReadRetainedInventoryAsync(plan, parent, token);
        var context = new CompiledAttachmentParent(parent,
            plan.Inputs.Single(item => item.Role == "parent-scan-manifest.json").Sha256,
            plan.Inputs.Single(item => item.Role == "parent-index.sqlite").Sha256);
        var result = CompiledAttachmentProducer.Create(context, Options(plan, output), roster.Read,
            roster.MaxFiles, roster.MaxBytes, token);
        CompiledAttachmentProducer.ValidateContext(result.Manifest);
        await ScanOutputTransaction.WriteAsync(output, plan.Configuration.SourceRoot, async staging =>
        {
            token.ThrowIfCancellationRequested();
            await ManifestWriter.WriteAsync(Path.Combine(staging, "scan-manifest.json"), result.Manifest, token);
            await JsonlFactWriter.WriteAsync(Path.Combine(staging, "facts.ndjson"), result.Facts, token);
            token.ThrowIfCancellationRequested();
            SqliteIndexWriter.Write(Path.Combine(staging, "index.sqlite"), result.Manifest, result.Facts);
            token.ThrowIfCancellationRequested();
            await MarkdownReportWriter.WriteAsync(Path.Combine(staging, "report.md"), result, token);
            Directory.CreateDirectory(Path.Combine(staging, "logs"));
            await File.WriteAllTextAsync(Path.Combine(staging, "logs", "analyzer.log"),
                "native-compiled-only-attachment;local-only;review-only;source-analysis-not-run;build-not-run;cross-index-joins-pending\n", token);
        });
    }

    internal static async Task ValidateDerivedAsync(WebFormsReviewPreflightManifest plan,
        WebFormsReviewValidatedInputs inputs, ScanManifest derived, CancellationToken token)
    {
        var parent = inputs.ParentManifest ?? throw Fail("PARENT_UNAVAILABLE");
        CompiledAttachmentProducer.ValidateContext(derived);
        var context = derived.CompiledAttachment!;
        var roster = await WebFormsReviewInputValidation.ReadRetainedInventoryAsync(plan, parent, token);
        using var generator = File.OpenRead(typeof(CompiledAttachmentProducer).Assembly.Location);
        var generatorSha = Convert.ToHexString(SHA256.HashData(generator)).ToLowerInvariant();
        if (context.ParentScanId != parent.ScanId || context.ParentSourceSnapshotDigest != parent.SourceSnapshotDigest
            || context.ParentManifestSha256 != plan.Inputs.Single(item => item.Role == "parent-scan-manifest.json").Sha256
            || context.ParentIndexSha256 != plan.Inputs.Single(item => item.Role == "parent-index.sqlite").Sha256
            || context.GeneratorSha256 != generatorSha || context.MaxSourceFiles != roster.MaxFiles
            || context.MaxSourceBytes != roster.MaxBytes || derived.SourceMetadataReconciliation is not null
            || derived.CompiledInputProvenance?.BoundedInputSha256 != inputs.CompiledProvenance.BoundedInputSha256)
            throw Fail("PARENT_CONTEXT_MISMATCH");
    }

    private static InvalidOperationException Fail(string code) => new("WEBFORMS_ATTACHMENT_" + code);
}
