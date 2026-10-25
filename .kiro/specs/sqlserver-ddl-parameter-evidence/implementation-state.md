# Implementation state

Branch: codex/sqlserver-ddl-parameter-evidence. Initial clean head:
70e57e329a9954de15712012d97d3c195d84cf6d (docs directly above requested
560fa24216516b17f75882d7d10011361b3f9c11). Fetched origin; dev unchanged.
Live #908/#870 inspected; both remain open and broader than this slice.
No private inputs, SQL, Docker, remote hosts, agents or paid reviews authorized.

Implementation in progress. Full gate must run after implementation commit with
source, revision and build outputs held stable; smokes follow full completion.

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
