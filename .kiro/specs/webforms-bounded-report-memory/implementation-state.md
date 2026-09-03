# Implementation state

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
