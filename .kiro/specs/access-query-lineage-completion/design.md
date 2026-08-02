# Design

`AccessQueryProjector` performs a bounded lexical projection over the already
read QueryDef text. It reuses the existing literal/comment masking,
dependency resolution, safe identities, and field lookup maps. Expressions and
pivot values are represented only by role-scoped hashes.

The existing `AccessQueryProjection` gains optional action and crosstab
lineage records. `AccessFactBuilder` emits one rule-backed candidate fact per
record. Existing query, dependency, and output facts remain unchanged.

The projector does not execute queries, inspect results, discover row-derived
pivot columns, or claim runtime reachability. Dynamic and ambiguous shapes are
explicit partial gaps.

## Representative accounting

The private handoff census is preserved as an acceptance accounting set:

| Kind | Baseline partial | Lineage projection |
| --- | ---: | --- |
| Append | 22 | action candidate for every member |
| Update | 10 | action candidate for every member |
| Delete | 1 | action candidate for every member |
| Crosstab | 18 | crosstab candidate for every member |
| Total | 51 | 51 accounted; unsupported shapes remain partial |
