# VB application startup bundle discovery

Scan this directory as a projectless website. `Global.asax` declares two local
bundles and interleaves their member calls. `Site.master` renders both virtual
paths. The seven expected syntax candidates comprise two declarations, three
members and two render references.

Run this query against the emitted SQLite index to check local ownership and
source locations.

```sql
SELECT d.target_symbol AS declared_bundle, m.target_symbol AS declared_member,
       json_extract(m.properties_json, '$.declaredOrdinal') AS ordinal,
       m.file_path, m.start_line
FROM facts m JOIN facts d
  ON d.fact_id = json_extract(m.properties_json, '$.bundleDeclarationFactId')
WHERE m.fact_type = 'WebFormsScriptBundleMemberCandidate'
ORDER BY m.start_line;
```

| Bundle | Member | Ordinal | ASAX line |
| --- | --- | --- | --- |
| ~/bundles/first | ~/Scripts/a.js | 0 | 7 |
| ~/bundles/second | ~/Scripts/b.js | 0 | 8 |
| ~/bundles/first | ~/Scripts/c.js | 1 | 9 |

Each row describes a literal include on the identified declaration. Startup
execution, application-scoped render resolution, loaded script order and HTTP
execution require separate evidence.
