# Framework Migration Evidence v0 Tasks

## PR 1 — Semantic Producer

- [ ] Add framework migration fact types, declaration/operation/gap rule IDs,
      explicit evidence tiers, extractor version update, and rule-catalog
      limitations.
- [ ] Implement the exact closed declaration/operation/gap property schemas;
      reject or test-fail on unlisted keys and vocabulary values.
- [ ] Add a pinned test-only `Microsoft.EntityFrameworkCore.Relational` package
      reference for positive metadata fixtures; reject identical
      application-source and unsigned same-name metadata lookalikes. Keep EF
      Core out of TraceMap product dependencies. Production admission requires
      the EF Core public-key token; do not emit Npgsql provider scope in v0.
- [ ] Emit migration declarations only from compiler-resolved EF Core base
      types.
- [ ] Require every operation's enclosing type to satisfy migration admission;
      reject ordinary helpers that accept a genuine `MigrationBuilder`.
- [ ] Emit the closed v0 `MigrationBuilder` operation vocabulary with constant
      bounded object identity, canonical owner roles, direction, provenance,
      and deterministic IDs.
- [ ] Keep all v0 framework operations provider-unknown; add no Npgsql explicit
      operation until a separately pinned package/signature fixture exists.
- [ ] Emit safe categorical gaps for dynamic identities, unresolved binding,
      helper direction, raw SQL, seed/data, annotation, default/computed, and
      unsupported operation shapes.
- [ ] Prove protected arguments and their digests never appear in facts,
      SQLite, report, or logs, and prove overlapping SQL-text/shape and runtime
      operation-call facts are absent for protected spans.
- [ ] Add same-name cross-assembly, overload/named-argument, reordered-file,
      false-positive lookalike, helper/local-function direction, constant-fold,
      array/lambda identity-shape, semantic-unavailable, and rule-catalog
      regressions.
- [ ] Aggregate gaps by migration type and `gapKind` with deterministic counts;
      prove protected invocations do not create unbounded per-call rows.
- [ ] Exclude the new fact family from generic contract-delta property matching
      until an evidence-preserving reducer contract is specified. Early-return
      by new fact type and gap rule ID; add adversarial type, member, SQL,
      migration-gap, and `UnknownAnalysisGap` downgrade regressions.
- [ ] Prove deterministic invocation ordinals preserve two identical operation
      shapes on one source line.
- [ ] Assert exact property-key sets, canonical symbol roles, fixed coverage and
      limitation values, canonical JSON column arrays, and the full closed
      `gapKind` vocabulary in producer/storage round trips.
- [ ] Audit snapshot diff, report counts, static HTML, and vault/evidence export
      behavior for the new fact types and gap rule; require preserved evidence
      metadata or explicit omission gaps.
- [ ] Add a checked-in synthetic CLI sample, inspect all five required
      artifacts, update `docs/VALIDATION.md`, and document the new rule family.

## PR 2 — Bounded Composition

- [ ] Inventory exact database design-review and release-review seams before
      changing either consumer, including the fact-type/property allowlists and
      object-kind mapping in `DatabaseDesignReviewReport.cs`.
- [ ] Display generic framework migration evidence without PostgreSQL claims.
- [ ] Correlate only operations admitted by a separately specified
      provider-explicit contract with one exact bounded same-source PostgreSQL
      identity; schema-unspecified identity remains unlinked.
- [ ] Preserve upstream rule ID, tier, file span, commit SHA, extractor
      version, coverage, supporting fact IDs, and limitations.
- [ ] Emit explicit provider-unknown, identity-unavailable, ambiguous,
      unsupported, and reduced-coverage gaps.
- [ ] Prove no output claims application, ordering, rollback, generated SQL,
      runtime provider, database state, compatibility, safety, or approval.

## Validation

- [ ] Run focused semantic, PostgreSQL schema, database design-review, release-
      review, storage, and rule-catalog tests for each changed slice.
- [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
- [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] Run a CLI scan against the checked-in synthetic fixture and inspect all
      five required scan artifacts.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Run `git diff --check`.
- [ ] Update this file and `implementation-state.md` only as work is completed.

## Deferred Follow-Ups

- Snapshot comparison and migration ordering across commits.
- Broader Npgsql provider operation vocabulary without independent fixtures.
- Custom migration-operation plug-ins and wrapper/interprocedural flow.
- Runtime migration history, execution telemetry, live introspection, or
  database connectivity.
- Shared SQL read/statement caching unless profiling proves material cost.
