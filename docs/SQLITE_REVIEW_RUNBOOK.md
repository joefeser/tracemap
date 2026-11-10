# Review a conversion question from a TraceMap index

For a paper review, print the [human evidence review worksheet](HUMAN_EVIDENCE_REVIEW.md).

Give the reviewer three things: the completed run's `START-HERE.md` (or scan manifest), its SQLite file, and one question such as “Where does the update text enter the wrapper?” The database here is TraceMap's local evidence index. An application database connection is unnecessary.

## Start with an existing run

Open `START-HERE.md` and select the source or compiled index for the question. Keep the existing scan, receipt and outputs together. For a single scan open `index.sqlite`; for a workspace-wide question open `combined.sqlite`. Use SQLite 3 with JSON functions. From PowerShell 7:

```powershell
sqlite3 -readonly $index
```

Set `$index` to the selected file's full path first. At the SQLite prompt use `.headers on`, `.mode column`, then `.tables`. In a **single scan** start here:

```sql
SELECT scan_id, repo, commit_sha, analysis_level, build_status FROM scan_manifest;
```

In a **combined index** list the original scans first:

```sql
SELECT source_index_id, label, scan_id, commit_sha, analysis_level, build_status
FROM index_sources ORDER BY label;
```

The next two recipes use a single scan. In a combined query use `combined_facts` and scope every join by `source_index_id`. IDs inside the original fact properties refer to `original_fact_id` within that source. A commit can have multiple scans.

### Where is there SQL worth reading?

```sql
SELECT fact_id, file_path, start_line AS call_line,
       json_extract(properties_json, '$.constructionStartLine') AS first_line,
       json_extract(properties_json, '$.constructionEndLine') AS last_line,
       json_extract(properties_json, '$.sqlOperation') AS operation,
       json_extract(properties_json, '$.commandInstanceBinding') AS binding,
       rule_id, evidence_tier, extractor_version
FROM facts WHERE fact_type = 'SqlTextArgumentCandidate'
ORDER BY file_path, start_line, fact_id LIMIT 50;
```

In the VB trial below, `Queries.vb` lines 3–5 construct an UPDATE passed on line 6; line 10 passes a SELECT. Both have `binding=unverified`. Read those lines in the matching checkout for the literal text. A source hit justifies reviewing the query for conversion. Establishing which command received it requires additional evidence. Zero rows means this rule yielded no hits in the selected scan; check its scope and coverage.

### Which click reaches a candidate handler, and why is it partial?

```sql
SELECT click.fact_id AS click_id, click.file_path AS page, click.start_line AS click_line,
       h.fact_id AS handler_id, h.file_path AS script, h.start_line AS method_line,
       g.fact_id AS gap_id, json_extract(g.properties_json, '$.gapKind') AS reason
FROM facts AS h
JOIN facts AS click ON click.scan_id = h.scan_id
 AND click.fact_id = json_extract(h.properties_json, '$.clickFactId')
JOIN json_each('[' || replace(json_quote(json_extract(h.properties_json, '$.supportingFactIds')),
                              ',', '","') || ']') AS support
JOIN facts AS g ON g.fact_id = support.value AND g.scan_id = h.scan_id
 AND g.fact_type = 'AnalysisGap'
WHERE h.fact_type = 'WebFormsAngularPartialClickHandlerCandidate'
ORDER BY page, click_line, handler_id, reason LIMIT 50;
```

The WebForms trial yields four rows. Each of two clicks points to the same method and carries two reasons: `AngularDynamicScriptSourceUnavailable` and `AngularThenCallbackInvocationUnobserved`. Follow the [service POST query](../samples/webforms-service-post/README.md) for the exact call, request and handler declaration IDs. To inspect a named fact, bind its ID to `@id` (SQLite shell: `.parameter set @id fact-…`) and run:

```sql
SELECT scan_id, commit_sha, fact_id, fact_type, rule_id, evidence_tier,
       file_path, start_line, end_line, extractor_version, properties_json
FROM facts WHERE fact_id = @id;
```

## Rebuild the reports and JSON

Run these commands from the TraceMap checkout in PowerShell 7. Build the reporting tool once. On a retained private run, use the matching tool revision and the paths printed by that run. Pick a fresh output directory outside the checkout.

