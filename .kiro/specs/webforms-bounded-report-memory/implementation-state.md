# Implementation state

## Final return-value and all-terminal validation (2026-10-01)

Code head bf0093c8d70d0bf57b292dcb8ceb7718c682f697 passed the final full
.NET suite: 3016 passed, zero failed, one Windows-only skip, in 17m15s.
TRX: /tmp/tracemap-migration-all-api-validation/all-api-full.trx.
Exact-head Windows push run 36817615894 passed 27 tests with zero skips,
including both authentic ASP.NET publish modes and 11 return-projection cases.
Downloaded source/test-result hashes, generator hash and bounded-input digest
were verified; execution assembly hashes are receipt-only because those binaries
were not uploaded for independent rehashing.

Return-value implementation and all-terminal package regression gates are now
complete. Private saved-result acceptance is not: rerendering an old ledger does
not re-extract return summaries or traverse the graph. The private logging text
producer and original broad-query cap omissions remain unclassified. The overall
goal remains open; no private parity or expensive rescan is claimed or requested.

## Full return-value validation and all-terminal packaging (2026-09-30)

Code head cc42259d passed the full .NET suite: 3016 passed, zero failed, one
Windows-only skip, in 16m35s. TRX is retained locally under
/tmp/tracemap-return-value-validation/return-value-full.trx. A CLI smoke with the
two synthetic DLLs emitted 59 return summaries and no missing-input gaps; its
projectless source coverage remains Level3SyntaxAnalysis/NotRun, not a clean
semantic build. The first smoke used incorrectly source-relative input paths;
that partial output is preserved separately from the corrected absolute-path run.

Found and removed the migration package's hard-coded Fill-only requery filter.
The selected handler now targets all database API terminal kinds within the same
existing traversal bounds. Explicit Fill-only diagnostics are unchanged. The
native regression adds a synthetic scalar audit call to its copied, committed,
actually compiled provider before analysis, and checks that the packaged focused
report retains both Fill and ExecuteScalar with no terminal-name filter. It
passed: 12 broad native paths, 406 work units, with existing partial labels.
Reviewer instructions inspect actual saved query scope and recognize older
Fill-only packages; they do not infer completeness from the absent filter.

Full-solution validation of this additional packaging change and the exact-head
Windows corpus still remain before publishing. Private unresolved producer
classification and the original 256-path-cap omissions are not proved by these
synthetic tests, and the goal must remain open.

## Producer-return resolution integrated (2026-09-30)

The reporter now follows a call-result producer through its exact admitted call
edge, unique callee body and return summary. Body/operand/summary provenance and
String/Int32 return signatures are checked. Nested returns and returned argument
slots are substituted with a shared 64-unit bound, 256-edge/site admission caps,
and recursion detection. Every retained return site must agree. Virtual producer
targets, external/runtime composition and legacy missing return evidence remain
explicit gaps. Existing callvirt endpoint gaps are not removed by a constant.

ReturnSteps retain exact producer, callee-body and return-fact IDs separately
from call-stack steps. Existing constant-on-encoded-call-path remains a static
candidate state and can include this separate return evidence. Indexed body,
return and producer-offset lookups read at most two competitors; the memory
reader retains the same semantics. Bounded-input hashes include new limits and
all inspected return evidence, rejected competitors and lookup counts.

Validation: the broader deep/path/command slice passed 162 with one Windows-only
skip. Final return/property cases passed 13 with zero skips, including exact JSON
equality between memory and indexed readers. Positive literal and nested
forwarding, runtime composition, legacy missing summaries, ambiguous/changed/
malformed summaries, count mismatch, conflicting returns, mutual recursion,
virtual targets and work exhaustion are covered. Deep Windows selection now
requires all 11 return-projection cases in addition to the two property cases.
Full solution and exact-head Windows results are still pending. No private
verify-8 upgrade, all-route completeness, or work-machine rerun is claimed.

## Return operand extraction implemented; reporting pending (2026-09-30)

Scanner version il-body-evidence/0.1.10 retains ManagedIlReturnValuesObserved
under dotnet.compiled.il-values.v1. Straight-line returns retain one valid stack
operand; control-flow returns use the final converged input state at each reached
ret. Both independent readers must agree on the complete value observations.
Each body emits a summary capped at 256 sites, with exact generator/input hashes,
body reference, return count, flow gaps and candidate/unavailable state. The cap
is included in the bounded-input digest. Limit overflow withholds all return
origins and emits IlValueReturnSiteLimit. Addresses project to unknown.

The actual compiled VB LiteralText helper now emits the same string fingerprint
as the direct-literal control. Reporting deliberately still asserts unresolved:
the producer-return consumer has not been implemented. Existing private saved
facts lack this new evidence and must not be silently upgraded.

Validation: IL body/value/command and property corpus slice passed 137 tests with
one Windows-only skip before the final cap checks. The final rebuilt slice of
return-cap, operand and property tests passed 28 with zero skips. Diff checks
passed. Full solution and Windows gates remain deferred until the reporting fix
is integrated; no PR push or private rescan is claimed for this change.

Next: add indexed body/return and exact call-offset lookup rather than scanning
all retained facts per endpoint. Join the call-result producer to the exact
callee body and return summary, verify call shape/signature/hash provenance,
substitute returned argument slots back into the producer call, and follow
nested return origins with a shared bound and cycle detection. Keep returned
string composition unknown, record return evidence separately from call-stack
steps, and test old/missing/ambiguous/tampered summaries and virtual targets.

## Return-value boundary isolated (2026-09-30, follow-up)

Added InsertReturnedLiteral through the same property/constructor/provider path.
Its private static LiteralText helper returns the identical literal used by the
working direct control. The test checks the actual compiled helper's instruction
shape and literal equality with Mono.Cecil, without executing SQL. Both mixed and
compiled-only projections still report call-result/unresolved-operand for this
constant-return case. This distinguishes a demonstrated return-tracing limitation
from the original BuildText(message) case, which performs runtime composition.
The fixture now has six broad paths, five scalar paths and one Fill path; the
three InsertLog paths are unchanged. Both updated cases passed locally.

Code inspection: IlCallValueExtractor discards the stack at ret, and the
control-flow extractor stops at ret without exporting its operand. IL body facts
retain instruction hashes, not reconstructable instruction streams. Consequently
reporter-only substitution cannot recover a callee return value from these facts.
Next implementation must retain bounded, independently agreed return operands,
then join producer calls to exact callee evidence and substitute arguments with
cycle/work bounds. Runtime composition and unproven virtual dispatch must retain
explicit gaps. Legacy saved evidence without return observations must remain
explicitly unavailable, not be interpreted as a constant. No private rescan has
been requested; no analyzer fix, private resolution, or cap-omission proof is
claimed by this diagnostic regression.

## Property-based constructor logging regression (2026-09-30)

Added a separate public synthetic projectless VB source corpus and two external
net48 build harnesses (website and logging provider). It models a handler that
constructs a choices object; argument evaluation reads a lazy property, whose
backing-field miss constructs an employee object. Nested field initialization,
an authorization branch and an exception branch reach InsertLog in the second
DLL. InsertLog passes a helper's returned string to an overridable ExecuteText
wrapper ending at ExecuteScalar. A literal-input control uses the same wrapper,
and an independent business lookup uses Fill. No session dependency is required.

Both local mixed and compiled-only regressions passed: five paths, four scalar
paths, three InsertLog paths and one Fill path; 69 compiled / 105 mixed work
units. The text origin resolves to the exact BuildText producer call by body and
IL offset, then remains call-result/unresolved-operand with a virtual-dispatch
gap. Literal text resolves. Fill-only excludes the scalar routes, rather than
resolving them. These synthetic counts are not private report variant counts.

The corpus is included in the deep Windows validation selection and input roster.
This change pins the documented current limitation, not a production fix or
return-value evaluator. It does not execute fixture SQL, prove warm/cold cache
behavior or exception feasibility, or classify private path-cap omissions.
Validation: both final property cases passed; the deep set passed 14 with one
Windows-only skip; command-binding and messy-workspace regressions passed 126
with one Windows-only skip. Corpus guard, private-path guard and diff checks
passed. No new Windows execution result is claimed for this fixture yet.

## Consolidated migration handoff in progress (2026-09-30)

Owner requested a normal one-command fresh workflow and a single evidence folder
for Claude, followed by a PR. Added native migration-review orchestration over
existing proof import/start, completed-run single-handler mixed Fill requery,
verified tool retention and a separate bounded evidence index. query-migration
reads the package's original application evidence plus focused compiled evidence,
without source/SQL/graph execution. START-HERE.md carries the packaged Claude
instructions; the canonical review prompt now recognizes this package.

The successful local deep-fixture execution tests the fresh explicit-attestation
route, ready-native-config reuse, historical receipt-format import, focused root,
manifest hashes, bounded query, changed-index refusal, existing-output refusal,
missing-handler failure preservation and unchanged original run. The complete
deep test passed in 28 seconds. Added receipt-tamper, excessive query-limit and
changed-proof admission refusals to the broader validation run. The SQL-route
PowerShell regression, scoped comparison regression and private-path guard pass.
Exact code head 221ca517 passed all 14 Windows corpus tests with zero skips in
run 36808893833 (also passed the PR-triggered run 36808947245). Both authentic
ASP.NET publish modes and the consolidated native test passed. Downloaded TRX
counts and hash, plus the validation receipt bounded-input digest, were verified.
PR #798 is open against dev and attached to the task; not merged. Full local
.NET validation passed 2,996 tests, zero failures, one Windows-only skip in
13m18s. That build preceded only the sanitized stage diagnostic and help-text
placement edits; exact-code-head Windows validation covered those final edits.
No private migration-completeness claim.

Owner-returned saved comparison shows 13 historical symbol sequences/41 variants
and 18 current/56: all 13 symbol sequences are shared, shared variant counts do
not differ, five sequences are current-only, and none are historical-only.
Exact identities differ across scans. The focused Fill query reports zero
unresolved command-text candidates out of 56 binding occurrences, with depth
and cycle stops but no path-cap stop. These screenshots are diagnostic evidence,
not admitted private artifacts or proof of exhaustive coverage. The three
unresolved candidates from the earlier broader query have not been individually
classified merely because this focused query has none.

Also corrected wsqlroute's VerificationRoot default output to the verification
root, outside checkpoint-owned report folders. Previously adding its HTML under
the native report tree could invalidate the complete artifact roster.

## Scoped saved-route comparison (2026-09-30)

wcompare now accepts optional Handler and SurfaceName=DbDataAdapter.Fill filters
for every supplied report. This enables broad-versus-single-root comparisons
without counting unrelated handlers or terminals as missing routes. Root identity
ambiguity is rejected; absent retained rows remain zero, with original query
headers and pre-filter counts displayed. Filter arguments are included in the
bounded-input digest. The public comparison regression passed raw/grouped input,
scope exclusion, zero matches, ambiguous identities, existing sequence identity
and saved-evidence checks. This supplies a comparison mechanism, not a private
baseline result. Windows CI run 36794705978 at native code head f7ad8996 passed
all 14 corpus tests with zero failures/skips, including native completed-run
mixed/IL requery, changed-input refusals, and both ASP.NET publish modes. The
downloaded TRX counts, validation bounded-input digest and TRX hash were checked.
The later scoped-comparison commit changes only PowerShell/docs, with its own
passing regression. Owner-returned unresolved fields and route comparisons are
still required to satisfy the active coverage goal.

## Completed-run handler requery implementation (2026-09-30)

Extended the existing native handler requery to completed checkpoints as well as
failed-report recovery. Completed mode requires the exact checkpointed bundle,
hashes its compiled report and combined index, and selects the root through the
checkpoint-bound evidence query. The original checkpoint/plan/report are checked
again after traversal. Completed receipts use a nullable recovery hash and an
optional completedReportSha256; the added property is absent from legacy recovery
serialization. All existing path/depth/work caps and single-root identity rules
remain in force. No source scan or combine is invoked.

whandler -VerificationRoot locates the bundle with the original pinned status
reader, then uses the current built CLI for the separately attributed requery.
Its orchestration regression passed for both recovery and completed inputs, plus
unverified-state refusal. The deep native fixture now exercises mixed/IL requery
of a completed run, command evidence, receipt hashes, original roster stability,
existing-output rejection, foreign bundle rejection and changed-report refusal.
Native execution/recovery validation passed 118/118 with no failures/skips in
4 minutes 4 seconds. The final focused deep-fixture rerun passed in 7 seconds,
including changed-index refusal. Private unresolved reasons and baseline comparison remain
pending owner output; neither the new command nor tests resolve those by themselves.

## Remaining coverage goal started (2026-09-30)

The owner requested a new goal for the three unresolved text occurrences,
256-path truncation and historical route comparison. These are distinct from
the repaired zero-binding failure. The broad query shares its path cap across
seven roots; aggregate counts cannot identify omitted routes for one handler.

Added wsqlroute -VerificationRoot to locate the checkpointed completed report
through its pinned native status reader, requiring matching generator, verified
artifacts and completed state. -UnresolvedOnly displays only route groups with
an explicitly retained unresolved command binding, preserving their binding
fields/reasons and variant counts. Summary counts still describe the entire
selected handler; saved gaps/query limits remain the original broad query.
No scan or graph traversal occurs. Tests cover repeated unresolved variants,
constant exclusion, completed-root resolution and unverified-state refusal.

Code inspection also found RequeryHandlerAsync requires ReportsFailed plus a
recovery receipt. Thus the existing wrapper cannot requery a successful fresh
run. The next implementation step is completed-run single-handler admission,
with immutable combined-index/checkpoint verification and local native tests.
Do not direct the owner to the recovery-only wrapper for this successful run.
Private unresolved binding fields and historical/current route artifacts are
still on the owner's machine; screenshots only supply their aggregate counts.

## Owner fresh-run result and ledger wording (2026-09-30)

Owner-supplied screenshots show a fresh run at f32c6ee9 completed native reports.
The selected handler's ledger reports 27 route-record groups, 45 variants and
45 command-binding occurrences: 42 constant-text fingerprints, 42 stored-procedure
type candidates and 3 unresolved-text candidates. The saved broad query reports
algorithm 1.3, 20,504 work units and 256 paths with truncation. This confirms the
observed zero-command-binding failure has changed on the owner's application;
counts overlap across variants and do not identify 42 distinct procedures.
Private raw artifacts have not been independently admitted, exhaustive baseline
parity is still open, and physical drive reads were not measured in these images.

