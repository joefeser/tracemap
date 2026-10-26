# Requirements — Phase 1: explicit result columns from one admitted table

Follows #912 (`fc14857a54044e8edfaea170dab3b01141c0842a`). Phase 2 (SqlDataAdapter.Fill to
DataTable) is a separate later branch and PR; nothing here starts it.

1. A separate opt-in DDL origin (`sqlserver-ddl-table-result-input/1`) admits, in one hashed
   manifest, bounded `CREATE TABLE` definitions and procedures. The existing parameter route
   (`sqlserver-ddl-input/1`) and constant result route (`sqlserver-ddl-result-input/1`) keep
   rejecting `CREATE TABLE` and every other whole-input failure exactly as before.
2. Identity is exact and manifest-bound: author-supplied server/database plus explicit two-part
   schema/object names. Table and procedure identities share one namespace; duplicate or
   case-only duplicate definitions anywhere in the input reject it. No short-name joins, no
   USE/connection inference, no migration ordering: the admitted files are one unordered set.
3. A procedure establishes a complete table-backed shape only when its whole body is one
   unconditional SELECT (optionally the only statement of one BEGIN/END) of direct column
   references with optional identifier output aliases from exactly one schema-qualified
   admitted table, optionally with one simple table alias used with exact binding. Every
   projected column must resolve exactly and uniquely against a supported table definition.
   Projection order and output names are retained, with the source column per output.
4. Missing tables or columns, wrong schema/database, unqualified, temporary, case-only or
   collation-dependent references, unsupported table definitions, expressions, SELECT *,
   joins, views, CTEs, subqueries, UNION, dynamic SQL, EXEC, conditional or multiple result
   sets and other clauses are explicit unknown shapes with Tier4 records and no columns.
   Constant SELECTs without FROM keep the existing constant-projection rules unchanged.
5. Existing C#/VB `sqlserver-reader/1` accesses are compared to these shapes through
   `sqlserver-result-bindings/1` and the existing comparison engine. No source extractor
   change. Mismatches only against complete admitted shapes. Types, nullability, rows,
   values, execution and runtime behavior are non-claims; table column types stay visible
   uncompared facets.
6. Distinct versioned contracts, rule, extractor and CLI route; older manifests, exports and
   bindings never reinterpret. Every derived artifact carries exact generator and bounded
   public-input SHA-256.
7. Independently authored expectations (tests and a new native smoke) cover normal names,
   output and table aliases, reordered projections, missing/renamed consumer names, invalid
   ordinals, missing tables/columns, wrong schema/database, duplicate/case ambiguity, stale
   inputs and each newly relevant unsupported grammar family, with deterministic repeats and
   wrong-origin/artifact refusals. Retained smokes keep 52-case 8/12/32 (JSON and DDL) and
   result JSON 36/10/36, DDL 36/10/58, 20 refusals; new cases have their own denominator.
8. Validation: focused lanes, implementation commit, frozen full Debug `-warnaserror`, then
   sequential retained and new smokes at the committed head; Python, private-path and
   whitespace guards; push; one Phase 2 continuation prompt. No PR, merge, SQL execution,
   containers, remote hosts or private inputs. Test wall-clock work stays deferred.
