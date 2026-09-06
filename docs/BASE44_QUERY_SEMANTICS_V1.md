# Base44 normalized query evidence — issue #713

Extractor `base44-evidence/0.3.0` adds `properties.querySemanticsJson` to
filter/list/deleteMany query facts. The encoded descriptor is
`88mph.entity-query.v1`, independently derived with the TypeScript compiler
API. No reverse-engineer or host-produced artifact participates in extraction.

Each descriptor contains method, completeness, gaps and positional arguments:
filter/sort/limit/skip/fields for filter, sort/limit/skip/fields for list, and
filter for deleteMany. Supplied arguments carry zero-based UTF-16 source offsets
(end exclusive), bound by the enclosing fact's exact file hash. Assertions and
parentheses unwrap before span capture. Missing trailing arguments and explicit
null have different representations.

Direct object filters retain ordered field/predicate structure and explicit
$eq/$ne/$gt/$gte/$lt/$lte/$in/$nin/$exists/$regex operator objects. Runtime
operand text is excluded: only literal type classes, reference spans and bounded
arrays are retained. Implicit untyped references remain unresolved because they
may evaluate to operator objects. Explicit operator operands can retain a
runtime reference without claiming its type or value.

Literal sort fields retain order/direction. Literal selected fields retain
order and string/array encoding. Pagination retains nonnegative safe integer
control literals, including zero. Dynamic control values, shadowable undefined,
root bindings, spreads, logical compositions, computed/duplicate keys, unknown
operators, empty operator objects and unsupported operands produce typed gaps.
Unresolved descriptors also downgrade the enclosing shape's evidence tier.
Traversal is bounded to 512 nodes and depth 16.

This is a bounded direct-syntax slice, not completion of #713. Injected-client
coverage, binding/candidate resolution and other unresolved producer shapes are
still separate work. A future host can compare this descriptor against the
reverse-engineer's independent Babel output, but only after the reviewed
producer identity is admitted. The extractor never publishes compatibility,
SQL, migrations, or release decisions.

## Validation record

Followed `docs/VALIDATION.md` for the applicable TypeScript adapter checks:

- `npm run check --prefix src/typescript`: build and 94 tests pass, including
  sample/fixture semantic and syntax-fallback behavior, query evidence,
  relationships and the fact-ID recomputation regression.
- `TRACEMAP_SKIP_BUILD=1 TRACEMAP_OSS_SMOKE_REPOS=scip-typescript,axios-npm-lock scripts/smoke-open-source-repos.sh /tmp/query-oss-cache /tmp/query-oss-out`:
  both pinned TypeScript smoke scans complete with required artifacts and
  populated relationship tables. `scip-typescript` at
  `891eb4293709a6a587bf4468dfa1b45a85182fd9` emits 14,060 facts, 2,461 call
  edges, 281 object creations and 1,475 argument flows, with 33 analysis gaps.
  `axios-npm-lock` at `84a9f3b9a4f3244b8c8e818f557d64c7b964fb25` emits 11,186
  facts, 2,127 call edges, 146 object creations and 1,363 argument flows, with
  56 gaps. Both honestly report `Level1SemanticAnalysisReduced` and
  `FailedOrPartial`; smoke completion is not full semantic coverage.
- `python3 scripts/test_validate_adapter_artifacts.py`: seven tests pass.
- `scripts/check-private-paths.sh`: passes.
- A fresh source-bound ShopGenie Base44 scan is required for candidate evidence;
  this uses executable source without installing or running the app.

Local .NET build/tests, JVM tests, Python adapter install/tests and endpoint
smoke, Swift tests, and non-TypeScript OSS samples are explicitly deferred:
those implementations are unchanged by this Base44 TypeScript extractor slice.
The full cross-adapter combine/report/paths/reverse/export matrix is delegated
to Adapter Validation CI, whose initial candidate run passed all five adapters
and the combine job after one retry of an unchanged .NET restore-diagnostic
assertion. That earlier CI run is not evidence for a later head; current-head
CI remains a merge gate. No full-reader .NET memory or downstream reducer change
is claimed here because this change does not touch those paths.

Review hardening includes signed numeric operands, static no-substitution
backtick strings, transparent `satisfies` wrappers, a bounded iterative wrapper
walk, and including `querySemanticsJson` before fact identity is calculated.
Filter values remain excluded, and unresolved descriptors remain blocking.
