# VB SQL argument review

Scan this directory with the built CLI and a fresh output directory:

```sh
tracemap scan --repo samples/vb-sql-argument --out <new-output>
```

Read **VB SQL text passed to wrapper calls** in report.md. Expect exactly two
hits from Queries.vb. The UPDATE is assembled on lines 3–5 and passed on line
6. The inline SELECT is passed on line 10. Compare each with its source.
LogOnly, Replaced, Conditional and OtherArgument should produce no hits.

```sql
SELECT file_path, start_line, json_extract(properties_json, '$.sqlOperation'),
       json_extract(properties_json, '$.commandInstanceBinding'),
       json_extract(properties_json, '$.constructionStartLine'),
       json_extract(properties_json, '$.constructionEndLine')
FROM facts WHERE fact_type = 'SqlTextArgumentCandidate'
ORDER BY file_path, start_line;
```

The operation and binding fields are the two review signals. The binding
field describes the scope of this source rule. Check compiled command facts
separately for stronger evidence. The fixture contains invented data and
requires no database or application execution.

The gateway and logger are declared interfaces. The VB file can be compiled
in a class library with Option Strict On. The projectless scan exercises
source syntax. The fixture ends at the wrapper call and supplies no database
implementation.
