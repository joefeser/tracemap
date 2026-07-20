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
- static and dynamic environment access;
- hashed provider origins and dynamic HTTP targets;
- SQL migration surface hashes and statement kinds; and
- customer-authored function/entity boundaries.

Facts contain rule, evidence tier, extractor identity/version, relative path, line span, source-file digest, and snippet digest. They never store source snippets, environment values, URL paths/query strings, tokens, or cookies.

## Consumer contract

Consumers must:

1. Validate `schemaVersion`, both accepted SHA-256 identities, the scan commit, artifact digests, rule IDs, evidence tiers, and extractor identities.
2. Treat source mismatch, artifact mismatch, unsupported rule/extractor identity, and unacknowledged coverage reduction as blockers.
3. Compare facts with their canonical capability/runtime/migration/requirements/ledger authorities instead of turning this packet into a second registry.
4. Report contradictions and gaps separately. A static fact can corroborate a runtime claim; it cannot prove bundling, reachability, browser behavior, provider delivery, IAM/secret access, tenant isolation, or migration completion.
5. Treat missing facts as clean absence only within the declared coverage label and known-gap set. Reduced coverage must never improve a verdict.

The JSON Schema at `docs/contracts/base44-static-evidence.v1.schema.json` defines the wire shape. Additive packet fields are allowed, while the named identity and provenance fields are required.
