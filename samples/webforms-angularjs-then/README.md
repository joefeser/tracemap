# Click method assigned in a service callback

Scan this directory with `--webforms-iis-mount '/Portal/Desk=.'`. Two markup
clicks select a method assigned in a controller's service-call callback.
The master also refers to an opaque dynamically loaded script.

```sql
SELECT c.start_line AS click_line, h.file_path, h.start_line AS method_line,
       json_extract(g.properties_json, '$.gapKind') AS gap_kind
FROM facts h
JOIN facts c ON c.fact_id = json_extract(h.properties_json, '$.clickFactId')
JOIN facts g ON g.fact_type = 'AnalysisGap'
 AND instr(',' || json_extract(h.properties_json, '$.supportingFactIds') || ',',
           ',' || g.fact_id || ',') > 0
WHERE h.fact_type = 'WebFormsAngularPartialClickHandlerCandidate'
ORDER BY click_line, gap_kind;
```

Expected click lines are 4 and 5, each with method Scripts/panel.js:6 and
two gaps: AngularDynamicScriptSourceUnavailable and
AngularThenCallbackInvocationUnobserved. Both candidates share the same
callback fact at Scripts/panel.js:4–7. No ordinary handler or runtime
dispatch is expected.
