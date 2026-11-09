# Quoted server setting in a master-page script

Scan this directory with `--webforms-iis-mount '/Portal/Desk=.'`.
The content page selects Panel.save in Scripts/panel.js. The master also
creates a script element whose URL contains a server settings expression.
The setting and the contents of that script are unknown. TraceMap retains
the external method found in the included sources as a partial candidate.
Use the gap ID to see why the complete click lookup remains unresolved.

```sql
SELECT h.file_path, h.start_line, c.file_path, c.start_line,
       json_extract(g.properties_json, '$.gapKind') AS gap_kind
FROM facts h
JOIN facts c ON c.fact_id = json_extract(h.properties_json, '$.clickFactId')
JOIN facts g ON g.fact_type = 'AnalysisGap'
 AND instr(',' || json_extract(h.properties_json, '$.supportingFactIds') || ',',
           ',' || g.fact_id || ',') > 0
WHERE h.fact_type = 'WebFormsAngularPartialClickHandlerCandidate';
```

Expect one row: Scripts/panel.js line 4, Page.aspx line 3, and
AngularDynamicScriptSourceUnavailable. No ordinary handler is emitted.
