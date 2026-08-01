# Access query lineage completion

## Scope

This slice implements issue #570 only. It projects deterministic, static
lineage for the representative action-query and crosstab shapes without
executing Access, reading rows, or persisting SQL/source text.

## Requirements

1. Action queries SHALL emit operation kind, resolved target identity when
   unique, target fields, source expression hashes, source field identities,
   predicates, parameter ordinals, and coverage.
2. Append, update, and delete shapes SHALL remain partial when a target,
   field correspondence, expression, parameter, or dynamic construct cannot
   be proved.
3. Crosstabs SHALL emit row-heading field identities, role-hashed aggregate,
   value, and pivot expressions, plus role-hashed static pivot columns.
4. Dynamic pivot values, ambiguous aliases, parameterized/dynamic SQL, and
   unsupported syntax SHALL produce explicit gaps and never be inferred.
5. New records SHALL preserve rule ID, evidence tier, commit, span, and
   extractor provenance through the existing fact builder.
6. Synthetic tests SHALL cover supported append, update, delete, crosstab,
   dynamic-pivot, and unresolved-target shapes.
7. The implementation SHALL not contain private object names, raw SQL,
   source paths, hashes, row values, or customer material.
