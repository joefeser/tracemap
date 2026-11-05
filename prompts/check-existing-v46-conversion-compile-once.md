# Existing v46-final conversion — review and one isolated compile attempt

The owner has finished the scanner investigation for this sample. The latest single-sample review found sufficient evidence for an annotated conversion with named limits, confirmed the existing v46-final artifact is already complete as that artifact kind, and found that the new framework source resolves none of its existing opaque-boundary TODOs. Do not reopen that investigation or generate another conversion.

This dispatch is the separate next task: inspect the existing annotated conversion and determine whether it compiles in an authentic dependency context. It does not authorize runtime execution, deployment, database access, SQL execution, source modernization or remediation of scanner gaps.

## Preserve and locate

Read applicable AGENTS.md instructions and inspect Git status/diff before work. Preserve all dirty files, existing source, scan outputs, configuration, receipts and conversions. No reset, clean, checkout-overwrite, PR, merge or push from this private review.

Locate the existing extraction handoff index, the ROOT-B v46-final annotated .vb artifact and its sanitized final note. Use their recorded sample binding and local paths; do not select an older attempt or substitute ROOT-A. The artifact is an annotated method retaining compiled-wrapper dependencies, not a self-contained program. Locate its actual containing type/project and dependency references from existing local artifacts/source. Ask only for a missing artifact or source folder if essential; do not ask the owner to type hashes or repeat setup. Keep private paths, source, identifiers and hashes local and outside public repositories.

Record original artifact bytes/hash and source status in a private note. Inspect the existing conversion and the original source for concrete executable differences; distinguish comments/annotations from executable code. Do not assume an old report proves current bytes. If the sample/artifact cannot be bound, stop and report that specific missing evidence.

## Compile decision and one attempt

1. Determine whether an authentic compile context is available: actual containing type, needed partial files, imports/compiler options, target framework, generated inputs and real wrapper/assembly dependencies. Inspect project/build configuration before invocation. Do not guess a .NET 4.x target from generic legacy-project warnings; use the actual project metadata.
2. Use a fresh isolated local copy/output location outside every repository for any compile harness or build outputs. Preserve original inputs. Include only the minimum authentic enclosing context needed for this method; preserve its relevant compiler options and real references. Record exactly which files/references were used and any mechanical enclosure or project adaptation. Do not replace wrappers with stubs, invented implementations, fabricated return values or simplified method bodies. If an isolated method cannot be compiled without editing behavior or fabricating context, report NOT VERIFIED with the exact missing context instead.
3. Inspect potential custom build targets/scripts before running them. Do not invoke deployment, signing, publication, database, network-dependent application startup or unknown custom build actions. If those cannot be excluded for the available build route, stop at the compile prerequisite and name it. Do not install workloads/targeting packs or modify source/project files to force a green result.
4. If a safe authentic context exists, perform ONE compile attempt of the existing artifact. Use an already available compiler/toolchain and real local references. No application/test execution. Keep diagnostics local. Use a 120-second wall limit and an existing supported 2048 MiB process-tree guard if available; do not invent guard success or substitute a broad repository build for an isolated check. If bounded invocation is unavailable, report that prerequisite rather than start an unbounded build. Stop on timeout/resource refusal or compile failure; no retries, cap increases, automatic fixes or dependency-install loops.
5. Preserve diagnostics and all generated outputs in the private isolated location. Recheck that the conversion artifact and original source/config/scan inputs were not changed. Compilation, if successful, proves only that the selected artifact/context compiles; it does not resolve wrapper TODOs, prove source-to-DLL correspondence, or establish runtime/database correctness.

One bounded session: at most 30 tool calls or 15 minutes, whichever comes first. No TraceMap rebuild, rescan, workspace recollection, SQL reparse, evidence regeneration, new conversion attempt or broad gap audit. Stop when the compile result or concrete prerequisite is known. Do not treat hypothetical integer-null analysis alternatives as demonstrated runtime defects.

## Short sanitized report

Return:

- Existing artifact/sample binding: verified / not verified.
- Artifact review: concrete executable differences found, none found, or not established; do not claim comprehensive correctness.
- Compile status: PASS / FAIL / NOT VERIFIED, with the precise reason.
- Context fidelity: real dependencies, enclosing context and compiler/target options; explain any harness adaptation without private code/identifiers.
- If attempted: duration/guard outcome and a short categorical compiler diagnostic summary. Keep exact commands, paths and diagnostics in the private note.
- Original artifact/source/config/scan inputs preserved: yes/no/not verified.
- Existing opaque-wrapper TODOs remain unresolved; no runtime/database execution performed.

If compile prerequisites are missing, identify the smallest concrete next action for owner review, not a request to restart scanning. Do not implement it in this dispatch. Stop after the report.