The ledger still printed an unconditional unresolved-command-text warning from
before command-binding support. Added the existing console counters to its HTML
and conditioned the missing-binding warning on the retained bindings. SQL-surface
absence remains distinct from command-binding candidates on database API nodes.
The helper only projects saved results; this display repair requires no scan.
The SQL ledger regression covers both present and absent command bindings and
passed, along with command-verification orchestration, privacy and diff checks.
The scanner code is unchanged from the full 2,996-pass local suite and 14-pass
authentic Windows corpus. The traversal/command-evidence repair goal is satisfied
at its stated scope; complete private migration coverage is not part of that
acceptance and remains unproven.

## Authentic Windows corpus accepted (2026-09-30)

Code head 6dbdf2baf64c3e8ce510ac2c4aa8483eed6f6372 passed CI run 36788401704:
14 passed, zero failed, zero skipped. Both authentic ASP.NET mapped/mapless
publish theories passed. The unchanged 32-MiB logical payload guard measured
19,483,643 bytes mapped and 15,027,147 bytes mapless; receiver/implicit bridge
stages read 120/74 rows. Native start/resume passed all assertions and retained
six mixed routes in 391 work units. Downloaded the public artifacts and checked
TRX counts, wrapper generator/source hashes (explicit Windows CRLF checkout
bytes), bounded-input digest, source commit, all published DLL/map hashes, map
inventory digests and separate provider PDB hashes. Publish roots contain no
PDBs. Execution assembly hashes are retained by the wrapper but binaries were
not uploaded, so independent execution-binary rehashing is not claimed.

The public corpus checklist is complete. The final full local suite at the same
code head passed 2,996 tests, zero failures and one explicit Windows-only skip in
16 minutes 38 seconds; that skipped theory was separately covered by both
passing authentic Windows cases above. The focused slice passed 231 tests with
one Windows skip. Privacy and diff guards passed. The owner-retained private
application fresh-scan gate remains open.
No physical drive-read, exhaustive private route parity, SQL execution or
migration-readiness acceptance is inferred from this synthetic Windows run.

## Authentic Windows corpus follow-up (2026-09-30)

CI run 36787260898 at 4c94db97 built both actual ASP.NET publish modes. It failed
two checks: mapped graph logical reads were 42,887,673 bytes against the unchanged
32-MiB guard, and Git read-only object files prevented synthetic native cleanup.
The retained native output still reported six mixed routes in 391 work units;
cleanup failure is not promoted to test acceptance.

Downloaded the public published DLLs and reproduced the mapped read excess on
macOS. VB receiver/implicit/constructor bridges still performed full fact-table
decodes for predicates limited to known fact types. Changed their input iterators
to indexed FactsOfTypes queries without changing their candidate predicates or
ambiguity rules. The same mapped replay fell from 13,943 rows/42,434,281 bytes to
6,432 rows/19,094,075 bytes. The 32-MiB bound is unchanged; dedicated bridge-stage
guards cap the small corpus at 200 payload rows per receiver stage. Synthetic
native teardown clears only the ReadOnly bit on test-owned temporary files before
deletion. The public replay override used for diagnosis was removed.

The focused 231-test slice passed with one explicit Windows skip and no warnings.
Final full-suite validation and a fresh authentic Windows CI run are pending.
These logical read counts are not physical drive-I/O measurements or private
application acceptance.

## Mixed-query traversal repair locally validated (2026-09-30)

The first completion continuation verified d3ff0281 at origin with a clean local
worktree. Only the private-path CI guard ran at that head; the existing Windows
workflow filters do not include this corpus. Added a public-only Windows CI lane
to execute the same required-publish wrapper, preserving both successes and
failed-attempt artifacts for exact-head inspection. Authentic Windows results
remain pending; no new private scan is launched by CI.

The previous corpus-only goal was closed too narrowly; the actual work-machine
fix remains the acceptance target. A native mixed-report assertion reproduced
zero deep routes under the 100,000-work limit before the repair. IL-first edge
priority recovered the handlers but alone still exhausted work. Algorithm 1.3
adds a bounded reverse terminal-distance pass: at most one quarter of the same
work budget and at most MaxFrontier state keys, reading indexed predecessor keys
without whole edge payloads. Only a complete pass can prune a branch; an
incomplete pass is discarded. Pruning uses an over-approximation of admitted
edges, does not admit any path, and retains explicit depth gaps. Roots without
an in-depth reverse witness retain the prior traversal/diagnostic behavior.

The repaired native regression retains six expected routes across all five
handlers in 391 work units, below a pinned 1,000-work assertion. It checks the
exact twelve-layer/two-DLL method sequence and command text/type, both branch
literals, unresolved unknown/computed operands, decoy exclusion, original
input preservation and byte-identical resume. Depth/cycle gaps remain partial.
The legacy work/path/frontier guards and compiled-only edge scope are unchanged.
The focused graph/packet/IL/admission/handoff/native slice passed 231 tests with
one explicit Windows skip. The final algorithm-1.3 corpus wrapper passed 12
tests and one Windows skip, retaining a source/executed-assembly-bound receipt.
The exact final full .NET suite passed 2,996 tests, zero failures and one explicit
Windows skip in 14 minutes 26 seconds. CLI scan emitted 744 facts with reduced
syntax coverage and passed artifact validation. Wrapper guards, command-verify
orchestration, command-fact reader, private-path and diff checks passed; the
build emitted no warnings. Changes remain on codex/webforms-config-migration.
The earlier corpus-only patch is superseded by this repair. Authentic Windows
mapped/mapless and private fresh-scan results remain open; do not close their
acceptance goal based on this Mac run. No physical drive-read, complete private
migration or unrun Windows acceptance claim is made.

## Deep projectless cross-DLL corpus started (2026-09-30)

Added projectless synthetic website source and an external net48 build harness.
The handler forwards through twelve layers into the separately built existing
legacy provider. The first local native scan/index/combine/compiled-path test
passed and retained caller-substituted text/type at Fill. All four compiled-only,
mixed mode, missing-provider and shallow-depth cases passed locally (one second
test execution after build). Diff whitespace validation passed. This is not yet an
ASP.NET publish or retained-proof workflow test, nor a full branch/parameter or
performance acceptance claim. The broader fixture task remains open.

Expanded the corpus with five actual Page handlers, twelve forwarding layers,
branches, a cycle, same-named decoys and unknown values. Direct query cases pin
exact method order and UTF-16 IL literal fingerprints. Computed Boolean caller
operands currently retain an explicit unresolved provenance gap; the fixture
does not silently infer their text. The native case builds exact committed
synthetic source in a temporary repository before preparation, avoiding false
attribution of unrelated prebuilt DLLs to its commit. Generated mapping provenance
is explicitly not an ASP.NET compiler claim.

The native broad mixed-source report reproduces work exhaustion at 100,000 work
units with no deep route returned. A separate single-root compiled-only query
over its retained combined index recovers text/type through the full chain.
No core scanner or traversal policy was changed, and broad mixed-source parity
remains open. Both synthetic DLLs are primary because this case compiles both
from the exact test commit; external private providers cannot inherit that claim.

The Windows publisher now accepts -DeepChain for mapped and mapless outputs,
and hashes every provider VB build input rather than just one source file.
Windows tests are visibly skipped on macOS. The retained validation wrapper
records exact script/input/executed-assembly hashes and refuses existing roots
or required Windows acceptance on macOS. The first full fixture wrapper passed
12 local cases with one explicit Windows skip in 10 seconds after build. The
related IL/admission/grouped-handoff/native slice passed 112 cases with the same
one skip in 23 seconds. CLI scan emitted 744 facts with syntax/reduced source
analysis and passed adapter-artifact validation. Wrapper guard tests, publisher
syntax, private-path and diff checks passed. Source snapshots are now checked
before/after wrapper execution. The final wrapper rerun passed 12 local cases,
one explicit Windows skip and no failures in 10 seconds, retaining the TRX and
generator/input-hashed receipt. The full .NET suite passed 2,995 cases with one
Windows skip and zero failures in 16 minutes 42 seconds. The build emitted no
warnings. All new tracked-intent source files passed the private-path guard.
No scanner/reducer code was changed. Authentic mapped/mapless Windows results
and broad mixed-query parity remain open; the one-command Windows handoff is
ready, not executed on this Mac.

## Legacy VB operand repair after owner zero-candidate validation (2026-09-30)

The owner fresh scan observed call-value facts but zero command candidates.
That result supersedes the earlier local completion language for private
acceptance. Correct provider IL evidence showed ordinary debug-field stores,
byref arrays, struct/local addresses, checked arithmetic and mapping loops, plus
timeout/transaction/mapping APIs that the local fixture had not exercised.

IL 0.1.9 / policy v7 now retains stack shape for these operations without
claiming heap values. Stores/address reads and calls expose affected objects;
exposed slots remain exposure-capable on later writes. Address identity depth,
length and per-instruction origin retention are bounded, and exposure work is
charged. Unknown calls preserve only unexposed allocation-local receivers;
argument/call-result origins and escaped objects remain conservatively aliased.
Known byref call shapes retain non-byref scalar origins. Exported addresses and
byref operands are unknown, not concrete caller values. Leave drops locals and
potentially rewritten arguments while re-establishing an empty evaluation stack.

Exact encoded timeout/transaction setters and table-mapping getter/Add contracts
preserve text/type only. Wrong overloads and mapping escapes discard state.
A public synthetic net48 VB legacy wrapper exercises the real compiler output,
with debug fields, ref arrays, struct parameters, loops and all three API shapes.
Its native encoded caller path carries hashed text through two wrapper calls
to Fill with a StoredProcedure enum candidate. No private source was copied.

Validation: the full .NET suite passed 2980 tests, zero failures/skips, in
14m21s before the final address-size guard and three additional regressions.
The final solution rebuild had zero warnings/errors; the final 122-test IL/native
slice passed with zero failures/skips in 1m34s. A direct CLI scan of the public
VB fixture emitted 631 facts and three command candidates, with Level1 semantic
source analysis, and passed adapter-artifact conformance. Compiled artifact
context, branch feasibility, command parameters and runtime proof remain partial.
The CLI smoke requires an absolute compiled-input path; a relative input is
resolved against the source root, not the invoking working directory.

PowerShell command-facts, immutable command-verification orchestration and
tool-copy tests passed. Seven artifact-validator tests and the private-path/diff
guards passed; the unchanged validator test emitted its existing SQLite
ResourceWarning. Unchanged TypeScript/JVM/Python adapters were not rerun for this
.NET-only repair. Owner Windows acceptance remains open and requires a new
output run; old evidence is unchanged and cannot gain the new extractor facts.

## Fresh command validation workflow and final local gates (2026-09-30)

The implementation requirement is complete locally: independently agreed bounded
IL operands, receiver/configuration joins, exact encoded caller substitution,
conservative control flow, explicit unresolved gaps and exact generator/input
provenance are covered by native end-to-end tests. `wcmdverify.ps1` now starts a
new retained-proof scan through `wverify.ps1 -Run`, then uses only the copied
distribution. It verifies native generator agreement and the full copied-tool
fingerprint. Existing output is refused; failures remain preserved.

Completed-run diagnostics filter retained routes without changing query scope.
Only an admitted evidence-node-limit report failure takes the separately labeled
recovery plus new compiled-IL handler/Fill requery branch. Public orchestration
tests cover both branches, failed recovery stopping, changed-tool refusal and
existing-output refusal. They do not substitute for native evidence admission
or owner Windows validation. The ledger reports as-supplied command binding,
hashed text, StoredProcedure-type and unresolved text candidate counts, without
SQL inference or raw command text. The operational guide gives the exact
PowerShell entry point and distinguishes both query scopes.

Final local validation against stable .NET binaries: full solution build had
zero warnings/errors; all 2965 .NET tests passed with zero failures/skips in
17m01s. SQL-ledger, fresh-command-workflow and tool-copy PowerShell tests passed.
A direct CLI scan of the public net48 VB wrapper emitted 330 facts, including
15 command-binding rule records, and passed adapter artifact conformance.
Source analysis in that explicit smoke remained syntax/reduced, not semantic
or runtime proof. TypeScript build plus 257 tests, JVM Java 21 tests, Python's
64 tests and endpoint smoke, seven artifact-validator tests, private-path guard
and diff check passed. The unchanged JVM extractor emitted a deprecation note;
the unchanged Python validator test emitted a ResourceWarning; dependency
installation reported two moderate advisories in the existing TypeScript lock.
No unrelated dependency update was performed.

Windows-native PDB/ILAsm, private full-site and historical dotnetperf acceptance
are not run on this macOS host. Original private runs remain untouched; no
resolved procedure identity, executed SQL, complete coverage or parity is
claimed. The complete implementation and workflow were pushed normally to
`origin/codex/webforms-config-migration` at `af420d56` after the local gates.
The compiled command-value implementation task is complete; the separate
owner-retained historical comparison checkbox remains open for private validation.

## Protected-region wrapper and AddRange validated (2026-09-30)

Normal-flow fixed points now include protected blocks when both readers agree
on exception entries. Handler/filter roots receive unknown pre-exception locals
and potentially modified arguments. Command state starts empty at these roots.
Leave discards operands/configuration rather than carrying them across unknown
finally effects. Exception dispatch and finally continuations remain explicit
gaps, not ordinary fall-through. Handler seeds are work-charged before allocation
and capped at 1024 declared entries. Policy v6 binds that bound; IL version 0.1.8.

Exact typed provider parameter-array AddRange and framework Array overloads are
modelled for text/type preservation only. Read-only field loads produce unknown
values without destroying unrelated stack/local origins; field values remain
unproven. Incorrect AddRange signatures invalidate the owning command.

The existing public VB net48 PublicSqlDataAccess wrapper builds with zero
warnings/errors and now has a build-only test project reference (never loaded
or referenced by net10). Framework negotiation is disabled on that non-reference
to avoid a proven NU1702 false positive. The native test uses its matching build
configuration, independently decodes its real compiler output, retains both
normal ExecuteNonQuery/Fill bindings with argument-slot text and StoredProcedure
type, and proves exact native/compiled-path generator/input provenance.
Synthetic tests also cover nested Using regions, parameter loops inside protected
blocks, caller propagation through those blocks, unknown handler locals, leave
invalidation, wrong overloads, field reads and entry limits.

