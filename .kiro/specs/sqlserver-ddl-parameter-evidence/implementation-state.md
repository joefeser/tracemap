# Implementation state

## PR #911 current-head repair

Two independently verified findings at e5f3886a: reused scan artifacts lacked byte
bindings, and the SQLCMD guard did not reject directive text in comments/strings
as documented. The JSON smoke now records all five scan artifact digests for both
languages; DDL reuse checks those bytes and rejects links before generating any
bindings, and checks again before receipt publication. The raw-source SQLCMD
guard covers recognized commands, shell escapes and variable forms regardless
of lexical context. Regression matrices cover each artifact's mutation/removal/
symlink, linked directories, old receipts, all command forms in three contexts,
and an ordinary-colon negative control. No source extractor or SQL execution added.

Repair implementation: `fa00656cf7e548ffc405b6760cf2d5995504f550`.
Focused Debug -warnaserror: 206 passed; Python guards: 21 passed (17 retained plus
4 reuse tests with per-artifact subcases). Frozen-head full Debug -warnaserror:
4,788 passed, zero failed, three expected skips, 30m 10s. Sequential committed-head
JSON and DDL smokes each passed all 52 cases (8 bounded matches, 12 mismatches,
32 unknowns), byte-identical repeated reports, with all five scan-output hashes
bound and verified per language. No SQL execution. This validation reconciliation
is docs-only after the validated implementation commit.

Branch: codex/sqlserver-ddl-parameter-evidence. Initial clean head:
70e57e329a9954de15712012d97d3c195d84cf6d (docs directly above requested
560fa24216516b17f75882d7d10011361b3f9c11). Fetched origin; dev unchanged.
Live #908/#870 inspected; both remain open and broader than this slice.
No private inputs, SQL, Docker, remote hosts, agents or paid reviews authorized.

Current validated implementation head: `7d20c1ec600916f02dd4f3e65092230bba859ba3`
(see "Independent review round" below). First implementation and validation was
`7a05e13e9e9db6c1020288597cb5e890cf2bd2bf`. Source, revision and shared build outputs
were held stable throughout the full gate; native smokes followed completion.
This documentation-only reconciliation records delivery evidence separately.

## Implemented scope and precommit validation

Separate sqlserver-ddl produce/compare, ScriptDom 170.191.0 / TSql160 parser,
DDL-origin schema/declaration/report versions, explicit file hash/identity
manifest, parser/Core binary provenance and whole-input fail-closed bounds.
Shared comparison engine extracted behind independent origin admission; old
JSON route and extractor versions unchanged. Defaults/decimal facets remain
unknown, parameter spans and unsupported evidence retained. #910 publication
helpers reused unchanged. Six authored public SQL templates and independent
DDL native-smoke expectations supplement the retained JSON matrix.

Focused Debug -warnaserror: 133 passed, zero failures/skips (15 seconds).
Original Python harness guards: 17 passed. First focused run (129 passed,
2 failed) found root-ancestor symlink handling incorrectly refused macOS temp
paths. Corrected to reject links at/below the explicitly trusted root; rerun
passed 131 tests, then added span/RETURN/output/link controls for the 133 pass.
Initial development build found one nullable warning; fixed with an explicit
DoesNotReturnIf contract, no suppression. No pass claimed for failed attempts.
Development JSON smoke: 52 selected cases, exact 8 bounded matches / 12 mismatches /
32 unknowns and 25 unbound-call gaps per report; dirty implementation at initial
Git revision, not committed-head acceptance. DDL development smoke also passed all 52 cases (8/12/32), with exact parser/Core
provenance and byte-identical JSON/Markdown repeats. Both development runs used
dirty code at the initial Git revision, not committed-head acceptance.

Fresh pinned C#/VB OSS adapter smokes explicitly deferred: no source extractor
change; focused semantic/native C#/VB fixtures cover the unchanged route. The
full shared-core suite remains required. Non-.NET and Windows private/application
checks deferred; no cross-platform or real DNN end-to-end acceptance claimed.

## Validation protocol and remaining work

