# Implementation state

Branch: `codex/workspace-scope-preflight`, based on the clean testing head
`36c1d94c9`. This work uses the existing `sql-budget-finalize/tracemap` checkout;
the owning ArrayList chat was idle at inspection. No unrelated changes were present.

Scope: bounded regular script-bundle discovery, not JavaScript execution or
end-to-end browser-to-SQL reachability. The .NET inventory does not currently
include standalone .js files. We retain explicit literal membership independently
of file existence. C#/VB object-creation/include syntax and markup Scripts.Render
output blocks carry Tier3 candidates; unsupported forms carry Tier4 gaps.
Members refer to an exact declaration fact; virtual paths across separate files
are deliberately not collapsed into a shared endpoint identity. Reference resolution
is deferred rather than joining same-named application paths or implying startup ran.

Bounds: 1 MiB inventory byte size / 1,048,576 decoded characters on actual read;
256 declarations or server output blocks per file; 128 arguments per declaration
or render call; 1,024 characters per admitted virtual path. Dynamic expressions,
CDN constructor overloads, wildcard/version placeholders, IncludeDirectory and
unsupported chained methods refuse the entire declaration. Whole-file syntax
diagnostics refuse its bundle declarations. Existing same-named custom framework
symbols may produce syntax candidates; framework symbol identity is not claimed.
No source body is retained: evidence carries a hash of decoded source text.

Initial validation: Core Debug -warnaserror build passed, 0 warnings/errors;
focused bundle tests passed 13/13. Final focused validation is recorded below after identity/bound
hardening. Full Debug attempt, synthetic CLI smoke and explicit pinned-OSS deferrals are
recorded below. No PR, merge, private scan or customer execution is part of this
local testing-branch change.

## Validation commands and observations

```sh
dotnet build src/dotnet/TraceMap.Core -c Debug -warnaserror --nologo --no-restore
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo --filter FullyQualifiedName~WebFormsScriptBundleTests
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo --filter 'FullyQualifiedName~WebFormsScriptBundleTests|FullyQualifiedName~LegacyWebFormsExtractorTests'
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo --filter 'FullyQualifiedName!~TraceMap.Tests.SelfDiagnosticsTests'
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo samples/webforms-script-bundles --out /tmp/tracemap-bundles-smoke-final-20261106
```

Focused bundle cases: 16 passed, 0 failed/skipped. The earlier combined run had
64 passed and 2 failures from hard-coded old extractor-version expectations;
both were updated to 0.15.0. The first full run was interrupted to reuse a single
bounded read buffer across input files rather than allocating it per file. The
replacement full run was cancelled after over 40 minutes; its single reported
regex guard failure was repaired and rechecked in the final focused run below.

Final CLI smoke: 45 total facts, including exactly 1 declaration, 2 members and
1 reference under the new rule in both NDJSON and SQLite. All new facts retained
Tier3 and version 0.15.0; IDs were unique. SQLite integrity_check returned ok and
all five required outputs existed. The projectless sample reports
Level3SyntaxAnalysis / NotRun and retains workspace diagnostics; this is not a
successful project build or runtime trace. A first ad hoc verifier looked for a
nonexistent coverageLevel key; corrected to the actual analysisLevel/buildStatus
fields, without changing scanner output.

Pinned C#/VB source OSS smoke reruns, Windows runtime bundle rendering and
private application scans are explicitly deferred. This change does not extend
semantic/compiler/IL behavior; no Angular or complete external-JS call tracing
claim is made. Local results validate the uncommitted testing-branch patch.


## Final local handoff

The full-suite attempt was cancelled after over 40 minutes without completion.
It reported one failure in the repository's bounded static-regex budget guard
and two skips (native HTTP receipt and optional full generated-golden sweep).
There is no complete pass count and no full-suite-green claim. The static call
now uses RegexBudget.LoadTolerant; the generated output-block regex has a
2-second bound. No regex guard was weakened.

Final repair validation:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --nologo --filter 'FullyQualifiedName~WebFormsScriptBundleTests|FullyQualifiedName~LegacyWebFormsExtractorTests|FullyQualifiedName~EvidenceDocsExportTests'
```

95 passed, 0 failed/skipped, 17 seconds; build completed without warnings/errors.
The separately isolated SelfDiagnosticsTests run passed 4/4 on the pre-repair
binary (before the regex-budget-only correction); it is not a final-binary full
suite result. The final repaired CLI smoke used output
`/tmp/tracemap-bundles-smoke-repaired-20261106` and passed the same four-fact,
NDJSON/SQLite parity, integrity, tier/version and five-required-output checks.
Tracked diff and new-file whitespace checks passed.

Changes remain uncommitted on the requested existing testing branch. No PR or
push was performed. Full Debug validation remains required before PR/merge.
Next functional work is application-scoped bundle reference resolution and
external JavaScript analysis; the browser-click-to-backend trace is not closed.
