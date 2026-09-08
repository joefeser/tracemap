# Base44 entity payload shape v3 — issue #713

Extractor `base44-evidence/0.14.0` emits payload `shapeVersion=3`. It preserves
the v2 outer-kind, reference-accounting, and runtime-obligation contract and
preserves `fieldsJson` as the complete syntactic observation list. It adds a
separate `semanticFieldsJson` projection so a consumer never has to reinterpret
an expression-class label as a runtime value type.

Each semantic field is unique by name and has this closed form:

```json
{
  "name": "status",
  "semanticPresence": "always | conditional | unknown",
  "valueType": "string | number | integer | decimal | boolean | date | object | array | uuid | unknown",
  "explicitNull": false,
  "provenance": [
    {
      "name": "status",
      "presence": "unconditional",
      "expressionType": "string-literal",
      "origin": "literal",
      "evidenceStartLine": 1,
      "evidenceEndLine": 1,
      "evidenceStartOffset": 0,
      "evidenceEndOffset": 12,
      "evidenceFilePath": "src/file.ts",
      "evidenceSourceFileSha256": "<sha256>",
      "evidenceSnippetHash": "<sha256>",
      "semanticValueType": "string",
      "semanticExplicitNull": false
    }
  ]
}
```

Provenance is the exact, deterministic `fieldsJson` occurrence set grouped by
field name. Source path/hash plus offsets distinguish identical syntax on the
same line. Flattening every semantic field's provenance after removing its two
semantic-observation members must reproduce `fieldsJson` exactly once. No
alternative source occurrence is discarded.

Presence aggregation is fail closed: any unknown observation produces
`unknown`; otherwise all `always` observations produce `always`; every other
combination produces `conditional`. A semantic value type is retained only
when every alternative proves the same type and explicit-null state. A type or
null-state conflict produces `unknown`. `explicitNull` remains true when any
alternative explicitly supplies null. Null alone is represented as
`valueType=unknown, explicitNull=true`.

The extractor proves only direct executable syntax: string/template, numeric,
boolean, object, and array constructions; numeric operators and unshadowed
primitive coercions; conditional/logical alternatives; and explicit null.
Array `map`/`filter`/`push` projection is admitted only when the complete scanned
project realm contains no mutation or escape of those intrinsic methods. Any
unshadowed prototype mutation/reflection capability or
`__proto__`/`constructor.prototype` access conservatively disables array-derived
completeness; an unrelated ordinary-object property such as `adapter.map` does
not. Dynamic `eval`/`Function` also disables it unless the evaluated expression
uses a direct, unshadowed evaluator, a source-normalized immutable string
binding, and a pristine literal regular-expression guard dominated by an exact
terminating allowlist whose grammar contains only digits, arithmetic operators,
parentheses, decimal points, and spaces. Aliased, escaped, member-accessed, or
otherwise indirect evaluators remain unsafe.
Statically unreachable mutations after a terminating statement, including a
terminating `try`/`catch`/`finally`, contribute no field evidence.
Identifiers, arbitrary calls, properties, or conflicting alternatives remain
`unknown`. It does not infer UUID or date from names, documentation, schema
prose, example values, or formatting. A consumer may promote a typed column
only after independent producers agree across the required executable
callsites with no unknown or conflict.

Packet validation recomputes presence, value type, and explicit-null aggregates
from the closed per-occurrence semantic observations. It rejects missing/open
semantic objects, unsupported enums, duplicate names, unauthenticated source
provenance, contradictory aggregates, reordered projections, or any projection
that loses or duplicates a syntactic occurrence. Shape v2 remains readable but
cannot authorize semantic type promotion. `Object(...)` coercion and numeric
operators with unproven JSON-number operands remain `unknown`; BigInt must never
be promoted as a JSON number.
