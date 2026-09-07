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
      "evidenceSnippetHash": "<sha256>"
    }
  ]
}
```

Provenance is the exact, deterministic `fieldsJson` observation set grouped by
field name. Flattening every semantic field's provenance must reproduce
`fieldsJson` exactly once. No alternative source observation is discarded.

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
Identifiers, arbitrary calls, properties, or conflicting alternatives remain
`unknown`. It does not infer UUID or date from names, documentation, schema
prose, example values, or formatting. A consumer may promote a typed column
only after independent producers agree across the required executable
callsites with no unknown or conflict.

Packet validation rejects missing/open semantic objects, unsupported enums,
duplicate names, empty/cross-field provenance, reordered projections, or any
projection that loses or duplicates a syntactic observation. Shape v2 remains
readable but cannot authorize semantic type promotion.