The pre-final 225-test IL/path/native regression passed with no build warnings.
After handler-seed work/entry hardening, all 58 focused tests and the final
226-test stable-binary IL/path/native regression passed.
The control-flow/parameter task is complete for the explicitly documented static
candidate contract, not exception/runtime proof. Broader final gates, the
immutable-tool Windows workflow and push remain open. Private acceptance is
still owner validation; no original run has been replaced or upgraded.

## Normal-flow command/parameter loops in progress (2026-09-30)

Normal branches and loops now use independently decoded equality-only operand
fixed points, with 20000 instruction, 256 slot and 200000 work-unit body bounds.
Actual fixed-point work is charged to a separate aggregate value budget per
reader, equal to MaxTotalWorkUnits and isolated from raw body/call admission;
successor cloning and merging is charged before allocation, including wide
switch fan-out. Policy v5 binds these limits, and IL extractor version is 0.1.7.

The command binder consumes the agreed control graph, intersects exact command
and adapter configuration records at joins, and emits endpoints only after
convergence. Exact encoded get_Parameters plus supported Add/AddWithValue/Clear/
RemoveAt contracts preserve text/type, not parameter values or ordering.
Unknown calls receiving a collection invalidate its owning command. Wrong
collection signatures, command escape and conflicting assignments fail closed.
The linear lane models the same collection contracts. Caller mapping accepts
both straight-line and converged normal-flow operand candidates.

All 45 focused operand/command tests passed before the final fan-out charge
hardening. Tests include independently decoded synthetic parameter loops,
straight-line collection use, conflicting assignments, command/collection
escape, incorrect signatures, native fact and compiled path provenance joins,
control-graph reader disagreement, deterministic convergence and work limits.
The broader stable-binary regression passed all 213 selected tests after that
hardening. A subsequent audit isolated value derivation budgets from raw body/
call admission and added a regression proving exhausted operand budgets retain
both readers' agreed bodies and calls. The focused 90-test gate and final
214-test IL/path/native regression both passed against unchanged binaries.

Exception-region bodies still use the reduced local lane: exception-edge and
loop configuration inside protected regions need a targeted audit/fixture before
closing the control-flow task. The existing public synthetic
samples/messy-dotnet-workspace/vb-publish-crossdll-framework/PublicSqlDataAccess.vb
is an authoritative next fixture: its Using command/adapter regions, conditional
ExecuteNonQuery versus Fill, and typed AddRange must be covered before claiming
the wrapper pattern is handled. AddRange is not yet in the supported collection
contract set. Broader final gates, immutable-tool Windows
workflow and push remain open. No private acceptance or SQL execution is claimed.

## Exact compiled caller substitution in progress (2026-09-30)

Command text/type argument slots now map through the ordered encoded IL call
path, with separate static/instance slot numbering, exact target/body/caller
joins and unique operand facts. Source-only bridges, missing/ambiguous operands,
changed provenance, unavailable slots and unresolved root arguments stop with
explicit nested gaps. Encoded callvirt targets retain a dispatch gap. Strings
stay hashed; no SQL is inferred from names or hashes.

The local-only compiled-command-path-value.v1 candidate binds the exact reporting
assembly SHA-256 and a bounded consumed-input projection, including rejected
operand candidates. Limits are 64 caller hops, 128 operands and two competing
operand records per lookup. Indexed graphs use a dedicated scratch SQL lookup
rather than a second full payload dictionary; scratch schema is v3. Native call
value facts additionally retain independently agreed call shape and IL extractor
version is 0.1.6.

All 17 focused command-binding tests passed, including multi-hop compiled
wrappers, instance slots, absent/ambiguous/changed operand facts and a source-only
bridge with orphaned IL operand evidence. Repeated queries retain identical
binding projections. The broader stable-binary IL/path/native regression passed
all 195 selected tests. Relevant control-flow/parameter
configuration, broader gates, immutable-tool Windows workflow and final push
remain open. This milestone does not complete the goal or validate private runs.

## Compiled command configuration and path projection in progress (2026-09-30)

The scanner now derives independently decoded straight-line command text/type
configuration candidates and joins them to the same command Execute or adapter
Fill receiver. Strings remain exact UTF-16 length/hash; argument slots and
call-return values remain unresolved. The binder invalidates configuration at
branch targets, exception boundaries and possibly mutating calls, including
commands exposed through adapters. It has 128 receiver / 200,000 work limits
per body and a separate aggregate phase bound matching the policy-bound IL
MaxTotalWorkUnits. Exhaustion withholds that body's command candidates.

Rule dotnet.compiled.il-command-binding.v1 and IL policy v4 document the
candidate contract. Facts retain exact body, endpoint and configuration-call
IDs plus generator/input hashes. Compiled path nodes expose an optional
CommandBinding only after unique same-source body/call/configuration joins with
matching raw-file/generator/input hashes. Invalid joins keep the existing API
path and add a binding gap. The local SQL ledger retains this evidence; it still
does not turn a command string hash or Fill into a resolved SQL/procedure claim.

Synthetic Cecil-written binaries are independently decoded by raw SRM and
Cecil. Tests cover adapter/command receiver identity, StoredProcedure enum,
hashed literals, wrapper argument slots, branch targets, mutation, framework
lookalikes, work caps, native fact materialization, end-to-end compiled path
projection and changed-provenance rejection without raw literal retention.
The stable-binary IL/path/native regression passed all 264 tests. A prior run
overlapped a rebuild and had one checkpoint failure; all four relocation cases
and the complete 264-test set passed when rerun against unchanged binaries.
The PowerShell SQL-ledger regression passed. The final 20 operand/command
checks passed after the setter signature guard. Supporting operand/configuration
rows are not independent graph symbols or traversal roots; the final 14
binding/baseline tests passed after that graph-support separation.

Remaining requirements are substantive: exact-call-path caller substitution,
configuration joins through relevant loops/parameter construction, broader
final gates, the fresh immutable-tool Windows workflow, and push. Original
private runs remain unchanged and no private acceptance is claimed. The overall
compiled handler-to-SQL task remains unchecked; this is not goal completion.

## Compiled operand-origin foundation in progress (2026-09-30)

The active goal is the actual compiled-IL command/receiver binding fix, not
another saved-report projection. The owned lane now has a bounded local
operand tracker over both independently decoded streams. It retains hashed
literal, argument-slot, allocation and call-result origins at exact call sites.
Value observations join to admitted body/call facts and carry the existing
exact generator and bounded-input hashes. Rule dotnet.compiled.il-values.v1
labels these Tier3 candidates; unsupported flow and value-reader disagreement
emit explicit gaps without upgrading or replacing body evidence.

Initial build passed with zero warnings. All 51 operand/body tests passed,
including fact joins, provenance and privacy after materialization. The final
eight focused operand/reader-disagreement regressions also passed after isolating
value disagreement from independently admitted body evidence.
This foundation is not the SQL fix: exception flow currently withholds all
origins, and command configuration, endpoint binding, interprocedural argument
substitution, end-to-end fixtures and the Windows validation script remain open.
The overall compiled command-value task checkbox remains unchecked.

## Source command-assignment extraction slice (2026-09-30)

Owner ledger succeeds with 18 groups / 56 variants, 56 database API occurrences
and zero SQL surfaces. Inspected retained Fill nodes have no command/table/text
hash evidence. This is an extraction/binding gap, not a report-rendering fix.
IL body observations currently retain calls and hashes, not command-value flow.

Found and corrected a separate concrete VB semantic boundary omission:
CommandText property assignments and DbDataAdapter command assignments were
not emitted. They now retain compiler-resolved receiver identities, constant
text hashes or dynamic classification, and direct-symbol binding candidates.
No raw text, parameter values, last-write/alias/branch/runtime binding is claimed.
Extractor version advances to vb-semantic/0.8.5. Framework lookalikes are rejected.

Validation: five ADO.NET boundary tests and 89 VB-focused tests passed. Modern
VB CLI smoke emitted 222 facts with Level1SemanticAnalysis and passed artifact
conformance. All 32 Web Forms composition/code-path review checks passed.
The build introduced no compiler warnings. Full .NET regression
and pinned CommunityVB OSS smoke are deferred for this bounded slice; they are
required before PR readiness. This source slice does not fix the private
projectless/IL-only command-value path. End-to-end SQL binding remains open;
do not rerun the old ledger as if it can acquire new facts.

## SQL ledger output-cap correction (2026-09-30)

Owner run at 3fab3a84 failed WEBFORMS_SQL_ROUTE_OUTPUT_LIMIT before output
creation. The helper rendered the entire retained gap array, contradicting its
bounded display intent. Replaced that dump with kind/reason counts and bounded
samples: 1,000 categories admitted, 200 shown, three samples per category,
1,024 characters per field and 8,192 characters per sample section. Overflow
counts and display truncation are explicit. Route display has an 8 MiB budget
and individual text/JSON sections are clipped at 65,536 characters. The existing
32 MiB final output cap remains; private inputs are not modified.

Regression uses 6,000 repeated gaps with a >32 MiB message payload (and HTML
escaping expansion) and requires successful output below 100 KiB with exact
gap counts and explicit truncation. Existing raw/grouped and reference/escaping
tests remain required. This fixes rendering only, not SQL binding or coverage.

## Saved handler SQL evidence ledger (2026-09-30)

Owner photos show the historical cart routes use downstream publish-member
candidates and projectless VB receiver bridges, and the bid-group route uses
VB constructor/receiver bridges. All three symbol hints occur in the saved
mixed-mode output with 3 / 1 / 3 variants. This resolves this particular count
discrepancy as a scope difference, not general route parity or complete coverage.

Added `wsqlroute.ps1 -Open`: bounded, file-only raw/grouped path projection of
database API and retained SQL surfaces, including node rule/tier/span/provenance,
non-IL transition kinds and original report gaps. No SQL is inferred from method
names or Fill; absent SQL surfaces and edges remain gaps. Input/output hashes
bind the private HTML, references fail closed and existing output is preserved.
Public PowerShell tests cover grouped/raw input, SQL/no-SQL cases, bridges,
escaping, source preservation, differing node evidence, repeat filenames,
explicit collision and missing references. Owner ledger readback is pending;
no SQL command, parameter propagation or runtime completion is claimed.

## Historical-only edge and mixed-mode diagnosis (2026-09-30)

Owner comparison confirms 13 historical symbol sequences / 41 variants versus
10 current / 34, with all ten shared counts equal, three historical-only hints
and no current-only hints. Photos show interleaved assembly-qualified and source
labels in those three sequences, but labels alone do not prove edge kind or a
porting defect. The compiled-only filter remains unchanged.

The file-only comparator now exposes every supplied historical edge-kind/rule/
tier/from/to sequence for each relevant symbol group, resolves grouped edge
references fail-closed, and checks the saved sibling mixed Fill report when
present (or an explicit -Mixed input). Mixed query/source context and its hash
are bound into the output. Empty edge arrays and absent mixed reports remain
explicit gaps. Public tests cover mixed overlap, edge evidence and missing edge
references. Owner edge readback remains required before any graph correction;
no runtime or route parity claim is made.

## Saved historical/current route comparison (2026-09-30)

Owner `wprewalk` readback is false for the saved compiled-only 10 / 34 report.
Added `wcompare.ps1 -Open` to read an original raw handler JSON and the current
raw or native grouped handoff. It discovers historical handler database API
JSON files within retained proof directories and asks for an explicit selection.
Both reports are grouped with the same exact identity-field projection; variant
counts remain separate. Symbol-only sequence overlap is a hint because it omits
scan provenance. Query, coverage, declared source/index context and file hashes
are shown in local HTML. No native admission, rescan or graph traversal is claimed.
The helper binds its generator and ordered input file hashes to the diagnostic.

Owner comparison of the retained Fill-only 13 / 41 report and current 10 / 34
report found zero shared exact identities and ten shared symbol sequences.
This does not yet distinguish three distinct missing routes from alternate
historical provenance representations. The helper now retains symbol-group
variant counts and displays historical/current-only symbol sequences plus
shared count differences ahead of exact-identity differences. Default repeated
comparisons use a free numbered filename; explicit output collisions still fail.

Regression checks passed for raw/grouped comparison, variant-count changes,
cross-scan identity differences, empty reports, missing references, output
collision, symbol aggregation across exact identities, numbered repeat outputs,
HTML escaping and input preservation. Actual private route diagnosis
remains pending the owner's symbol-difference details; no traversal fix is inferred
from arithmetic alone.

## Independent review: compiled-only depth prewalk leak (2026-09-30)

Independently reproduced the review's F1 on `b9223223`: the main compiled-only
walk depth-truncated, then unrestricted FindShortestTerminalWitness emitted a
`calls -> surface-evidence` source shortcut with TerminalReachabilityPrewalk.
That positive path suppressed CompiledBaselineNoPath and expanded reachable
diagnostics. The review repro was promoted into product tests, with controls
for valid compiled Fill chains and historical mixed depth recovery.

Fix: skip the unrestricted depth-recovery prewalk for compiled-only queries.
Their normal bounded IL walk and depth gaps remain authoritative. No new BFS
policy was introduced. An attachment and terminal each consume a depth edge.
Owner readback 10 chains / 34 variants at b9223223 is provisional; the photos
do not establish whether the fallback ran in that specific artifact.
The fallback skips any root with a positive TerminalPathCount and emits at
most one witness per zero-terminal root. With one selected root and 34 retained
variants, depth truncation alone is insufficient to infer contamination.
`wprewalk.ps1` streams the saved grouped HTML and prints only marker presence,
without a build, graph walk, native validation or output artifact.

Historical `abcfe32b:wpath.ps1` uses all database APIs, default traversal work
and raw path-row counts. Later wpath supports Fill and provider filters.
Those code differences do not establish which exact invocation/artifacts
produced the historical 13 / 41 grouped readback. Route parity requires its
actual query provenance and sequences; counts alone remain insufficient.

Validation: the imported leak regression failed on b9223223 before the fix.
After the fix, 52 focused scope/grouped/attachment tests and six legacy-prewalk/
report-recovery tests passed. The marker helper passed split-buffer, absence,
missing-input, content privacy and input-preservation checks. CLI build:
zero warnings/errors; diff check passed. Full .NET suite and private readback
were not executed. The historical route comparison remains open.

## Separate compiled call-tree baseline (2026-09-30)

Owner Fill-only photographs show 18 exact chains / 56 variants, 1,579,018
traversal work units and cycle/depth gaps. Names are readable; mixed paths still
include source bridges and cannot close historical 13 / 41 parity.

