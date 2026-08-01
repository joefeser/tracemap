# Implementation state

Status: implementation complete locally; PR handoff pending.

Branch: `codex/access-vba-source-export`

Scope: issue #570 only. Issues #571 and #572 remain deferred.

The preserved branch began at `a1e5c48b72cb039d0a382f0c6fd942727f859d3a`
and includes the required prior VBA/export compatibility work. This slice
adds optional query lineage fields and synthetic tests; it does not widen
Access COM or read rows.

Baseline accounting from the private handoff is 22 append, 10 update, one
delete, and 18 crosstab partial queries (51 total). The implementation emits a
lineage candidate for each classified kind and leaves unsupported/dynamic
correspondence partial. A measured private before/after corpus rerun is still
pending and is deliberately not fabricated from the baseline census.

Implementation details: action lineage is emitted as
`AccessQueryActionLineageCandidate`; crosstab lineage is emitted as
`AccessQueryCrosstabLineageCandidate`. The screen-flow reporter consumes
resolved action targets as static edges. The copy/clone reporter consumes
resolved action targets and suppresses its field-correspondence gap only when
the action projection is complete. No crosstab row-derived columns are
invented.

Validation: focused Access tests 183/183, full solution tests 1055/1055, full
solution build clean with 0 warnings/errors, private-path guard passed, and
`git diff --check` passed. The private 51-query corpus was not rerun here;
the exact baseline accounting remains 22 append, 10 update, one delete, and
18 crosstab, with 51 lineage candidates expected after its next scan.
