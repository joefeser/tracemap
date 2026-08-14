# Review prompts: Cross-adapter scan-truth conformance

## Contract review

- Can materially different selected bytes ever retain the same scan ID or source snapshot digest?
- Can an inaccessible, removed, or changed selected source disappear while coverage remains full or successful?
- Are exclusions applied consistently to inventory and semantic/parser inputs without rewriting evidence paths?
- Are five artifacts published transactionally, and can failed staging replace a prior completed scan?
- Does every readiness claim cite executable evidence and a rule ID?

## Persistence review

- Compare NDJSON and SQLite fact IDs, endpoints, rules, tiers, spans, extractor versions, coverage, limitations, and support IDs.
- Confirm deterministic comparisons exclude only documented non-authoritative metadata.
- Confirm malformed or unknown schemas fail closed.

## Boundary review

- No semantic parity claim across toolchains.
- No raw synthetic source in the report beyond fixed fixture labels.
- No protected source, network, runtime execution, LLM, embeddings, vectors, or prompt classification.
- Go #665 remains deferred until required conformance rows pass or have explicitly accepted owner exceptions.