Added `whandler -Requery -CompiledOnly -FillOnly -Open`, isolated default folder
`handler-requery-compiled-fill`. Traversal permits one existing root attachment
(semantic identity, PDB identity or publish candidate), then only compiled IL
call/callvirt/database endpoint edges. It cannot return through source to gain
extra paths. Full competitor admission and prior budgets stay unchanged.
Optional query `TraversalScope` is included in the derived receipt input hash;
legacy queries omit it. No runtime/dispatch/build authenticity claim. An empty
restricted walk emits a scoped gap rather than NoBackendEvidence placeholders.

Validation: 221 path/recovery/grouped-report tests passed; after the final
Tier4 query-gap refinement, 28 focused scope/recovery/attachment tests passed.
PowerShell handler dispatch/guards passed. CLI build: zero warnings/errors;
diff check passed. Synthetic explicit PDB and publish attachment walks actually
executed on macOS; Windows-only ASP.NET publish smoke and the full .NET suite
were not run. Private compiled-only readback remains pending.

## Explicit retained Fill-only comparison scope (2026-09-29)

`whandler.ps1 -Requery -FillOnly -Open` now forwards the exact allowlisted
`--surface-name DbDataAdapter.Fill` selector and uses a separate default
`handler-requery-fill` destination. The default all-database-api scope remains
unchanged. Invalid surface names fail before output writes. The existing receipt
query/bounded-input digest includes the filter. Global graph admission, source
bridges, overload/dispatch competitors and budgets are unchanged; no claim of
IL-only execution or old/new parity. This repeats graph construction/traversal,
not scan/combine. The owner's Chain 11 photographs retain the intended constructor,
business/data layer and both framework overloads through the Fill candidate.

Code inspection confirms Cecil and SRM both decode/check IL bodies in
IlBodyEvidenceExtractor; the reporting bridge consumes those retained agreed
calls, matching exact MethodDef/assembly-scoped MemberRef targets. Callvirt is
candidate evidence and calli cannot supply a named member target. No replacement
IL decoder, assembly loading or guessed external resolution was added.

Validation: 30 grouped/recovery tests, 44 IL body extractor tests and the PowerShell handler guard passed,
including query filter receipt binding, unchanged original index, invalid-filter
refusal, default behavior and isolated output. CLI build had zero warnings/errors;
diff check passed. Private Fill-only count/path
comparison remains pending and cannot be inferred from passing synthetic tests.

## Pre-PR reconciliation and local label regression (2026-09-29)

The earlier readable-path change `a9267263` shortened canonical method symbols
in the PowerShell local handoff. Native writer `8762069c` ported that formatter,
but only supplied `DisplayName`. The graph's general safe-display policy hashes
canonical compiled identities because `publicKeyToken` contains `token`. Exact
`SymbolId` remains in the private lossless handoff. Native local-only rendering
now uses that retained canonical method/constructor symbol when its display is
redacted; normal global/shared privacy projections and graph matching are
unchanged. HTML labels are escaped and never used for grouping or identity.
Regression tests pin method and constructor labels with publicKeyToken, exact
identity detail, unchanged JSON and lossless restore.

The earlier `037ac6e2` inline-object-creation exclusion from implicit-Me bridging
and `6783dc0b` constructor qualification are ancestors of current HEAD and their
guards remain in the current implementation. The IL bridge diff from PR #797's
merge head `8cae5664` contains indexed fact-type lookups, not target matching
changes. These inspections do not establish equality of the owner's old/new
private index bytes or complete private path parity. Old `wpath.ps1` also offers
an explicit Fill-only query and provider selection; the isolated native query
currently selects all database APIs. Do not conflate differently scoped counts.

Validation: 35 focused grouped-report and constructor/inline-receiver tests
passed, plus five public explicit-attachment cases. The Windows mapless provider
test returns early on macOS, so its nominal pass is not execution evidence; that
aspnet_compile/decoy check remains deferred to Windows. CLI build had zero
warnings/errors and diff check passed. Full .NET suite and private historical
path-identity parity remain unverified. Existing reports are immutable; the repair applies
to newly generated reports and does not require an immediate owner graph rerun.

## Owner handler requery readback and chain diagnostic (2026-09-29)

Owner hit the diagnostic's 64 MiB JSON cap. The revised v2 helper streams the
already-generated HTML instead, extracts one complete chain section, and renders
all its collapsed evidence as safely re-encoded plain text. It hashes the locked
HTML and selector, never opens the JSON inventory or combined index, and retains
create-new output semantics. HTML is capped at 512 MiB, a selected section at
8 MiB and each rendered line at 1 MiB. Tests pass with an oversized unused JSON,
chain isolation, encoded method names, evidence visibility, immutable input,
output collision, missing chain and incomplete section rejection. The v1 limits
and JSON readback description below are historical and superseded by v2.

Owner photographs at `74d2e417` show the isolated query completed with 28 chains,
116 variants, 1,579,018 work units and truncation (cycle/depth gaps). Those counts
do not establish identity parity with the historical 13/41 baseline. Visible
employee/vacation/cart branches require connecting-edge inspection before any
correctness conclusion. Logical payload observations total 7,728,443 rows and
24,662,128,780 bytes; these are not physical disk I/O counters.

`wchain.ps1 -Chain 7 -Open` renders a separate, explicitly unadmitted private
diagnostic from the small handler requery grouped handoff. It exposes exact
retained symbols (when available), source/scan/commit identities and ordered edge
records for every variant in the selected chain. It neither recovers missing
symbol identity nor weakens privacy-safe graph display policies. Input is bounded
to 64 MiB, variants to 4096, references per variant to 256, and body to 8 MiB.
The file is read once under a write-denying lock; output is create-new. Script
generator, input file and selector-bound input hashes are embedded. This helper
does not validate native commitments or assert graph correctness. Public helper
tests cover escaping, evidence visibility, provenance, immutable input, selection,
output collision and invalid endpoints. Private transition inspection is pending.

## Exact handler requery and read observations (2026-09-29)

Branch `codex/webforms-config-migration`. Owner's recovered handler readback was
below the earlier handler-specific baseline and the aggregate report was capped.
Inspection of legacy `wpath.ps1` confirmed it called the same C# path reporter
with one exact symbol; the native report selected several source-bound roots
under one shared path/work budget. This is a confirmed scope difference, not
proof of missing port logic.

`whandler.ps1 -Requery -Open` now invokes `requery-handler` against the exact
failed run and its recovery bundle. It verifies their manifest/checkpoint/index
commitments, selects one unambiguous source/scan/commit/symbol root through the
bounded recovery index, hashes the retained combined index, and repeats only
bounded global graph construction plus that root's traversal. All global
competitors remain present. A separate new output owns its grouped handoff,
HTML and generator/input-bound requery receipt. No scan, combine, website build,
original checkpoint completion, or edits to the recovery bundle occur. Existing
outputs, cancellation/failure partials and original artifacts are preserved.

Fixed stage readbacks and receipt-bound per-stage elapsed milliseconds and
logical fact-payload row/byte reads help localize expensive work. These are not
physical I/O counters. Typed source-metadata/PDB reconciliation lookups and
per-source endpoint analysis-gap lookups now use the existing fact-type index
instead of decoding the complete fact corpus to filter it. The full observed
Windows repeated-I/O cause is still unproven.

Validation: 245 focused .NET report/memory/execution/query tests passed; 11 final
integration checks passed, including all five public compiled/PDB/publish
attachment cases. The seven-root synthetic fixture demonstrates shared 256-path
truncation while the isolated 13-chain/41-variant fixture exactly matches the
historical full-reader path evidence. This synthetic count is not private parity
evidence. Three PowerShell helper tests passed; CLI build had zero warnings and
errors. Full .NET suite and owner-retained isolated handler readback remain
pending. Matching counts alone will not close path-identity parity.

## Evidence-node limit and report-only recovery (2026-09-29)

Private verify-5 completed its scan and generated handoffs/workbench, then failed
the hard-coded 2,000,000-token evidence-index limit. New implicit plans explicitly
select 20,000,000 tokens (supported ceiling 50,000,000); historical absent settings
retain 2,000,000 and old contexts remain readable. Index construction now builds
unique secondary indexes after bulk token insertion; duplicate-property admission
and response/storage/token limits remain enforced.

`wstatus.ps1 -Recover -Open` invokes current-tool `recover-reports` against the
exact selected failed run. Recovery verifies scan artifacts, original report
hash commitments and grouped evidence consistency, then copies handoffs/renders
a new workbench/rebuilds the query index in a separate bundle. It neither changes
old checkpoints nor claims original completion. A new generator/input-bound
receipt owns its artifacts; `query-recovery` returns a distinct recovery claim.
Partial recovery remains on failure. Full native graph repeated-read performance
is still unresolved; recovery bypasses it, without implying private path parity.

Validation: 187 focused evidence-query/preflight/execution tests passed, including
more than 2,000,000 synthetic indexed nodes, historical context readback, explicit
small-cap refusal, duplicate/cancellation handling, recovery tamper cases and
unchanged original files. Gap/recovery PowerShell helper tests passed. Full .NET
suite and private-corpus recovery/parity remain unverified.

## Native preflight capacity consistency (2026-09-29)

Branch `codex/webforms-config-migration`. Private retained diagnostics confirmed
`GraphInputLimitReached` with `graph-facts`: native reporting used unrelated
250,000-fact defaults while the scan configuration admitted a larger corpus.
New preflights now explicitly materialize report fact/edge capacity from
`MaxParentFacts` and text/scratch capacity from the retained artifact bound,
capped at 16 GiB. Explicit report budgets and historical null-policy digests
remain unchanged. Text capacity is a 64-bit byte count. No selector pruning or
unbounded graph admission was introduced; traversal/frontier/output caps remain.
Larger admission can cost more working set and scratch disk. Historical completed
runs cannot be upgraded in place; new verification is required. Private-corpus
path parity and representative scale remain unverified pending owner readback.

Validation: 166 focused preflight/execution/selected-root tests passed; CLI and
ReportProbe builds had zero warnings/errors; gap-status public tests passed.
Full .NET suite was not run for this bounded change. An initial test pass had
stale expected storage defaults plus a runtime-hash race from a concurrent build;
the corrected expectations and serialized no-build rerun passed completely.

## Independent snapshot and handler-graph admission (2026-09-14)

Large private indexes exposed a budget-ordering defect: the packet snapshot and
selected-handler graph shared one mutable input budget, so snapshot admission
could consume the allowance before graph composition began. The two stages now
have independent fact, edge, and text budgets. A graph that is itself limited
still fails closed and publishes no path classifications. When only the broader
snapshot is partial, a fully provenance-supported positive graph path and its
terminal boundary may be retained; missing-path, no-downstream, and other
absence conclusions remain `UnknownAnalysisGap`. Regression coverage pins both
the positive-path retention and the fail-closed no-path case. No private source
names or snippets are included in the fixture.

The existing page-list runner intentionally publishes a standalone packet and
does not mutate a completed pipeline workbench. Added a separate standalone
review wrapper that selects the newest page-list packet, creates a new immutable
`workbench/` plus compatible hash receipt, and can immediately invoke the
anonymous page exporter against that exact workbench. This prevents a report-only
rerun from accidentally exporting an older receipted pipeline packet. Its config
reader deliberately consumes only `outputRoot`; requiring the page-list runner's
`indexPath` and form array at this post-packet stage was an unnecessary coupling.
The page-list runner also accepts `-ReviewRoot`, deriving `scan/index.sqlite` and
the packet output location from the completed pipeline root while retaining the
configured form list. This avoids copying or retyping a stale absolute index path.
Because page aliases are report-local ordinals, a dedicated export wrapper now
validates the original and newest standalone receipts, matches the retained
private route locally, and exports the corresponding new alias without printing
or copying the route. This replaces a manual PowerShell folder-selection pipeline.

## Handler-rooted HTML and anonymous review packet (2026-09-10)

The one-case source review now renders private HTML organized as trigger,
handler root, nested retained callees, and anchored evidence excerpts. Repeated
caller/callee edges collapse into one tree entry while retaining links to every
call-site witness; cycles and shared callees are references rather than recursive
duplication. A separately generated shareable HTML/JSON pair contains only
report-local aliases, structural classifications, edge counts, public rule IDs,
evidence tiers, closed conclusions, and limitations. It excludes source text,
paths, symbols, fact IDs, SQL, URLs, configuration, and commit identity. The
shareable HTML renders an alias-only Mermaid graph when its browser module is
available; private HTML loads no remote script.

Validation: focused code-path review tests 2/2; full .NET suite 1789/1789;
PowerShell parse, scoped formatting, private-path guard, and diff checks passed.

Field dogfood confirmed the alias graph and repeated-call collapse rendered in
the work browser. The private report now embeds that shareable graph in a
sandboxed script-only frame and provides a private alias-to-symbol/evidence
legend outside the frame. The remote Mermaid module can access only the
anonymous child document, not the source-bearing parent. Bounded trigger source
is duplicated inline before the handler-rooted path so a reviewer can read down
without an initial navigation jump; the full anchored evidence remains below.
Validation after this layout refinement: focused tests 2/2 and full .NET suite
1789/1789; scoped formatting, private-path guard, and diff checks passed.

The private review now uses native disclosure regions. Trigger, retained call
path, and human verdict start open; the Mermaid graph and detailed evidence
start closed. Fragment navigation opens any enclosing disclosure automatically,
so evidence links remain usable without forcing the long sections open at load.
Validation for the disclosure refinement: focused review tests 2/2, formatting,
privacy, and diff checks passed. The full suite passed 1788/1789; the unrelated
global-activity isolation assertion observed a concurrent scan, then passed 1/1
when rerun alone.

## Batch local handler review (2026-09-10)

Field triage after Fill terminal projection still has seven terminal-free chains
on three pages, while the traversal-limited group fell from 51 to 50. The user
requested an end to repeated single-question diagnostics. Added
`New-FocusedWebFormsBatchInspection.ps1`: one command reuses the existing settings,
builds the diagnostic helper, and writes a private Markdown review plus JSON for
every selected handler. Windows opens the Markdown in Notepad. No rescan is needed.