Commit implementation, then freeze all source, Git revision and shared outputs
for full Debug solution -warnaserror. No commit/rebuild/competing smoke during
that gate. Run original JSON and DDL committed-head smokes sequentially afterward.
A documentation-only delivery reconciliation may record results after the gate;
its SHA is distinct from the validated implementation SHA and must be disclosed.
No implementation changes may piggyback on that reconciliation.

No new work-machine blocker. Remaining future work is result-column/consumer
comparison, catalog/live evidence, private SQL-A, application builds and migration
readiness; DNN selected-reader/wrapper/qualifier gaps persist. #908/#870 stay open.

Final precommit focused repeat: 133 passed, 0 failures/skips (16 seconds).
Final DDL-only repeat after the oversize-export regression: 48 passed,
0 failures/skips (18 seconds). Producer rejects exports over the 4 MiB admission
ceiling; Markdown includes supported parameter spans as well as unsupported ones.
Private-path and staged whitespace guards passed before implementation commit.


## Independent review round (current)

Started from delivered head 0a48b40 without assuming its gates still held.
Probed ScriptDom directly and through the CLI. Fixed, one commit each, each with
a regression test: parser stack overflow inside bounds (84a70b0a2; reproduced
exit 134, and the new test crashes the test host when the fix is reverted),
silent ANSI type-synonym admission (63318851c), documented-but-rejected leading
BOM (1624cf592), plus LF pinning for DDL smoke/template bytes (7d20c1ec6).

Validated implementation head: `7d20c1ec600916f02dd4f3e65092230bba859ba3`.
Focused 151 passed; Python guards 17 passed; full Debug -warnaserror 4,733
passed / 0 failed / 3 expected skips (33m 13s) with frozen source/HEAD/outputs;
sequential JSON then DDL committed-head smokes 52/52 each, 8/12/32, 25 unbound
per report. Core SHA-256 a9a368ff…1098; parser unchanged 2dcf224a…54ce.
One development focused run printed 58 passed then "Test Run Aborted" with no
recorded reason; seven repeats passed fully. Not counted; cause unconfirmed.

Known conservative bound kept as-is: the 128 opening-paren/BEGIN/CASE/IF/WHILE
budget is per file, so sized parameters can exhaust it before 128 parameters.
Changing it is a deliberate future decision, not a defect fix.

## Earlier committed-head acceptance and delivery (superseded by the round above)

Implementation commit: `7a05e13e9e9db6c1020288597cb5e890cf2bd2bf`.
Full Debug solution -warnaserror: 4,715 passed, 0 failed, 3 expected skips;
32m 20s tests / 32m 30.78s command wall-clock. Skips: opt-in full golden sweep,
unset Python receipt interop environment, Windows-only deep ASP.NET publish.
No compiler/analyzer warnings/errors. No source/HEAD/shared-output change or
competing smoke/build occurred during the gate.

Sequential committed-head JSON and DDL native smokes each passed 52 selected
cases: 8 bounded matches, 12 mismatches, 32 unknowns, plus 25 retained unbound
call gaps per report. Exact current revision, five scan outputs, public input
hashes, Core/parser bytes and repeated report bytes verified. These are the
acceptance runs; earlier dirty-source smokes remain development evidence only.
Core SHA-256: 3bca2009f8ab0395bba3575111766dc4ee2bc3048572f2b2136d4bc8f9709ceb.
Parser SHA-256: 2dcf224afac347fa1788eeaa6f8db78a17805b82338590b7e51448ab356a54ce.

Consolidated implementation report, exact matrices, guards, provenance, limitations
and delivery boundary: docs/validation/SQLSERVER_DDL_PARAMETER_EVIDENCE.md.
Follow-up: prompts/continue-sqlserver-ddl-parameter-evidence.md.
The delivery reconciliation is documentation-only on top of the validated
implementation; no implementation change follows those gates. All source changes
are committed for normal push on this branch; no PR/merge is requested.
No remaining blocker for the bounded offline slice. Pinned OSS/non-.NET/Windows
reruns remain explicitly deferred; DNN binding, result/consumer, catalog/live,
private SQL-A, application build and migration acceptance remain future work.
