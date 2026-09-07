# Base44 entity callsite disposition v1

Extractors `base44-evidence/0.13.0` and `base44-evidence/0.14.0` may replace an entity operation row with a
`Base44EntityCallsiteDisposition` fact only when executable source proves the
SDK call cannot be reached from the rooted application module graph or from a
real React Query mutation handle.

The closed JSON contract in `callsiteDispositionJson` is:

```json
{
  "schemaVersion": "88mph.base44-entity-callsite-disposition.v1",
  "disposition": "dormant-unreachable",
  "callableName": "exportedHelper | mutationHandle.mutationFn",
  "authorityPath": "repo/relative/path.ts",
  "authoritySha256": "64 lowercase hex characters",
  "sourceSnapshotDigest": "64 lowercase hex characters",
  "externalModuleReferences": 0,
  "ambiguousDynamicModuleReferences": 0
}
```

The extractor admits only two bounded proofs:

- An exported function in a closed module whose literal import/re-export graph
  is not reachable from a source root such as `main`, `App`, `index`, or
  `pages.config`. Any dynamic import/glob, re-export ambiguity, mutable export,
  import, or caller keeps the entity call in the operation denominator.
- A callback supplied as `mutationFn` to the exact
  `@tanstack/react-query` `useMutation` import when the returned handle has no
  `mutate`/`mutateAsync` reference and does not escape through an unknown or
  computed use. A same-named local function is not accepted as React Query.

A disposition is not a passing runtime test and does not erase source. It
records why a syntactic SDK call is excluded from the executable operation
denominator. If reachability is ambiguous, the extractor emits the ordinary
Tier-4 dynamic operation and coverage gap instead.

Validation must reject malformed or open disposition objects, nonzero
reference counters, mismatched authority path/hash, unsupported extractor
versions, and non-Tier-3 disposition facts. Replays must keep the disposition
fact and source snapshot digest deterministic.