The existing raw exact semantic closure is shared across all handlers; one extra
bounded query retrieves call sites, selected binding/handler locations, and exact
declaration witnesses. Each case includes all direct calls, stopping locations,
the retained closure, provenance, explicit bounds, and an unreviewed result slot.
Private UI/database classifications require local source review. Reports use a
separate filename prefix so database audits cannot accidentally select batch JSON.
The location query shares remaining row/text budgets and has a 20k-record ceiling.
Existing input and per-handler traversal limits remain in force.

Validation: full .NET suite 1786/1786; multi-handler/sibling/location/privacy and
bounded-case regressions passed; actual PowerShell command created both artifacts
from a synthetic SQLite index. Modern sample CLI scan, scoped formatting,
private-path guard, and diff checks passed. Windows Notepad launch awaits work
machine use. Existing PropertyMappingTests nullable warning remains unrelated.

## Receiver and property assignment extraction (2026-09-09)

Incremented semantic extractor to 0.21.0. Ordinary explicit member invocations
retain receiver symbol/type/identity; property access retains receiver identity
and direct simple assignment RHS symbol/identity. No raw values or snippets added.
Database census consumes this metadata and remains compatible with older indexes.
Real System.Data.Common framework compilation is scanned and persisted through
SQLite before audit assertions, including typed parameter display, inherited
Fill, a StoredProcedure assignment, a read, and a later Text reassignment. The
census deliberately does not infer value-at-Fill from assignment occurrence.
Full provider-specific SQL command object flow and private Windows scan remain
operator validation; the test uses real abstract framework parameter types.
Pinned public OSS smoke is deferred for this slice; full .NET suite and the
compiled framework scan/storage fixture provide local validation.
Validation: full solution 1784/1784; modern-sample CLI scan completed with semantic
analysis; formatting, private-path guard, and diff checks passed. Existing
PropertyMappingTests nullable warning remains unrelated.


## Database evidence linkage diagnostic (2026-09-09)

The exact-caller census now privately compares command construction `assignedTo`
metadata with adapter-constructor `argumentSymbol` metadata and reports only the
match count. It explicitly distinguishes unretained Fill receiver identity and
CommandType assigned-value metadata from absent source. Same-method local-name
matching supports command-to-adapter flow but is not claimed as object identity;
adapter-to-Fill linkage remains unestablished by the retained fact shapes.
Validation: focused audits 17/17; full solution 1783/1783; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.


## Qualified framework display symbols (2026-09-09)

Accept one leading `global::` for database census framework classification only.
Keep the original exact caller identity in SQLite queries. Regression coverage
includes qualified Fill signatures with parameter names, qualified constructors
and properties, fake framework names, and exclusion of unqualified caller rows.
No application scan, source changes, or inspection regeneration required.
Validation: focused audits 17/17; full solution 1783/1783; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.


## Distinct Fill audit failures (2026-09-09)

Field returned RawAuditFillCallerUnavailable, which conflated inspection-hop
recognition with missing exact-caller index evidence. Split these into closed
codes for no recognized hop, missing caller identity, multiple callers, and
missing index witness. Emit safe hop counts and post-provenance caller counts
before failure. Wrapper prints allowlisted generated filename, UTC timestamp,
and content hash, never a private path/custom filename. No type matching or
source assumptions broadened; this diagnoses the actual cause on the next run.
Validation: focused audits 15/15; full solution 1781/1781; PowerShell/helper
smoke, formatting, private-path guard, and diff checks passed.

## Database evidence census (2026-09-09)

Operator confirmed command construction with SQL/connection, StoredProcedure
assignment, parameters, adapter(command), and Fill in the source. Added a
read-only original-index census scoped to the exact framework Fill caller from
the newest local inspection. Reports closed fact and semantic metadata counts
only; never SQL, parameters, symbols, paths, or arbitrary property values.
Does not assert object linkage from cooccurrence or stored-procedure mode from
a property reference. Validates snapshot; caps 10k rows/8MiB text. Wrapper uses
existing path settings, requires no new method hint, and does not regenerate
the local inspection or application report.
Validation: full solution 1780/1780 before the additional missing-Fill regression;
final focused audits 15/15, including inherited CommandType references and
missing Fill rejection. PowerShell/helper smoke, formatting, privacy guard,
and diff checks passed. No conclusion about command/adapter identity yet.

## Method-starting local inspection (2026-09-09)

Added New-FocusedWebFormsMethodInspection.ps1 with a local interactive method
hint. Does not commit the private method name. Resolves one exact semantic
call/invocation signature (short or qualified name); absent/ambiguous matches
fail closed. Reuses the bounded raw closure and local call-site report, without
requiring hints for intermediate abstraction layers. Report supplies snapshot
identity, not an inferred page/event association. Fill-named stopping symbols
are preferred for the sample path, but this is explicitly not SQL evidence.
All direct sibling calls remain visible. No application scan or execution.
Validation: focused audit 12/12, full solution 1778/1778, and method-wrapper
PowerShell/helper smoke passed. Formatting, private-path guard, and diff checks
passed. The work index must still be checked for the operator's method match.

## Whole-handler direct-call inspection (2026-09-09)

Operator inspection showed the selected stopping branch only toggles panel
visibility; later operations are sibling calls in the parent event handler.
This is not evidence of a tracing defect. Extended the local inspection with
all retained exact Tier1 direct call sites and a separate downstream stopping
summary per callee. CallEdge/MethodInvoked witnesses at the same target/span
are grouped; repeated sites are retained. Source order is not execution order.
The previous one-path section remains compatible but is explicitly one sample
branch. The form list, application index and sanitized console stay unchanged
except a safe direct-site count. Bounds: 2,000 direct witness rows, 500 symbols
and 10k edges per cached callee summary; limits are not silently hidden.
Validation: focused audit tests 9/9, full solution 1775/1775, and local inspection
PowerShell/helper smoke passed. Formatting, private-path guard, and diff checks
passed. Field source comparison is still required; no extraction defect claimed.

## Local-only source inspection (2026-09-09)

User requested concrete source locations after the raw audit found seven
handlers, 86 selected facts, 18 exact symbols, and no invocation-source without
a semantic call source witness. Added an opt-in local inspection JSON via
New-FocusedWebFormsLocalInspection.ps1. It selects one unbounded raw stopping
sample, reconstructs its BFS parent chain, and queries retained fact locations.
The final hop is labeled a call site, never a callee definition. A local Go To
Definition check and source-commit verification are required. Private symbols
and paths are stored only in this explicitly requested file, not console output.
Existing raw summary behavior is unchanged unless inspection is requested.
Output is create-new, uses the existing path configuration, and does not edit
the form list, read source text, execute the application, or regenerate reports.
Validation: full solution 1774/1774; final focused audit tests 8/8; new PowerShell
wrapper/helper smoke passed. Formatting, private-path guard, and diff checks
passed. Source lookup on the work checkout remains the operator's next step.

## Raw audit text-limit fix (2026-09-09)

Field run failed with RawAuditTextLimit because the helper materialized all raw
call/invocation/declaration symbol text before traversal. Replaced that preload
with parameterized exact-symbol frontier reads batched across all handlers.
Each symbol is loaded once; declarations select by target and call witnesses by
source. At most twelve fact queries (eleven depth layers plus a final witness
read for admitted work-bounded symbols). No limits were raised. Index remains
read-only; no source index is added or original scan/report regenerated.
Regressions cover unrelated oversized text/row exclusion and continued rejection
of oversized selected evidence, alongside snapshot, privacy, and traversal tests.
Validation: focused audit tests 6/6; full solution 1772/1772; PowerShell helper
smoke against a retained SQLite fixture passed. Formatting, private-path guard,
and diff checks passed. Work-machine performance is still a field check.

## Independent raw-index audit (2026-09-09)

Correction: compacted symbol witnesses cannot rule out extraction/attachment
problems. The packet does not retain exact unresolved leaf IDs. Added
`Test-FocusedWebFormsRawEvidence.ps1` and a separate .NET helper to audit exact
semantic call/invocation closure from priority handlers against the original
read-only index, before compaction. This is not report-leaf reconstruction.
The helper validates scan/commit, enforces input and traversal bounds, and emits
closed counts only. No form-list edits, application scans, or report generation.
See README for limits and diagnostic rule limitations.

Validation: full solution 1769/1769 (before the additional traversal-bound test),
then focused audit tests 4/4 including that test. PowerShell/helper end-to-end
SQLite fixture passed; the index-byte preservation and syntax-isolation checks
passed. Private-path guard and diff check passed. No work-machine index or
application source was available locally, so field behavior remains unverified.

## Exact canonical leaf source availability (2026-09-09)

The field call-evidence diagnostic classified all seven terminal-coverage-review
chains as having no exact source-owned call-shaped evidence in the compacted
reader. This cannot rule out original-index call facts or attachment problems.
Added a second closed leaf diagnostic that independently records exact method
declaration evidence and exact source-owned body-operation evidence. The selected
bounded reader admits `MethodDeclared` rows only by exact target-symbol equality
and retains those witnesses even when the symbol node already exists. Simple-name
syntax declarations are not reconciled to qualified canonical methods. The four
canonical states distinguish declaration+body, declaration only, body only, and
neither; noncanonical leaves are not applicable. The last state deliberately
cannot distinguish external, generated, excluded, empty, or unavailable bodies.
No identities, paths, or source are published and no traversal/join changed.

Validation: focused packet/path tests 68/68, full .NET solution 1766/1766, and
the synthetic PowerShell summary test passed. Targeted formatting verification,
private-path guard, and diff check passed before commit.

## Exact canonical leaf call-evidence classification (2026-09-09)

Field leaf/reconciliation evidence showed seven terminal-free chains containing
both canonical Tier1 method leaves and deliberately isolated Tier3 projected
targets. Rule IDs on a leaf can originate from evidence that created the node and
therefore do not prove that the leaf method owns an outgoing invocation. Added a
bounded closed `LeafCallEvidenceStates` set that compares exact canonical source
node IDs with retained `MethodInvoked`/`CallEdge` facts and actual outgoing graph
edges. It distinguishes missing call-shaped evidence, an invocation without its
paired call fact, a call fact without its graph edge, path-local cycle rejection,
dispatch cross-hop rejection, and defensive retained-but-untraversed edges.
Noncanonical leaves are marked not applicable. No raw identity is emitted and no
name-based reconciliation or traversal bound changed. The actionable-gap script
prints the new aggregate field and remains compatible with older packets.

Validation: focused packet/path tests 68/68, full .NET solution 1766/1766, and
the synthetic PowerShell actionable-gap test passed. Targeted formatting
verification, private-path guard, and diff check passed before commit.

## Per-chain truncation reason retention (2026-09-09)

Field evidence showed identical depth-8/10 terminal-free results: all resolved
truncated chains already had joined downstream edges and nonzero handler-owned
call evidence. Added a closed, sorted `TruncationReasons` set to internal root
observations and an additive initialized property on the public Web Forms
observation, preserving its existing constructor and older JSON readability.
Search marks the applicable root for `depth`/`cycle` and all affected pending
roots for `frontier`/`path`/`work`; merged roots take a deterministic union.
Packet JSON and Markdown expose the set under the existing rule and limitations.
The read-only triage prints direct per-chain reason counts; old packets print
`not-retained`. Reasons describe static bounds, can coexist, and are neither
runtime conditions nor exclusive causal claims.

Validation: focused packet/path tests 67/67, full .NET solution 1765/1765,
and the synthetic PowerShell retained-triage test passed. Formatting verification
passed for both changed C# files. The whole-solution formatting check remains
noisy in unrelated pre-existing files and was not used to rewrite them. Private
path guard and diff check passed before commit.

Field handoff: a separate run-and-triage wrapper calls the locally configured
page-list runner and then passes the one freshly written JSON to read-only
triage. Missing or ambiguous fresh JSON fails closed; no prior report is
selected. Keeping orchestration separate avoids modifying the tracked file in
which the operator keeps the local 43-form list.

## Retained observation-state output (2026-09-09)

Field comparison at depths 8 and 10 found the same terminal-free page buckets;
all exact-node truncation-reason associations remained `not-established`.
Extended the existing read-only triage to print whitelisted `stopState` and
`callEvidenceState` counts plus aggregate retained handler-owned call evidence
per alias. This uses packet fields already present in the completed JSON and
does not launch another traversal. Synthetic tests cover each useful state,
numeric aggregation, unknown-state withholding, and existing privacy bounds.

## Retained reason comparison (2026-09-09)

Added `Compare-CompletedWebFormsPageTriage.ps1` on the existing restricted-run
branch. Reads depth-8/10 sequentially, buffers small output until source and
selection equality checks pass. Extended triage with closed truncation reasons
associated only through exact retained path-node IDs. No causal attribution or
new traversal. Missing associations stay `not-established`; shared-node matches
are not exclusive per-page stopping reasons. Synthetic tests pass for linked
depth reason, unrelated-node exclusion, missing associations, matched pairs,
source mismatch rejection, optional fields and private-string suppression.
Scripts-only change; Windows field run pending. No .NET changes or full suite
rerun for this follow-up.

## Retained page triage (2026-09-09)

On `codex/restricted-webforms-run-evidence-20260902`, added
`Triage-CompletedWebFormsPages.ps1` after the user confirmed the completed run
used ff4059. Reads existing depth-8 only; no execution or traversal path.
Separates terminal, missing handler, truncated observation, downstream without
terminal, zero downstream edge, and unavailable observation buckets. Includes
no-retained-event pages. Focus aliases page-004/page-026 link gaps by exact
binding supporting ID only, without treating links as proof of cause.
Synthetic PowerShell tests passed for discovery, absent/null fields, buckets,
exact links, unrelated-gap exclusion, and private-string suppression.
No .NET code changed in this follow-up; Windows field execution remains pending.

## Selected-handler graph admission follow-up (2026-09-08)

The 43-page work-machine report matched all requested forms but returned 191
globally downgraded event chains, no downstream boundaries, and a truncated
packet after the repository-wide graph input ceiling was reached. That output
is retained as regression evidence; it is not an application-level absence
claim.

The bounded single-index reader now uses the packet's exact selected handler
fact IDs to seed a deterministic symbol closure through call, object-creation,
parameter-forward, and symbol-relationship edges up to the existing
depth/frontier bounds. It reads only facts and normalized edges connected to
that selected closure, plus exact supporting fact IDs. Unselected callers
cannot consume the selected page budget. All existing fact/edge/text limits
remain fail-closed, and an
over-limit selected neighborhood still suppresses path classifications and
emits `GraphInputLimitReached` through the packet's existing rule-backed gap.

