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

Validation: `npm run check --prefix src/typescript` and a fresh source-bound
ShopGenie scan. The .NET implementation is unchanged; its suite is explicitly
deferred for this TypeScript-only extractor change.
