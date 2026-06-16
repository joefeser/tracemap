# Legacy Story Reconciliation Requirements

## Introduction

Recent legacy-analysis branches landed close together: WCF/service-reference
mapping, WCF metadata normalization, WebForms event flow, legacy flow
composition, legacy data metadata extraction, legacy baseline artifacts, legacy
codebase validation, and .NET Remoting detection. The merge was mechanically
validated, but future contexts need a small reconciliation pass so state notes,
tasks, and tests describe the current product accurately.

This phase does not add a new extractor or public claim. It proves the legacy
stories coexist and removes stale spec noise that makes implemented work look
queued or unfinished.

Public claim level: hidden.

## Background

The reconciliation targets these shipped or MVP legacy specs:

| Spec directory | Current issue to reconcile | Target state |
| --- | --- | --- |
| `.kiro/specs/legacy-wcf-service-reference-mapping/` | `tasks.md` and implementation state still looked spec-only/unchecked after implementation landed. | Mark implemented tasks complete and update state to implemented. |
| `.kiro/specs/legacy-wcf-metadata-normalization/` | No product change needed; it is a dependency for current WCF evidence wording. | Leave as implemented context. |
| `.kiro/specs/legacy-remoting-detection/` | Recently merged sibling legacy detector. | Prove it coexists with WCF and legacy data evidence. |
| `.kiro/specs/legacy-data-metadata-extraction/` | MVP implemented, but some state still used spec-only wording and deferred breadth tasks looked like stale work. | State MVP implemented and label unchecked items as deferred follow-ups. |
| `.kiro/specs/legacy-flow-composition-reporting/` | Still described legacy data metadata as queued. | Describe legacy data metadata as optional implemented MVP input. |
| `.kiro/specs/legacy-codebase-validation/` and `.kiro/specs/legacy-baseline-regression-artifacts/` | No new behavior needed; they provide validation/baseline context. | Keep current state, no public claim promotion. |

Baseline examples from `dev` before this reconciliation:

- `.kiro/specs/legacy-wcf-service-reference-mapping/tasks.md` had all tasks
  unchecked while WCF fact/rule/test code was already merged.
- `.kiro/specs/legacy-flow-composition-reporting/implementation-state.md`
  described legacy data metadata extraction as queued even after the MVP landed.
- `.kiro/specs/legacy-data-metadata-extraction/implementation-state.md` mixed
  historical spec-only wording with implemented MVP validation.

## Requirements

### Requirement 1: Preserve legacy evidence families together

**User Story:** As a maintainer, I want one regression test proving the recently
merged legacy extractors can run together, so future merges do not accidentally
drop a legacy evidence family.

#### Acceptance Criteria

1. WHEN a synthetic repository contains WCF/service-reference evidence, .NET
   Remoting evidence, and legacy data metadata evidence THEN focused regression
   tests SHALL prove `tracemap scan` emits facts from all three families.
2. WHEN the synthetic repository contains raw endpoint URLs, remoting object
   URIs, or connection strings THEN serialized facts and Markdown report output
   SHALL NOT expose those raw values.
3. WHEN coexistence evidence is asserted THEN assertions SHALL check the
   following minimum evidence:
   - WCF: `legacy.wcf.mapping.v1` with non-unknown static evidence, expected
     `Tier2Structural` for the synthetic mapping fixture.
   - Remoting: `legacy.remoting.registration.v1` with static registration or
     activation evidence, expected `Tier3SyntaxOrTextual` or stronger for code
     and `Tier2Structural` or stronger for checked-in config.
   - Legacy data: `legacy.data.dbml.v1` and
     `legacy.data.generated-link.v1`, expected `Tier2Structural` for DBML and
     generated-code linkage in the synthetic fixture.
4. WHEN coexistence evidence is asserted THEN facts SHALL include non-empty
   extractor ID, extractor version, commit SHA, rule ID, evidence tier, and
   line span metadata.

### Requirement 2: Clean stale state notes and task status

**User Story:** As an agent resuming TraceMap work, I want legacy specs to state
which slices are implemented versus deferred, so I do not route work based on
stale unchecked checkboxes.

#### Acceptance Criteria

1. WHEN a legacy spec is fully implemented on `dev` THEN its `tasks.md` SHALL not
   leave completed implementation tasks unchecked.
2. WHEN an implementation state file says a slice is spec-only, queued, or ready
   for implementation after code has landed THEN it SHALL be updated to current
   implemented or MVP status.
3. WHEN a task remains unchecked THEN it SHALL represent a real deferred
   follow-up, not historical merge noise.
4. WHEN stale notes mention branch/process oddities THEN they SHALL avoid local
   paths, private repo names, raw remotes, or machine-specific details.

### Requirement 3: Keep scope bounded

**User Story:** As a reviewer, I want reconciliation to be safe and reviewable,
so it does not sneak in product behavior changes.

#### Acceptance Criteria

1. The implementation SHALL NOT add new fact types, rule IDs, CLI commands, or
   report classifications.
2. The implementation SHALL NOT change reducer, diff, impact, reverse, or
   portfolio semantics.
3. The implementation SHALL NOT add LLM calls, embeddings, vector stores, or
   prompt-based classification.
4. The implementation SHALL keep public claims hidden unless a separate site
   spec promotes them with proof paths.

### Requirement 4: Validate the reconciliation

**User Story:** As a maintainer, I want the cleanup validated by the same checks
used for merge conflict repair, so the branch is safe to merge.

#### Acceptance Criteria

1. `dotnet build src/dotnet/TraceMap.sln` SHALL pass.
2. `dotnet test src/dotnet/TraceMap.sln` SHALL pass.
3. `npm test && npm run build` from `site/` SHALL pass if site files are touched.
4. `./scripts/check-private-paths.sh` SHALL pass.
5. `git diff --check` SHALL pass.
6. IF scanner behavior or extractor interaction is changed beyond the
   reconciliation test THEN the implementer SHALL run a checked-in demo or smoke
   script and document the result. This phase is expected to avoid behavior
   changes, so the unit regression plus full test suite is sufficient.