Synthetic coverage verifies that 100 unrelated call-shaped facts no longer
hide a selected handler-to-database path under a deliberately small admission
budget. Oversized payload evidence inside the selected neighborhood still
fails before JSON allocation. Existing full-reader compaction behavior remains
unchanged when no explicit handler selection is supplied.

The first restricted rerun after this change had no input-admission, traversal,
event-chain, or boundary limit classification. It retained exactly one
`WebFormsModernizationGapLimitReached`, so its remaining `truncated: true` was
the 1,000-row output gap cap. The committed page-list wrapper now passes
`--max-gaps 5000` and reports any remaining `LimitReached` or
`TruncatedByLimit` classifications directly after publication. This changes
report retention only; it does not widen graph traversal, scan the repository,
or alter application evidence.

The completed non-truncated report exposed overlapping human-readable states:
a handler-unavailable chain also has no terminal, so raw text counts cannot be
added. `Summarize-FocusedWebFormsPageList.ps1` now selects the newest retained
page-list JSON and emits three mutually exclusive chain counts, exclusive page
categories with safe aliases, and terminal-kind totals. The script is read-only
and excludes private paths, symbols, SQL, and source content from its output.

The complete 43-page result contains 191 exclusive chains: 41 without a
resolved handler, 143 with a resolved handler but no terminal, and 7 with a
static SQL-query terminal. Seven pages have at least one SQL terminal, 35 have
resolved handlers but no terminal, and one has no static event binding. Added a
second zero-argument, JSON-only triage script to split the 143-chain population
by defensible stop state and aggregate its public evidence metadata. No raw
application identifier is emitted.

## Retained-index page-list report (2026-09-06)

Rebased this diagnostic branch cleanly onto `origin/dev` at `af72e8b9`, which
contains the merged Base44 work through PR #719. Added optional
`webforms-modernization --surface-list <file>` filtering and the committed
`Invoke-FocusedWebFormsPageListReport.ps1` wrapper for the work-machine run.
The input is a line list or first-column CSV; repo-relative paths match exactly
and filename-only values must be unique. Output retains deterministic page
aliases and hashes rather than copying raw list values. Matched entries restrict
page/event traversal; unmatched and ambiguous entries emit packet-rule gaps.
The Markdown coverage table reports only static event-chain and downstream
boundary evidence plus the first unresolved state. It makes no runtime,
rendering, branch, binding, SQL-execution, or whole-application claim.

On 2026-09-08, added `Run-FocusedWebFormsPageList.ps1` as the human-operated
entry point. Its top edit block contains the retained-index path, output root,
and a here-string accepting one unquoted `.aspx` path per line. It creates the
temporary list and timestamped output internally, then delegates to the tested
parameterized wrapper. This avoids chat copy/paste and repeated command editing.

## Field ownership result and next handoff (2026-09-03)

Operator screenshots report ownership-verified for the same event on 0.7.1:
one projection, six supporting facts and two edges resolved, zero mismatches
or missing IDs. Both prior unrelated edges remain indexed but are absent from
this projection; the legitimate direct edge remains. Syntax support stays Tier3.
This closes the field check for the selected event only. No runtime or broad
coverage claim follows.

Added claude-database-backed-event-trace.prompt.md for an operator-selected
different event using the existing index, with the original bounds and privacy
constraints. No new scan required. Future work-machine handoffs must be pushed
as repo prompt files and linked in README because the computers are separate.
Documentation-only change; validate privacy and diff checks, not a new code-test run.

## Retained projection verification handoff (2026-09-03)

Field screenshots report scanner 963392f4, extractor 0.7.1, zero workspace
diagnostics and the retained three-hop semantic path to an HTTP boundary.
The report says no projection was found for the binding; that is not sufficient
to verify ownership isolation because projection lookup uses handler identity
and exact supporting fact IDs, not direct bindingFactId. Added a bounded,
read-only prompt for that lookup and support validation. README makes clear no
new scan is needed. Field ownership verification remains pending; omitted
branches remain unverified. Documentation-only change; privacy and diff checks
are the relevant validation, with no new code-test claim.

## Handler ownership correction (2026-09-03)

On the current diagnostic branch, `legacy-webforms/0.7.1` fixes direct event-flow
support accepting same-name members from unrelated files/types. Admission now
requires the resolved handler file and contained line span. Tier1 support also
requires its canonical source symbol ID (including assembly identity). Logic
signals share admission and select their syntax method by the resolved span.
Syntax name/span evidence remains lower-tier; overlapping same-line declarations
are an explicit limitation, not compiler-resolved ownership.

Synthetic regressions cover same-name different-file/type edges, a same-span
different-assembly semantic identity, retained own-handler support under reduced
coverage, syntax fallback, and reversed-input determinism. No private source is
used. Validation: full .NET suite 1760/1760; focused extractor suite 45/45;
synthetic non-compiling CLI smoke 65 facts with reduced coverage; changed-file
format verification, private-path guard, and diff checks. Existing unrelated
PropertyMappingTests nullable warning remains. README and one-page prompt now
require a fresh 0.7.1 index and the same selected event; no PR or merge requested.

Original implementation branch: `codex/webforms-bounded-report-memory`
Current diagnostic-debug branch: `codex/restricted-webforms-run-evidence-20260902`
Base: `ce6b449f0be49b04f524c23641c42ff56c155ec8` (fresh origin/dev).
Implementation commit: `15c699ee` (reader, packet, CLI, rules, and 11 regression cases).

## Scope and design

The reported work scan produced approximately 1.65 million facts and a 9 GB
SQLite index. Screenshots report successful 200/300-surface packets and OOM at
larger caps; they are not a heap profile. Local source confirms unbounded fact
materialization and whole-packet string serialization.

The first implementation deliberately preserves repository-wide graph candidate
context. Filtering by roots before existing symbol reconciliation/dispatch would
risk hiding collisions and promoting ambiguous evidence. Instead, stream the
single-index reader in its existing graph insertion order, compact a closed list
of graph-inert syntax facts to the first symbol witnesses, and retain full rows
for all graph-relevant facts, unknown types, declared surfaces, and legacy rules.
Retain referenced supporting IDs/provenance even for omitted symbol witnesses.
Preserve the ordinary paths/combine readers.

Explicit admission limits bound retained facts, edges, serialized text bytes, and
individual row size before allocating strings/JSON dictionaries. Incomplete
graph input must never be classified: return a typed rule-backed gap and retain
independent Web Forms snapshot evidence. Snapshot input also has admission bounds.
Stream JSON bytes directly to the staging file, preserving atomic publication
and the existing v1 schema. Limits are not runtime reachability claims or a hard
OS RSS guarantee.

Root-specific lazy graph loading remains a later optimization requiring a
complete ambiguity/context contract. This slice removes repetitive fact payload
retention and whole-JSON string copies and adds safe failure behavior, with
synthetic parity and memory validation. The private work index stays local to the
owner; its all-surfaces rerun is final real-world validation, not claimed here.

## Validation

- Build passes with the pre-existing `PropertyMappingTests.cs:560` nullable
  warning (`CS8602`); no build errors. Unrelated CLI switch formatting was left
  unchanged after limiting formatter churn.
- Final full solution: **1,729/1,729 passed**. Focused reader/packet/path suite:
  **74/74 passed**, including 11 new memory regression cases. The final parity
  fixture also protects duplicate `surfaceKind` JSON keys: any key presence
  prevents compaction, avoiding SQLite first-key versus JSON last-key disagreement.
- CLI scan of `samples/modern-sample`: 27 facts. Separate scratch synthetic
  non-compiling .NET Framework 4.5 Web Forms project with a missing generated
  compile input: 67 facts, reduced semantic coverage. Its packet preserves one
  page and one event chain; no backend terminal is invented. A constrained CLI
  rerun emits the typed snapshot input-limit gap and `truncated: true`.
- Input hash, report JSON/provenance parity, and 518 independently rooted
  surface/chain/boundary assertions pass. Packet serialization is byte-equivalent
  to the previous JSON contract, including its final newline.
- Formatting verification passes for the changed reporting/test files;
  private-path guard and diff whitespace checks pass.
- Non-.NET language extractors are unchanged. TypeScript checks pass **49/49**;
  JVM/Python and pinned adapter smokes are deferred as unrelated to this
  single-index .NET report-reader change.

### Synthetic memory experiment (macOS, .NET 10.0.10)

| Noise rows | Reader experiment | Index bytes | Report-call managed allocations | Retained graph input |
| --- | --- | --- | --- | --- |
| 100,000 | original full reader | 153,645,056 | 767,308,848 bytes | all facts/properties materialized |
| 100,000 | bounded reader | 153,645,056 | 12,976,968 bytes | 5 facts + 1 edge; 2,010 text bytes |
| 1,000,000 | bounded reader | 1,536,245,760 | 128,179,640 bytes | 5 facts + 1 edge; 2,010 text bytes |

Managed allocations count temporary objects, not retained heap. The bounded
100,000-row call inside the full-reader comparison allocated 12,971,440 bytes;
small test-process variation is expected. At one million rows, the test visited
1,000,005 facts and still returned identical report bytes. A separate 50 ms
`ps` sampler observed peak aggregate RSS of the test command and descendants:
928,416 KiB for the full-reader comparison, 396,800 KiB for bounded 100,000,
and 402,208 KiB for bounded one million. Those are sampled test-process-tree
measurements including fixture creation/runner overhead, not an isolated
reporter RSS claim or a Windows prediction. The native .NET peak-working-set
counter returned zero on this host and was not used as evidence.

### ACK preflight / review boundary

No PR or external review request was created; no merge was attempted. The exact
documented ACK v0.4.4 tag/HEAD `855428f7a8e9bd084decc3a1569aa59f7d50583d`
was located and rebuilt, but its release receipt is missing. Release verification
fails with ENOENT; doctor loads the lane and returns `LOCAL_ACK_CHANNEL_NOT_ALLOWED`
because the build has `unverified_build` / `preview` release provenance. Do not
substitute a mutable or unverified binary or declare merge readiness.

The consumer lane test independently fails 1 of 3 cases on the fresh base:
the committed lane allows `>=0.4.4 <0.6.0`, while the test expects
`>=0.4.4 <0.5.0`. Neither lane nor its test was changed in this slice. This
review-tooling mismatch and verified-release setup need a separate repair;
recorded failures are not a reason to weaken gates in the Web Forms patch.

## Handoff

The sanitized restricted Windows rerun observation from 2026-09-02 is recorded
in [`restricted-run-2026-09-02.md`](restricted-run-2026-09-02.md). It completed
with a partial/reduced result, 1,653,627 facts, 13,460 gaps, and complete timing
coverage. Artifact writing dominated the 1,104,151 ms run. No OOM or failed
process was observed. A later bounded local review of the sanitized summaries
reported 932,070 Tier1 facts and 10,588 occurrences of
`LegacyWorkspacePrerequisitesUnresolved|UseCompatibleMSBuildToolset`. Code
inspection subsequently established that this projection can conflate ordinary
`CompilationDiagnostic` rows with genuine workspace failures for legacy
projects. It is not evidence of 10,588 proven toolset failures. The bounded
count-only queries, local-inspection boundary, and synthetic reproducer are
recorded in [`README.md`](README.md). The on-device count-only follow-up found
only one retained `WorkspaceDiagnostic`, one scan-scope gap, and no retained
`CompilationDiagnostic` rows under `csharp.semantic.workspace.v1`; the 10,588
projected rows therefore remain origin-indeterminate and demonstrate missing
diagnostic lineage rather than a proven environmental root cause.

Implementation and runbook are committed locally; pushing/opening a PR and
repairing ACK setup remain separate next steps. No merge readiness is claimed.
Use the README's large-index/OOM link for the private Windows rerun after the
branch is made available there. Keep the successful 300-surface packet and the
original scan index, select a new output directory, and compare counts/gaps and
memory. A deterministic input-limit gap is a truthful partial result, not proof
that every requested event chain was analyzed. Full private-index completion
and any subsequent root-specific lazy-loading design remain unverified here.

## Diagnostic-lineage debug patch

At the operator's direction, the diagnostic projection correction is being
debugged on `codex/restricted-webforms-run-evidence-20260902` alongside the
sanitized field notes. After synthetic and restricted validation settle the
behavior, the code/test/rule/script changes should be cherry-picked onto a fresh
branch from `origin/dev`; the field documentation need not be included in that
product PR.

The patch separates ordinary compiler diagnostics from workspace admission,
adds closed origin lineage and safe diagnostic IDs to projected environment
facts, limits legacy-prerequisite corroboration to genuine workspace/load
origins, deterministically aggregates exactly equivalent projections, and makes
the PowerShell readback report unknown lineage explicitly for pre-fix indexes.
No raw native diagnostic message or private identifier is added.

Validation on 2026-09-02:

- full .NET solution: **1,737/1,737 passed**;
- focused diagnostic/snapshot suite: **32/32 passed** before the final safe-ID
  extraction case, followed by a green full solution run containing that case;
- `Export-FocusedWebFormsWorkspaceSummary.Tests.ps1`: passed, including legacy
  unknown-lineage, compiler-origin, workspace/load-origin, static-origin, and
  occurrence-count cases;
- changed-file `dotnet format --verify-no-changes`: passed;
- private-path guard and `git diff --check`: passed;
- synthetic CLI classifier fixture: 37 facts, truthful
  `Level1SemanticAnalysisReduced`; `CS0103` remained a compiler-origin
  `AnalysisGap`, no legacy-toolset prerequisite projection was emitted, and
  Web Forms page/control/event/handler evidence remained present.

The repository-wide formatter still reports unrelated pre-existing formatting
violations outside this change, so validation is scoped to all changed C# files.
No non-.NET adapter changed; pinned language-adapter smokes are deferred.

### Projection-boundary follow-up

The restricted post-fix review verified the lineage patch and exposed a second,
independent projection defect: 10,609 `PropertyMappingShapeUnsupported` and 24
`PropertyMappingTruncated` occurrences were duplicated as unknown
`BuildEnvironmentDiagnostic` workspace failures. Two genuine workspace-callback
occurrences remained. The legacy-prerequisite count was zero.

Commit `08ec7348` bounds environment projection to the closed admitted gap kinds,
preserves the original property-mapping gaps, and bumps the build-environment
extractor to `0.5.0`. Validation: focused build-environment tests **22/22** and
full .NET solution **1,738/1,738** passed; changed-file format verification,
private-path guard, and `git diff --check` passed. The full suite retains one
pre-existing nullable warning in `PropertyMappingTests.cs:560`.

