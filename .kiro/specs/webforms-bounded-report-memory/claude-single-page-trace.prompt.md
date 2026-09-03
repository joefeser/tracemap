# Claude task: verify one Web Forms event-to-database static trace

Execute this read-only diagnostic task on the work computer. Do not merely
return instructions. No shared chat history or filesystem is required. This is
the next step after the bounded coverage fixes, not a BRD task.

## Scope and privacy

Use one existing retained scan and matching local application source. Do not
rescan, rebuild, restore packages, install tools, attach a debugger, access a
database/network service, run application code, or modify either repository or
the retained scan. Do not run broad whole-index graph/report commands: earlier
large runs encountered memory pressure. Use bounded read-only SQLite queries.

Keep all private paths, repository/project names, symbols, markup/control names,
source text, SQL/procedure/table names, configuration, credentials, and raw
diagnostic messages on this computer. Do not upload artifacts. Return only
public TraceMap rule IDs, tiers, versions, scanner SHA, numeric counts, and the
closed statuses below. Use `page-1`, `event-1`, `handler-1`, `method-1`, etc. as
non-derived sequential aliases; do not return private hashes or identifiers.

No claim of event execution, runtime reachability, successful binding, branch
execution, database access, or complete application behavior is permitted.

## 1. Verify the retained run

Locate the newest matching completed workspace/accuracy summaries and retained
review output using the existing local configuration. Normal directory roots
are `C:\work\tracemap-summary` and `C:\work\tracemap-output`. Verify summary/index
correspondence through run identifiers, scan ID, source digest and scanner SHA;
do not combine different runs merely because their timestamps are close.
If the run cannot be selected unambiguously, ask the operator once for its path.

The scanner SHA must contain the bounded coverage fix:

```text
git merge-base --is-ancestor cd8dc053 <tracemapHead>
```

Use the scanner SHA from retained provenance, not the application commit shown
in the scan console. Require exit 0; do not fetch/rebuild if it fails. Confirm
the retained Web Forms extractor version is `legacy-webforms/0.7.0` (or verify
a later version against local history) and workspace failure counts remain zero.

Open `scan/index.sqlite` read-only using an installed tool, such as
`sqlite3 -readonly <local-index-path>`, then `PRAGMA query_only=ON`.
Inspect `PRAGMA table_info` for `facts`, `fact_symbols`, `symbol_occurrences`,
`symbol_relationships`, and `call_edges` before using their columns. Do not load
all NDJSON or all edges into memory. Missing tools/schema/provenance are explicit
blocking results, not permission to start a fresh scan.

Before reading source to explain retained evidence, compare the application
commit and current checkout state to the retained snapshot. Changed/untracked
or ignored files relevant to a sampled span must be checked separately; a clean
tracked git status alone does not verify generated files. Where snippet hashes
are available, use the repository's documented hash method. Mark unverifiable
source `source-unverified`, and do not treat it as the scanned content.

## 2. Select exactly one page and event before following the graph

If the operator already identified a priority page in this local task, use it.
Otherwise select the first inventoried `.aspx` page with an explicit server
event binding, ordered by repository-relative path using ordinal ordering.
Inspect at most ten candidate binding rows to make this selection. If no page
can be selected within the bound, report `selection-incomplete`; do not broaden
the query or manufacture a representative page.

On that page choose the first explicit static server-event binding ordered by
line, then fact ID. Prefer neither a known-successful chain nor a database-related
name. Exclude lifecycle auto-wireup and client-only `OnClient` attributes from
this test. Record the selection method, then keep this event even if it fails.
One deterministic example is not statistically representative of every page.

Use the exact page evidence path to query its `WebFormsEventBindingDeclared`
facts. Join handler facts by **binding fact ID**, not handler name:

```sql
SELECT h.fact_id, h.rule_id, h.evidence_tier, h.target_symbol,
       h.file_path, h.start_line, h.end_line, h.properties_json
FROM facts h
WHERE h.fact_type = 'WebFormsHandlerResolved'
  AND json_extract(h.properties_json, '$.bindingFactId') = :binding_fact_id
ORDER BY h.fact_id
LIMIT 3;
```

