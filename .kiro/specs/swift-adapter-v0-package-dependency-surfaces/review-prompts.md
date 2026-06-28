# Swift Adapter v0 Package And Dependency Surfaces Review Prompts

Use these prompts after reading:

- `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/requirements.md`
- `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/design.md`
- `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/tasks.md`
- `.kiro/specs/swift-adapter-v0-package-dependency-surfaces/implementation-state.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- GitHub issue #377
- GitHub issue #382

## Prompt For Opus

Please review this TraceMap Kiro spec for issue #382, "Swift adapter v0:
package and dependency surfaces."

Context:

- This is spec-only. It must not implement Swift analyzer/runtime code.
- TraceMap is deterministic static analysis. No LLM calls, embeddings, vector
  databases, runtime tracing, app execution, simulator/device inspection,
  package-manager restore, registry lookup, vulnerability lookup, license
  lookup, or prompt-based classification belongs in scanner/reducer behavior.
- Facts require rule IDs, evidence tiers, repo-relative file paths, line spans,
  commit SHA, extractor versions, and documented limitations.
- Swift package/dependency evidence is checked-in metadata evidence only. It
  does not prove dependency resolution, package install, build success, runtime
  loading, compatibility, vulnerability status, license status, freshness, or
  production usage.

Review goals:

1. Identify any overclaim that could imply package restore/build proof, runtime
   usage, compatibility, vulnerability/license/freshness status, or production
   impact.
2. Check whether the proposed fact vocabulary is conservative and useful for
   SwiftPM, CocoaPods, and Carthage.
3. Check whether Swift-specific fact types are justified instead of forcing
   metadata into misleading shared runtime dependency tables.
4. Check whether unsafe values are handled strongly enough: raw URLs,
   hostnames, local paths, raw remotes, credentials, source snippets, manifest
   snippets, and private labels.
5. Check whether stable identity inputs are deterministic and avoid timestamps,
   output paths, local absolute paths, raw unsafe values, and arbitrary winners.
6. Check whether gap behavior is explicit enough for dynamic manifests,
   unsupported lockfiles, malformed metadata, unsafe identities, duplicate
   entries, and absent metadata.
7. Check whether validation commands and test requirements cover the risk.
8. Suggest missing limitations, tests, or rule catalog entries before
   implementation.

Please return:

- Blockers
- Medium findings
- Minor findings
- Suggested first implementation cut

## Prompt For Sonnet

Please review this TraceMap Swift adapter v0 package/dependency surfaces spec
for implementability.

Focus on:

1. Whether the future implementation tasks are sized for a normal feature PR.
2. Whether the parser strategy is specific enough to implement without
   rediscovering SwiftPM/CocoaPods/Carthage boundaries.
3. Whether proposed rule IDs, fact types, properties, gap kinds, and stable-key
   inputs are specific enough for tests.
4. Whether evidence tiers are correct for manifest syntax, lockfile structure,
   and unsupported/dynamic metadata.
5. Whether export/combine/report compatibility is clear without overclaiming.
6. Whether validation commands are exact for this spec PR and realistic for a
   future implementation PR.
7. Whether tasks accidentally mark work complete or imply this PR implemented
   analyzer behavior.
8. Whether any site/product/public copy claim leaked into the implementation
   scope.

Assume this spec PR must pass:

```bash
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase swift-adapter-v0-package-dependency-surfaces --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
git diff --check
./scripts/check-private-paths.sh
```

Return:

- Blockers
- Medium findings
- Minor findings
- Recommended first implementation cut
