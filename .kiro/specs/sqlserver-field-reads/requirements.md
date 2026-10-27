# Requirements — Phase 6: `Field<T>` / `Field(Of T)` column-identity reads on bounded DataRow consumers
Follows Phase 5 (#918, merge `7e96bca84`, branch `codex/sqlserver-internal-helpers`). Exactly one
widening in C# and VB: `row.Field<T>("Name")` / `row.Field<T>(ordinal)` (VB
`row.Field(Of T)("Name")` / `row.Field(Of T)(ordinal)`) read as a column name/ordinal access on
DataRows that the unchanged Phase 2-5 rules already bound. Every other consumer, helper and
command rule is unchanged.
1. Source: the unchanged Phase 2 (DataTable), Phase 3 (DataSet), Phase 4 (private helper) and
   Phase 5 (internal helper) row-binding rules, with `DataRowExtensions.Field<T>(DataRow,
   string)` and `Field<T>(DataRow, int)` admitted as accesses: the C# extension form and the VB
   reduced form only, classified by `(method.ReducedFrom ?? method).ContainingType`, with the
   declaring assembly identity checked. The accepted syntax matrix is authored in `design.md`
   before implementation; every other cell is unsupported.
2. The access record keeps the name or ordinal and records the generic type argument as a
   bounded rendering that is never compared: no CLR/SQL type, conversion, nullability or
   `DBNull` claim.
3. Fail closed with typed gaps: `Field<T>(DataColumn)`, every `DataRowVersion` overload,
   `SetField` (unchanged `datatable-mutation`), `AsEnumerable()` / LINQ pipelines (unchanged
   `ambiguous-row-ownership`), the explicit static call form, `Field` on rows of tables that the
   rules did not bind, Field reads inside helper bodies, unrenderable type arguments and
   declaring-assembly identity mismatches.
4. Evidence: a distinct sibling property `sqlServerFieldReadState` (`sqlserver-field-read/1`) on
   Fill DatabaseOperationCandidate unless a bounded compilation-wide token check proves no
   possible Field read exists. Unknown/oversized scopes retain analysis. The Phase 2-5 properties, refusals, outputs and
   denominators stay unchanged for every input (with or without `Field<T>`): Field reads remain
   `datarow-field-extension-unsupported` there. Distinct bindings `sqlserver-field-read-bindings/1`
   bind one `(factId, siteIndex)` each; DataTable, DataSet, helper, internal-helper, reader and
   parameter bindings or digests never substitute. C#/VB extractor versions bump; historical
   records stay unknown.
5. Comparison: one separate route (`sqlserver-field-read`) over JSON, constant DDL and table-backed
   DDL shapes, each admitted by its unchanged origin route, with the shared Phase 2 column
   decision. A site whose rows carry no Field read is `field-read-absent` here and stays the
   evidence of its own Phase 2-5 route, so no access is compared twice.
6. Claims: source tier preserved; no claim that Fill or helpers execute, that tables or rows exist,
   about later result sets, values, types, conversions or nullability. Reports are partial with
   no clean verdict.
7. Independent public synthetic multi-document C#/VB fixtures with expectations authored before
   running the extractor, wired into `TraceMap.Tests.csproj` as `ReferenceOutputAssembly="false"`
   references and verified with no pre-existing fixture `bin/`/`obj/`; the same expectations in
   tests and a new native smoke with its own denominator. Derived artifacts carry exact generator
   SHA-256 and bounded-input SHA-256.
8. Live matrix: extend the existing disposable linked static/runtime matrix with Field reads on
   DataTable, DataSet and a cross-document internal helper under the unchanged isolation, pinned
   image, limits, transient credentials, ownership-checked cleanup and failed-run evidence; the
   retained 22 cases keep their outcomes.
9. Validation: focused lanes, implementation commit, frozen full Debug `-warnaserror`, then
   sequential committed-head retained smokes with unchanged denominators (52-case 8/12/32 ×2;
   result JSON 36/10/36, DDL 36/10/58, 20 refusals; table per language 22/5/44, 24 refusals;
   DataTable per language 54/12/88 over 148 cases, 22 refusals; DataSet per language 78/12/88
   over 169 cases, 24 refusals; helper per language 42/15/58 over 115 cases, 24 refusals;
   internal-helper per language 36/12/30 over 78 cases, 24 refusals), the new smoke, Python
   guards, private-path and whitespace guards, the pinned C#/VB OSS smoke with honest reduced
   coverage, and the extended linked live harness on the reviewed disposable context. Push the
   branch. No PR, merge, ACK-policy change or paid review.
