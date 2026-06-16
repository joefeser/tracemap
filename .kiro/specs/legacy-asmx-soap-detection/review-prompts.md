# Legacy ASMX/SOAP Detection Review Prompts

## Opus/Sonnet Spec Review Prompt

Review the Kiro spec in branch `codex/spec-legacy-asmx-soap-detection` for
merge readiness. This is a spec-only PR; do not review it as an implementation.

Files:

- `.kiro/specs/legacy-asmx-soap-detection/requirements.md`
- `.kiro/specs/legacy-asmx-soap-detection/design.md`
- `.kiro/specs/legacy-asmx-soap-detection/tasks.md`
- `.kiro/specs/legacy-asmx-soap-detection/review-prompts.md`
- `.kiro/specs/legacy-asmx-soap-detection/implementation-state.md`

Context:

TraceMap is deterministic static evidence tooling. It already has specs and
implementations around WebForms, WCF/SVC, .NET Remoting, legacy data metadata,
combined paths/reverse, impact, release review, portfolio, and public-safe
validation/catalog workflows. This spec proposes ASMX/SOAP as a sibling legacy
service-boundary evidence family.

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Failed build is not a clean repo.
- Partial analysis is useful, but must be labeled partial.
- Prefer deterministic, testable extractors.
- No LLM calls, embeddings, vector databases, or prompt-based classification in
  scanner/reducer logic.

Review questions:

1. Does the spec clearly separate ASMX/SOAP from WCF/SVC and Remoting?
2. Does it avoid runtime claims about SOAP request execution, deployment,
   endpoint reachability, auth, vulnerability status, or impact?
3. Are `.asmx` directives, WebService/WebMethod attributes, SOAP method
   attributes, generated proxies, checked-in WSDL/DISCO metadata, and config
   evidence covered with appropriate tiers?
4. Are rule IDs complete enough, and does every proposed emitting surface have
   documented limitations?
5. Is the generated proxy and operation mapping strategy deterministic enough,
   especially under duplicate candidates and name-only evidence?
6. Does the spec forbid raw URLs, SOAP actions, config values, credentials,
   local paths, remotes, snippets, analyzer output, and private names in tracked
   artifacts?
7. Are reduced-coverage and older-index cases handled as gaps rather than clean
   absence?
8. Are report/path/reverse/impact/release-review relationships precise without
   requiring everything in the first implementation slice?
9. Are tasks implementable in reviewable PR slices?
10. Are tests strong enough for synthetic fixtures, semantic/syntax fallback,
    mapping, ambiguity, WCF/Remoting separation, and redaction?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested fixes.
- Whether the spec is ready to merge after fixes.

## PR Review Prompt

Review PR `<PR_NUMBER>` in `joefeser/tracemap`: legacy ASMX/SOAP detection spec.

Focus on spec merge readiness only. Confirm that the PR changes only
`.kiro/specs/legacy-asmx-soap-detection/` and does not implement scanner or site
code.

Check for:

- public/private path leaks;
- raw remotes, raw URLs, SOAP actions, config values, secrets, snippets, or
  local sample names;
- runtime overclaims;
- missing rule IDs or limitations;
- unclear ASMX vs WCF/Remoting boundaries;
- missing reduced-coverage behavior;
- unimplementable task slices.

Return merge readiness and exact required fixes.