Restricted validation completed on TraceMap head `a3de925b`: provenance and
extractor versions passed; unknown-origin and legacy-prerequisite counts were
zero; independent property-mapping gaps remained in their own rule family; and
exactly two sanitized `WorkspaceDiagnostic` callback occurrences remained. No
safe diagnostic ID was present. The next investigation is local-only native
callback classification, not another inference from shareable artifacts.

### COM-reference task-host follow-up

Local-only inspection classified both remaining callbacks as the same bounded
COM-reference task-host failure: kind `Failure`, no safe diagnostic ID, aggregate
occurrence count 2. A separate Visual Studio build of the selected solution
succeeded, so the evidence does not support a broken-solution claim.

The follow-up adds `MSBuildTaskHostIncompatible` classification and a bounded
workspace admission fallback for projects declaring `COMReference` or
`COMFileReference`. The fallback temporarily overrides only the two COM
reference resolution targets, keeps independent semantic extraction available,
and emits `ComReferenceResolutionSkipped` so COM-defined symbols remain an
explicit Tier 4 limitation. Project-defined `CustomAfterMicrosoftCommonTargets`
hooks are never replaced; those projects receive
`ComReferenceResolutionFallbackUnavailable` and retain normal workspace
behavior. Extractor versions advance to `build-environment/0.6.0` and
`csharp-semantic/0.20.0`.

### Post-COM coverage handoff (2026-09-03)

The field summary at `ad8fdd98` reports zero workspace/uncategorized diagnostics
and 932,070 Tier1 facts, with reduced coverage retained. The accuracy report's
workspace-repair priority was still triggered by static legacy markers. The
summary now admits only non-informational workspace-rule diagnostics for that
decision; generic unknown failures request classification and COM host failures
receive task-host-specific guidance. No scanner or evidence rule changed.

Added `claude-retained-coverage-triage.prompt.md` as the current handoff, linked
at the top of README. It selects and verifies a retained run, inspects at most
five samples in each of four gap kinds read-only, and returns only closed
aggregate categories. It forbids scan/rebuild, source changes and BRD work.

Validation: accuracy-summary tests (including fourteen priority cases), evidence-
summary tests, workspace-summary tests and review-launcher tests all passed.
Private-path guard and diff whitespace checks passed. The .NET suite was not
rerun for this PowerShell/documentation-only change; the preceding scanner fix
passed 1,742/1,742 .NET tests.

### Bounded post-triage extraction follow-up

Same diagnostic branch; no PR or merge. `legacy-webforms/0.7.0` changes only
markup type-name casing (namespace and project ownership unchanged), positive
postback branch candidates, and client/non-identifier event gap classification.
Exact tag matches do not override case-collision ambiguity. Negative-branch
identity/limitations and client-script negative-branch attribution are retained.
DLL metadata support, compound conditions, boolean comparisons and arbitrary
receiver inference remain out of scope. Synthetic non-compiling Framework 4.5
fixtures cover these boundaries; the restricted sample observations are not
treated as a population-wide guarantee of gap reduction.

Validation: focused extractor/coverage tests 59/59; full .NET suite 1,758/1,758;
legacy-codebase validation Python tests 13/13. A synthetic non-compiling Framework
4.5 CLI scan emitted 65 facts with reduced coverage; its persisted SQLite facts
include canonical control-type composition, both postback polarities and the
client-attribute gap. Changed-file formatting, private-path guard and diff checks
passed. The existing nullable warning in PropertyMappingTests remains unrelated.

### One-page trace handoff

Added `claude-single-page-trace.prompt.md` and made it the first README handoff.
This is documentation-only: actual execution needs the work computer's retained
index and private source. Selection is fixed before traversal to avoid selecting
only a successful example. Bounds are six hops, fifty symbols, one hundred
edges and ten terminal candidates. Handler joins use binding IDs; downstream
joins preserve canonical symbol/assembly identity and explicit support. Manual
source observations cannot fill missing extracted edges. No new scan or BRD.

### Per-handler traversal observation follow-up

The branch was rebased without conflicts onto `origin/dev` at `b025a6d3`; the
focused Web Forms packet baseline passed after that rebase. The non-truncated
43-page field report then showed 191 event chains: 41 handler-unavailable, 143
handler-resolved without a terminal, and 7 SQL-terminal chains. All 143 resolved
nonterminal chains lacked a retained legacy path, so the prior packet could not
distinguish a handler with no observed downstream edge from a traversed path that
stopped before a supported terminal.

The bounded path search now retains per-start observations during the existing
single traversal: reached-node count, traversed-edge count, downstream-edge count,
terminal-path count, and traversal truncation. The synthetic
`legacy-root-selection` edge is excluded from downstream counts. Web Forms event
chains expose only those counts, the public `legacy.flow.static-traversal.v1`
rule, a closed stop state, and explicit non-runtime limitations; no private
symbols, paths, SQL, or source content are added. The zero-argument triage script
uses the new field and safely falls back to `traversal-observation-unavailable`
for older packets. Synthetic tests cover a non-compiling Web Forms scan plus
separate no-edge, nonterminal-traversal, and SQL-terminal paths.

Validation after the rebase: focused packet and bounded-memory tests 28/28;
duplicate-start route-flow compatibility regression passed; full .NET solution
1,763/1,763; PowerShell parser clean. Changed-file formatting, private-path guard,
and diff checks are recorded after their final rerun.

### Handler-owned call-evidence diagnostic follow-up

Field evidence established 143 resolved nonterminal chains with zero observed
downstream edges. The packet now reports whether each exact handler flow
projection retained one or more supporting call-edge fact IDs. Closed states
separate retained-but-unjoined handler call evidence from no retained
handler-owned call evidence. The diagnostic does not synthesize edges or widen
identity matching. The alias-only triage script reports state, chain/page counts,
and aggregate handler-owned call-edge counts.

Validation: focused packet tests 15/15; full .NET solution 1,763/1,763;
changed-file format verification and PowerShell parser clean. Private-path and
diff checks pass.

### Exact handler-call support bridge

The field split found 137/143 resolved nonterminal chains with 3,032 exact
handler-owned call-edge references. The bounded reader now seeds canonical
handler display identities directly from the selected handler facts and admits
the projection's exact supported call facts. Only
compiler-resolved call targets seed further target-symbol closure. Graph
construction re-anchors a call only when one exact Web Forms handler is in the
projection support set, the projection source equals its canonical handler
identity, and the call fact ID occurs in both support lists. The bridge is a
`webforms-handler-call-support-projection` under
`legacy.flow.static-traversal.v1`, remains review-tier projection evidence, and
does not join unrelated same-named calls. Packet path matching now recognizes
both canonical handler display identity and symbol ID. Syntax-only call targets
remain isolated candidates and cannot create global simple-name joins.

Validation: focused Web Forms packet and bounded-memory tests 29/29; full .NET
solution 1,764/1,764; changed-file format verification clean. The synthetic
reduced-analysis index proves an exact supported canonical call can continue to
an SQL terminal while excluding an unsupported same-named call; a separate
fixture proves syntax-only support stops at one isolated projected candidate.
## Partial triage handoff

The work-machine report after de298671 matched 43 pages and retained 191 event
chains, 7 boundaries, and 82 TruncatedByLimit gaps. The packet drops the underlying
path-gap reason, so no specific limit or cycle cause is established. Triage now
accepts this retained JSON with partial status, retained-only count scope, and an
explicit no-absence warning. No traversal limits or scanner behavior changed.
PowerShell synthetic empty-chain packets tested with truncated true and false;
both produced the expected status and unavailable-reason output. Full .NET tests
not rerun for this script/documentation-only change.
## Closed truncation-reason reporting

Added optional gap.truncationReason for TruncatedByLimit, accepting only depth,
frontier, path, and cycle from the existing path gap. Null is omitted for other
gaps or unknown reasons. Both page-list reporting and retained triage print
reason counts; old packets and unexpected values become unavailable. No traversal
limits, cycle handling, or evidence classification changed. The field result shows
137 previously unjoined chains now observe downstream edges; among 143 unresolved
terminal chains, 130 are truncated, 7 have nonterminal traversal, and 6 have no
retained handler-owned call evidence. These are retained-report observations only.

The synthetic handler-to-terminal fixture verifies depth truncation survives JSON
publication (depth 3; depth 1 does not admit the same fixture graph). PowerShell
checks cover all four reasons plus absent/unknown values without leaking the
unexpected value. Formatting and private-path guard passed.
Validation: focused packet tests 16/16 and full .NET solution tests 1764/1764
passed, including the synthetic scan/index/packet integration test.
## Legacy frontier scheduling

Field report after 236a942d identifies 81 cycle gaps and one frontier gap. The
frontier is a global pending-path queue, not a depth limit. Legacy reports now
schedule branches depth-first in existing deterministic edge order to reduce
breadth-wide pending expansion. Non-legacy searches retain breadth-first order.
No global node deduplication, limits increase, or suppression of cycle gaps was
introduced. Cycles remain partial; path-limited legacy result subsets can change
because traversal is not shortest-first. Private graph performance is not proven
locally and requires regenerating the report from the retained index at work.

Synthetic regression preserves 64 reconvergent routes under frontier 24, checks
repeat determinism, checks true depth/frontier/path truncation, and preserves all
64 terminal routes when a cycle is present. Existing scan-to-packet integration
tests remain part of validation.
Validation: full solution 1765/1765 passed; focused packet/traversal 17/17
passed. The final added breadth-first comparison assertion also passed: ordinary
breadth-first hits frontier 24 on the same fixture. Formatting, private-path
guard, and diff check passed. Existing nullable warning in PropertyMappingTests
is unchanged.
## Bounded depth comparison handoff

Added Compare-FocusedWebFormsDepth.ps1, reusing the user's configured runner
without re-entering forms. Sequential depths 8/10/12 reuse the retained index,
with other caps unchanged and separate output directories. Summary counts exact
boundary kind/target/evidence tuples, page aliases, gains/losses and closed limit
reasons; source and page-selection mismatch fails comparison. It does not equate
boundary records with runtime operations or treat missing evidence as absence.
Field result after depth-first scheduling: frontier gap cleared, 931 boundary
records, 1031 chains, 59 resolved-handler chains without terminal, 349 cycle and
394 depth gaps. Work-machine depth comparison is pending.

Validation: synthetic PowerShell test covers duplicate tuple counting, gains and
losses, page counts, gap reasons, private identity non-disclosure, provenance
rejection and CLI depth forwarding. All changed runner scripts parse; private-path
guard and diff check passed. No .NET code changed; full suite not rerun (previous
code commit passed 1765/1765). Test entry: scripts/tests/Test-FocusedWebFormsDepth.ps1.
## Read-only completed depth summary

Follow-up: field failure at line 59 was the mandatory GetProperty call for
terminalKind. Production serialization omits null fields. The reader now treats
omitted or explicit-null handlerFactId/terminalKind as unavailable; other required
schema fields remain required. Regression covers omitted, null, and populated
optional fields. PowerShell regression, private-path guard and diff check passed.

The depth comparison consumed unacceptable resources on the work machine; depth 8
and 10 completed and remain local (66 and 84 MB), while no depth-12 completion is
established. New Summarize-CompletedWebFormsDepths.ps1 only reads those files.
It discovers a shared comparison folder, processes one bounded JSON document at
a time, keeps compact terminal identity sets, validates provenance/selection, and
prints counts plus gained/lost page aliases. No code path launches another script
or process; no outputs are changed. Hard input cap 128 MiB per report; boundary and
chain count caps 10,000, selection cap 1,000. This does not fix traversal resource
safety; deeper traversal must remain paused pending that separate change.

Validation: PowerShell synthetic fixture verifies auto-discovery, duplicate terminal
identity handling, gains, null handlers, unknown reasons, private identity omission
and mismatched provenance rejection. No .NET changes; full .NET suite not rerun.
## Resource safety and targeted baseline triage

Depth 8/10 read-only comparison confirmed 153/161 distinct terminal evidence tuples,
29 pages with terminals in both, 8 gained terminal tuples and none lost. Unavailable
handlers remain 41 chains; resolved without terminal remains 59. No further deeper
run is justified. Automatic comparison and wrapper depth >8 now fail closed.

Implemented Search work budget (100,000 state/edge operations per invocation),
typed work truncation, and preservation of terminal-free search gaps. Pending roots
are marked truncated. Wrapper directly supervises the built report DLL rather than
a dotnet-run parent: 300-second timeout, sampled 4-GiB working/private memory,
2-GiB managed-heap limit, kill owned tree on failure/cancel. These limits do not
prove whole-process-tree memory containment; build child memory is not aggregated.
No private 50-GiB workload reproduced here. Windows watchdog validation remains
pending; no user rerun requested. Only the read-only -Details handoff is recommended.

Synthetic work tests cover bounded terminal-free/reconvergent/cyclic paths and
deterministic exhaustion; process tests cover success, nonzero exit, timeout and
sampled memory termination. Read-only tests cover targeted alias handler counts.
Final validation: full .NET solution 1765/1765; focused packet/traversal 17/17;
all three PowerShell regression scripts passed. Formatting verification, PowerShell
parsing, private-path guard and diff check passed. Existing CS8602 warning at
PropertyMappingTests.cs:560 remains unchanged.

## Fair bounded handler scheduling

The post-rebase 43-page field run retained 262 event chains but only 96 downstream
boundaries. Its closed truncation evidence contained 64 cycle, 50 depth, and one
global work exhaustion; per-chain observations showed later handler roots inheriting
`work`. This establishes deterministic root starvation under the single 100,000-unit
depth-first queue, not absence of downstream behavior.

Legacy traversal now retains the same global work/frontier/depth/path ceilings but
rotates a root after the smaller of 64 work units or its equal ceiling share. A
partially expanded high-fan-out state resumes at its deterministic edge cursor.
Ordinary breadth-first paths are unchanged. `webforms-modernization` now exposes
`--max-traversal-work` (default 100000) and packet construction forwards it to the
shared traversal. Fairness does not guarantee completion, runtime reachability,
successful binding, or a terminal for every handler.

The synthetic regression places the noisy handler first, caps global work at 40,
and proves a later cheap handler still retains its SQL terminal while the noisy
root receives explicit `work` truncation. A repeated packet is byte-equivalent by
serialized model comparison.

