# Requirements — Phase 4: one same-compilation static helper returning a filled DataTable/DataSet
Follows Phase 3 (#915, `b55b2d107`) and the linked disposable SQL Server provider matrix (#916,
`834d74fe8`). One bounded helper boundary in C# and VB. `Field<T>`, named tables/DataSets, later
result sets, reader-returning helpers, ORMs, DNN hydration, broader wrappers and type/nullability
comparison stay separate later slices.
1. Source: exactly one level. A non-virtual, non-generic, `private` static helper declared in the
   same document of the same compilation whose body is precisely one supported Phase 2
   (`Fill(DataTable)`) or Phase 3 (`Fill(DataSet[, constant])`) shape with the command text taken
   from one `string` parameter, returning the filled local; called from a consumer method with a
   constant procedure argument whose returned value initializes a fresh local that then follows
   the Phase 2/3 table, rows and row rules. The accepted syntax matrix is authored in `design.md`
   before implementation; every other cell is unsupported.
2. Fail closed with typed gaps: recursion, overloads (ambiguity), virtual/interface/extension/
   generic/async/instance helpers, non-private helpers (callers in other documents or
   compilations cannot be enumerated), partial types, `ref`/`out`/optional/params/extra
   parameters, named/nonconstant/null procedure arguments, multiple returns, any helper mutation,
   guard, second execution or unrelated call, method-group escapes of the helper, and every
   Phase 2/3 caller-side gap on the returned local.
3. Evidence: a distinct sibling property `sqlServerHelperResultState` (`sqlserver-helper-result/2`)
   on every existing Fill DatabaseOperationCandidate, carrying the helper execution once and one
   caller entry per call site (procedure argument hash, returned-consumer accesses). It never
   overloads DataTable, DataSet, reader or parameter state; those properties and every Phase 2/3
   output stay byte-for-byte unchanged in meaning. Distinct bindings
   (`sqlserver-helper-result-bindings/1`) bind one `(factId, callerIndex)` each; the digest covers
   the helper execution plus exactly that caller, so callers, procedures and other routes'
   bindings never substitute. C#/VB extractor versions bump; historical records stay.
4. Comparison: one separate route (`sqlserver-helper-result`) compares each bounded caller's
   accesses against the JSON, constant DDL and table-backed DDL shapes, each admitted by its
   unchanged origin route, with the shared Phase 2 column decision.
5. Tiers and claims: source tier preserved; no claim that the helper or Fill executes, that the
   enumerated callers are all callers at runtime (reflection is never claimed), that tables or
   rows exist, about later result sets, values, types or nullability. Reports are partial with
   no clean verdict.
6. Independent public synthetic C#/VB fixtures with expectations authored before running the
   extractor, wired into the normal test-project restore/build graph, duplicated in tests and a
   new native smoke with its own denominator. Regression tests cover multiple callers, different
   procedure arguments and binding substitution. Derived artifacts carry exact generator SHA-256
   and privacy-projected bounded-input SHA-256.
7. Linked runtime: representative C# and VB helper cases extend the existing disposable
   Microsoft.Data.SqlClient matrix; the same fixture methods are scanned and executed. Static
   predictions are authored first and runtime observations never promote static unknowns.
8. Validation: focused lanes, implementation commit, frozen full Debug `-warnaserror`, then
   sequential committed-head retained smokes (52-case 8/12/32 ×2; result JSON 36/10/36, DDL
   36/10/58, 20 refusals; table per language 22/5/44, 24 refusals; DataTable per language
   54/12/88 over 148 cases, 22 refusals; DataSet per language 78/12/88 over 169 cases, 24
   refusals), the new helper smoke, Python guards, private-path and whitespace guards, the pinned
   C#/VB OSS smoke with honest reduced coverage, and the linked live harness on the reviewed
   disposable context. Push the branch. No PR, merge, ACK-policy change or paid review.
