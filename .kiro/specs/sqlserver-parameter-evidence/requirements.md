# Bounded SQL Server parameter evidence

Implement only independent synthetic declaration admission and explicit source-call
parameter comparison after #909, not completion of #908/#870. Offline only; no
SQL, Docker, private inputs, result columns, connection inference or deployed parity.

- Versioned public declaration input and deterministic export with exact generator
  assembly SHA-256 and canonical public input SHA-256. Verify expected repo/revision,
  schema versions, duplicate identities, malformed fields, substituted input and hashes.
- Exact logical server/database/schema/procedure identity and separate fact-ID binding;
  never join by short name. Preserve missing/conflicting/stale/ambiguous state.
- Bounded Roslyn method-local C#/VB per-parameter names, types, sizes, directions and
  source lines at each execution site; unsupported state remains typed unknown.
- Native opt-in producer/admission/report commands; separate JSON/Markdown report,
  rules, tiers, source/schema locations, revisions and extractor identities.
- Explicit Unicode/ANSI/MAX/default/OUTPUT/ReturnValue policies. Partial reports never
  claim aggregate clean, truncation, SQL execution, deployment or real DNN reader parity.
- Focused deterministic/regression/CLI tests, existing DNN/Python guards, full Debug
  -warnaserror suite, committed-head synthetic smoke, documented adapter gates.
