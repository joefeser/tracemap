# Base44 SDK Callsite Identity v1

Extractor `base44-evidence/0.10.0` binds every entity operation and its
payload/query shape to the exact Base44 SDK authority that reaches that
callsite. This prevents a repository-level SDK version from being applied to
operations that execute under a different package identity.

The fact property `sdkIdentityJson` is a deterministic JSON string with this
closed contract:

```json
{
  "schemaVersion": "88mph.base44-sdk-callsite-identity.v1",
  "packageName": "@base44/sdk",
  "version": "0.8.4",
  "scope": "function-runtime",
  "rawSpecifier": "npm:@base44/sdk@0.8.4",
  "evidence": [
    {
      "authorityPath": "functions/example.ts",
      "authoritySha256": "<64 lowercase hex>",
      "kind": "source-import"
    }
  ]
}
```

The closed values are:

- `packageName`: `@base44/sdk`
- `version`: `0.8.4` or `0.8.5`
- `scope`: `function-runtime` or `frontend-package`
- evidence `kind`: `source-import`, `package-manifest`, or
  `package-lock-resolution`

Evidence entries are sorted by kind, path, and hash and contain no intermediary
traversal files. The operation fact independently identifies the callsite.

`function-runtime` requires an exact runtime import of
`npm:@base44/sdk@0.8.4` in the operation source file. Its sole evidence entry is
that source file and its digest must equal the operation's `sourceFileSha256`.

`frontend-package` requires an exact runtime import of `@base44/sdk`, the root
`package.json` request, and the root `package-lock.json` resolution. The
manifest request must equal the lock root request and the lock must resolve
`node_modules/@base44/sdk` to `0.8.5`. The evidence array contains exactly the
lock, manifest, and direct SDK-import source file. An indirect helper call
retains the direct import root; helper/intermediary files are not identity
authority.

The same identity is copied exactly to the operation's payload or query fact.
Packet validation rejects shape/operation disagreement, extra keys, invalid or
duplicate evidence, non-deterministic ordering, a source digest that is not
backed by the extracted runtime-import fact, and function evidence that does
not match the callsite source digest.

## Fail-closed gaps

When exact identity cannot be derived, `sdkIdentityJson` is empty,
`sdkIdentityGap` contains one of these closed tokens, and the operation remains
`Tier4Unknown`:

- `sdk-identity-source-root-missing`
- `sdk-identity-source-root-ambiguous`
- `sdk-identity-specifier-unsupported`
- `sdk-identity-package-authority-missing`
- `sdk-identity-package-authority-invalid`
- `sdk-identity-version-unsupported`

The extractor never infers identity from a directory name. Multiple reachable
roots with different source-bound identities are ambiguous even when a global
package version appears obvious. Missing package authority, unsupported
versions, and conflicts remain blockers rather than falling back to a
repository-wide default.

This contract proves only static package identity at an extracted callsite. It
does not prove bundling, runtime reachability, backend compatibility, or that a
declared operation succeeds.
