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

Implementation committed at `1d123c325b46300e08266e7a78bac06ef076bda2` on
`codex/workspace-scope-preflight`. The Claude verification dispatch is
`prompts/verify-webforms-script-bundle-discovery-once.md`. Full Debug validation remains required before PR/merge.
Next functional work is application-scoped bundle reference resolution and
external JavaScript analysis; the browser-click-to-backend trace is not closed.

## VB application startup continuation

Testing branch `codex/workspace-scope-preflight`, previous head `641660422`.
Version 0.16.0 reads an explicit VB inline server block in an ASAX file and maps
its syntax nodes back to the physical source lines and offsets. A direct method
local initialized by `As New` or `= New` can supply several standalone literal
Include calls. Match receivers case-insensitively inside their declaring method,
preserve per-local ordinal order, and refuse aliases, reassignment, duplicate
names, conditional uses, unsupported uses and excessive member counts. Refusals
discard the affected declaration's staged members. A malformed or unsupported
inline application containing bundle syntax produces a Tier4 gap.

Five initial red regressions reproduced the missing ASAX facts and gaps.
The final focused Debug gate with warnings as errors passed 109/109 (bundle,
legacy WebForms, evidence export and diagnostics). During hardening the regex
inventory guard caught the bounded regexes generated as static fields. They now
use the existing shared budget through the static API and that guard passes.
The first broad run was interrupted early to harden parsing of malformed script
blocks. The broad final-binary result is recorded separately below.

The public CLI control is `samples/webforms-script-bundle-startup`. Its exact
three membership rows and physical lines are documented in the fixture README.
Use `dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo
samples/webforms-script-bundle-startup --out <fresh-directory>` after a Debug
build. This checks portable syntax evidence only. Separate application-scoped
render resolution, external JavaScript tracing, IIS execution and private
verification remain open. Pinned C#/VB OSS smokes are deferred for this
syntax-only ASAX change.

ASAX markup hardening at `fff0b0967f3757b8b5097d116019c2a548e8b3f3` rejects
lookalike tag/directive names, malformed or duplicate attributes and conflicting
application language. Server comments preserve physical offsets and use the
shared load-tolerant regex budget. Final focused bundle, WebForms and evidence
export tests passed 110/110 in Debug with warnings treated as errors.

An intermediate broad run encountered three generated compilation-input snapshot
mismatches while the branch advanced to the verification prompt. After rebuilding
on a fixed head, the affected seven parameterized cases passed. The subsequent
partial broad run was stopped to harden tag admission. Neither partial run is a
complete validation result. Full final validation uses a dedicated checkout of
the pinned verification head to keep the source revision fixed.


## Final verification on 2026-11-07

Published implementation `fff0b0967f3757b8b5097d116019c2a548e8b3f3`.
Verified its documentation-only descendant `d39d533a0ed704606f6330ddc171636dbcc1c9e4`
in a clean detached checkout.

```sh
dotnet build src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --nologo
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --no-build --filter 'FullyQualifiedName~TraceMap.Tests.SelfDiagnosticsTests'
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --no-build --filter 'FullyQualifiedName!~TraceMap.Tests.SelfDiagnosticsTests' --logger 'console;verbosity=normal'
```

Build completed with zero warnings and errors. The main run passed 6,163, skipped
3 and failed 0 across 6,166 cases in 43.9372 minutes. Isolated diagnostics passed
4/4. Combined coverage was 6,167 passed, 3 skipped, 0 failed across 6,170 cases.
The skips were the native HTTP receipt, optional full generated-golden sweep,
and deep projectless Windows ASP.NET publish. All three snapshot cases seen in
the earlier mutable checkout passed within this full run.

The focused WebForms bundle, legacy WebForms and evidence export filter passed
110/110 on the hardened code. Both public CLI fixtures passed again using the
pinned build. Verified all five required outputs, SQLite integrity, identical
NDJSON/SQLite bundle fact IDs, commit, Tier3 and extractor version. The original
control emitted four bundle facts. Startup emitted seven, including the three
expected member-to-declaration joins with ordinals 0, 0, 1 on ASAX lines 7, 8, 9.
Private work-machine verification remains with the owner via
`prompts/verify-vb-asax-bundle-locals-once.md`.

## AngularJS included-handler continuation

Branch `codex/workspace-scope-preflight`, based on `ea4d8ae67`. New rule
`legacy.webforms.angularjs-click.v1` uses extractor `legacy-webforms/0.17.0`.
The selected WebForms inventory now fingerprints admitted .js sources. The
extractor parses referenced files for click-bearing markup. Source filters and
directory exclusions continue to apply. This expands the source snapshot of a
WebForms scan, including its exact-source roster.

The new public website `samples/webforms-angularjs-click` exercises a master
content slot, a named app on the master, a controller on the page and a VB bundle
with a module alias and two controllers that both assign save. A retained
handler must point to the external assignment in editor.js. Includes, markup
scope, JS module and registration, and the click site carry supporting fact IDs,
endpoints, spans, commit, extractor version and producer/input digests. Acornima
1.9.1 parses classic JavaScript. AngleSharp 1.10.0 supplies the HTML tree and
positions with script execution disabled. The rule catalog records supported
forms and resource bounds.