```powershell
pwsh -NoProfile -File ./scripts/tm.ps1 -Build version
$cli = (Resolve-Path ./artifacts/workspace-cli/Release/tracemap.dll).Path
$out = Join-Path ([IO.Path]::GetTempPath()) ("tracemap-review-" + [guid]::NewGuid())
New-Item -ItemType Directory -Path $out | Out-Null
```

**Public trial only.** These two small source scans create the example indexes. For an existing run skip this block and set `$web` and `$vb` to its corresponding single-scan files and `$combined` to its combined index. Keep unlike runs separate.

```powershell
dotnet $cli scan --repo ./samples/webforms-service-post --webforms-iis-mount '/Portal/Desk=.' --out "$out/web"
dotnet $cli scan --repo ./samples/vb-sql-argument --out "$out/vb"
$web = "$out/web/index.sqlite"
$vb = "$out/vb/index.sqlite"
$combined = "$out/combined.sqlite"
dotnet $cli combine --index $web --label web --index $vb --label vb --out $combined
```

The original `report.md`, `scan-manifest.json`, `facts.ndjson` and analyzer log are **scan outputs**. Open them in the retained scan directory. The commands below generate fresh **report projections** from the saved indexes. An export JSON has a different format from the original NDJSON.

| Run in PowerShell | Open afterward |
|---|---|
| `dotnet $cli report --index $combined --out "$out/dependency"` | `dependency/dependency-report.md` and `dependency-report.json` |
| `dotnet $cli export --index $web --out "$out/index-export.json" --format json` | `index-export.json`, including facts and relationships |
| `dotnet $cli webforms-modernization --index $web --out "$out/packet"` | `packet/webforms-modernization.md` and `webforms-modernization.json` |
| `dotnet $cli docs-export --index $vb --out "$out/evidence-docs" --format markdown,jsonl` | `evidence-docs/index.md`, `QUERY_RECIPES.md`, `chunks.jsonl` and `manifest.json` |
| `dotnet $cli explorer generate --input "$out/vb" --out "$out/explorer"` | `explorer/index.html` in a browser (public synthetic scan directory) |

To find candidate root selectors in a combined index, bind `@source` to the source label and `@name` to the exact display name. Inspect the returned identities before choosing a caller. For a public walkthrough use `web` and `Sample.SaveHandler`; this fixture exercises the report writer and has no proven SQL path from that handler.

```sql
SELECT s.original_symbol_id, s.display_name, s.symbol_kind, i.source_index_id, i.commit_sha
FROM combined_symbols s JOIN index_sources i ON i.source_index_id = s.source_index_id
WHERE i.label = @source AND s.display_name = @name
ORDER BY s.original_symbol_id LIMIT 25;
```

For the **original selected-caller SQL wrapper report**, supply the combined index and the exact caller selector from your original command or saved report. Repeat `--root` for each original caller. In PowerShell, set `$caller` to the full selector first:

```powershell
python ./scripts/sql-wrapper-report.py --index $combined --cli $cli --root $caller --out "$out/sql-wrapper"
```

Read `sql-wrapper/sql-wrapper-inventory.local.md` and `sql-wrapper/root-001/paths-report.json` together. Reproducing a historic report requires the same source indexes, tool, selectors and query bounds. Preserve an empty result, timeout or explicit gap in the review.

For a visual review of the trial, open `explorer/index.html` and the generated Markdown side by side. Keep private reports in the authorized workspace. The VB fixture passes docs export; the WebForms service fixture can produce `UnsafeValueRejected` on its legacy projection. In that case use the local packet or read-only index and record the refusal. The [docs-export guide](EVIDENCE_DOCS_EXPORT.md) describes the publication profiles.

## Paste into Claude

> Use the supplied completed TraceMap index read-only and the matching authorized source checkout. Investigate one conversion question using the recipes above. Record the scan or source-index identity, commit, query, and supporting fact IDs. For each retained hop cite its rule, tier, extractor version and source span or IL location. Show the first unresolved hop and its recorded reason.
>
> If the source supplies additional evidence, write it in a separate local review note keyed to that fact or gap ID. Identify the file and lines, what they establish, and what would verify the remaining uncertainty. Finish with what the conversion can use now and one concrete acceptance test. Preserve the original index and facts. Use the existing run only; the task requires no application execution, deployment or database connection.

For complete collection and operator workflows see the [workspace guide](WORKSPACE_GETTING_STARTED.md), [compiled and SQL guide](WORKSPACE_SQL_GETTING_STARTED.md), and [WebForms agent handoff](WEBFORMS_AGENT_HANDOFF.md).
