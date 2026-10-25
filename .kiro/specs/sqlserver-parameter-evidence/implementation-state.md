# Implementation state

Branch: `codex/sqlserver-parameter-evidence`.
Base: freshly fetched `origin/dev` `fe48109124912d0b22516ce009ee34e7d59ff7bd`
(PR #909); ancestry verified. Other worktrees preserved. Live #908/#870 read.
Implementation and delivery complete. Implementation commit:
`84e531984932d3ce7b3157d9f464f603a14e0c05`, pushed to origin on the named branch.
This documentation-only reconciliation records the verified delivery evidence.

## PR #910 review correction batch

Four exact-head findings were admitted by ACK: unsupported implicit receivers,
two output-publication findings, and lost mismatch reasons under unknown facets.
The two publication findings share one invariant: only publish complete new
outputs, never replace a concurrently created destination. The generic scan
transaction permits replacement and requires five scan artifacts, so this
two-file report uses its own no-replace staged promotion. Produce claims the
destination directly with CreateNew; its owner removes failed output.

Command initializers and implicit VB With property mutations return unknown;
C#/VB extractor versions advance to 0.22.8 / 0.10.11. Proven type/direction
reasons survive unsupported facets while overall status remains unknown.
Regressions cover both languages, concurrent publication, partial-write cleanup
and retry, and exact reason lists. Focused gate: 77 passed. Python guards:
17 passed. An initial full run exposed a Unix File.Move no-overwrite race in
the new concurrency regression; it was stopped and produce was corrected to
direct CreateNew. No pass is claimed for that interrupted run. Corrected full Debug
-warnaserror validation: 4,659 passed, 0 failed, 3 expected skips in 29m 2s.
The concurrency regression also passed ten consecutive repetitions. Native smoke
at repair commit 06865ce5dfb0d6888596a31edefad4e0bfd50469 passed all 52 cases:
8 bounded matches, 12 mismatches, 32 unknowns; provenance and byte-identical
repeated reports verified. Pinned OSS adapter
smokes from the implementation remain historical; this bounded repair uses
the focused semantic regressions and native synthetic smoke, with fresh pinned
OSS runs explicitly deferred. No SQL/container/private execution or merge.

## Contract and bounded scope

Strict independently authored synthetic declaration input and native schema export/
admission, not DDL parsing, catalog metadata or runtime observations. Exact Core
assembly generator SHA-256 and canonical public input SHA-256, strict JSON/identity/
version checks. Explicit binding ties native fact ID, public source-evidence digest,
command spelling and full logical server/database/schema/procedure to repo/revision.
No short-name, connection or DNN owner/object-qualifier inference.

C#/VB additive parameter state with extractor bumps to csharp-semantic/0.22.7 and
vb-semantic/0.10.10. Per-parameter Add and Direction assignment spans are retained.
Legacy DatabaseCallEvidence/aggregate witnesses and closed receipts remain unchanged.
Aliases (including cross-command, collection and array), constructors, helpers,
unsupported mutation/control flow and work bounds retain typed gaps. Separate native
JSON/Markdown report preserves source/schema/conclusion rules, tiers and extractor
identities; every report is partial with no aggregate clean verdict.

Authored JSON was chosen deliberately: no SQL parser dependency was found in the
.NET tree, and this origin does not interpret SQL. Full schema/DDL/catalog/result
comparison is not claimed. See docs/validation/SQLSERVER_PARAMETER_EVIDENCE.md for
exact format, commands, policies, matrix, provenance and limitations.

## Validation

- Final full Debug solution `-warnaserror`: **4,651 passed, 0 failed, 3 skipped**;
  30m 52s. Skips: opt-in full oracle sweep, unset Python receipt interoperability,
  Windows-only deep projectless ASP.NET publish. Compiler/analyzer gate passed.
- Final focused gate: **70 passed, 0 failed/skipped**. Includes new parameter/
  admission tests, DnnSqlValidationTests, both native CLI round trips and the
  mandatory C# version-assertion update. Earlier interrupted full runs are not
  claimed as passes; one identified that stale version assertion.
- Existing Python DNN harness guards: **17 passed**. Offline defaults remain
  NOT FETCHED / NOT RESOLVED / NOT RUN. No #909 harness scripts/receipts changed.
- Development native smoke: **52 selected cases**, 26 sites per language across
  25 methods each; **8 bounded matches / 12 mismatches / 32 unknowns**. Expectations
  and the two-command order are independently authored. Each report preserves the
  other 25 unbound calls as unknown. Repeated output bytes are identical. This
  precommit smoke used dirty implementation content and the base Git revision.
- Committed implementation-head rebuild: **0 warnings / 0 errors**. Native smoke
  at `84e531984932d3ce7b3157d9f464f603a14e0c05`: **all 52 selected cases passed**,
  exact 8/12/32 matrix, current revision, actual Core generator bytes, explicit
  projected source-evidence bindings and byte-identical repeated reports verified.
  The receipt binds exact smoke script bytes and only its public bounded inputs.
  Both language scans built successfully and retained all five required outputs.
- Final compiled-contract pinned C# ProjectExtensions.Azure.ServiceBus
  `2a8e72c8f5680edf2096b05ac08c39d47a95cef8`: 12,949 facts / 4,732 gaps.
  Pinned Community.VisualBasic `20d2a51dfc9f342848ad134952ceaa8d79302559`:
  114,806 facts / 79,222 gaps. Both Level1SemanticAnalysisReduced / FailedOrPartial;
  artifact/extraction checks, not clean application builds or SQL acceptance.
- Staged private-path and whitespace guards pass. The privacy guard caught a
  private-name token in a prohibition sentence; it was replaced with generic text.
- Windows and non-.NET adapter smokes remain explicitly deferred for this .NET
  source/CLI contract slice. The relevant pinned C#/VB gates were run.

## Delivery and follow-up

Consolidated prompt: `prompts/continue-sqlserver-parameter-evidence.md`.
No new authority blocker or work-machine round trip. No PR opened, paid review
requested, merge, private input, SSH, Docker/image/EULA operation, SQL, IIS or
application deployment. Live activation, other schema origins, additional provider/
default/facet semantics and result-column comparison remain separate future decisions.
Synthetic comparison does not clear the real DNN selected-reader/qualifier gaps or
complete #908/#870/private SQL-A acceptance.