Validation: focused Web Forms packet and combined traversal tests 68/68; full .NET
solution 1766/1766; scoped formatting, private-path guard, and diff check passed.
The existing nullable warning in PropertyMappingTests remains unchanged.

## Batch terminal-free evidence conclusions

The local batch inspection now separates exact allowlisted framework UI/control
endpoints from other unresolved leaves. JSON, private Markdown, and privacy-safe
console lines report the observed endpoint count, unresolved leaf count, and a
deterministic evidence conclusion. The conclusion remains
`no-supported-backend-terminal-observed`; it does not claim backend absence,
runtime behavior, or complete source coverage. Human review remains `unreviewed`.

Validation: focused raw-audit tests 14/14; full .NET solution 1787/1787;
scoped formatting and diff checks passed. The existing nullable warning in
`PropertyMappingTests.cs:560` remains unchanged.

## Local working-tree code-path review prototype

Added a one-case, local-only Markdown review over the batch inspection. It reads
bounded excerpts directly from a supplied source root without requiring Git,
renders retained call edges and evidence locations, and provides a human verdict
checklist. The report explicitly labels source mode as `working-tree` and does not
claim equality with the scan commit. A missing exact declaration may receive a
definition navigation candidate only when a syntax parse finds one unique method
name within already witnessed files; that candidate is not promoted to evidence.
Console output is counts-only and contains no private paths, symbols, or source.

Validation: code-path and raw-audit tests 16/16; full .NET solution 1789/1789;
Release diagnostic helper build, PowerShell parse, scoped formatting, and diff
checks passed. The existing nullable warning in `PropertyMappingTests.cs:560`
remains unchanged.

## Compiler-resolved DataAdapter Fill terminal projection

The fresh field scan retained the exact public framework Fill target plus receiver
identity, while the database census independently confirmed same-method command,
adapter, CommandType assignment, and Fill shapes. The bounded page reporter still
left that call as a nonterminal because `MethodInvoked` was treated only as compact
symbol metadata and the shared surface projection did not recognize framework Fill.

The shared surface projection now maps only Tier1 `csharp.semantic.methodinvocation.v1`
facts whose exact target is a supported public `DbDataAdapter`/`SqlDataAdapter.Fill`
signature and whose receiver symbol is retained to an `sql-query` surface subtype
`data-adapter-fill`. The compact single-index reader preserves properties for that
narrow fact shape. Same-name application methods, lower-tier facts, and missing
receiver evidence remain nonterminals. Tests cover positive/negative projection,
real framework extraction into the projection, and a complete Web Forms handler
path reaching the new SQL terminal. This remains static boundary evidence only;
command value flow, execution, success, returned rows, and branch feasibility are
not claimed.

Validation: full .NET solution 1785/1785; focused projection, real-framework
extraction, and handler-to-Fill terminal tests 3/3; modern sample CLI scan passed;
scoped formatting and diff checks passed. Repository-wide formatting remains
blocked by pre-existing whitespace findings outside this change. The existing
nullable warning in `PropertyMappingTests.cs:560` remains unchanged.

### Mermaid 11 report compatibility

The anonymous call-path graph now emits conservative Mermaid 11 flowchart
syntax: pipe-delimited edge labels are unquoted, node navigation uses the
explicit `click ... href` form, and the browser module is pinned to 11.17.2.
This removes permissive-parser and floating-version dependencies from field
reports that displayed Mermaid's syntax-error fallback only in the iframe. The
private iframe also loads the anonymous document at its root rather than during
fragment navigation, matching the field-confirmed standalone rendering path
while preserving the script-only sandbox. The report can be regenerated from
the retained local inspection; no repository rescan is required.

The first field rerun still rendered standalone but failed only inside Chrome
and Edge iframes. Mermaid navigation directives are therefore no longer emitted
as diagram grammar. The anonymous document renders a navigation-free graph with
strict security, then attaches alias-only node navigation after rendering; plain
HTML alias links remain as a no-script fallback. The remote module remains
confined to the anonymous script-only sandbox.

The second field rerun showed that browser-specific iframe rendering remained
unreliable even with navigation removed. The private report no longer embeds an
iframe or loads Mermaid at all. It renders the same alias-only nodes and retained
edges as deterministic inline SVG with evidence links. The separately shareable
HTML remains Mermaid-based because standalone rendering was field-confirmed.
Private graph rendering now has no CDN, script, sandbox, or browser-origin
dependency.

The private report trigger context is independently configurable from zero to
100 lines on each side of the exact retained binding span, defaulting to 12 and
capped at 256 displayed lines. This supports unusually tall Web Forms control
declarations without widening downstream evidence excerpts or changing the
underlying evidence span.

Added `New-FocusedWebFormsCodePathReviewSet.ps1` for the post-dogfood workflow.
It selects all retained batch-inspection cases by default, builds the diagnostic
helper once, and places each private/shareable report triplet in one timestamped
private subfolder. A root `review-queue.md` contains case/evidence IDs, relative
private and anonymous report paths, an allowlisted human-verdict column, and a
free-form comment column for local AI or workflow ingestion. Field review made
the entry-point convention explicit: the queue is named `index.md` in the root
of its timestamped report-set folder. Later runs use new folders and cannot
overwrite edited decisions. Review decisions remain metadata and do not become
scanner evidence automatically. A PowerShell regression covers multi-case
selection, index placement, relative paths, trigger context, queue fields, and
artifact counts.

The review-set folder also contains a private `index.html` for browser-first
navigation. It links every private and anonymous case report in a new tab and
links the editable `index.md`. Batch-generated private reports contain bounded,
validated `index.html` return links at both the top and bottom. Anonymous reports
do not link into the private review set. Tests cover link validation, return-link
placement, index generation, new-tab behavior, and the Markdown queue.

The first field run of the indexed set failed before case generation because the
helper could not reopen the selected inspection path. The batch launcher now
copies the already validated input to `inspection.snapshot.json` inside the new
private set and passes its explicit argument array to every helper invocation.
This also freezes the provenance input for the set. Single-case selection remains
an array, and regression mocks require the snapshot to exist at the helper
boundary. Validation included the two-case mocked PowerShell regression and a
real one-case launcher-to-helper run that produced both indexes, the private and
shareable reports, and the inspection snapshot.

The private HTML and Markdown indexes now group cases by retained event-binding
file path, falling back to the handler file and then `surfaceId` only when a more
useful path is unavailable. They report distinct item count separately from
handler-case count and show the private handler identity on each row. Evidence
case IDs do not wrap in the HTML table. This prevents seven handler cases across
three pages from reading as either seven pages or one unexplained batch.

Field acceptance requested contiguous case numbering inside those file groups.
Batch inspection now sorts candidate handlers by retained binding file path,
then handler identity and fact ID, before assigning local case IDs. The index no
longer has to display groups such as 001, 002, 005 followed by 003. A focused
regression deliberately interleaves handler fact ordering across file paths and
proves the emitted case IDs follow the file grouping.

The accepted workflow is packaged in `scripts/webforms-review/README.md`. Public
entry scripts remain at their already dogfooded root paths instead of being moved
behind compatibility wrappers. The guide separates the normal two-command batch
and review-set path from optional diagnostic tools, inventories every set artifact,
and repeats the private/shareable and static-evidence boundaries.

On `codex/vb-webforms-battle-test`, handler-resolution gaps now retain the event
binding fact that caused them. The modernization packet joins that evidence back
to an unavailable event chain and emits one closed diagnostic state: resolved,
missing linked method, ambiguous linked method, unproven cross-file, or
unclassified unavailable. The private workbench associates gaps by retained
support as well as scope ID, and the anonymous page-path export preserves only
the closed state. This fixes pages that previously reported unavailable handlers
and zero gaps even though the scanner had emitted the resolution evidence.

The anonymous page-path exporter now assigns chain aliases in the deterministic
source-evidence order already established by the private workbench. It no longer
sorts presentation order by opaque fact-derived chain hashes, which caused noisy
whole-page diffs whenever an extractor version changed. The order remains static
evidence order and explicitly does not claim runtime execution sequence.

Field validation isolated six apparently unavailable handlers to unfamiliar
framework-specific grid event attributes. The extractor already retained these
identifier-valued `On...` attributes as Tier 3 server-event candidates but
intentionally skipped the otherwise independent linked-method resolution step.
`legacy-webforms/0.13.3` now resolves an exact method in the linked page class
for these candidates while preserving reduced candidate coverage and an explicit
limitation that the unfamiliar attribute's framework event semantics, binding,
and runtime execution remain unproven. Client-prefixed attributes and dynamic or
non-identifier values remain excluded.

### Post-fairness field result and actionable summary

The work-machine rerun retained 466 event chains and 361 downstream boundaries.
The exclusive summary found 361 terminal-resolved chains across 25 of 43 pages:
275 SQL-query, 70 SQL-persistence, and 16 HTTP-client terminals. The remaining
chains are 41 handler-unavailable and 64 handler-resolved without a terminal.
Among the latter, retained observations separate 51 bounded-traversal truncations,
six chains with no observed downstream edge, and seven chains across three pages
with downstream evidence but no supported terminal. These are static retained
counts, not runtime reachability or absence claims.

Added `Summarize-FocusedWebFormsActionableGaps.ps1`, a zero-argument, read-only,
128-MiB-bounded handoff. It reports aliases and aggregate allowlisted rule, tier,
coverage, and linked-gap metadata for handler resolution, missing handler-owned
call evidence, and terminal-coverage review. It keeps traversal-truncated chains
separate and deferred. Synthetic validation covers all buckets, linked gaps,
priority ordering, and private identity non-disclosure.

## Terminal-free leaf and frontier diagnostics

Field actionable triage reduced the highest-value unknown set to seven chains on
three page aliases with joined downstream edges but no supported terminal. Existing
packet observations retained only counts and therefore could not identify which
closed evidence shapes exhausted or bounded those traversals.

Per-root traversal observations now retain deterministic, sorted sets of exhausted
leaf node kinds, leaf surface kinds, leaf rule IDs, bounded frontier node/surface
kinds and rule IDs, plus traversed downstream edge kinds and rule IDs. Each set is
capped at 32 distinct values with an explicit `diagnosticShapesTruncated` marker.
Synthetic root-selection edges are excluded from downstream shape metadata. The
packet and Markdown carry only these public aggregate classifications; identities,
names, paths, source text, SQL, and terminal targets are not added. The actionable
summary prints the new fields by existing alias-only bucket.

Validation: focused packet/traversal tests 68/68; full .NET solution 1766/1766;
PowerShell actionable-summary regression, scoped formatting, private-path guard,
and diff check passed. The pre-existing nullable warning in
`PropertyMappingTests.cs:560` remains unchanged.

The first field run with leaf shapes showed all seven terminal-coverage chains
exhausting both `Method` and `SymbolCandidate` leaves, with no frontier and no
surface kind. The candidate leaves are created only for exact handler-owned
syntax-tier call support; their target is intentionally hashed and isolated rather
than globally reconciled by a simple method name. This is not evidence of a missed
safe reconciliation candidate. Added leaf evidence-tier and closed reconciliation
states so the next packet distinguishes intentional syntax isolation from canonical
symbol exhaustion without retaining the private target identity. Matching behavior
remains fail closed.

## PR 723 head-review hardening

Head-review remediation preserves the field workflow while closing its remaining
boundedness and evidence-labeling gaps. Surface lists are streamed under byte,
row, and entry ceilings, comma-bearing list paths stay verbatim, and requests
that cannot be resolved after a truncated fact snapshot are labeled
`unavailable` rather than `unmatched`; filename-only matches also remain
unavailable when truncation prevents a uniqueness claim. Selected-symbol closure now applies its
frontier limit in SQL and while reading, and path work limits are validated even
when no root matches.

Private code-path reports require explicit `-IncludeRawSource` opt-in before
serializing excerpts. Physical link resolution prevents source-root escapes,
filesystem roots remain valid, and witnessed C# files are parsed once under a
bounded definition-candidate work limit. COM fallback discovery now includes
repository-contained literal imports and `Directory.Build.props/targets`, while
preserving imported custom-after-target settings. Database Fill audits require a
complete selected method symbol and an exact normalized Tier1 caller witness.

Validation: affected focused tests 112/112; full .NET solution 1799/1799;
PowerShell review-set regression and diff check passed. The pre-existing nullable
warning in `PropertyMappingTests.cs:560` remains unchanged.

The surface-list byte ceiling is also enforced by the opened read stream, not
only by a pre-open file-length observation. This closes the replacement/append
race and prevents a newly oversized single line from allocating beyond the
bounded workflow before the row ceiling can apply. A direct stream regression
proves the fifth byte fails with the stable `WebFormsSurfaceListLimitReached`
diagnostic under a four-byte test limit.

## Local page-list configuration isolation

The work-machine index path, output root, and `.aspx` page list now live in the
ignored `scripts/Run-FocusedWebFormsPageList.json` rather than a tracked edit
block. A generic `Run-FocusedWebFormsPageList.example.json` documents the exact
three-property schema. The runner, run-and-triage wrapper, raw-evidence audit,
batch/single inspections, database audit, and code-path review generators share
one strict bounded reader. Missing, malformed, oversized, extra-property, and
invalid-form configurations fail closed with stable diagnostics. Explicit path
parameters remain supported for automation. Private application naming was
removed from the checked-in example and runner.

PR review hardening now reads the local configuration through one bounded open
stream, requires JSON strings for both paths and every form, rejects blank or
multiline form entries, and preserves one page per array element. Explicit
index/report and index/inspection diagnostic invocations no longer require an
unrelated output root or local configuration. Regression coverage exercises the
strict schema, byte ceiling, and both clean-checkout explicit-input modes. The
operator handoff now describes the ignored JSON workflow instead of editing the
runner source.

## Recovered handler comparison (2026-09-29)

On `codex/webforms-config-migration`, recovery readback showed nonzero compiled
paths but a shared path cap across multiple requested handlers. This is not
evidence of lost C# port logic or single-handler parity. `whandler.ps1` uses the
recovery evidence index to count one method's retained exact chains and evidence
variants, reporting distinct source/scan/commit/symbol roots and global truncation.
The response is bounded, provenance-bound, private, and review-only: no full
handoff deserialization, scan, or graph traversal. Six focused .NET tests and the
public script test passed. Owner handler readback and full-graph repeated-I/O
cause remain pending. No runtime, full coverage, or parity claim.
