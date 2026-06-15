# Review Prompts

## Merge-Readiness Review

Review branch `codex/legacy-codebase-validation-impl` for the
`legacy-wcf-service-reference-mapping` spec.

Focus on spec merge readiness and implementation readiness:

- Does the baseline avoid local paths, private repo names, raw remotes, endpoint
  addresses, config values, raw SQL, source snippets, and secrets?
- Are WCF/client endpoint, service contract, operation contract, generated
  client, `.svc` host, and mapping facts clearly bounded?
- Are evidence tiers conservative enough for generated proxies and config-name
  matches?
- Does the spec avoid runtime claims such as service reachability, endpoint
  deployment, WSDL compatibility, or handler execution?
- Are ambiguity and partial coverage handled as gaps rather than arbitrary
  conclusions?
- Are tests and validation commands sufficient?

Return blocking issues, important non-blocking issues, suggested fixes, and
whether the spec is ready to implement.
