# Public compiled-wrapper SQL evidence

This is a finite compile-only corpus and a **partial** native evidence addition,
not completed SQL-A acceptance. No SQL, provider methods, containers or private
application inputs execute. Source fixtures are not proof of deployed binaries.

## Denominator and implementation

Two separate assemblies per language: a C#/VB caller and wrapper, built against
.NET Framework 4.8 reference assemblies. Ten independently authored entry cases
per language cover direct local construction, incoming parameters, custom field
and property forwarding, mutation, escape, branches and three DataSet Merge cases.
The spec's design matrix was committed before extractor implementation.

`dotnet.compiled.il-sql-shape.v1` emits a new sibling
`ManagedIlSqlShapeCandidate` at **Tier3SyntaxOrTextual**. It admits only a fresh
local command/adapter, a constant-name/type/size SqlParameter constructor, exact
typed attachment, one Fill into a fresh DataSet and that allocation's single
return operand. The constructor, attachment, Fill and body retain exact native
fact links and generator/bounded-input/binary hashes. Names remain UTF-16 hashes.
Encoded framework names are not authentication or runtime semantics.

The IL extractor is `0.2.1`; source C#/VB extractors remain `0.22.23`/`0.10.26`.
Legacy command facts, parameter-unavailable gaps and comparison contracts remain
unchanged. No existing source SQL bindings accept the new candidate as a match.
Bounds: 256 calls/body, 16 parameters/body, independent aggregate derivation work
equal to the existing IL policy limit. Unsupported control flow, effects, scope,
incoming parameters and returns produce unknowns. Generic unmodeled-call effects
remain visible even on the admitted encoded shape.

## Reproduction

Build the normal test project (it builds both compile-only fixture pairs), then:

```sh
dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj -c Debug -warnaserror --filter FullyQualifiedName~IlSqlShapeExtractorTests
python3 -m unittest discover -s scripts/validation -p test_sql_compiled_wrapper_smoke.py
python3 scripts/validation/sql-compiled-wrapper-smoke.py --cli src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll --out /tmp/public-compiled-wrapper-new-run
```

The output directory must not exist. The native scans and local-only matrix retain
input and generator hashes. Expected: **20 entry cases, two native local shapes,
20 whole-path unknowns**. A shape reached by multiple entries is not counted as
multiple successful bindings. Whole-path status cannot become clean merely because
the smoke's expected unknowns reproduce.

## Finite remaining requirements (not optional SQL expansion)

1. Constructor-to-field/property forwarding needs actual compiled heap-store/load
   evidence and exact caller/callee identity, not a wrapper-name exemption.
2. Incoming parameter and returned DataSet substitution need provenance-bound
   interprocedural object identity and effect/alias controls. Current local shape
   evidence does not link a caller consumer to an admitted SQL result contract.
3. Merge into a field-held destination requires conservative schema union,
   pre-existing destination state and mutation/escape evidence. A freshly allocated
   destination alone does not make it identical to the incoming result.
4. Actual schema/server/procedure identity and supported SQL contract admission
   remain external acceptance inputs. General joins, ORM expansion and runtime
   execution are not implemented by this package.

These are explicitly unimplemented legs, not requested work-machine retries.
Do not ask the operator to rerun this corpus to resolve them.

## Validation record

Development: both fixture pairs built without warnings. Initial four tests failed
because the prototype rejected the generic call-effects gap; the implementation
now preserves it while admitting only recognized encoded calls. The next four
passed. Existing IL lane: 172 passed, one existing Windows-only skip. A later native
test build correctly failed `-warnaserror` on xUnit2031 despite ten passing tests;
the assertion was repaired, then 12 focused tests passed. The development native
replay produced the exact 20/2/20 denominator. Final frozen gates pending.
