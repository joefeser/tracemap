# Return one handoff for the two WebForms clicks

Use the 0.35.0 implementation pinned in verify-webforms-registered-member-post-once.md. If that dispatch already ran at the pinned implementation, reuse its public results and private index. Otherwise follow its public gate and existing authorization for at most one private static scan. If the private index is unavailable and a scan is not authorized, report that dependency and complete the public portion.

From the same index, work through the following questions in one session. Keep exact IDs, source spans, extractor versions, commit identities and queries in the private working notes. For the shareable answer use synthetic examples and stage labels.

1. Trace each click forward by supporting IDs through its partial handler, service call, alias and truncation gaps, and observed registration. Record which observations the bounded lookup retained.
2. Starting at that registration, trace its definition and any POST by supporting IDs. Independently check whether the configured mount and literal target identify a unique handler declaration. Starting at that declaration, inspect any already indexed backend call or SQL evidence relevant to the original trace. Record which relations have persisted joins and which come only from source inspection. Keep observations from other scans separate.
3. Continue from independently identifiable evidence on either side of a missing join. For each missing relation, inspect its extractor predicate and the local source shape. Record a minimal invented fixture, one near-miss that must stay unresolved, and the facts or gaps expected from each. Group failures caused by the same predicate. Mark boundaries that require runtime evidence or unavailable inputs.
4. Return one short Markdown handoff with the furthest ID-backed stage for each click, a table of the missing joins, and the smallest set of synthetic regressions that exercises them together. If available evidence cannot answer a question, state what input would change that result. Do not stop at the first failed predicate.

This is a read-only diagnosis. Keep private source, IDs, paths, query results and screenshots on the authorized machine. No source patches, extra scans, site execution, external transfers or private-worker runs.
