# Legacy ASMX/SOAP Detection Implementation State

Status: spec-review
Branch: codex/spec-legacy-asmx-soap-detection
Scope: spec-only
Public claim level: hidden
Readiness: ready-for-review

## Summary

This spec defines a future deterministic ASMX/SOAP evidence family for old
ASP.NET and WebForms-era codebases. It covers `.asmx` host directives,
`[WebService]` and `[WebMethod]` attributes, SOAP operation attributes,
generated SOAP clients/proxies, checked-in WSDL/DISCO/proxy metadata, config
evidence, static mapping evidence, and downstream report/path/reverse/release
review consumption boundaries.

The spec is intentionally static. It does not authorize runtime hosting, SOAP
requests, WSDL downloads, deployment inference, endpoint reachability, auth or
security claims, production usage, or impact conclusions.

## Scope Decisions

- This branch creates spec files only under
  `.kiro/specs/legacy-asmx-soap-detection/`.
- Scanner, storage, reporter, CLI, site, docs outside the spec, and tests are
  implementation work for later PRs.
- ASMX/SOAP is modeled as a sibling to WCF/SVC and .NET Remoting. Fact types,
  rule IDs, selector surfaces, and limitations remain distinct unless a future
  old-service-reference normalization spec says otherwise.
- Generated SOAP proxy mapping is probable static evidence only. Duplicate
  candidates, name-only matches, dynamic proxy factories, config transforms,
  and external WSDL imports are gaps or review-tier evidence.
- Public claim level remains hidden until separate validation or evidence-pack
  work promotes a safe demo/public claim.
- No local sample paths, private repo names, raw remotes, raw URLs, SOAP action
  values, config values, source snippets, analyzer output, or secrets are
  stored in this spec.

## Review State

- Initial files drafted:
  - `requirements.md`
  - `design.md`
  - `tasks.md`
  - `review-prompts.md`
  - `implementation-state.md`
- Tasks are intentionally unchecked because this branch is spec-only.
- Opus and Sonnet first-pass reviews completed with full coverage.
- Review fixes applied:
  - documented existing ASMX host extraction under `legacy.wcf.host.v1` and
    `WcfServiceHostDeclared`;
  - defined the ASMX host split as a migration from current WCF behavior, not a
    brand-new parallel detector;
  - added older-index compatibility expectations for ASMX evidence stored under
    historical WCF host facts;
  - defined deterministic WCF-vs-ASMX metadata ownership for `.wsdl`, `.disco`,
    `.discomap`, `.map`, and `.svcmap`;
  - clarified ASMX config facts as supplemental to generic `ConfigKeyDeclared`;
  - clarified mapping tier caps, dual `[WebMethod]` plus SOAP attribute
    behavior, `legacy.asmx.flow.v1` rule timing, and task/test coverage.

## Validation

Planned:

```bash
node scripts/kiro-review.mjs --self-test
node scripts/kiro-review.mjs --phase legacy-asmx-soap-detection --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-asmx-soap-detection --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
./scripts/check-private-paths.sh
git diff --check
```

Completed:

```bash
node scripts/kiro-review.mjs --self-test
node scripts/kiro-review.mjs --phase legacy-asmx-soap-detection --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase legacy-asmx-soap-detection --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
./scripts/check-private-paths.sh
git diff --check
```

First-pass review output:

- Opus coverage: Full.
- Sonnet coverage: Full.
- Blocking findings: existing ASMX host evidence is currently emitted under WCF
  host rules/facts; WSDL/DISCO metadata ownership overlaps with WCF metadata;
  validation was pending.
- Result: patched in spec files; private-path and whitespace validation passed.

First re-review:

- Opus re-review coverage: Full.
- Sonnet re-review coverage: Reduced because the reviewer reported denied tool
  access after reading the relevant files.
- Sonnet findings patched in spec files:
  - clarified that `rules/rule-catalog.yml` edits belong in the implementation
    slice that first emits ASMX facts or report rows, because this branch is
    spec-only and writes only this spec folder;
  - clarified the same-slice requirement for narrowing existing WCF rule
    descriptions;
  - added explicit dual `[WebMethod]` plus SOAP-attribute acceptance criteria;
  - named the target `.asmx` inventory kind as `AsmxServiceHost`;
  - added a regression-test requirement that `.asmx` no longer emits
    `legacy.wcf.host.v1` in new indexes after migration.
- Opus findings patched in spec files:
  - removed the `.svcmap` acceptance-criteria contradiction by keeping
    `.svcmap` WCF-owned;
  - named ASMX `Web References` and WCF `Service References` folder-shape
    ownership rules;
  - clarified that code-behind/code-file directive values with path separators
    are reduced or hashed;
  - clarified that fully qualified type and operation symbols are safe code
    identifiers while operator-local labels, paths, endpoint values, and raw
    diagnostics remain redacted;
  - added consumer older-index, `.svcmap` negative ownership, and
    `Web References` versus `Service References` test requirements.

Second re-review:

- Opus re-review coverage: Full.
- Sonnet re-review coverage: Reduced because the reviewer reported denied tool
  access after reading the relevant files.
- Sonnet finding status: no blockers; spec reported ready to merge.
- Opus finding status: one design wording blocker found and patched.
- Sonnet non-blocking suggestions folded into `tasks.md`:
  - require implementation state notes to record reused `legacy.flow.*` rule IDs
    when `legacy.asmx.flow.v1` is not used;
  - add a hash-only config mapping negative test;
  - add a generic source-map negative metadata test;
  - add an explicit mapping tier-cap test.
- Opus suggestions folded into spec files:
  - made `.svcmap` ownership unconditional for WCF in `design.md`;
  - documented existing WCF metadata behavior and required coordination with
    WCF metadata normalization;
  - included `.map` in the ASMX metadata acceptance criteria;
  - added `.svcmap` nearby-corroboration negative tests and `.discomap`
    inventory tests.

## Follow-Ups For Implementation

- Slice 1 should likely implement host/directive and service/operation
  attributes with focused fixtures.
- Slice 2 should add generated SOAP client/proxy and checked-in WSDL/DISCO
  metadata extraction.
- Slice 3 should add mapping and report/path/reverse consumption.
- Any public sample smoke should use neutral labels and reviewed redacted
  summaries only.
