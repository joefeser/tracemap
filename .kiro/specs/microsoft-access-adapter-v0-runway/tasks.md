# Microsoft Access Adapter v0 Runway Tasks

This is a spec-only runway. Implementation tasks are intentionally unchecked.
The first implementation PR should complete Phase 0 through Phase 6 only. Later
phases must remain separate reviewable slices.

## Phase 0: Scope Lock and Safety Review

- [ ] 0.1 Create an implementation branch from latest `origin/dev` and update
      `implementation-state.md` before coding.
- [ ] 0.2 Re-read this spec, `docs/LANGUAGE_ADAPTER_CONTRACT.md`,
      `docs/ACCEPTANCE.md`, `docs/VALIDATION.md`, and the SQL secret-safety docs.
- [ ] 0.3 Confirm the adapter is Windows-local, deterministic, non-executing,
      and tied to repository plus commit SHA.
- [ ] 0.4 Confirm the first slice excludes forms/reports, VBA flow, macro bodies,
      runtime analysis, composition reports, and site work.
- [ ] 0.5 Record the exact .NET project layout, CLI name, Access/DAO versions,
      local Windows validation environment, and validation commands.
- [ ] 0.5a Choose and record the bounded IPC mechanism, heartbeat framing,
      protected-material contract, and platform-neutral test strategy before
      worker implementation.
- [ ] 0.6 Threat-model startup macros, startup forms/functions, action and
      pass-through queries, linked sources, hostile files, trust prompts, COM
      hangs, working-copy cleanup, raw-value leakage, and dirty Git inputs.

## Phase 1: CLI, Environment, and Provenance Foundation

- [ ] 1.1 Add `TraceMap.Access` and the dedicated Access CLI project to the
      solution without breaking non-Windows builds.
- [ ] 1.2 Implement deterministic `scan`, `--repo`, `--database`, `--out`,
      `--help`, and `--version` behavior.
- [ ] 1.3 Add runtime Windows and Access/DAO COM capability probes that do not
      initialize COM for help/version.
- [ ] 1.4 Require a Git worktree and concrete commit SHA.
- [ ] 1.5 Reject database paths outside the worktree, traversal, symlink/reparse
      escape, non-files, untracked files, and working-tree bytes that differ
      from the selected commit.
- [ ] 1.6 Implement safe output directory preparation that only replaces the
      requested output path.
- [ ] 1.7 Add tests for unsupported platform, missing COM, invalid paths,
      dangerous output paths, missing Git metadata, and dirty/untracked inputs.
- [ ] 1.8 Document and test that v0 scans checked-out `HEAD`; historical scans
      require checkout, and Git LFS pointer/unverifiable content is rejected.

## Phase 2: Controlled Copy and Non-Execution Boundary

- [ ] 2.1 Implement a private temporary working directory with restrictive
      Windows ACLs and bounded cleanup.
- [ ] 2.2 Copy the selected database, verify its hash, and open only the copy.
- [ ] 2.3 Force-disable Office automation macros before database open and keep
      Access UI hidden.
- [ ] 2.4 Encapsulate COM acquisition/release and close/quit behavior with
      deterministic `finally` cleanup.
- [ ] 2.5 Add extraction duration, object, string, module, fact, gap, and
      diagnostic bounds with explicit limit gaps.
- [ ] 2.6 Prohibit recordsets, row counts, query execution, object invocation,
      link refresh, macro execution, and pass-through/action query field
      enumeration in the adapter API surface.
- [ ] 2.7 Add an AutoExec/startup-form/action-query/pass-through/linked-source
      hostile canary and prove none fires during the scan.
- [ ] 2.8 Add cleanup-failure diagnostics that never persist temporary absolute
      paths in artifacts.
- [ ] 2.9 Implement the supervisor/worker heartbeat and total timeout, record and
      validate the owned Access PID, and terminate only the owned worker/Access
      processes on hang.
- [ ] 2.10 Add timeout, worker-crash, close/quit-hang, stale/foreign-PID, Job
      Object rejection/fallback, and modal repair/trust prompt tests.

## Phase 3: Rule Catalog, Fact Model, and Standard Artifacts

- [ ] 3.1 Add only the first-slice rule IDs to `rules/rule-catalog.yml` with
      limitations and non-claims.
- [ ] 3.2 Add required Access-specific fact types, rule constants, and extractor
      versions to the shared model.
- [ ] 3.3 Reuse shared `LegacyData*`, query, file-inventory, and gap facts only
      where their semantics match exactly.
- [ ] 3.4 Implement deterministic Access database/object/field/relationship/query
      stable keys with role-separated hashes.
- [ ] 3.5 Use the repository-relative database path and `1:1` evidence span for
      binary catalog objects; document that this anchors the container.
- [ ] 3.6 Write the required manifest, NDJSON, SQLite, report, and analyzer log
      using shared contracts.
- [ ] 3.7 Record `rowDataRead=false` and `executionPerformed=false` as extractor
      capability declarations without overstating hostile-input safety.
- [ ] 3.8 Add catalog-guard, artifact-schema, deterministic-ID, sorted-property,
      and raw-value-suppression tests.
- [ ] 3.9 Reuse `LegacyDataSafeValues` or a shared equivalent and record any
      Access-specific secret-bearing identifier heuristics before emitting raw
      table/field/form/report names.
- [ ] 3.10 Enforce aggregate fact, gap, safe-projection-byte, and artifact-byte
      ceilings with explicit reduced-coverage gaps.

## Phase 4: Schema and Relationship Extraction

- [ ] 4.1 Inventory `.accdb` and provider-compatible `.mdb` inputs.
- [ ] 4.2 Enumerate local non-system table design metadata without recordsets.
- [ ] 4.3 Emit table and field facts with safe/hash identities, ordinals, Access
      type family, declared size, and required/nullability metadata.
