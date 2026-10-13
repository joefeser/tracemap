# Implementation state

## Independent compatibility completion (2026-10-12)

The earlier work-machine transfer prompt is withdrawn. No work-machine patch,
commit or implementation was imported. The authorized checkout independently
generated net452/net472 C#/VB cases using the existing HttpReferenceMatrix tool
and Microsoft.NETFramework.ReferenceAssemblies 1.0.3. Both sets enumerate 116
overloads per language; generated OVERLOADS.md records exact public signatures.
The net472 Website control is independently authored public synthetic source.

VB syntax fallback now emits a per-readable-file Tier4 HTTP coverage gap, including
files with no HTTP-looking names. This is deliberately a coverage statement,
not HTTP call inference. Semantic files are excluded. Markdown displays these
gaps explicitly. Projectless VB HTTP operation classification remains unsupported;
the silent-reporting defect is fixed, not the entire extraction capability.

Local validation: solution build passed with zero warnings/errors; six collector
guard tests, 74 HTTP/VB/capability tests, private-path guard and all 96 deterministic
native CLI outputs/artifact conformance checks passed. Public Windows CI run
[38296179466](https://github.com/joefeser/tracemap/actions/runs/38296179466) passed
at implementation head dfc4ebd2d32c7f4715d61fdc04e4a91975f98f7e: 29 HTTP tests,
six collector guards and 96 CLI outputs. No private work-machine access was needed.
The full local regression suite is running; pinned OSS smoke and application-specific
acceptance are not claimed for this patch.

Branch: `codex/backend-extraction-inventory`, based on `origin/dev` 0d3107cc.
Issue: #861. Phase one is a retained-query projection, not full-index discovery.

New grouped handoffs carry optional `backendExtraction`; legacy handoffs without
it remain readable. The builder derives it from indexed full node records and
ordered variants. Restore verifies a supplied projection against those records.
Rows are evidence records, not unique business operations; callers are query
roots, not asserted UI events. Candidate tiers remain explicit. Inventory-only
records remain found-not-connected. Human decisions remain undecided.

Constructed-generic edges now carry limitations from a deterministic rule-owned
mapping. Newly generated grouped/method-graph JSON carries those edge fields;
HTML also explains them. Existing artifacts are not rewritten.

No changes to ACK, lane policy, raw SQL defaults or source-sharing authority.
Base44 stash and primary dev checkout remain untouched.

PR #862 review corrections: preserve candidate path/edge classifications and
mixed-variant state; expose querySurface and explicit HTTP-coverage exclusion
for database-filtered producers; materialize legacy rule limitations in both
method-graph JSON and HTML while hashing the original bounded input. HTTP
extractor expansion remains in its separate lane.

Validation so far:
- 222 focused generic/framework/two-hop/grouped/lazy-constructor tests passed
  with warnings as errors.
- 30 grouped-projection and compiler-built DB/HTTP sample tests passed.
- Graph-share, wizard-requery and SQL-route PowerShell suites passed.
- Initial synthetic semantic+IL CLI smoke: 243 facts, Level1SemanticAnalysis;
  revealed absent GetStringAsync HTTP surface. Positive control now uses GetAsync,
  with native scan/report regression coverage. Convenience HTTP coverage logged.
- 229 broader path/native execution regression tests passed (4m15s).
- Full suite and authentic Windows/private-site validation not run for this branch.

Commands (from the worktree root):
```
dotnet test src/dotnet/TraceMap.sln --filter 'FullyQualifiedName~GroupedCompiledPathHandoffTests|FullyQualifiedName~ConstructedGeneric|FullyQualifiedName~FrameworkSignatureCandidateTests|FullyQualifiedName~LazyConstructorLoggingTests|FullyQualifiedName~CompiledTwoHopCutoffTests' --verbosity quiet -warnaserror
dotnet test src/dotnet/TraceMap.sln --filter 'FullyQualifiedName~BackendExtractionSampleTests|FullyQualifiedName~GroupedCompiledPathHandoffTests' --verbosity quiet -warnaserror
dotnet test src/dotnet/TraceMap.sln --no-build --no-restore --filter 'FullyQualifiedName~WebFormsReviewExecutionTests|FullyQualifiedName~CombinedDependencyPathTests' --verbosity quiet
pwsh -NoProfile -File scripts/tests/Test-WebFormsGraphShare.ps1
pwsh -NoProfile -File scripts/tests/Test-WebFormsWizardRequery.ps1
pwsh -NoProfile -File scripts/tests/Test-WebFormsSqlRoute.ps1
dotnet build samples/backend-extraction/BackendExtraction.Sample.csproj --verbosity quiet
```

## Isolated outbound HTTP follow-up

Branch `codex/outbound-http-coverage-861`, in a separate isolated worktree.
Dependency/base: inventory `d55eb44b3d4781d2adeffa11a6d42a8ca90e7ea5` atop dev
`0d3107cc4e7edb9fca20928f43e6604a4ff2b992`. The backend-extraction and debugging
lanes were not modified. Source API classification is shared across C#/VB and
requires exact signed framework identities and resolved parameter shapes. JSON
content reads are excluded; inline trusted HttpRequestMessage constructors can
retain standard HttpMethod properties, while request variables stay UNKNOWN.
WebClient constant standard explicit verbs are retained; default/dynamic/custom
verbs remain UNKNOWN. WebRequest response verbs and mutable request state remain
unresolved. WebClient protocol is never inferred from API recognition.

Public net10.0 fixtures enumerate 166 overloads per language with literal String/
Uri and HttpRequestMessage forms, cancellation/completion/options/context/typeinfo,
event/user-token/task forms and controls. Every overload is checked through
semantic facts, SQLite, retained query handoff and HTML. Projectless and compiled
paths have independent assertions; compiled-only retains original IL calls, without
HTTP operation classification. New optional HttpCallEvidence carries only bounded
privacy-safe API metadata through surface/path/grouped reports. Existing generator/
bounded-input hashes bind those reports. No new standalone machine-readable matrix.

Implementation validation completed at `4d52ca6a1c862622d69f95e975ffd650f9f6bae4`. net48/other-framework overload parity, pinned OSS
smokes, non-.NET adapters, private Windows/site and runtime behavior are deferred.
Full-index discovery, private readable contract capture and owner decision receipts
remain open. The convenience gap recorded above describes the first delivery,
not this follow-up implementation.

Compatibility: existing backendExtraction limitation text is preserved because it
participates in deterministic reconstruction. New handoffs add optional validated
HttpCoverageLimitations; older handoffs may omit it. Nullable HttpCallEvidence is
omitted when absent, preserving old node hashes. Altered coverage metadata is
rejected. Report projection admits only bounded metadata values, never arbitrary
property strings or new private destination hashes.


Validation receipt for the implementation commit:
- Warning-as-error solution build succeeded with zero warnings/errors.
- 82 focused API, integration, VB external-boundary, retained-handoff, projection,
  sample and Razor version regressions passed.
- Full solution suite passed: 3,900 passed, one gated Windows ASP.NET corpus skip,
  zero failures (21m02s). Binaries remained unchanged during this accepted run;
  earlier interrupted/invalidated exploratory runs are not acceptance evidence.
- Twelve native CLI outputs (both languages x three modes x two repeats) validated
  with scripts/validate-adapter-artifacts.py. Facts and report.md were byte-identical
  within each pair. Semantic outputs retain 171 Tier1 HTTP callsites per language
  (166 overloads + five controls); C# projectless retains 176 unresolved Tier3
  candidates; VB projectless and both compiled-only outputs classify no HTTP calls.
  All 166 compiled Case methods retain original IL call observations per language.
- Seven artifact-validator tests passed; whitespace checks passed.
- GitHub Private Path Guard passed on the implementation commit.
- No fixture method was executed; no application HTTP/DB calls or private artifacts.

The implementation branch is pushed for testing. The later inventory/debugging
commit `022e1c722195033e1975226daee5e76f894115af` advanced independently after
isolation and was not integrated. The HTTP branch depends on recorded inventory
`d55eb44b`; integration with the newer reporting changes needs its own validation.
No merge, ACK, lane-policy change or whole-site completeness claim.

The follow-up documentation-only commit records this receipt. Rebuild and rerun
the 82 focused tests plus twelve CLI outputs on that final head; no repeat of the
full suite is required for documentation-only changes. The full-suite claim is
explicitly bound to the implementation commit above, not to a later commit.

## Legacy reference compatibility extension

Current origin/dev `278bb821fe41aca8221d2193d15abaa7b92c6e64` (inventory #862)
merged cleanly into this isolated branch in merge commit
`922fc0b182e9d39d9e41f4971ecef625af3434a6`. The historical no-integration
receipt above remains bound to its original head; this extension supersedes that
integration gap. No dev checkout/debugging lane was modified.

Public reference-built C#/VB net20/net35/net40 fixtures each enumerate 57 overloads;
net45/net48 enumerate 116 each, alongside 166 net10.0 overloads. All ten legacy
projects compile on macOS with zero warnings/errors, and ten legacy tests passed
(39s): every overload through semantic facts/SQLite/handoff/HTML, independently
decoded emitted IL signatures, signed assembly identities, TFM metadata and separate
projectless/compiled-only limits. System 2.0.0.0 is shared by net20 and net35 and
does not establish application TFM; missing binary TFM remains unknown. C# legacy
SendAsync may emit a HttpMessageInvoker base-slot reference, retained as emitted.
Trusted framework checks have not changed.

Windows public compatibility CI is configured; observed run/head and broader
local regression/native CLI receipt are pending. Admitted application framework
evidence has not been identified; a path/admission reference was requested. This
application-specific prerequisite remains open, with no inferred TFM/private
application compatibility claim. See the legacy compatibility matrix for details.

Corrected public validation receipt at
`ef7d9cdd5ce9b3d6aad2c5d0b67650c8cbbaa7ce`:
- Solution warning-as-error build: zero warnings/errors.
- 99 scoped extractor, integration, retained-report and legacy regressions: passed
  with zero failures/skips (1m01s). Includes integration with inventory #862.
- 72 native CLI outputs (six TFMs x two languages x three modes x two repeats):
  validated; facts.ndjson and report.md byte-identical within each pair. Per-TFM
  semantic counts match the matrix; unresolved syntax candidates are separate rows
  and are never counted as extra supported overloads.
- Native retained path JSON contains bounded HTTP evidence for every selected
  language/framework; all compiled Case methods retain IL observations, with no
  invented HTTP operations. Seven artifact-validator tests passed.
- GitHub Private Path Guard passed at this head. Windows compatibility run
  38283440213 is still pending at receipt-writing time. Earlier CI runs were
  cancelled after correcting smoke-script tier denominators/output-directory names;
  they are not acceptance evidence.
- No fixture method or application network call was executed. No private artifacts.

The earlier full-suite 3900/1 receipt remains bound to its earlier implementation
head; it is not an exact-head full-suite claim after this extension/dev merge.
The 99 scoped regressions cover the changed extraction/reporting/integration paths.

Windows receipt supersedes the pending status above: run
[38283440213](https://github.com/joefeser/tracemap/actions/runs/38283440213)
completed successfully at code head `ef7d9cdd5ce9b3d6aad2c5d0b67650c8cbbaa7ce`,
including reference/signature/IL/retained-report tests and all 72 native CLI outputs.
Five legacy reference inventories regenerated byte-identically locally.
Issue [#861 compatibility handoff](https://github.com/joefeser/tracemap/issues/861#issuecomment-6129265884)
records the per-framework/API matrix, mode limitations, merge dependency, validation
and remaining gaps. The admitted-application TFM/reference comparison remains open
until its evidence path/admission reference is supplied. No application TFM was
inferred from an assembly version and no private artifacts were included.

Subsequent receipt commits change documentation only; executable/sample/script/test
validation above is bound to the recorded code head. Origin/dev was re-fetched and
remains `278bb821fe41aca8221d2193d15abaa7b92c6e64`, an ancestor of this branch.

## Owner-directed offline validation handoff

The admitted application evidence is on a separate work machine inaccessible from
this session. Owner instructed that this does not block this branch. Legacy fixture
TFMs are now explicitly representative test targets, not confirmed application
targets. Application-specific compatibility is pending work-machine validation.
No further hostname/private-artifact request is required for public branch delivery.

New standalone InspectHttpFramework public tool reads only explicit bounded local
XML/binary selections. It records project TFM fields, Web.config compilation and
httpRuntime fields, binary TFA, public framework AssemblyRefs and HTTP MemberRefs
as separate local structural evidence. It never evaluates imports, builds the app,
loads application assemblies, resolves dependencies, executes methods or uploads.
DTD/entity resolution is prohibited; errors/gaps and multiple/conditional/unresolved
values remain explicit. No application TFM is inferred from assembly versions.
Output is private/local Markdown, with exact generator/dependency hashes and a
bounded-input hash; inputs are read-only and existing outputs cannot be overwritten.
No private paths/snippets/app types/arbitrary values are retained; private input
hashes remain local. A manual sanitized summary template is the only return handoff.
Public guard validation and Windows receipt follow in the delivery notes.

Local collector guards passed: six synthetic tests covering separate/conflicting/
conditional declarations, unknown net20/net35 binary TFM with System 2.0.0.0,
modern HTTP/JSON signatures, blocked XML DTDs, malformed/missing inputs, size
refusal and non-overwrite/read-only behavior. Public source-only collector build
passed warning-as-error. No private work-machine input was inspected here.

Offline handoff validation receipt:
- Collector/property-scope code head: `3f4e607ca8ee84bfedec6f037de7fcd07e0b468e`.
  Six local collector guards passed on this exact head; build uses warning-as-error.
  Guards also reject same-named ItemGroup metadata as project TFM properties.
- Unchanged HTTP matrix rerun at `6a8431c7ba2b20c3e4681fb4a06e58c2c245cbac`:
  24 local HTTP tests passed and all 72 local CLI outputs validated deterministically.
- Windows [run 38288325932](https://github.com/joefeser/tracemap/actions/runs/38288325932)
  passed on exact corrected head `3f4e607c`: 24 HTTP tests, six collector guards,
  all 72 native CLI outputs. Earlier run 38287840200 also completed successfully;
  the corrected head/run above is the final code acceptance receipt.
- Private Path Guard passed on the corrected code head. Origin/dev remains
  `278bb821fe41aca8221d2193d15abaa7b92c6e64`, already merged and an ancestor.
- Public collector commands/template are ready. No private application evidence
  was accessed here; no application or fixture method was executed.
- Application compatibility remains deferred/pending work-machine validation,
  explicitly not a blocker for this public branch. No hostname/artifact request.
The final receipt-only commit changes no executable/sample/script/test code.

## Projectless VB HTTP implementation (2026-10-12)

User-required follow-up on `codex/outbound-http-coverage-861`: warning-only
projectless VB support was insufficient. The syntax extractor now emits bounded
Tier3 explicit-receiver HTTP candidates under `vb.syntax.http-candidate.v1`.
Parameters, explicit locals/fields, inline construction, qualified names and
file-local type/namespace import aliases are supported. Same-file type shadows,
unrelated receiver types and content-only JSON reads are excluded.

Candidates keep family unresolved, verb UNKNOWN and destination unavailable.
The per-file Tier4 gap now describes partial coverage. Variable aliases, inferred
factory returns, late binding, With receivers and cross-file imports remain
limitations, not clean absence. Semantic and compiled-only behavior is unchanged.
The report projection and migration prompt preserve those distinctions. The
synthetic projectless Website proves a wrapper-to-candidate route and an
outbound-http candidate-query-path inventory row plus HTML output.

Local validation: 52 HTTP/capability tests and 103 adjacent VB regressions passed;
six collector guards and private-path guard passed; all 96 native CLI outputs
passed deterministic facts/report and artifact validation, including each VB
matrix overload and HTTP path projection. No fixture method was executed.
The earlier broad full-suite run was stopped before rebuilding after 24 minutes
without a final result; it is not claimed as passing. Current implementation
Windows CI validation completed successfully (receipt below). No private work-machine input or patch
was consumed; public synthetic fixtures only.

Final projectless VB HTTP receipt:
- Code head: `68820989cb1f5dccb41ebe31decd4ed23c67ed6b`, pushed to origin.
- Exact-code-head local repeat: 155 targeted tests passed; all 96 native CLI
  outputs passed deterministic comparison and artifact validation. Final focused
  warning-as-error build/tests passed (16 tests).
- Windows [run 38299653713](https://github.com/joefeser/tracemap/actions/runs/38299653713)
  passed on that exact code head: 42 HTTP tests, six collector guards and all
  96 native CLI outputs. The remote Private Path Guard also passed.
- Personal Windows access was not required; public Windows CI supplied the
  Windows acceptance result. The private work machine was not used.
- This receipt update changes only task/state documentation. No PR opened,
  merge performed, ACK invoked or lane policy changed.

## PR 863 Baz validation follow-up

Both confirmed validation weaknesses are patched without extractor changes:
- PR workflow triggers now cover `src/dotnet/**`, including semantic producers,
  projections, handoffs and report writers. HTTP oracle scripts also trigger it.
- Native CLI expectations cross-check `OVERLOADS.md` identities with source
  declaration rosters and verify each case, not hardcoded count/range offsets.
  A hand-authored per-control/per-mode oracle pins positive and negative controls;
  expected results are never derived from scanner output. Required controls,
  duplicate cases, unlisted controls and unmapped/missing/extra results fail closed.
- Eight oracle/workflow mutation guards pass, and all 96 native CLI outputs pass
  deterministic artifact validation with the stronger assertions. Private path
  and whitespace guards pass. New-head hosted CI/reviews remain pending push;
  earlier Windows receipts are historical, not a new-head result.

## SQL/database issue #864 implementation lane (2026-10-12)

Branch `codex/sql-database-matrix-864`, isolated from origin/dev
`6cb7532528d687a2fc20ca1135db130b244549da` (HTTP #863 merge). Primary checkout
and existing HTTP worktree preserved. Initial matrix published on #864 in comment
6131964679, documentation commit `64b53620`. No ACK, merge or lane-policy change.

Public metadata-only inventories: 46 declared overloads each net20/net35/net40,
70 each net45/net452, 112 each net472/net48/net10.0, in both C# and VB. Pin classic
references to Microsoft.NETFramework.ReferenceAssemblies 1.0.3; modern System SQL
provider 4.9.0 and Microsoft provider 6.1.1; SDK 10.0.302/reference pack 10.0.10.
Selected Microsoft provider is unavailable below net462; absent task/token forms
on old targets are unavailable, not skipped positive cells. All inherited members
are represented at declaring bases. VB GetObjectValue helpers and emitted base
slots remain original IL, distinct from source API declaration identities.

Implemented bounded SQL command/XML/APM/async/token, adapter Fill/FillSchema/Update,
IDbCommand/DbCommand and separate bulk operation classification. Source, explicit
syntax and compiled-only API terminals remain separate tiers/modes. Optional
DatabaseCallEvidence carries privacy-bounded availability/API metadata through
report projection; grouped handoffs have an optional validated SQL coverage marker.
Older handoffs omit it and retain their historical inventory reconstruction.
New handoffs include sql-persistence operation records, including bulk boundaries.

Command state is a local straight-line witness, not runtime state: hashes or
explicit dynamic/unavailable values, Text/StoredProcedure, parameter direction/type
without values, transaction/connection assignments without credentials. Branches,
unknown calls, conditional expressions, reassignment and alias escape reset state;
fields/cross-method state and inferred concrete factory returns remain unavailable.
The matrix/README and migration prompt record provider/ORM/late-binding/generic/
full-index/private-contract limitations. No method/database connection is invoked.

Per-cell tests independently cross-check compiler declarations/public inventories,
Cecil original IL, named controls and every applicable source/syntax/compiled path,
SQLite and grouped HTML/JSON/inventory. The public projectless VB App_Code proof is
Entry -> Wrapper -> ExecuteScalar, a candidate query route, not a verified page event.
Seven independent native-oracle/workflow mutation guards passed. Earlier exploratory
runs found fixture warnings, language signature/base-slot differences and VB shared
initializer state boundaries; these were corrected and are not acceptance receipts.
Final local/native and public Windows acceptance is pending the code-head commit.
Full-suite, other-provider, private application and whole-site acceptance is not claimed.

First accepted implementation head `5efea3d33dc72da03d2f8f85130476d755ded04d`:
warning-as-error build (zero warnings/errors), 406 scoped local tests (zero failures/
skips, 3m22s), seven oracle guards, seven artifact-validator tests and all 96 native
CLI outputs passed. facts.ndjson/report.md were byte-identical within every pair.
Private Path Guard passed locally/remotely. Windows run 38306629860 is running.
This is an exact-head scoped receipt, not a full-suite claim.

Final retention supplement: compiled terminals now embed the original encoded API
operand, raw-binary hash, IL generator/bounded-input hashes and bound/unbound state.
An 8,192-character signature cap is explicit; the API path remains present when
signature display is truncated. Source metadata now includes positive connection
constructor/assignment witnesses and safe public enum parameter type names. Added
named controls in every framework/language and per-case compiled-handoff identity/
provenance assertions. Eleven focused command-state/privacy/failed-load tests pass.
Final supplemented-head regression/native/Windows receipts remain pending.


## Final SQL local acceptance receipt

Frozen executable head: `6448790cba9c5d0b0681f90c69befab141a923a0`.
This supersedes earlier SQL pending receipts. The final fixes retain original
compiled operand/provenance, reject unexpected candidate tiers in the independent
oracle, and classify XML reader/adapter schema paths as SQL query surfaces.

- Warning-as-error .NET build: zero warnings/errors (12.39s).
- Scoped .NET regressions: 406 passed, zero failures/skips (3m44s), covering SQL
  matrix/state, VB extractors, integration extractors, grouped compiled handoffs,
  backend inventory, compiled paths, framework signatures and generic boundaries.
- Independent Python oracle/workflow mutation guards: eight passed.
- Artifact-validator regressions: seven passed (existing unclosed-SQLite-resource
  warning in the validator test; no failed assertion).
- Native CLI: all 96 outputs (eight TFMs x two languages x three modes x two
  repeats) validated, including individual cases/controls, byte-identical
  facts.ndjson/report.md and scan artifact conformance. No fixture methods ran.
- Separate native projectless VB App_Code scan/path/report retained
  `PublicFeature.Entry(DbCommand) -> Wrapper(DbCommand) -> ExecuteScalar`,
  NeedsReview/Tier3, reduced coverage and untruncated. This is a candidate query
  connection, not verified ASP.NET event binding or runtime use.
- Separately authored public config-only negative fixture produced zero database
  operation candidates and zero SQL query paths; scan artifacts validated.
- Private path and whitespace guards passed; public remote Private Path Guard
  passed on this executable head.

Scoped .NET command after the warning-as-error build:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~SqlDatabaseMatrixTests|FullyQualifiedName~DatabaseCommandStateTests|FullyQualifiedName~VisualBasic|FullyQualifiedName~IntegrationExtractor|FullyQualifiedName~GroupedCompiledPathHandoff|FullyQualifiedName~BackendExtractionSample|FullyQualifiedName~CompiledTwoHop|FullyQualifiedName~FrameworkSignatureCandidate|FullyQualifiedName~ConstructedGeneric|FullyQualifiedName~LazyConstructor' --logger 'console;verbosity=normal'
```

Other acceptance commands:
`python3 scripts/validation/test_sql_matrix_expectations.py`,
`python3 scripts/test_validate_adapter_artifacts.py`,
`python3 scripts/validation/sql-coverage-smoke.py --output <temporary-output>`,
`bash scripts/check-private-paths.sh` and `git diff --check`.
Public Windows [run 38308628487](https://github.com/joefeser/tracemap/actions/runs/38308628487)
succeeded on the frozen executable head: 181 scoped .NET tests (zero failures/skips),
eight oracle/workflow guards and all 96 deterministic native CLI outputs. Remote
Private Path Guard run 38308628263 also succeeded. Windows CI provided public
Windows acceptance; the personal Windows and private work machines were not used.
Superseded Windows runs 38306629860, 38307346982 and 38308013821 were canceled;
they are not acceptance evidence.

Full-suite, other-provider/ORM parity, full-index/whole-site discovery, private
application compatibility and readable private SQL/procedure capture remain
unverified or deferred as documented. No database connection, SQL execution,
private work-machine contribution, PR, merge, ACK or lane-policy change.


Delivery: branch pushed and [issue #864 receipt](https://github.com/joefeser/tracemap/issues/864#issuecomment-6132878782)
published with exact executable commits, matrix coverage, public Windows/local
validation and remaining gaps. This final commit changes only task/state receipt
documentation; executable acceptance remains bound to `6448790c`. Primary checkout
and existing HTTP worktree preserved. No PR opened, merge performed, ACK used or
lane policy changed. Parent #861 full-index/private-contract tasks stay open.


Follow-up: added `prompts/assess-sql-migration-report-improvement.md` and prompt
index entry for an application-neutral, authorized local baseline/new SQL report
comparison. Public executable heads remain pinned to `6cb75325` / `6448790c`.
No application identity or private evidence is included. This is a prose-only
prompt update, checked with private-path and whitespace guards; executable tests
were not rerun. No private trial or returned feedback is claimed.

## Independently authored projectless SQL correction (2026-10-12)

Continued the clean SQL worktree from `209fcd28`. No work-machine patch, code,
binary or artifact was imported. Public synthetic requirements were reproduced
locally. Baseline SqlDatabaseMatrixTests passed 17/17; the reported sixteen
failures were not reproduced in this environment.

VB syntax extractor 0.4.1 expands relative qualified types using file-local
namespace imports without discarding arbitrary qualification. Alias ownership
and same-file prefix shadows are refused. Unrelated namespaces, alias decoys,
Global qualification and lifecycle-only operations have explicit controls.
Report projection now includes DatabaseOperationCandidate facts, rule/tier and
availability, empty-coverage qualifications and a deterministic 50-row display
with total/omitted counts. This does not alter fact admission tiers.

The existing projectless sample adds direct qualified and negative controls.
A separate synthetic projectless website passes procedure/text arguments into a
net472 wrapper library. Tests separate application-only source (no database
operation candidates, explicit report limitation), application plus wrapper
source (two Tier3 candidates and Search-to-wrapper migration inventory), and
application plus admitted wrapper IL (separate IL calls, no automatic source
binding). Cross-method procedure/SQL argument propagation, connection identity,
parameter contracts and runtime use remain unavailable. The generic report
warning is not per-call unresolved-wrapper classification or full-site inventory.

Local validation: warning-as-error build and 79 scoped SQL/state/VB-boundary/
grouped-report tests passed; 49 adjacent Markdown/VB/integration/backend tests
passed. Eight independent oracle guards and seven artifact-validator tests passed
(the latter retains its existing unclosed SQLite ResourceWarning). All 96 native
SQL matrix outputs passed per-case checks, artifact validation and deterministic
facts/report repeats. Two additional native projectless CLI scans validated direct
candidate display and application-only missing-wrapper warnings. No fixture
method, SQL statement or connection was executed.

The final report-direction wording and cap-count assertion passed a final
warning-as-error rebuild, 19 focused tests and a validated native CLI scan.
Full solution, pinned external OSS smokes and a new
Windows correction receipt are deferred; older Windows receipts above apply only
to their frozen heads. This change does not claim private application compatibility,
complete wrapper discovery, an extracted procedure contract, PR/merge or ACK action.

### Windows event-wiring assertion correction

Public personal-Windows validation reported at executable head `e1932ae0`:
isolated SDK 10.0.302 / reference pack 10.0.10, clean CLI build, 127 focused tests
passed and one failed, eight oracle guards passed, all 96 native SQL outputs
passed, and the projectless candidate/negative controls passed. Overall acceptance
was not green. The failure was the legacy event-edge exclusion assertion matching
a legitimate VisualBasicEventBindingDeclared fact targeting a Click event.

The correction is test-only: restrict the edge prohibition to CallEdge and
SymbolRelationship facts and retain the existing event-target exclusions. A
portable net10 synthetic Click event positively asserts its semantic declaration;
mutating that fact into either prohibited edge kind must trigger the predicate.
The existing Web Forms fixture can lack event-binding metadata on non-Windows
hosts, so positive semantic coverage uses the independent fixture rather than
assuming framework availability. No extractor or event-resolution policy changes. A new exact-head
Windows focused-suite receipt is required; the previous failure is not waived.
Private assessment attachments are not imported into the repository.

Local warning-as-error validation passed 56 tests across
VisualBasicExtractionTests and VisualBasicWebFormsCompositionTests, zero failures
or skips. Whitespace and private-path guards passed. Windows rerun remains pending.

### SQL external-wrapper delivery

Personal Windows correction receipt at `9546796be294dd06735583c2b9e947728d3f541e`:
SDK 10.0.302/reference pack 10.0.10, warning-as-error build, 151 tests passed,
zero failures/skips. This closes the previous event-wiring assertion failure.
The older 96-output SQL smoke is still bound to e1932ae0, not this later head.

Independent net472 SyntheticWebsite compilation harness now admits compiled
application callers alongside a separate SyntheticStore wrapper DLL. Five
regression cases cover both procedure/text routes and caller-only, provider-only,
source-only controls. Procedure forwarding spans two overloads; the preserved
constant is hash-only, while the text case is a symbolic composition. These tests
exercise existing compiled resolution, not a new resolver implementation. They do
not prove private application coverage, ASP.NET publishing, event wiring, source
binding or runtime execution. Fixture methods are never executed.

`scripts/sql-wrapper-report.py` consumes a closed existing index and up to 32
exact caller selections. It retains unmatched and failed selections separately,
ordered routes, edge rules/tiers, command availability and native restrictions.
It records script/component/index/selector hashes and links native path JSON;
it introduces no new machine-readable schema and is local-only. It does not
discover every caller or recover readable SQL/procedure contracts. The new
`prompts/produce-sql-wrapper-inventory.md` is SQL-only and forbids private artifact
or patch export. Missing compiled application evidence is a blocker to caller
proof, not something a wrapper DLL alone can resolve.

Local validation: eleven Python report guards, eight matrix oracle guards, and
native CLI scan/report with procedure, dynamic-text and unmatched selections
passed. The native report had three selections, two retained routes, one unmatched
selection and zero failed queries. Warning-as-error focused .NET validation passed
43 tests across SqlWrapperRouteTests, ProjectlessSqlReportingTests,
SqlDatabaseMatrixTests and DatabaseCommandStateTests, zero failures/skips.
Full-solution and new Windows fixture execution are not yet claimed. CI now runs
the wrapper/projectless cases and helper guards on Windows and watches the helper
and prompt paths. No scanner/resolver runtime code changed in this increment.

Implementation/fixture delivery was pushed as `d00bc224`. Final report review
identified graph-wide native gaps that must not be called caller-specific stopping
gaps. The helper now labels retained-path node/fact association separately from
unproven caller relevance; neither label claims blockage. A regression guard pins
associated provenance warnings and unrelated gaps. Native replay of this change
passed. The complete five-case route suite also passed after adding explicit
ordered-edge adjacency assertions. No private application evidence was imported.

Supplemental personal-Windows acceptance (SDK 10.0.302/reference pack 10.0.10):
`d00bc224c45e515eef4c6800f70d23c9ba2f5f13` passed all five warning-as-error
wrapper-route tests and ten helper guards. Native public scan/report retained two
routes, one unmatched selection and zero failed queries, preserving hash-only
procedure and symbolic dynamic-text states. Reporting follow-up
`4ffb26cfe6c09a0420d233675c39f8cdce76df1d` passed eleven guards and a fresh
report from that same unchanged index/CLI, with the same counts and both gap
association labels verified. No private inputs, fixture methods or database
connections were used. This receipt closes the bounded reporting delivery, not
private-site acceptance, full caller discovery or readable SQL contract recovery.

### Retained SQL index size admission correction

The helper's fixed 512 MiB cap can reject an otherwise usable retained compiled
index before any native query. Added explicit `--max-index-mib` (default 512,
range 1..16384) and `--query-timeout-seconds` (default 120, range 1..3600).
Report headers record both selected bounds and input byte size; size refusal is
actionable and public-safe, and timeouts remain failed queries. Hashing remains
streamed. Native graph depth/path/work limits and output-size guard are unchanged.
The reconciliation prompt now directs reuse of the same retained index with
explicit admission, never a reduced rescan or application rebuild to evade a cap.

Fourteen Python guards passed, including an actual 768 MiB sparse file, default
refusal before output/process creation, explicit admission, inclusive boundary,
invalid options, timeout categorization and unchanged file size. Native CLI smoke
also passed with a valid public SQLite fixture index padded to 768 MiB: default
refusal, explicit 1024 MiB admission, two routes, one unmatched and zero failures.
Padding tests file-size admission, not logical graph scale or private corpus
compatibility. No scanner/extractor changes or database execution.

Personal Windows validation passed at
`f53129ad95ac07553b3636c021e03b3ed7f2ae9c`, SDK 10.0.302/reference pack 10.0.10:
14 Python guards, zero-warning/error CLI and wrapper builds, and native 768 MiB
SQLite smoke. Default admission refused; explicit 1024 MiB/600 seconds accepted
with route counts 1/1/0 and zero failed queries. No private inputs or application,
fixture-method or database execution. This closes platform validation of the size
admission correction, not private historical-index compatibility or graph scale.

### Explicit local literal recovery and truncation diagnostics

Added standalone RecoverSqlCommandText diagnostic. It requires explicit local
disclosure consent and an operator-supplied expected binary SHA, reads metadata
without assembly execution/dependency resolution, and matches only retained
constant database command origin hashes and exact UTF-16 lengths to ldstr operands.
Output is private Markdown with generator/component and input hashes. It never
changes default scanner disclosure or claims independent historical binary binding.
Dynamic text is not evaluated, and missing/overlength/nonunique matches remain
unknown. Rules and limits are documented under sql.local.command-literal-match.v1.
The new work-machine prompt requires provenance reconciliation and prohibits
private uploads, patches, application builds and SQL execution.

The SQL wrapper report now displays native TruncatedByLimit reason categories
beside each selection; a truncated flag without reasons stays explicitly unknown.
This exposes diagnostics without silently raising graph limits. No core extractor
or traversal semantics changed. Local validation includes warning-as-error utility
build, native public fixture literal recovery/negative controls, and 15 helper
guards.

Final native guards also cover inert Markdown rendering and explicit UTF-16
surrogate escapes. Personal Windows validation passed at
`9d61f97a2d9ce0a01ea7223be43a15a6d17a9bea`: utility build zero warnings/errors
with warnings-as-errors; ten command-text guards and fifteen wrapper-report
guards passed using SDK 10.0.302/reference pack 10.0.10. Reused public native
report and matching fixture DLL hashes remained unchanged. No private inputs or
application/database execution. Local final ten/fifteen guards also passed.
Private literal recovery and historical binary availability remain work-machine
checks; this receipt is not private application acceptance.

### Recovery report truncation preservation

The literal-recovery Markdown now copies the original native query truncation
flag and sorted TruncatedByLimit reasons. A true flag with no reasons stays
truncated with reason unavailable; a missing flag stays unknown. False flags with
reason entries preserve the conflict. Recovery success cannot clear enumeration
limits. The work-machine prompt explicitly requires these fields in delivery and
reuses the same retained JSON/binary, without rescanning. Warning-as-error utility
build and all 14 native public guards passed locally, including four new truncation
regressions. No extraction or route semantics changed; no private input used.

### Consolidated local SQL evidence inventory

RecoverSqlCommandText now accepts repeated native --paths inputs against one
explicitly admitted binary. It preserves each input hash, query, native source
provenance, coverage, limitations and truncation, plus row-level classification,
confidence, notes and terminal limitations. Empty roots remain visible; identical
input files/content are refused. Rows stay input/path scoped, never deduplicated
into procedure counts or spliced into routes. Contract/schema decisions remain
explicitly unsupplied; parameter/connection/transaction evidence is not recovered
by this projection. No new extraction or completeness claims.

Bounds are 32 reports, 64 MiB aggregate report bytes, 256 paths per report and
1024 total rows. Existing binary/literal/work limits and disclosure consent remain.
Local warning-as-error build passed with zero warnings/errors; 20 native recovery
guards and 15 report-helper guards passed. A public three-root native smoke retained
one constant match, one symbolic route and one empty root in the consolidated
Markdown. No application methods or SQL executed. Windows revalidation of this
increment is not yet recorded. The new consolidate-local-sql-inventory prompt
reuses existing private inputs without another discovery/build/scan cycle.

### Optional local procedure contract candidates

The diagnostic accepts an explicit local sql-contract-catalog.v1 input and records
its digest. Unique exact recovered StoredProcedure literals can display supplied
procedure, database label, source reference and parameter name/type/direction.
No normalization or database guessing occurs; duplicate entries stay ambiguous,
and text/dynamic rows are ineligible. Metadata remains operator-supplied candidate
evidence, not verified schema or runtime bindings. Empty parameter lists do not
prove zero parameters. The public example is independently synthetic and does not
claim the fixture actually executes that parameter contract. The work-machine
prompt uses only an existing authorized catalog; otherwise it identifies the
missing input without another scan. Windows validation remains pending.
Local warning-as-error diagnostic build passed with zero warnings/errors;
29 native public-fixture diagnostic tests and 15 wrapper-report guards passed.
No private inputs, application execution or database connections were used.

### Client verification handoff

Inspection found existing database extraction metadata and broad contract-review
obligations already present in the grouped handoff. The missing delivery was an
actionable evidence-linked checklist, not proof that all SQL was absent. Grouped
HTML now renders per-operation tasks for entry/identity bridges, candidate paths,
command contracts, argument/results mapping, authorization, transactions and human
decisions. Local literal inventories render per-path private checklists, selecting
unresolved-expression review when no constant identity is retained. Checks never
auto-complete from a catalog or static path. Client tracker decisions remain
separate, with reviewer, evidence and API acceptance required per slice.
No machine schema or extraction evidence is upgraded; historical handoff validation
remains unchanged. Source-to-compiled wiring and dynamic SQL remain genuine gaps,
not silently fixed by reporting. Platform revalidation on Windows remains pending.
Local validation: 35 grouped-handoff tests, 39 SQL extraction/report/sample tests,
29 native literal-inventory guards and 15 wrapper-report guards passed. Diagnostic
and reporting/test builds passed with warnings-as-errors. No private inputs used.

### Separate application inspection from schema availability

The local inventory now preserves bounded native databaseCallEvidence and
commandBinding objects with node references independently of optional catalogs.
Missing objects mean targeted application inspection is pending, not absence of
behavior. The one-slice prompt requires caller/wrapper source or retained IL review
before application questions are escalated; schema-owner questions remain separate.
This is a reporting/procedure correction, not a new argument-flow extractor.
Warning-as-error diagnostic build passed; 31 native public-fixture guards passed,
including missing-object handling and parameter evidence without a catalog.
Windows revalidation remains pending. No private content or database access used.

### Projectless VB parameter construction extraction

vb-syntax/0.4.2 adds independent DatabaseParameterCandidate facts for explicit
SqlParameter constructor syntax under vb.syntax.database-parameter.v1. Facts carry
caller/source spans, hashed names/argument expressions and explicit SqlDbType
syntax candidates. Unique import/alias resolution or qualification is required;
ambiguous import sets are conservatively excluded. Named arguments and dynamic
expressions do not receive guessed types. Direction and command attachment remain
unknown; no wrapper/procedure parameter mapping or schema verification is claimed.
Scan Markdown includes bounded candidate rows with fact references. Added public
projectless fixture and nine focused positive/negative tests to Windows CI.

Local warning-as-error tests passed: 73 across projectless parameter/SQL reporting,
Markdown reporting, command state and VB extraction; eight independent SQL oracle
tests passed. CLI warning-as-error build and native projectless sample scan passed,
retaining two independent parameter candidates (one explicit type, one unknown).
Full 96-output SQL smoke and exact-head Windows validation are deferred to CI;
they have not been claimed for this increment. No fixture execution/database use.

### Root-scoped VB type lookup performance correction

Replaced per-resolution full-file declaration walks and import/alias rebuilding
with a lazy immutable-root lookup and memoized immutable resolution arrays. Weak
root keys prevent completed syntax trees from being retained by a global cache;
changed import roots receive independent contexts. Concurrent reads are supported.
No name prefilter or early return was added to ordinary object creation, so alias
parameters and normal ObjectCreated/CallEdge evidence remain intact. Resolution
ordering, case handling, shadowing and ambiguity semantics are unchanged.

Added old/new resolver equivalence, root-isolation/concurrency, ordinary-fact
preservation and large synthetic lookup tests. Local lookup-only measurement for
500 repeated lookups on a 2000-creation/20-import file: original 1296.91 ms versus
warm cached 0.02 ms (cache initialization excluded; not whole-scan performance).
Initial 56 VB/SQL/parameter regressions passed and native CLI sample retained 67
facts. Windows and full matrix validation remain separate pending gates.
The second warning-as-error run passed 78 HTTP/external-boundary/configured-import/
effective-import-context/lookup tests, including final concurrent cache guards.

### Projectless collection Add parameter evidence

vb-syntax/0.4.3 adds typed command.Parameters.Add candidates for a simple command
identifier with explicit System.Data.SqlClient or Microsoft.Data.SqlClient command
type evidence. Cached imports/aliases and relative qualification are supported;
same-file shadows and competing supported providers are refused. Shared argument
projection retains only hashes plus recognized enum syntax. Direction and command
instance/procedure association remain unresolved. Existing CallEdge emission is
preserved. With blocks, factories, collection aliases and AddWithValue remain
outside this increment. Added public fixture and positive/negative tests; the
existing Windows test filter includes them. No private source or SQL execution.
Local surrounding suite passed 63 tests before the final relative-qualification
case; final candidate suite and native CLI smoke are recorded below.
Final warning-as-error build/candidate suite passed 18 tests. Native public sample
scan retained three parameter candidates, including one collection Add candidate.
Windows and full 96-output matrix validation remain pending for this increment.

### 2026-10-13: bounded local parameter command association

vb-syntax/0.4.4 adds optional syntax-local-straight-line-candidate association:
fresh zero-argument local command creation, consecutive literal/enum parameter
Add calls, then direct execution. Maximum 256 method statements and 64 inspected
statements per candidate. Intervening statements, reassignment, aliases and
control flow stop association. Constructor-only parameters remain unresolved.
No compiler identity, procedure association, direction or runtime claim is added.
Reports show association status and execution line. Independent public sample
includes two associated candidates and one reassignment-negative candidate.
Warning-as-error focused suite: 70 passed, zero failed/skipped. Native sample
scan: 45 facts, exactly two association candidates and one unresolved candidate.
Fixture methods and SQL were not executed. Full SQL matrix and Windows validation
are deferred; the private caller has not been verified against this narrow shape.

### 2026-10-13: nested same-block association coverage

vb-syntax/0.4.5 supports a single Try/Catch/Finally/Using statement list in addition
to a method list. Fresh local constructor arguments permit up to three positional
literals/non-command identifiers. Simple parameter Value and command configuration
assignments permit similarly bounded right sides. Unknown calls, reassignment,
aliasing, branches and cross-block declarations remain unresolved. Every parameter
candidate now has an association reason; reports display it and explicitly separate
parameter extraction coverage from command association. No procedure/runtime proof.
Combined synthetic Try/constructor/Value/Transaction fixture plus negative controls:
78 focused tests passed. Final CLI warning-as-error build: zero warnings/errors.
Final native sample: 66 facts, three association candidates, one unresolved negative;
all four have reasons. No application/SQL execution. Windows and full matrix deferred.

### 2026-10-13: separate receiver and execution evidence

vb-syntax/0.4.6 retains commandReceiverAssociation and declaration spans once the
backward local search succeeds, independently of commandExecutionEvidence. The
existing commandAssociation property remains the combined forward-check result.
Complex later value expressions do not erase the Add-site receiver witness and
do not prove execution reachability. Pre-Add uncertainty still refuses association.
Reporting and the local inspection prompt now distinguish these counts explicitly.
Public property-chain/conversion/call controls added; no private source copied.
Focused warning-as-error suite: 82 passed. Native synthetic sample: 93 facts,
five receiver candidates, three following-execution candidates, two unresolved
execution results (complex value and reassignment). No fixture or SQL execution.
Windows and full SQL matrix validation remain deferred.

### 2026-10-13: consistent relative SqlClient resolution

vb-syntax/0.4.7 shares ResolveParameterCommandTypes between Add extraction and
backward declaration matching. Relative SqlClient.SqlCommand now expands through
Imports System.Data in both checks. No broad global resolver behavior changed.
Combined nested/constructor-arguments/complex-value tests cover relative names,
type and namespace aliases, Global qualification, unrelated types, same-file
prefix shadows and competing providers. Warning-as-error focused suite: 89 passed.
Native synthetic sample: 93 facts; all five receiver candidates retained, three
following-execution candidates and two unresolved execution results. No SQL or
fixture execution. Windows and full matrix checks remain deferred.
