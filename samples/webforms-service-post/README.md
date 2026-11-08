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

The service registration is the terminal call after a literal factory
registration. Its supporting IDs include the factory at Scripts/services.js:2,
which in turn references the external module declaration. The service site
starts at its own property on line 3.

An external adapter also calls `this['service'](selectFeature(), ...)`.
For each click the expected endpoint carries an unboundProviderGapIds entry.
Follow it to the adapter's line 2 and verify that the member supports the
same gap. The adapter cannot establish the module, service token or endpoint.

For 0.28.0 the fixture uses bracket notation. The retained library gap should
report providerMember `service`, ownerFailure `unsupported-receiver` and
factoryCount `0` while both click candidates keep the same app registration.