- [ ] 4.4 Emit primary/unique/index membership without reading values.
- [ ] 4.5 Emit simple and composite relationship facts with deterministic field
      ordering and supporting IDs.
- [ ] 4.6 Omit system/temporary/hidden objects by default and preserve bounded
      summary counts.
- [ ] 4.7 Emit schema ambiguity/unavailable/limit gaps rather than guessing.
- [ ] 4.8 Add zero-row fixture tests proving schema evidence without row or
      attachment/OLE access.
- [ ] 4.9 Add case-only identifier collision tests and a provider-incompatible
      `.mdb` failure/gap test; use a real disposable `.mdb` smoke only when the
      installed provider supports creating it.

## Phase 5: Saved Query and External Boundary Extraction

- [ ] 5.1 Enumerate saved query declarations and classify query kinds without
      executing or materializing results.
- [ ] 5.2 Hash query SQL in memory and persist only hash, length, kind, parameter
      metadata, and safe dependency projections.
- [ ] 5.3 Implement a bounded Access SQL identifier projector for supported
      local-object reference shapes.
- [ ] 5.4 Emit partial/unknown coverage and query dependency gaps for dynamic,
      malformed, unsupported, ambiguous, action, DDL, union, crosstab, and
      pass-through shapes as appropriate.
- [ ] 5.5 Never enumerate pass-through/action query result fields.
- [ ] 5.6 Emit external-link/pass-through boundaries with safe provider family
      and role-separated connection/source hashes only.
- [ ] 5.7 Add planted fake SQL, credential, server, DSN, URL, UNC, drive-path,
      and private-name leak tests across all artifacts and logs.
- [ ] 5.8 Assert the worker IPC never carries raw SQL, connection, VBA, macro,
      absolute-path, or other protected material, including error frames.

## Phase 6: First-Slice Validation and PR Readiness

- [ ] 6.1 Add a deterministic PowerShell generator for a disposable synthetic
      zero-row Access database and hostile canaries.
- [ ] 6.2 Run the platform-neutral Access unit/contract tests.
- [ ] 6.3 Run the Windows + installed Access integration smoke twice to different
      output paths and compare deterministic evidence.
- [ ] 6.4 Prove canaries did not fire and original input hash did not change.
- [ ] 6.5 Run Access-index export, combine, and combined report smokes.
- [ ] 6.6 Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] 6.7 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 6.8 Run or explicitly defer relevant pinned checks from
      `docs/VALIDATION.md` if shared readers changed.
- [ ] 6.9 Run `./scripts/check-private-paths.sh`.
- [ ] 6.10 Run `git diff --check`.
- [ ] 6.11 Update completed checkboxes and implementation-state with branch,
      scope, validation, failures, deferred work, and public claim level.
- [ ] 6.12 Run concurrent scans against the same source and prove distinct
      working copies, stable outputs, and an unchanged original input hash.

## Phase 7: Forms, Reports, Controls, and Bindings (Follow-Up PR)

- [ ] 7.1 Add catalog rules/facts for Access forms, reports, controls, and direct
      bindings.
- [ ] 7.2 Inventory surfaces and controls through design metadata only.
- [ ] 7.3 Emit direct record/control/row-source binding candidates; hash complex
      expressions and emit gaps for ambiguous targets.
- [ ] 7.4 Classify event properties without exporting expressions or embedded
      macro bodies.
- [ ] 7.5 Add sensitive caption/label/expression/value suppression tests.
- [ ] 7.6 Validate form/report extraction does not render or invoke surfaces.

## Phase 8: VBA and Event Flow (Follow-Up PR)

- [ ] 8.1 Add catalog rules/facts for module/procedure inventory, bounded calls,
      events, navigation, and dynamic gaps.
- [ ] 8.2 Read VBA source in memory and persist only hashes, counts, safe names,
      spans, and bounded call candidates.
- [ ] 8.3 Tokenize while excluding comments and string contents from ordinary
      call matching.
- [ ] 8.4 Project direct Access APIs such as `DoCmd.OpenForm`,
      `DoCmd.OpenReport`, `DoCmd.OpenQuery`, DAO object references, domain
      functions, and `OpenRecordset` as Tier3 candidates without execution.
- [ ] 8.5 Map exact same-module event procedures and emit ambiguity/missing gaps.
- [ ] 8.6 Add `Eval`, `Run`, callbacks, variables, concatenation, COM, external
      process, and conditional-target gaps.
- [ ] 8.7 Prove raw VBA, comments, literals, SQL, paths, and command bodies never
      enter artifacts or failure messages.

## Phase 9: Macro/External Depth and Reporting (Follow-Up PR)

- [ ] 9.1 Inventory named/UI/data/startup macros without command bodies.
- [ ] 9.2 Decide through a separate threat review whether any macro command
      semantics can be safely projected; keep body inspection deferred unless
      approved by rule and leak tests.
- [ ] 9.3 Add Access evidence sections to human reports and documentation exports
      with hidden public claim level.
- [ ] 9.4 Verify vault/release-review consumers preserve provenance or emit an
      unsupported-consumer gap.
- [ ] 9.5 Add a local-only representative customer/sample validation workflow
      using labels, counts, hashes, and gaps only.

## Deferred Beyond V0

- Password-protected/encrypted database secret channels.
- Workgroup security and effective permissions.
- Runtime query, form, report, macro, or VBA execution.
- Record sampling, row counts, data profiling, quality analysis, or PII
  classification.
- Cross-database live resolution and linked-schema refresh.
- Full VBA semantic compiler integration.
- Access migration or rewrite generation.
- Public site claims before public-safe fixture and claim review.
