# Requirements — Phase 3: SqlDataAdapter.Fill(DataSet) to first-table DataRow column reads
Follows Phase 2 (#914, merge `f0850183d`, implementation `f4f74f01c`). One bounded consumer path in
C# and VB. Named DataTables, `Field<T>`, reader-returning helpers, ORMs, DNN hydration, broader
provider wrappers, type/nullability comparison and later result sets are separate later slices.
1. Source: the Phase 2 command and adapter rules unchanged (fresh trusted `SqlCommand` local with
   constant stored-procedure intent, one fresh `SqlDataAdapter` tied to it, every #912/Phase 2
   refusal kept), one fresh `System.Data.DataSet` filled exactly once by `Fill(DataSet)` or
   `Fill(DataSet, constantSourceTable)`, and constant name/ordinal reads through rows of exactly
   the first table, selected by `set.Tables[0]` or by the exact constant name a fresh adapter
   with empty `TableMappings` and default `MissingMappingAction`/`MissingSchemaAction` creates
   (`"Table"`, or the constant source-table name). The accepted syntax matrix is authored in
   `design.md` before implementation; every other cell is unsupported.
2. Fail closed (unknown with a specific gap): later tables (`Tables[1]+`, other names), dynamic,
   negative, case-only and namespace selectors, `Tables.Count` other than the bounded guard,
   `Tables.Add`/other collection members, `Merge`, `ReadXml`, `Relations`, `EnforceConstraints`,
   `CaseSensitive` and every other DataSet member, typed or named DataSets, aliases,
   reassignment, escapes, helpers, repeated Fill, adapter options, table/row mutation and every
   control flow except bounded guards bound to the selected DataSet/table.
3. Evidence: a distinct sibling property `sqlServerDataSetState` (`sqlserver-dataset/1`) on every
   existing Fill DatabaseOperationCandidate; it never overloads `sqlServerDataTableState`,
   reader or parameter state. A distinct digest and bindings contract
   (`sqlserver-dataset-bindings/1`, `dataSetEvidenceSha256`); DataTable, reader and parameter
   bindings never substitute. C#/VB extractor versions bump; historical records stay.
4. Comparison: one separate route (`sqlserver-dataset-result`) compares the accesses against the
   JSON, constant DDL and table-backed DDL shapes, each admitted through its unchanged origin
   admission, with the Phase 2 column semantics (exact-case unique match, mismatch only against
   a complete shape; case-only, duplicate-renaming, dynamic, unknown and unbound stay unknown).
5. Tiers and claims: source tier preserved; no claim that Fill executes, that tables or rows
   exist, about later result sets, values, types or nullability. Every report is partial with
   no clean verdict.
6. Independent public synthetic C#/VB fixtures with expectations authored before running the
   extractor, wired into the normal test-project restore/build graph (the #914 regression must
   not recur), duplicated in tests and a new native smoke with its own denominator. Derived
   artifacts carry exact generator SHA-256 and privacy-projected bounded-input SHA-256.
7. Validation: focused lanes, implementation commit, frozen full Debug `-warnaserror`, then
   sequential committed-head retained smokes (52-case 8/12/32 ×2; result JSON 36/10/36, DDL
   36/10/58, 20 refusals; table per language 22/5/44, 24 refusals; DataTable per language
   54/12/88 over 148 cases, 22 refusals) and the new DataSet smoke; Python, private-path and
   whitespace guards; pinned C#/VB OSS smoke with honest reduced coverage. Push the branch.
   No PR, merge, SQL execution, containers, remote hosts, private or work-machine inputs.
