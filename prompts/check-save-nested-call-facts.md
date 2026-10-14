# Resolve the one missing nested call from existing evidence

Five-minute TOTAL limit. Read-only. No build, test, scan, combine, handler
requery, DLL search, context change, application execution, or database access
other than read-only queries of the existing local TraceMap SQLite index.
Do not rebuild the tool or change SDK versions. Do not edit original artifacts.

Local synthetic tests now pass for a concrete field calling a same-named
protected nested-class method, including a missing base class, an inherited
framework receiver, and an unrelated sibling DB operation. They do not prove
the private run contains the corresponding call facts. Do not assume a nested
identity bug or connect the unrelated bulk-write method to Save.

Use the already identified M3 outer wrapper, M4 nested method, exact source file
and call-site line from the previous source-witness inspection. Open the SAME
completed run's combined.sqlite read-only, using an already available SQLite
client. Do not install tools. Preserve source-index identity throughout.

Run this parameterized SELECT, binding the known private file and the small line
range covering the M3 declaration/body. Keep private values and output local.
Use column names exactly as below; never guess a replacement database.

```sql
SELECT combined_fact_id, source_index_id, fact_type, rule_id, evidence_tier,
       source_symbol, target_symbol, start_line, end_line,
       extractor_id, extractor_version, properties_json
FROM combined_facts
WHERE file_path = $file AND start_line BETWEEN $first AND $last
ORDER BY start_line, fact_type, combined_fact_id
LIMIT 101;
```

Repeat for M4's declaration and first body call. If 101 rows are returned,
narrow to MethodDeclared, CallEdge, MethodInvoked, PropertyAccessed,
InvocationName and AnalysisGap; disclose any remaining truncation. Do not
search the entire corpus or dump payload_json/source snippets.

Compare exact identities:
- Is M3's Tier1 MethodDeclared target identical to the source of its call fact?
- Does the invocation site have a Tier1 CallEdge/MethodInvoked, only fallback,
  a property-access fact, a gap, or no retained fact?
- If a call fact exists, is its target identical to M4's declaration target
  within the SAME source_index_id? Include receiver/dispatch evidence where
  present; do not normalize names or infer joins.
- Does the diagnostic graph contain those exact fact IDs and node identities?
  Check existing artifacts only; do not regenerate them.

Only if the call fact is absent from combined.sqlite, inspect the corresponding
original source index read-only at that same file/line to distinguish combine
loss from source extraction absence. Its table is facts; omit combined_fact_id
and source_index_id from the SELECT. Check the schema first. If the source
index is unavailable, report that distinction as unresolved and stop.

Return a short sanitized table for M3 declaration, M3 call site and M4
declaration: present/absent, tier, rule, extractor version, exact caller/target
identity match yes/no, and diagnostic inclusion yes/no. Use M3/M4 labels rather
than private symbols or hashes. State the first layer where evidence is lost,
or explicitly state which comparison could not be made. Include elapsed time.

Do not call ExecProc_Scalar a proven DB terminal based on its spelling. Its
implementation was not inspected. The two retained standard DB operations
belong to the other caller chain and are not this task's target.
