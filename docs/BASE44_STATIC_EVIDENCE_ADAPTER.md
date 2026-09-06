# Base44 Static Evidence Adapter

TraceMap's Base44 adapter emits source-bound static facts for consumption by a host migration system. It does not decide whether an application is migration-ready and it does not duplicate the host's capability registry, runtime manifest, migration completion record, requirements plan, or release ledger.

## Commands

```bash
tracemap-ts base44-evidence \
  --repo <accepted-source-directory> \
  --out <dedicated-output-directory> \
  --accepted-source-sha256 <64-hex-digest> \
  --accepted-tree-sha256 <64-hex-digest> \
  --coverage-label <operator-reviewed-label>

tracemap-ts base44-diff \
  --before <base44-evidence.json> \
  --after <base44-evidence.json> \
  --out <diff.json>
```

The evidence command also writes the normal `scan-manifest.json`, `facts.ndjson`, `index.sqlite`, `report.md`, and analyzer log. It adds stable JSON and Markdown packets plus a credential-free static HTML explorer. Artifact SHA-256 values bind the normal scan outputs. The accepted source and normalized tree digests are supplied by the authority that inspected the packet; TraceMap validates their shape and binds them without claiming to have established packet trust.

## Facts

The additive JavaScript/JSX/TypeScript/TSX `base44.*.v1` rules cover:

- requested SDK imports and statically visible SDK primitives;
- auth, entity, function, integration, Analytics, and AppLogs calls;
- statically named frontend function invocation and backend function surfaces;
- mutation payload shapes for create, update, upsert, and bulk operations, including
  statically visible fields, presence class, expression type class, bounded local
  binding derivation, object spreads, and typed analysis gaps;
- query shapes for filter, sort, and select arguments without retaining literal
  values;
- static and dynamic environment access;
- hashed provider origins and dynamic HTTP targets;
- SQL migration surface hashes and statement kinds; and
- customer-authored function/entity boundaries.

Facts contain repository and commit identity, rule, evidence tier, extractor identity/version, relative path, a first-class line span, source-file digest, and snippet digest. Each statically derived field also carries the contributing syntax's line span and snippet digest so identifier, spread, and mutation evidence remains independently inspectable even when it is defined away from the SDK callsite. They never store source snippets, environment values, URL paths/query strings, tokens, or cookies.

`base44.entity.payload.v1` and `base44.entity.query.v1` are additive facts in
the v1 packet. Structured field, spread, binding, and gap collections are
deterministically encoded in string-valued JSON properties so the existing
fact/index wire contract remains stable. Payload facts are `Tier3SyntaxOrTextual`
only when the modeled shape is complete. Any unresolved binding, dynamic
computed property, unresolved spread, or unsupported construction marks that
fact `Tier4Unknown`; a consumer must not interpret omitted fields as absent.

Conditional presence propagates through every nested object spread. Payload
bindings that escape through another local alias, an object property, helper
arguments, or a method call retain observed fields but receive typed gaps:
`binding-alias-escape-unresolved`, `binding-call-escape-unresolved`, or
`binding-method-call-unresolved`. The extractor does not prove these uses are
pure, simulate their mutations, or reconstruct a general alias graph. The same
uses after an alias capture produce `post-capture-alias-mutation-unresolved`.
Parameters, catch bindings, destructured bindings, and later local declarations
shadow outer bindings; unsupported initializers remain unresolved rather than
borrowing unrelated outer fields. Destructuring emits
`destructured-binding-unresolved`.

Operation linkage includes source character offsets as well as line spans and
syntax hashes, so identical calls on one line remain distinct. Extractor
`base44-evidence/0.2.1` changes these derived IDs; regenerate both comparison
snapshots with the same extractor version rather than treating an ID migration
as a source change.

The extractor intentionally records only expression classes such as
`decimal-number-literal`, `binary-expression`, `call-expression`, and
`property-access`. Literal customer values are not retained. Binding and
property origins contain code identifiers needed to trace source flow, never
the values held by those identifiers. String-literal element-access segments
in origins are replaced by a non-value-bearing marker.

## Consumer contract

Consumers must:

1. Validate `schemaVersion`, both accepted SHA-256 identities, the scan commit, artifact digests, rule IDs, evidence tiers, and extractor identities.
2. Treat source mismatch, artifact mismatch, unsupported rule/extractor identity, and unacknowledged coverage reduction as blockers.
3. Compare facts with their canonical capability/runtime/migration/requirements/ledger authorities instead of turning this packet into a second registry.
4. Report contradictions and gaps separately. A static fact can corroborate a runtime claim; it cannot prove bundling, reachability, browser behavior, provider delivery, IAM/secret access, tenant isolation, or migration completion.
5. Treat missing facts as clean absence only within the declared coverage label and known-gap set. Reduced coverage must never improve a verdict.
6. Treat payload/query `completeness` and `analysisGapsJson` independently for
   every callsite. A complete sibling callsite does not close a partial or
   unresolved callsite.

The JSON Schema at `docs/contracts/base44-static-evidence.v1.schema.json` defines the wire shape. Additive packet fields are allowed, while the named identity and provenance fields are required.

The additive normalized query descriptor introduced in `base44-evidence/0.3.0`
is documented in [Base44 query semantics v1](BASE44_QUERY_SEMANTICS_V1.md).
It retains literal pagination controls but never filter operand values.

Extractor `base44-evidence/0.4.0` also follows an SDK client through a local
helper parameter when executable callsites prove the binding. The proof is
conservative: every directly resolved callsite for that parameter must supply
the same SDK-derived alias. Parameter names, JSDoc, type annotations, and prose
never create client authority. See
[Base44 injected-client dataflow](BASE44_INJECTED_CLIENT_DATAFLOW.md).
