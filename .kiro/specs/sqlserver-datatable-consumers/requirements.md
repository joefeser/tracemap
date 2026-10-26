# Requirements — Phase 2: SqlDataAdapter.Fill to DataTable/DataRow column reads
Follows Phase 1 (#913, merge `4f664daf5`, implementation `c28dbd3df`). One bounded consumer path in
C# and VB. DataSet, reader-returning helpers, ORMs, DNN hydration, broader provider wrappers,
type/nullability comparison and multiple-result semantics are separate later slices.
1. Source: a fresh trusted `SqlCommand` local with constant stored-procedure intent (the #912
   command rules and every mutation, alias, nested-assignment, ref and control-flow protection,
   unchanged), one fresh `SqlDataAdapter` tied to that command, one fresh `DataTable` filled
   exactly once by `DbDataAdapter.Fill(DataTable)`, and constant name/ordinal reads through a
   directly associated `DataRow`. The accepted syntax matrix is authored in `design.md` before
   implementation; every other cell is unsupported and never counts as supported.
2. Fail closed (unknown with a specific gap): aliases, reassignment, ref/out escapes, helper
   boundaries, repeated Fill, adapter reuse, command mutation after adapter association,
   table/column/row mutation, `TableMappings`, `MissingSchemaAction`, `MissingMappingAction`,
   `AcceptChangesDuringFill`, `ContinueUpdateOnError` and other adapter options, ambiguous row
   ownership (`Select`, `DefaultView`, `Copy`, LINQ, other tables), switch/try/lambdas and
   every control flow the reader route rejects, except the bounded guards bound to the
   selected table (`Rows.Count > 0`, `foreach` over its `Rows`).
3. Evidence: a distinct versioned fact property `sqlServerDataTableState`
   (`sqlserver-datatable/1`) on the existing Fill DatabaseOperationCandidate; it never
   overloads `sqlServerReaderState` or parameter state. A distinct digest and bindings contract
   (`sqlserver-datatable-bindings/1`, `dataTableEvidenceSha256`); parameter and reader bindings
   never substitute. C#/VB extractor versions bump; historical validation records stay.
4. Comparison: one separate route compares DataTable accesses against the JSON
   (`sqlserver-result-schema`), constant DDL (`sqlserver-ddl-result`) and table-backed DDL
   (`sqlserver-ddl-table-result`) shapes, each admitted through its unchanged origin
   admission. Existing semantics: exact-case unique match; mismatch only against a complete
   shape; case-only, duplicate, dynamic, unknown and unbound stay unknown. DataTable runtime
   case-insensitive fallback is not claimed (case-only stays unknown); duplicate shape names
   that Fill would rename keep missing-name results unknown.
5. Tiers and claims: source tier preserved; structural correlation never upgrades it.
   No claim that Fill executes, rows exist, values convert or types/nullability agree.
   Failed semantic loading keeps its gaps. Every report is partial with no clean verdict.
6. Independent public synthetic C#/VB fixtures with expectations authored before running the
   extractor, duplicated in tests and a new native smoke with its own denominator. Every derived
   artifact carries exact generator SHA-256 and privacy-projected bounded-input SHA-256.
7. Validation: focused lanes, implementation commit, frozen full Debug `-warnaserror`, then
   sequential committed-head retained smokes (52-case 8/12/32 ×2; result JSON 36/10/36, DDL
   36/10/58, 20 refusals; table 64 cases/language 22/5/44, 24 refusals) and the new DataTable
   smoke; Python, private-path and whitespace guards; pinned C#/VB OSS smoke with honest reduced
   coverage. Push the branch. No PR, merge, SQL execution, containers, remote hosts, private or
   work-machine inputs. Test wall-clock restructuring stays deferred.
