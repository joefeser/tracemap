# Implementation state

- Branch: `codex/access-crosstab-alias-provenance`
- Base: `origin/dev` at `6a8b5bd14187e5258a9805ee73cad6ff66cb9079`
- Tracking: focused follow-up for the `qrptSAsByWeek` crosstab `[1]` versus
  `W0`–`W11` output mismatch; issue number is owner-assigned separately.
- Scope: static QueryDef/SQL projection provenance only. Existing output facts
  now preserve alias kind, role-scoped source-expression hashes, aggregate
  source candidates, and pivot-expression source candidates.
- Decision: do not infer that numeric pivot literals are aliases for report
  control names. The output catalog exposes the evidence needed for that
  reconciliation while keeping the mismatch a gap.
- Safety: no query execution, recordset/row access, report rendering, or new
  Access COM surface.
- Validation: the complete focused Access suite passed (329 tests); the full
  solution built with zero warnings/errors; all 1,310 solution tests passed;
  the private-path guard and `git diff --check` passed.
- Review correction: calculated aliases now preserve name-aligned provenance
  without weakening direct-field lineage trust; non-output source roles fail
  closed in composition/flow while missing pre-0.3.2 roles retain their legacy
  output-expression meaning; pre-provenance public constructor signatures are
  retained; empty crosstab output catalogs emit an explicit rule-backed gap;
  calculated row-heading expressions whose output name cannot be recovered are
  retained with a deterministic ordinal identity, partial coverage, source
  candidates where provable, and an output-scoped gap rather than disappearing;
  crosstab pivot parsing now binds the static column list to the top-level `IN`
  clause that terminates the pivot statement so nested or preceding expression
  `IN` operators cannot truncate provenance or manufacture complete coverage;
  statement termination is also located outside bracketed identifiers and
  parenthesized expressions, so punctuation in a field such as
  `[Month;Code]` cannot truncate recoverable pivot lineage;
  the complete output-field and crosstab projection constructors are explicitly
  designated for source-generated JSON deserialization, and the worker protocol
  now round-trips populated provenance fields so compatibility overloads cannot
  make a successful Access worker result unreadable;
  aggregate aliases are stripped
  before value/source resolution so a supported aliased `TRANSFORM` expression
  retains complete lineage.
- Validation procedure: followed the Mac-only Microsoft Access adapter smoke in
  `docs/VALIDATION.md`. The private Windows corpus rerun remains explicitly
  deferred because this correction does not widen COM or require a real
  database.
- Deferred: exact DAO QueryDef property reconciliation from the private Windows
  copy, dynamic pivots, DCount runtime behavior, and report layout/runtime
  validation.