The focused gate passed 150/150, zero skips and failures:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter 'FullyQualifiedName~WebFormsAngularClickTests|FullyQualifiedName~WebFormsScriptBundleTests|FullyQualifiedName~LegacyWebFormsExtractorTests|FullyQualifiedName~EvidenceDocsExportTests'
```

The overridden-placeholder regression initially found a false inclusion. The
extractor now checks active render markers in the parsed markup and omits
replaced master defaults, comments and inert render expressions. Tests cover
direct and bundled includes, different and duplicate controllers, source scope,
parser errors, conflict/refusal and resource limits. The frontend fixture
provides syntax evidence. HTTP dispatch, runtime execution, named callback
indirection and general inline onclick remain separate. Pinned C#/VB semantic
OSS smoke reruns are deferred for this WebForms/JS syntax slice. The final
pinned full Debug and public CLI results will be recorded below.

### Pinned AngularJS CLI and Debug validation

Checkout `dade38e25cd935b0bf5cfe2e32947110f2cf27cd` includes the
`4e32864e4` implementation and its verification prompt. The Debug build
reported zero errors and warnings. The public CLI smoke ran the following
command once for each of the three fixture repositories with fresh outputs:

```sh
dotnet src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll scan --repo "$fixture" --out "$output"
```

The AngularJS fixture produced one handler and five bundle facts (one
declaration, three members, one render reference). The regular and ASAX
bundle controls retained four and seven bundle facts respectively. All
three produced the five required scan artifacts and passed SQLite integrity
and NDJSON/SQLite fact-ID checks. The smoke verification originally expected
six bundle facts for the AngularJS fixture. Correcting the expected count to
five matched the fixture and the unchanged scan outputs.

An independent static probe copied the public AngularJS sample into a
temporary committed repository. Adding an inline script with
`var count = <%= 1 %>;` yielded a click and MalformedAngularScript.
Changing that single initializer to literal `1` restored the external
handler candidate. This isolates one effect of server-block masking in
0.17.0. The verification prompt describes how to distinguish this case
using local evidence from the original page.

Full Debug suite on the pinned checkout:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug --no-build -warnaserror --filter 'FullyQualifiedName~TraceMap.Tests.SelfDiagnosticsTests'
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug --no-build -warnaserror --filter 'FullyQualifiedName!~TraceMap.Tests.SelfDiagnosticsTests' --logger 'console;verbosity=normal'
```

The isolated diagnostics run passed 4/4. The remaining suite passed 6,203,
skipped 3 and failed 0. Combined, 6,207 passed, 3 skipped and 0 failed.

### Inline WebForms initializers on codex/workspace-scope-preflight

Extractor 0.18.0 recovers a one-line declaration whose entire initializer
is an unquoted WebForms output expression containing a dotted identifier,
a plain identifier or an unsigned integer. The placeholder occupies the
original offset and must parse as a variable initializer. Each occurrence
has an unknown-value fact supporting the external handler. Separate genuine
JS errors, unsupported server syntax, conflicting controllers and the
64-initializer cap continue to block the join. LF and CRLF tests retain
physical spans. The public fixture has three independently authored examples.

Implementation `5e597a958e83ec7ac934b6988561d4a317ff50dc`. Focused Debug
gate passed 158/158 using this filter:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests -c Debug -warnaserror --filter 'FullyQualifiedName~WebFormsAngularClickTests|FullyQualifiedName~WebFormsScriptBundleTests|FullyQualifiedName~LegacyWebFormsExtractorTests|FullyQualifiedName~EvidenceDocsExportTests'
```

Pinned Debug build had zero warnings/errors. CLI scans of
`samples/webforms-angularjs-click` and `samples/webforms-script-bundles`
completed. On the Angular fixture SQLite integrity, NDJSON/SQLite fact-ID
parity, five required outputs, the external target at line 2 and all three
unknown initializer supports at lines 7–9 were checked. The initial focused
run caught a missing regex inventory entry, now included. Pinned semantic
C#/VB OSS reruns are deferred for this inline static JavaScript slice.

Pinned full Debug results on `5e597a958`, using the two full-suite commands
recorded above: 4 isolated diagnostics passed, remaining suite 6,211 passed,
3 skipped and 0 failed. Combined 6,215 passed, 3 skipped and 0 failed.
The regular and startup bundle CLI controls retained 4 and 7 bundle facts,
respectively, passed SQLite integrity and NDJSON/SQLite ID parity, and
produced zero AngularJS handler candidates.

### AngularJS controller chains on codex/workspace-scope-preflight

Extractor 0.19.0 indexes the receiver links of a complete top-level controller
chain once during parsing. The joined registration keeps the original module
identity and its own source location. The public fixture selects the first of
three controllers with same-named methods. Regression tests cover all three
positions, malformed and ambiguous inputs, unknown intermediate calls, and
the 32-link limit. Absolute script URL mapping and server expressions inside
master-page strings remain independent gaps. C#/VB semantic OSS reruns are
deferred for this JavaScript syntax slice.

Pinned implementation `4344b34fc93876b6de7c17635ca13b7d968e0b68` built in
Debug with zero warnings/errors. The focused filter used for the previous
slice passed 165/165. The dynamic-name negative case initially expected a
generic registration refusal and now checks the emitted dynamic-name gap.
Pinned CLI scans of angularjs-click, script-bundles and script-bundle-startup
used the same commands as above. All passed SQLite integrity and NDJSON/SQLite
ID parity. Only angularjs-click emitted a handler; its retained click ID,
external line 2 and three inline initializer supports were verified.
