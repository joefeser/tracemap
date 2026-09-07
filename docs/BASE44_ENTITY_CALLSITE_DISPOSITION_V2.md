# Base44 entity callsite disposition v2

Extractor `base44-evidence/0.14.0` may replace an entity operation row with a
`Base44EntityCallsiteDisposition` fact only when executable source proves the
SDK call cannot be reached from the rooted application module graph or from a
real React Query mutation handle. The v1 shape is revoked because it discarded
the entity selector, SDK identity, primitive, and suppressed operation identity.

The closed v2 `callsiteDispositionJson` binds:

```json
{
  "schemaVersion": "88mph.base44-entity-callsite-disposition.v2",
  "disposition": "dormant-unreachable",
  "callableName": "exportedHelper | mutationHandle.mutationFn",
  "authorityPath": "repo/relative/path.ts",
  "authoritySha256": "<sha256>",
  "sourceSnapshotDigest": "<sha256>",
  "externalModuleReferences": 0,
  "ambiguousDynamicModuleReferences": 0,
  "operationName": "create",
  "operationEvidenceIds": ["operation-<20 hex>"],
  "primitiveCapabilities": ["entities.Order.create"],
  "entitySelector": { "schemaVersion": "88mph.base44-entity-selector.v1", "kind": "static-member", "candidates": ["Order"], "evidence": [], "gap": "" },
  "sdkIdentity": { "schemaVersion": "88mph.base44-sdk-callsite-identity.v1" },
  "sdkIdentityGap": "",
  "callsite": {
    "filePath": "src/file.ts",
    "sourceFileSha256": "<sha256>",
    "startLine": 1,
    "endLine": 1,
    "startOffset": 0,
    "endOffset": 40,
    "snippetSha256": "<sha256>"
  }
}
```

The extractor admits only two bounded proofs:

- An exported function in a closed module whose literal import/re-export graph
  is not reachable from a source root such as `main`, `App`, `index`, or
  `pages.config`. Dynamic imports/globs, re-export ambiguity, mutable exports,
  imports, or callers keep the entity call in the operation denominator.
- A callback supplied as `mutationFn` to the exact
  `@tanstack/react-query` `useMutation` import when the returned handle has no
  `mutate`/`mutateAsync` reference and does not escape through an unknown or
  computed use. Exported/re-exported/CommonJS-exposed handles cannot be
  dormant. A same-named local function is not React Query authority.

A disposition is not a passing runtime test and does not erase source. The SDK
primitive remains in the raw denominator, while the disposition records the
exact operation identities withheld from the executable operation set. If
reachability is ambiguous, the extractor emits the ordinary Tier-4 dynamic
operation and coverage gap. Removing any fact is a coverage reduction.

Every v0.14 entity SDK primitive and active operation also carries the exact
callsite offsets and snippet digest. Packet validation recomputes the operation
identity and requires a bidirectional one-to-one accounting relation: each
entity primitive has exactly one active operation or one suppressed disposition
row, and each active or suppressed operation has exactly one retained primitive.
An orphan, duplicate, or cross-callsite primitive invalidates the first packet;
it is not deferred to a later diff.

An unresolved entity selector remains a Tier-4 gap on the retained raw SDK
primitive. It does not duplicate that gap on a source-proven Tier-3 reachability
disposition, whose only claim is that the exact callsite is dormant. Missing
literal local import/require/re-export edges make the module graph open and
prevent dormancy. Explicit static stylesheet imports are outside the executable
source graph; their existence and bytes remain a separate build/package gate.
Configured TypeScript/JavaScript path aliases participate in the same graph;
an unresolved aliased edge blocks dormancy instead of being mistaken for an
external package. Local `extends` chains must remain inside the accepted source.
Malformed, unreadable, cyclic, externally extended, or ambiguous multi-target
configuration opens the graph. A bare import is external only when it is a
platform builtin, an explicit protocol import, or its root package is declared
by package authority; otherwise it may be a bundler alias and blocks dormancy
unless it resolves to exact source.

Validation rejects open objects, nonzero reference counters, source/callsite
mismatch, unsupported extractor versions, incorrect tiers, selector/SDK
contradictions, missing retained SDK primitives, recomputed-operation mismatch,
or duplicate/overlapping suppressed operation identities.