Bind parameters with the local SQLite tool; never interpolate private source
text into shell commands. Query output here is local-only. Zero matches is
`missing`, one is a candidate to inspect, and multiple distinct handler
identities are `ambiguous`. Do not select an arbitrary handler. Confirm page,
linked code file, exact method span and supporting IDs all agree. Structural or
syntax linkage must retain its lower tier even if subsequent call edges are Tier1.

## 3. Follow bounded static calls to a database boundary

Start with the exact `handlerSymbolId` on the resolved handler. Inspect canonical
symbol relationships and their supporting source facts. Preserve assembly
identity, direction, rule, evidence tier, exact span and supporting fact ID at
each hop. Use `fact_symbols`/`symbol_occurrences` to bridge canonical IDs to call
facts; do not join different assemblies by display name or short method name.
Inspect the local writer implementation if relationship kinds or symbol roles
are unclear. Display-name matches alone are not connected edges.

Traversal bounds: maximum **6 call hops, 50 unique symbols, 100 edge rows total,
and 10 terminal candidates**. Track visited canonical IDs to stop cycles. Fetch
at most the remaining row budget plus one (to detect truncation), order
deterministically, and stop with `bounded-incomplete` when any bound is exceeded.
No unbounded recursive CTE or full graph materialization. Keep unrelated outgoing
branches visible as omitted/unsupported counts rather than silently dropping them.

For methods reached through actual edges, inspect retained database operation,
query or stored-procedure facts. An existing `WebFormsEventFlowProjected` fact
may help locate evidence, but verify its supporting facts; it is not independent
proof of a complete chain. Attribute a terminal only through explicit supporting
IDs/canonical method identity or verified containment in the **same exact method
span and source snapshot**. Same file, same procedure string, or nearby line
alone is insufficient. Mark containment-based attribution separately from an
explicit graph edge and retain its limitations.

Classify the terminal, if supported, as `stored-procedure-candidate`,
`inline-sql-candidate`, `database-operation-only`, `external-service-boundary`,
`dynamic-boundary`, or `no-terminal-within-bounds`. A database-operation fact
without procedure evidence must not become a stored-procedure claim. No terminal
found within these bounds does not prove the handler has no database effects.

If a hop is missing, inspect a small local source window (+/- 10 lines first)
around the last supported method/call. Stop at the first unsupported hop and
classify it as `missing-handler`, `ambiguous-handler`, `missing-call-edge`,
`ambiguous-call-identity`, `missing-database-attribution`, `dynamic-boundary`,
`external-service-boundary`, `source-unverified`, or `bounded-incomplete`.
Do not fill a missing TraceMap hop using manual reading and label it extracted.
Manual observations must be reported separately and cannot upgrade evidence tiers.

## 4. Return a short sanitized result

Return these sections, without private identifiers, source or raw SQL:

1. `result=static-trace-supported`, `result=static-trace-partial`, or
   `result=blocked`; scanner SHA, extractor versions and workspace counts.
   `static-trace-supported` requires an evidenced selected-event-to-database
   path, not just a database call elsewhere. Other cases remain partial/blocked.
2. Selection: `operator-priority` or `deterministic-first-explicit-event`.
3. A table: sequential hop alias, evidence family, public rule ID, tier,
   connection basis (`explicit-support-id`, `canonical-symbol-edge`, or
   `verified-method-containment`), and `supported`/`missing`/`ambiguous` status.
4. Traversal counts: hops, symbols, edges, terminals, whether a bound was hit,
   and whether any source was unverified. Report the weakest tier on the
   connected path; Tier1 downstream edges do not upgrade its structural start.
5. Terminal category or first unsupported-hop category. Separate any manual
   local observation from what the retained TraceMap facts demonstrate.
6. One next action: `review-another-page`, `add-bounded-extractor-regression`,
   `investigate-identity-join`, `review-dynamic-boundary`, or
   `needs-local-evidence`. Do not implement it or write a BRD.

Finish with: `nonClaim=static-evidence-only-runtime-behavior-unproven`.
