# Framework Migration Evidence v0 Review Prompts

Branch:

```text
codex/postgres-framework-migration-v0
```

Issue: [#531](https://github.com/joefeser/tracemap/issues/531)

## Kiro Opus Review

Run a fresh read-only Kiro CLI review on the exact branch head using explicit
model `claude-opus-5`. Treat the result as advisory unless ACK verifies and
admits a durable exact-head `kiro-local` receipt.

Review the specification files under
`.kiro/specs/postgres-framework-migration-evidence-v0/` against:

- existing EF mapping ownership in `.kiro/specs/ef-core-mapping-v0/`;
- database-operation ownership in
  `.kiro/specs/database-operation-call-patterns-v0/`;
- PostgreSQL raw-DDL ownership in `.kiro/specs/postgres-schema-migration-*`;
- current semantic extraction and fact/storage/report contracts; and
- TraceMap's evidence, privacy, determinism, and non-claim principles.

Check specifically:

1. Does generic `MigrationBuilder` evidence remain provider-neutral?
2. Is provider scope correctly closed to `unknown` in v0, with
   `postgresql-explicit` deferred until exact Npgsql metadata signatures are
   independently pinned?
3. Are canonical migration owner and `Up`/`Down` direction requirements strong
   enough for same-name and helper-method cases?
4. Are protected raw SQL, seed, annotation, default, and computed arguments
   excluded without retaining their digests?
5. Are the operation vocabulary, dynamic-identity gaps, and unsupported-shape
   behavior implementable using Roslyn evidence?
6. Can the producer persist through existing contracts without a schema bump?
7. Does later composition avoid confusing declared migration code with applied
   PostgreSQL state or proven rollback?
8. Does PR 1 audit the generic reducer and other existing consumers even
   though feature-specific composition is deferred?
9. Are PR 1 and PR 2 bounded enough for meaningful single-pass review?

Return blocking issues, important non-blocking issues, missing adversarial
fixtures, recommended edits, and whether the spec is ready for implementation.

Do not mutate the branch during the read-only review.
