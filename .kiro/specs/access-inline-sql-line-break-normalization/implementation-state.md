# Implementation State

- Branch: `codex/access-inline-sql-wildcard-lineage`
- Base: `origin/dev` at `fffda79f7d9bb8df3fc19c9fffc95f99afe270eb`.
- Starting evidence: 40 deterministic binding remainders, including 27 report
  cases, from the immutable snapshot used by PR #584.
- Target cluster: one partial inline record source and six unmatched report
  control outputs beneath the same qualified-wildcard saved-query source.
- Root cause: the retained Access SaveAsText property contains `\015\012`
  line-break escapes. The quoted-scalar parser preserves those bytes literally,
  so the otherwise supported `FROM` boundary cannot be recognized.
- Decision: decode only the two observed SaveAsText newline escapes at the
  scalar boundary and reuse the existing dependency-scoped wildcard field
  composition. Do not invent wildcard output order or upgrade the record source
  beyond available evidence.
- Corpus observation: the immutable design bundle contains nine `\015` and nine
  `\012` occurrences and no other three-digit escaped property codes.
- Implementation: `AccessUiTextParser` now converts only `\015` to carriage
  return and `\012` to line feed while parsing quoted SaveAsText scalars. The
  existing inline-SQL and dependency-scoped field projectors remain unchanged.
- Regenerated evidence: the immutable base scan and normalized design bundle
  produced 8,972 facts and passed adapter-artifact validation. Deterministic
  binding remainders fell from 40 to 34. All six
  `AccessBindingInlineSqlOutputUnmatched` cases beneath the multiline qualified
  wildcard report disappeared; the two genuinely partial wildcard projections
  remain labeled partial.
- Focused validation: 67 Access UI, design-composition, and screen-flow tests
  passed.
- Full validation: solution build passed with zero warnings/errors; all 1,144
  tests passed; changed-file whitespace verification, private-path guard, and
  `git diff --check` passed. Repository-wide whitespace verification still
  reports unrelated pre-existing drift outside this slice.
- Pull request: #585 targets `dev`; ACK pending on the final exact head.
- Deferred: the 12 DCount/query-output mismatches, owner identity/obsolescence
  confirmations, genuinely ambiguous record sources, unresolved functions,
  dynamic wildcard outputs, and report-layout reconstruction.
