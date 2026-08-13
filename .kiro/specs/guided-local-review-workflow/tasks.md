# Guided Local Review Workflow Tasks

## Spec-Only Slice

- [x] 0.1 Inventory current CLI packaging, scan receipt, Web Forms packet, and explorer seams.
- [x] 0.2 Define requirements, design, task runway, review prompts, and implementation state for #666.
- [x] 0.3 Keep distribution selection gated on reproducible package smoke evidence.
- [x] 0.4 Run spec formatting, private-path, and diff validation.
- [x] 0.5 Review the spec and patch blocking or P1/P2 contract findings.
- [ ] 0.6 Commit, push, and open the focused spec PR to `dev` after #656 is merged or the owner approves parallel review.

## Implementation Runway

- [ ] 1. Validate distribution candidates. Requirements: 1, 8, 10.
  - [x] Add a committed candidate matrix with pass/fail evidence and explicit host claims.
  - [x] Build synthetic .NET tool, framework-dependent archive, self-contained archive, and offline/container probes where the host supports them.
  - [x] Prove install/run/remove outside the source checkout.
  - [x] Inspect package/archive contents and native dependencies.
  - [ ] Select one v1 distribution only after the gates pass; otherwise retain source-checkout runbooks.

- [x] 2. Add installed version/readiness output. Requirements: 2, 8, 9.
  - [x] Define and add the version JSON schema.
  - [x] Add bounded local capability detection and closed readiness states.
  - [x] Add concise human rendering from the structured result.
  - [x] Add deterministic and privacy tests.

- [x] 3. Extract reusable command services. Requirements: 3, 9.
  - [x] Identify the smallest service seams shared by standalone scan, Web Forms packet, and explorer commands.
  - [x] Preserve existing command behavior and schemas.
  - [x] Avoid recursive shelling to the CLI.
  - [x] Add standalone-versus-service parity tests.

- [x] 4. Implement output transaction and artifact hashing. Requirements: 4, 5.
  - [x] Add generated-only collision validation and unsafe-path refusal (v1 refuses replacement rather than exposing force).
  - [x] Add sibling staging and atomic publication where supported.
  - [x] Add bounded SHA-256 artifact records using relative paths.
  - [x] Add upstream pre/post hash verification.
  - [x] Add cleanup-state tests including Unix symlink fixtures; Windows reparse smoke remains a host gate.

- [x] 5. Implement the v1 guided workflow. Requirements: 3, 4, 6, 7, 9.
  - [x] Validate and lock the final command name.
  - [x] Run scan through the shared service and compose `scan-receipt.json`.
  - [x] Optionally run Web Forms packet generation when compatible evidence exists.
  - [x] Optionally run the ordinary static explorer on compatible artifacts.
  - [x] Keep #667 Web Forms packet rendering unavailable until its reader ships.
  - [x] Emit `local-review-result.v1` and README/terminal output from one structured model.
  - [x] Add closed outcomes, next actions, exit codes, and failure tests.

- [ ] 6. Package and document the selected distribution. Requirements: 1, 2, 8.
  - [x] Add reproducible package metadata and version provenance.
  - [ ] Add Windows, macOS, and Linux install/upgrade/uninstall instructions for proven hosts.
  - [x] Add offline installation guidance without security-control bypasses.
  - [x] Document integrity versus authenticity limitations.

- [ ] 7. Validate end to end. Requirements: 9, 10.
  - [x] Run focused package/version/workflow tests.
  - [x] Run synthetic Web Forms standalone-versus-guided parity.
  - [ ] Run claimed-host package smoke tests.
  - [x] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [x] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [x] Run `./scripts/check-private-paths.sh`.
  - [x] Run `git diff --check`.

## Recommended PR Slices

1. Spec-only contract and distribution decision gates.
2. Distribution probe matrix plus installed `version --json` surface.
3. Output transaction, shared command services, and guided scan result.
4. Optional Web Forms packet and ordinary explorer orchestration.
5. Cross-platform documentation and claimed-host smoke receipts.
6. #667 integration after the Web Forms explorer reader is merged.

## Deferred

- Web Forms packet explorer rendering remains #667.
- Hosted execution, upload, telemetry export, automatic restore, signing
  authority, container publication, self-update, and shell completion require
  separate owner decisions.
- Go adapter #665 remains deferred until #664 conformance is complete.
