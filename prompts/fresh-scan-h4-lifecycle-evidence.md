# Fresh isolated H4 lifecycle evidence check

The operator has authorized one fresh scan with the original accepted scope and
inputs. This is NOT another query over the old index: that index lacks H4 lifecycle
candidate facts. Preserve every accepted H1/H2/H3/H4 and database artifact and note
byte-for-byte. The reporting fix now preserves entry inventories with legacy roots.

## Bounds and safety

Budget: 30 minutes, one CLI build, one fresh scan, one completed-report generation
and one H4 HTTP requery. Stop at the limit and retain partial diagnostics. No source
edits, dependency installation, application execution, HTTP requests, database-server
access, publication, Git mutation or new helper. Use existing approved local tools.
No widened include scope, references, path limits or work budgets. Do not run new
repository setup scripts or application builds; only build the TraceMap CLI.

Keep private configuration, selectors and outputs outside Git, outside original
evidence and in a new isolated directory. Do not overwrite or move old artifacts.
Do not manufacture provenance attestations, compiled context or missing inputs.
Stop and ask if the recorded workflow would require actions beyond these bounds.

## Prepare the same denominator

1. Read tool branch, full HEAD and dirty state live from the operator-prepared
   codex/http-evidence-regressions checkout. Stop on build-affecting tracked changes.
   Record original source commit, scope, references, extractor versions and retained
   artifact hashes locally from the accepted inventory and manifests. Verify source
   checkout identity and inputs match. If not, stop with INPUT DRIFT; don't checkout,
   repair or substitute another commit. Tool revision is intentionally newer.
2. Copy the original validated private configuration into the new directory. Preserve
   source/include/exclude/reference/compiled-input and budget settings; change only
   output-location fields needed for isolation. Use fresh mode only if that was the
   original mode. If the old config is attach-only, incomplete or incompatible, stop
   for operator direction rather than silently converting it. Inventory original
   artifacts' hashes so their immutability can be checked afterward.
3. Build the CLI once:

   ```text
   dotnet build src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj --nologo -c Debug
   ```

   Record build exit/warnings, full HEAD and hash of the actual invoked tracemap.dll.
   Check HEAD and binary unchanged after the workflow. Match native generator hashes
   to their corresponding generator assemblies; never equate a Git ID with a SHA-256.

## Scan once, inspect and report

Use the native workflow with the copied config and new run directory:

```text
dotnet <built-tracemap.dll> webforms-review preflight --config <new-private-config> --out <new-run>
dotnet <built-tracemap.dll> webforms-review run --run <new-run>
```

Inspect status/receipts before any continuation. If scanning completed and reports
are pending, one native resume for report generation is allowed only when status
confirms it will not repeat scanning. Stop on a failed scan, unavailable inputs or
unexpected stages and report the exact boundary. A successfully completed scan
with reduced coverage may proceed with all gaps preserved; never label it clean.

Inspect the fresh retained index read-only for the exact H4 method declaration,
override relationship and framework witness. Resolve identities from facts, not the
method name. Look for WebFormsEntryRelationship / legacy.webforms.entry-lifecycle.v1
with relationshipKind framework-lifecycle-candidate, scoped supporting override and
declaration facts, rule/tier/location and unresolved boundary. Compare with the old
index's absence. Different lifecycle branch/request fact types are not substitutes.

If the candidate is absent, record whether the override resolves directly to a
supported signed System.Web method, to an intermediate application override, to an
unresolved/missing reference, or to a same-named nonframework method. Use retained
evidence only; report unknown if the boundary cannot be established. Do not loosen
identity checks, add a candidate manually or rescan again.

After native report completion, select the exact fresh H4 MethodDeclared using a
private JSON sourceIndexId/scanId/commitSha/symbolId tuple from the NEW index. Do not
reuse old source-index or scan IDs. Requery once:

```text
dotnet <built-tracemap.dll> webforms-review requery-handler --run <new-run> --bundle <new-verified-bundle> --source-root <new-H4-selector.json> --out <new-query-output> --surface http-client --max-depth <original-recorded-depth>
```

Verify the expected H4 terminal, ordered calls, supporting facts, rule/tier/location,
receipt hashes and unchanged limits. Check `header.webFormsEntries` in the handoff
and the HTML's separate entry-relationships section. Match candidate to H4 by exact
source-scoped identity. Candidate evidence must not become a traversed CallEdge or
upgrade path tiers. Preserve truncation and actual cutoff reasons. Missing output
under bounds does not prove no path; absence of a candidate does not prove no runtime
dispatch. Candidate presence proves only a structural candidate, not execution.

## Return

Write a new private note. Verify old baseline artifact hashes stayed unchanged.
Return only public tool SHA, build result, input match yes/no, fresh scan status,
extractor versions, candidate present old/new, candidate tier, candidate visible
JSON/HTML, H4 method-to-HTTP path present, path tier, truncation, categorized first
boundary and elapsed time. Verdict: FRESH CANDIDATE RETAINED; DISPATCH UNPROVEN /
CANDIDATE STILL ABSENT / REPORTING GAP / INPUT DRIFT / PARTIAL ANALYSIS / TIME LIMIT.
Keep private names, source, paths, hashes, references, URLs and payloads local. Do
not declare H4 runtime entry proven or all HTTP coverage exhaustive.
