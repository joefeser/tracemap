# AngularJS click method through a service POST

Both buttons select Panel.save. The controller's injected client calls submit
inside the selected function. The earlier list call installs the callback. A
second service exposes the same submit name. The explicitly mapped POST
points to Api/Save.ashx.

When validation is authorized, scan this sample using
`--webforms-iis-mount '/Portal/Desk=.'`. The following query follows exact IDs:

```sql
SELECT c.start_line AS click_line, h.fact_type AS handler_kind,
       call.file_path AS call_file, call.start_line AS call_line,
       req.file_path AS post_file, req.start_line AS post_line,
       decl.file_path AS endpoint, decl.start_line AS endpoint_line
FROM facts edge
JOIN facts req ON req.fact_id = json_extract(edge.properties_json, '$.requestFactId')
JOIN facts decl ON decl.fact_id = json_extract(edge.properties_json, '$.declarationFactId')
JOIN facts member ON member.fact_id = json_extract(req.properties_json, '$.supportingFactIds')
JOIN facts call ON call.fact_type = 'WebFormsAngularServiceCallCandidate'
 AND instr(',' || json_extract(member.properties_json, '$.supportingFactIds') || ',', ',' || call.fact_id || ',') > 0
JOIN facts h ON h.fact_id = json_extract(call.properties_json, '$.clickHandlerFactId')
JOIN facts c ON c.fact_id = json_extract(h.properties_json, '$.clickFactId')
WHERE edge.fact_type = 'WebFormsAngularPostHandlerCandidate'
ORDER BY c.start_line;
```

Expected: click lines 4 and 5, partial handlers, call Scripts/panel.js:6,
POST Scripts/services.js:5 and declaration Api/Save.ashx:1. The existing
dynamic-script and callback-invocation gaps stay attached to each partial
handler. This query describes source candidates and observed declarations.

The service registration follows a literal factory
registration and precedes filter, directive, config and run calls. Its supporting IDs include the factory at Scripts/services.js:2,
which in turn references the external module declaration. The service site
starts at its own property on line 3.

The external adapter has three unresolved providers. Its calls at lines 2 and 3
use `this` with an absent or computed token. Line 4 uses a member receiver
and an identifier token. Each endpoint carries all three `unboundProviderGapIds`.
The application registration has separate module and factory evidence.

The service registration spans lines 3–8. Its moduleContinuations property
records the four following calls at lines 9–12 in source order. The POST's
source line stays 5.
