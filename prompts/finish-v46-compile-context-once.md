# Finish the existing annotated conversion's compile context — one fresh attempt

Fresh owner dispatch following the ROOT-B Compile Check v2 report and public Windows compiler POCs. The last private attempt finished with 12 reported errors outside the selected method: a missing namespace import for a referenced library and a namespace ambiguity. The annotated method was included and original inputs were preserved. This is NOT a request to rerun a consumed prompt or restart scanning.

Read applicable AGENTS.md and inspect status/diff. Preserve dirty work, application source, conversions, configuration, scan outputs and all previous compile attempts. Locate the v2 report, previous response file/compiler logs, authentic containing source/context and existing v46-final artifact from local notes/handoff. Do not ask the owner to retype already recorded paths, hashes or wizard answers. Keep all employer material and path-bearing diagnostics local, outside repositories. No public commit, push, PR or merge.

## What the public POC establishes

Read `docs/VB_COMPILE_CONTEXT_POC.md` and the public fixtures/runner at public implementation commit `e5d59a275a38a21088665d74ae150b396717086c`, or a descendant retaining those files. No need to rerun those tests or rebuild TraceMap. Fifteen native Windows case oracles passed:

- VB 8 and the newer framework compiler both compiled the annotated public consumer with a CLR2 dependency. A .NET 2 dependency alone does not select the website compiler.
- On the tested host, an ASP.NET page namespace applied to App_Code. Removing it caused BC30002; explicit compiler imports restored success.
- A controlled public namespace collision produced BC30561 and was corrected through narrower imports without source edits.
- The actual framework namespace probes did NOT reproduce the private collision. They do not justify automatically switching compilers or reference assemblies.
- An annotated-input tripwire proved which source entered compilation. Successful compiler exit plus fresh output was required.

These are public fixture results, not proof of this website's exact effective configuration or private compilation. Do not copy the fixture namespace list into the private harness.

## Prepare an evidence-backed harness correction

1. Bind the existing sample/artifact from its recorded identity, verify its bytes and identify the exact compiler used in v2. Record resolved executable path, file/product version, actual website target and real reference identities separately from dependency targets. Use saved metadata first; at most one help/version probe if necessary. Keep a private ledger separating probes and source compilations. Do not search/cycle through compilers or install/retarget anything.
2. Create ONE fresh private sibling attempt directory. Copy the previous isolated harness/context and response file, retaining original reference/compiler options unless a specific correction below is established. Preserve all prior directories. Ensure only isolated copies/output paths are writable.
3. Missing import: confirm the reported unresolved public type exists in the already referenced library and recover its full namespace from the real assembly metadata/source/configuration. Add that evidenced namespace/alias to the new response file only. A reference is not itself a namespace import. Do not guess imports or add packages.
4. Ambiguity: read the complete BC30561 diagnostic and the exact affected source usages. Identify both competing namespace/type identities and the imported names causing the conflict. Establish the intended referenced type/member from the real available declarations/metadata and existing usages. Prefer a compiler import alias matching the existing source qualifier, or a narrower import, ONLY if it preserves the needed bindings across the existing compile inputs. Do not blindly remove System.Web/System.Text, rewrite source, replace dependency binaries, switch compilers or swap runtime/reference assembly sets merely to get green. If intended binding is not established, stop with the precise ambiguity rather than compile a guessed choice.
5. Treat any explicit alias/narrowing as a documented isolated-harness adaptation. It can support compilation in that selected context; do not relabel it as recovery of the exact historical compiler command. Retained original settings remain authoritative where available. Root/page config and public App_Code controls are useful context, not a substitute for private applicability checks.
6. Confirm the new compilation actually contains the existing annotated method in its authentic containing type/partial context. Reuse the previously verified mechanical substitution; recheck artifact/source-copy hashes and executable text. Do not create a different conversion, edit behavior, duplicate the method/type, drop failing unrelated files, or introduce stubs. Inspect the response file for duplicate sources and stale outputs. Preserve real dependencies and relevant compiler options; report unresolved context settings.

Make both justified harness corrections BEFORE invoking the compiler. This dispatch authorizes one new compile attempt, not one attempt per error or per candidate configuration. If the evidence does not support a correction, report NOT VERIFIED and stop; no user request to restart setup/scanning.

## One bounded compile, then stop

Use the same available compiler and established standalone route with the existing 120-second / 2048 MiB process-tree guard. No full solution/MSBuild/custom targets, application startup, tests, signing, publishing, database access or SQL execution. If the established guard is unavailable, report the prerequisite instead of running unbounded.

Perform ONE source compilation after preparation. Capture compiler exit, elapsed time/guard outcome, complete diagnostics and whether the compiler hit an error limit. PASS requires compiler exit zero AND the expected newly emitted output in this attempt's fresh output directory. Do not load/execute the DLL. A file with zero diagnostics inside an overall failed build is not a successful compilation.

On failure/timeout, retain all results and stop. No second compile, dependency installation, application-source fix, new conversion, scanner build/rescan, workspace recollection or SQL reparse. Recheck all original artifact/source/config/scan inputs and earlier attempts are unchanged. Bound the review to 30 tool calls / 15 minutes, whichever comes first.

## Short sanitized report

- Sample binding and annotated-method inclusion: verified/not verified.
- Compiler version/target context; retained versus changed settings; probe versus actual compile counts. Private executable path and exact commands stay in the local note.
- The two harness corrections, evidence for intended bindings and whether either remains uncertain. Distinguish an adapted harness from exact historical build-context reproduction.
- Compile result PASS / FAIL / NOT VERIFIED; exit, fresh-output check, error-limit status, duration and guard outcome if attempted.
- Remaining error categories and attribution, without claiming an error prefix exhaustive or inferring unrelated runtime defects.
- Original inputs and previous attempts preserved; private diagnostic note saved; no application/database execution.
- If PASS: compilation of this artifact/context only. Existing opaque-wrapper TODOs and runtime/source-to-DLL questions remain unresolved.

No employer identifiers, paths, code, SQL or private hashes in the response. Stop after the report.
