# Requirements

1. TraceMap shall emit additive, rule-backed Base44/88mph JavaScript, JSX, TypeScript, and TSX static facts with exact relative spans, evidence tiers, source/snippet hashes, and extractor identities.
2. The adapter shall bind an operator-inspected source digest, normalized tree digest, repository commit, coverage label, and normal TraceMap artifact hashes.
3. The adapter shall cover SDK imports/primitives, frontend function calls, backend function surfaces, entity operations, environment access, provider targets, migrations, and customer-authored boundaries.
4. The adapter shall not retain source snippets, environment values, credentials, URL paths/query strings, or private absolute paths.
5. Dynamic or reduced evidence shall be Tier 4 or an explicit known gap. Reduced coverage shall never become clean absence.
6. Before/after comparison shall be deterministic and shall distinguish added, removed, unchanged, and coverage-reduced states.
7. TraceMap shall remain a static evidence producer. The 88mph host remains authoritative for capability support, runtime execution, migrations, requirements, deployment, and release evidence.
8. The implementation shall prove the contract with controlled tests and then be exercised on Harbor, DigitalTwin-Fork, and ShopGenie source authorities by the consuming workstream.
