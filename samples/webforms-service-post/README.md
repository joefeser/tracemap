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

The external adapter has four unresolved providers. Its calls at lines 2 and 3
use `this` with an absent or computed token. Line 4 uses a member receiver
and an identifier token. Line 5 uses `this` with a property read as its token.
Each endpoint carries all four `unboundProviderGapIds`.
The application registration has separate module and factory evidence.

The service registration spans lines 3–8. Its moduleContinuations property
records the four following calls at lines 9–12 in source order. The POST's
source line stays 5.

To inspect the POST inside the registered service by source evidence alone,
follow the exact supporting IDs. Both clicks share one definition and POST.

```sql
SELECT reg.file_path, reg.start_line, member.start_line, post.start_line
FROM facts post
JOIN facts member ON member.fact_id = json_extract(post.properties_json, '$.supportingFactIds')
JOIN facts reg ON reg.fact_id = json_extract(member.properties_json, '$.supportingFactIds')
WHERE post.fact_type = 'WebFormsAngularServiceMemberPostCandidate'
  AND member.fact_type = 'WebFormsAngularServiceMemberDeclaredCandidate'
  AND reg.fact_type = 'WebFormsAngularServiceRegistrationCandidate';
```

Expected row: Scripts/services.js, 3, 4, 5. These syntax facts can also
be retained when a provider gap prevents selection for a click.

For each declaration-side POST, follow its independent mapped handler edge:

```sql
SELECT post.file_path, post.start_line, decl.file_path, decl.start_line
FROM facts edge
JOIN facts post ON post.fact_id = json_extract(edge.properties_json, '$.requestFactId')
JOIN facts decl ON decl.fact_id = json_extract(edge.properties_json, '$.declarationFactId')
WHERE edge.fact_type = 'WebFormsAngularServiceMemberPostHandlerCandidate';
```

The fixture yields one row from Scripts/services.js line 5 to Api/Save.ashx
line 1. The edge has no click ID. A mismatched mount or missing declaration
leaves a gap supported by the POST.
