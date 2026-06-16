# Review Prompts

## Spec Merge-Readiness Review

Review branch `codex/legacy-remoting-detection-spec` for the
`legacy-remoting-detection` spec.

This is a spec review, not an implementation review. Do not edit files.

Focus on spec merge readiness and implementation readiness:

- Does the spec treat Remoting as a sibling detector to WCF/SVC instead of
  mixing Remoting facts into WCF/SVC fact types?
- Are the target evidence shapes complete enough for `System.Runtime.Remoting`,
  `MarshalByRefObject`, `TcpChannel`, `HttpChannel`, `IpcChannel`,
  `ChannelServices.RegisterChannel`,
  `RemotingConfiguration.RegisterWellKnownServiceType`,
  `RemotingConfiguration.RegisterWellKnownClientType`, `Activator.GetObject`,
  and `<system.runtime.remoting>` config blocks?
- Are evidence tiers conservative enough for syntax/config-heavy evidence and
  semantic analysis failures?
- Does the spec avoid runtime claims such as host activation, endpoint
  reachability, deployment, production usage, security exposure,
  exploitability, or proven impact?
- Are rule IDs, limitations, line-span expectations, commit SHA, extractor
  versions, coverage labels, and supporting evidence requirements explicit?
- Are privacy constraints strong enough for URLs, object URIs, ports, config
  values, local paths, repo remotes, raw snippets, secrets, and generated public
  smoke artifacts?
- Are tests and validation commands sufficient for a future implementation
  branch?

Return blocking issues, important non-blocking issues, suggested fixes, missing
tests, and whether the spec is ready to implement after fixes.

## Re-Review Prompt

Re-review `.kiro/specs/legacy-remoting-detection/` after the first review
findings are patched.

Confirm whether Medium+ or blocking findings from the first pass are resolved,
whether any new blockers were introduced, and whether `tasks.md` is
implementation-ready while still unchecked.
