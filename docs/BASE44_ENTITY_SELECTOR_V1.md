# Base44 entity selector v1

Extractors `base44-evidence/0.12.0` through `base44-evidence/0.14.0` publish a closed, source-bound selector
contract on every entity operation and its paired payload/query fact:

```json
{
  "schemaVersion": "88mph.base44-entity-selector.v1",
  "kind": "static-member | finite-source-domain | unresolved",
  "candidates": ["EntityName"],
  "evidence": [
    {
      "filePath": "repo/relative/path.ts",
      "sourceFileSha256": "64 lowercase hex characters",
      "startLine": 1,
      "endLine": 1,
      "snippetSha256": "64 lowercase hex characters",
      "derivation": "literal | array-element | object-property | caller-argument | set-membership"
    }
  ],
  "gap": ""
}
```

`entitySelectorJson` contains the serialized contract and
`entitySelectorGap` repeats the closed gap token for fail-closed consumers.
The only v1 gap token is `entity-selector-dynamic-unresolved`.

Finite domains may come only from executable, immutable source constructs:

- string literals, conditional literals, and literal arrays or object maps;
- finite `for...of` and destructuring over those values;
- local or imported callable parameters when every discovered callsite supplies
  a finite argument;
- an immutable literal `Set` whose membership check dominates the operation
  through a terminating rejection branch; and
- a source-bound local entity-client alias, including a local dynamic import.

Version `0.13.0` additionally follows finite executable source through closed
React state setters, controlled literal `<select>` options, append-only result
arrays, destructured mutation inputs from the exact React Query import,
terminating multi-statement guards, and mutable client aliases whose every
assignment has the same finite SDK-derived outer kind. Object values derived
from one `for...of` element remain correlated, so entity and computed query
field candidates are not cross-producted.

Every branch must resolve. A runtime argument mixed with a literal branch does
not yield a partial candidate set. Mutation, assignment, rest/spread caller
ambiguity, unsupported construction, or an escaped source container remains an
`unresolved` Tier-4 operation. Its paired shape retains the same gap. An
unresolved computed SDK primitive is separately visible as Tier 4.

An opaque branch predicate invalidates the whole branch-specific selector; it
must not be treated as false. A source assignment that acquires or restores an
SDK entity client is not itself an entity operation. Only the downstream SDK
method call at its real source location enters the operation denominator.

Source-proven dormant SDK calls are represented separately by
[Base44 entity callsite disposition v1](BASE44_ENTITY_CALLSITE_DISPOSITION_V1.md).
They are not converted into successful operations and are excluded only when
the closed reachability proof is present.

The evidence array is deterministic and bound to a repo-relative source path
and full-file hash already present in the packet's source authority. Consumers
with accepted source bytes should also recompute the snippet hashes.

This contract proves possible entity identities at a source call. It does not
prove route reachability, runtime execution, database compatibility, query
field correlations, or Docker behavior. A finite selector does not turn an
otherwise incomplete query/payload shape into a passing fact.
