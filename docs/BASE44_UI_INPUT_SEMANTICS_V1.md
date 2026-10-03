# Base44 UI input semantics v1

Extractor `base44-evidence/0.15.0` emits `Base44UiInputSemantics` facts under
rule `base44.ui-input-semantics.v1`. Each fact contains a string-valued
`uiSemanticsJson` property with schema version
`88mph.base44-ui-input-semantics.v1`.

The descriptor is source-bound widening and test-vector evidence. It is never
schema-narrowing authority. In particular:

- `required`, `min`, `max`, `pattern`, and `maxLength` are preserved under
  `validation` with `validationAuthority=test-validation-only`;
- select option literals are bounded, sorted representative smoke values with
  `optionAuthority=representative-only`, never enum authority;
- date and datetime classes describe values accepted by the UI, not a required
  database date/timestamp representation; and
- every descriptor sets
  `storageAuthority=widening-only-never-narrowing`.

## Descriptor shape

```json
{
  "schemaVersion": "88mph.base44-ui-input-semantics.v1",
  "controlKind": "input|select|textarea|component|submitted-value",
  "componentName": "input",
  "fieldBinding": "row.cost",
  "valueBinding": "row.cost",
  "submittedEntity": "MaterialItemPriceBreak",
  "submittedField": "price",
  "operationName": "create",
  "operationEvidenceId": "operation-...",
  "valueClass": "decimal",
  "reasons": [],
  "confidence": "high|medium|low",
  "correlationStatus": "proven|partial|unresolved",
  "unresolvedCorrelationReason": "",
  "representativeValues": [],
  "optionAuthority": "none",
  "validation": {},
  "validationAuthority": "test-validation-only",
  "storageAuthority": "widening-only-never-narrowing"
}
```

`valueClass` is closed to `string`, `integer`, `decimal`, `number`, `boolean`,
`date-string`, `datetime-string`, `array`, `object`, and `unknown`. Reasons are
source spans with hashes and one of `native-input-type`, `component-prop`,
`parse-cast-function`, `label-context-clue`, or
`submit-handler-propagation`.

## Correlation rules

The extractor admits an entity/field correlation only when the same
source-bounded component scope contains an already proven Base44 create,
update, or bulk-create operation and the control binding matches the payload
value path. Direct object fields, const payload objects, conditional payloads,
array literals, and direct `Array.map` object results are supported. A direct
payload root such as `create(formState)` can correlate a control bound to
`formState.field`.

If no exact target is proven, entity and field remain empty and
`unresolvedCorrelationReason` is either
`no-proven-submitted-payload-correlation` or
`multiple-submitted-payload-targets`. The latter never selects one candidate.
Payload expressions using `parseFloat`, `parseInt`, `Number`, `Boolean`, or
`String` also emit a `submitted-value` fact tied directly to the proven entity
operation. Numeric cost/price/rate-style field clues are medium-confidence
widening evidence only.

## Authority and limitations

Only executable `.js`, `.jsx`, `.ts`, and `.tsx` source is inspected. Type
declarations, JSON/JSONC, documentation, comments, generated outputs, tests,
fixtures, stories, specs, and mock paths do not establish UI semantics.
Runtime-created props, dynamic option values, computed payload fields,
cross-module handler flow, mutable payload roots, and ambiguous bindings remain
unresolved. Static correlation does not prove that a component rendered, a
handler ran, a request succeeded, or a database accepted the value.
