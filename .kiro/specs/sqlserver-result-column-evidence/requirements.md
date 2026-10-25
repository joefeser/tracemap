# Requirements

Add a bounded, offline, public-synthetic comparison from a compiler-resolved C# or
VB SqlCommand/SqlDataReader consumer to one admitted first-result-set column shape,
after #911 (`730610582660fe32354479b35cf80eb2aa954a62`). Answer only: within this
explicit static subset, does the selected reader's column-name or ordinal access
agree with the declared projection? Never produce an aggregate clean verdict.

1. Independent synthetic result declarations: versioned public JSON with exact
   logical repo/revision/server/database/schema/procedure identity, ordered column
   names, explicit `complete`/`unknown` state, source locations, rule/tier/version
   and exact generator / bounded-input SHA-256. Not compiler, catalog, runtime or
   deployed evidence.
2. Separate opt-in DDL result-shape origin: one procedure body of exactly one
   unconditional SELECT of constants or explicit CAST constants, each with an
   explicit unique alias, no FROM, at most one simple BEGIN/END wrapper. The whole
   body is inspected; anything else is an explicit unknown shape, never a partial
   list labeled complete. Malformed SQL, identity ambiguity, SQLCMD, digest and
   bound failures still reject the whole input. Parameter admission is unchanged.
3. Source evidence in C# and VB: fresh resolved SqlCommand local, constant
   StoredProcedure intent and command text, reader local directly assigned from
   that command's ExecuteReader(); constant name indexers, constant ordinal getters,
   GetOrdinal(constant) used directly or via a fresh unreassigned ordinal local;
   simple using lifetime; a single Read() guard/loop. Alias, reassignment, escape,
   helpers, NextResult and other control flow stay typed unknown. Every access is
   bound to its exact execution fact and reader instance.
4. Comparison of column identity/existence and ordinal bounds only. Exact-case
   unique name = bounded match; case-only, duplicate, dynamic, incomplete/unknown
   shape or ambiguous binding = unknown; missing name / out-of-range ordinal =
   mismatch only against an admitted complete shape. Source and DDL spans and
   distinct tiers in JSON and Markdown. No CLR/SQL type, nullability, cardinality,
   value, execution or runtime-exception claim. Type facets stay visible, uncompared.
5. Distinct versioned contracts and CLI routes; the parameter-only `/1` contracts,
   JSON and DDL parameter origins, receipts and 52-case 8/12/32 smokes are unchanged
   and older receipts are never reinterpreted as result-shape evidence.
6. Small public synthetic corpus with an independently authored expected/observed
   matrix and documented denominator; missing cases are explicit gaps. Unknowns
   survive into native JSON and Markdown. Derived artifacts carry exact generator
   and privacy-projected bounded-input SHA-256.
7. Validation: focused tests and Python guards, implementation commit, frozen full
   Debug `-warnaserror`, then sequential JSON, DDL and new result smokes at the
   committed head; privacy/whitespace guards; push branch and one follow-up prompt.
   No PR, merge, SQL execution, containers, remote hosts or private inputs.
