# Collect a focused Web Forms gap and extractor summary

Use this prompt only after a focused three-folder Web Forms local review has
completed and its private output remains on the restricted workstation. The
checked-in PowerShell utility summarizes the retained `facts.ndjson`; it does
not rerun TraceMap and does not require an agent to interpret raw facts.

## Direct Windows commands — no agent required

Run these commands in PowerShell 7. They fetch the current utility, show only
the locally retained output folder names, run the deterministic summary once,
and identify the resulting sanitized file.

```powershell
Set-Location C:\work\tracemap

if (git status --porcelain) {
    throw "TRACEMAP_WORKTREE_DIRTY"
}

git fetch origin dev
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_FETCH_FAILED" }

git switch --detach origin/dev
if ($LASTEXITCODE -ne 0) { throw "TRACEMAP_CHECKOUT_FAILED" }

git rev-parse --short HEAD

$Candidates = @(
    Get-ChildItem "C:\work\tracemap-output" -Directory -Filter "focused-webforms-*" |
        Sort-Object LastWriteTime -Descending
)

$Candidates | Select-Object LastWriteTime, Name
```

Select the completed focused run from that local list. Set only its folder name
below; do not paste the name into chat or commit it:

```powershell
$CompletedRunFolderName = "<select-one-focused-webforms-folder-name>"
$OutRoot = Join-Path "C:\work\tracemap-output" $CompletedRunFolderName

if (-not (Test-Path -LiteralPath $OutRoot -PathType Container)) {
    throw "FOCUSED_WEBFORMS_RETAINED_OUTPUT_UNAVAILABLE"
}

pwsh -NoProfile -File .\scripts\Export-FocusedWebFormsEvidenceSummary.ps1 `
    -ReviewOutputPath $OutRoot

if ($LASTEXITCODE -ne 0) { throw "FOCUSED_WEBFORMS_SUMMARY_FAILED" }

Get-ChildItem "C:\work\tracemap-summary" -File `
    -Filter "focused-webforms-gap-extractor-*.txt" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1 LastWriteTime, Name
```

Bring back only that newest small text file. Do not bring back the retained
review output, progress checkpoint, facts, index, report, manifest, logs, or
any terminal transcript. This workflow performs no scan and requires no Bedrock
or other model call after the commands are available locally.

## Agent prompt

```text
Run the deterministic command documented in
prompts/collect-focused-webforms-gap-extractor-summary.md exactly.

Use only the retained output from the most recent completed focused Web Forms
review. Do not rerun the scan. Ask me locally to select the output directory if
more than one candidate exists.

Return only the utility's three categorical completion lines. Keep
the full output and every private identifier on this workstation. Do not modify
either repository, commit, push, publish, upload, use network services, or
return raw facts, reports, logs, indexes, paths, source, configuration, routes,
symbols, project names, repository identity, commit SHA, scan ID, or hashes.
```

## Boundary and input selection

The selected directory must be an existing focused local-review output outside
the source and TraceMap repositories. It must contain:

- `local-review-result.json`;
- `scan/facts.ndjson`;
- `scan/scan-manifest.json`.

Use the already known output path when available. Otherwise enumerate only:

```powershell
Get-ChildItem "C:\work\tracemap-output" -Directory -Filter "focused-webforms-*" |
    Sort-Object LastWriteTime -Descending |
    Select-Object LastWriteTime, Name
```

If exactly one candidate is not unambiguously authorized, ask the owner to
select it locally. Do not infer the path by searching other drives or folders.
Store the selected path in `$OutRoot` locally and never return it.

## Deterministic command

From the TraceMap checkout, run:

```powershell
pwsh -NoProfile -File .\scripts\Export-FocusedWebFormsEvidenceSummary.ps1 `
    -ReviewOutputPath $OutRoot
```

Do not ask an agent to parse or summarize `facts.ndjson`. The utility validates
the retained result, reads facts incrementally, checks rule IDs against the
checked-in catalog, bounds extractor identities, and writes the sanitized file.
It prints the generated filename without printing the private input path.

Verify the expected artifacts without printing their paths or contents:

```powershell
$Required = @(
    (Join-Path $OutRoot "local-review-result.json"),
    (Join-Path $OutRoot "scan\facts.ndjson"),
    (Join-Path $OutRoot "scan\scan-manifest.json")
)

if (@($Required | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -ne 0) {
    throw "FOCUSED_WEBFORMS_RETAINED_OUTPUT_INCOMPLETE"
}
```

## Utility aggregation rules

Read `scan/facts.ndjson` one line at a time. Do not copy it, serialize selected
facts, quote properties, or print parse errors. Aggregate only:

- total fact count;
- total `AnalysisGap` fact count;
- counts of `AnalysisGap` facts by catalogued TraceMap rule ID;
- counts of catalogued gap rules by bounded `classification` or `gapKind`;
- total facts and `AnalysisGap` facts by bounded extractor ID and version.

Before returning a rule ID, require that it appears in the checked-out
TraceMap `rules/rule-catalog.yml`. Aggregate absent or non-catalogued rule IDs
only as `uncataloguedGapRuleIdCount`; never return their values.

Before returning an extractor ID, require it to match
`^[A-Za-z0-9][A-Za-z0-9._-]{0,79}$`. A version may use the same bounded token
or one tool/version pair separated by a single `/`; each side remains bounded
to alphanumeric, dot, underscore, plus, or hyphen characters. Aggregate
rejected or missing identities only as
`unavailableExtractorIdentityFactCount`; never return their values.

Sort rule rows by descending count and then ordinal rule ID. Sort extractor
rows by descending fact count and then ordinal extractor ID and version. Return
at most ten rows of each. Counts are exact over the retained facts file; the
top-ten lists are bounded projections.

Do not use rule IDs, extractor identities, fact types, or counts to infer
business intent, runtime behavior, correctness, migration readiness, security,
feature completeness, or source ownership. A large gap count may contain
expected repeated reduced-coverage evidence; this summary does not classify it
as a defect.

## Sanitized file contract

The utility constructs exactly this block:

```text
focused-webforms-evidence-summary=<completed|boundary-stop>
failureCode=<none|retained-output-incomplete|result-not-completed|facts-parse-failed|catalog-unavailable>
factTotal=<nonnegative-count-or-unavailable>
analysisGapTotal=<nonnegative-count-or-unavailable>
cataloguedGapRuleKinds=<nonnegative-count-or-unavailable>
uncataloguedGapRuleIdCount=<nonnegative-count-or-unavailable>
gapReasonKinds=<nonnegative-count-or-unavailable>
unavailableGapReasonCount=<nonnegative-count-or-unavailable>
extractorKinds=<nonnegative-count-or-unavailable>
unavailableExtractorIdentityFactCount=<nonnegative-count-or-unavailable>
topGapRule01=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule02=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule03=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule04=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule05=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule06=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule07=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule08=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule09=<catalogued-rule-id>|count=<count-or-unavailable>
topGapRule10=<catalogued-rule-id>|count=<count-or-unavailable>
topGapReason01=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason02=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason03=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason04=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason05=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason06=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason07=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason08=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason09=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topGapReason10=<catalogued-rule-id>|field=<classification|gapKind>|reason=<bounded-token>|count=<count-or-unavailable>
topExtractor01=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor02=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor03=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor04=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor05=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor06=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor07=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor08=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor09=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
topExtractor10=<extractor-id>|version=<extractor-version-or-unavailable>|facts=<count-or-unavailable>|gaps=<count-or-unavailable>
```

Use `unavailable` for unused top-ten rows. A boundary stop returns all count and
row fields as `unavailable`. Before responding, verify that every non-count
value is one of the fixed categorical values, a catalogued rule ID, or a
validated extractor ID/version. Remove any extra prose.

## Persisted summary

The utility writes the block and no other content to a new local text file
outside both repositories and outside the immutable review output. Its default
directory is:

```powershell
C:\work\tracemap-summary
```

The utility writes UTF-8 text and verifies every line. The file contains no
heading, prose, source value, path, project/repository identity, filename, symbol, hash, commit SHA,
scan ID, route, configuration, log text, raw JSON, or raw fact.

The utility returns only:

```text
focused-webforms-evidence-summary-file=created
summaryDirectory=tracemap-summary
summaryFile=focused-webforms-gap-extractor-<timestamp>.txt
```

If safe persistence fails, return
`focused-webforms-evidence-summary-file=boundary-stop` and no path or raw
content. Do not modify or delete the retained scan output.
