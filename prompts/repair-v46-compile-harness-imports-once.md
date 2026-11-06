# Repair isolated VB compile imports — one fresh attempt, existing conversion

Fresh owner dispatch: repair the isolated compiler harness described in the previous ROOT-B compile report, then make ONE new bounded compile attempt. The previous attempt is closed and its failure/outputs must be retained. This dispatch explicitly allows local harness/response-file changes for this attempt; it does not authorize changes to the conversion, application source, scanner, dependency binaries or scan configuration.

The reported failure was a standalone vbc.exe invocation missing the original web build's global namespace imports. That is a hypothesis supported by the diagnostics, not proof that imports are the only remaining issue. The compiler reached its error limit; absence of a method-specific diagnostic in that prefix does not prove the method compiles.

## Reuse the previous work

Read applicable AGENTS.md and inspect status/diff. Preserve dirty work. Locate the last private compile report, saved response file/command, diagnostics, isolated inputs and v46-final conversion through the existing handoff/local notes. Do not ask the owner to reenter known paths, hashes, source/DLL/SQL selections or wizard answers. Ask only if a necessary existing artifact cannot be located.

Keep all private source, paths, names, references, hashes and commands local, outside every repository. No public upload, commit, push, PR or merge from this work-machine dispatch. Record the selected sample/artifact binding and original bytes before doing anything. Do not substitute another method, older attempt or ROOT-A.

Create a NEW private sibling attempt directory. Retain the old directory unmodified. Reuse verified real dependency references and authentic source context; do not restart collection or manufacture a replacement framework. Preserve original source/configuration/artifact files and all scan results.

## Repair the harness before invoking the compiler

1. Inspect the existing response file and the original website's actual compiler/build configuration or retained build command. Recover the global imports that really applied to this VB compilation, along with their provenance. Prefer a retained compiler invocation/response file. Otherwise inspect the applicable project/global-import declarations, effective compiler options and relevant inherited web configuration locally. Do not execute arbitrary imported targets to discover values.
2. Distinguish configuration scopes: a namespace list for generated page code is not automatically proof of the imports used for App_Code or every VB file. Do not blindly copy every namespace from web.config, infer imports solely from unresolved symbol names, or invent a standard namespace list. Explain the source and applicability of each recovered import. Preserve aliases and any binding-sensitive order where applicable.
3. Before compilation, record the selected compiler's full resolved path, file/product version and available version banner in the private note. Establish the website's actual target framework and relevant framework-reference versions separately from the dependency DLL's target; a .NET 2 dependency alone does not select VB 8 or the website compiler. Reuse the previous compiler unless actual original build configuration establishes it was the wrong selection; do not hunt for an old compiler based only on its name. If a selection change is evidence-backed, document it before the single attempt. Confirm the installed compiler's supported imports-option syntax using its existing local help if necessary. Add the evidenced imports to the NEW response file only. Keep real references, compiler identity, target/profile, root namespace, defines, Option Strict/Explicit/Infer/Compare and authentic enclosing/partial-file context consistent with the original compilation where established. Report unknown settings rather than silently choosing values to force success. Do not add packages, install targeting packs, retarget projects, rewrite source or fabricate dependency stubs.
4. Verify that the compile input actually contains the v46-final annotated method. Compiling only untouched original source establishes only an original-source baseline. If needed, make a mechanical replacement of that single method in an isolated copy of its authentic containing file, using the exact existing artifact method; verify the executable statements match and that unrelated methods are unchanged. Do not duplicate the type/method, discard annotations to hide problems, edit the method body or create another conversion. If the artifact cannot be placed in authentic context without guessing/editing behavior, report NOT VERIFIED and the exact missing context.
5. Before compilation, inspect the NEW response file's source/reference paths and output path. All outputs must stay in the new attempt directory. Do not invoke the full solution, MSBuild custom targets, application startup, deployment, signing, publication, tests, SQL or database access. Use the existing standalone compiler route. If authentic imports cannot be established, stop with that concrete missing prerequisite rather than a guessed repair.

## One bounded compilation

Use the already available toolchain and existing supported process-tree resource guard: 120 seconds wall / 2048 MiB, no cap increase or automatic retries. If the established guard is unavailable, report it; do not claim a guarded attempt or start an unbounded build. Make ONE compiler invocation after the response-file preparation is complete. The earlier failed attempt does not count against this fresh single attempt.

Keep a small private invocation ledger distinguishing compiler discovery/help/version probes from compilations with source inputs. Reuse existing probe results; do not cycle through candidate compilers or repeatedly invoke help. Report the counts yourself so the owner need not monitor calls. Only ONE source compilation is authorized; preparation probes are not compile attempts. If an actual compilation has already occurred under this dispatch, stop and report it rather than treating this prompt clarification as permission for another attempt.

Capture exit code, whether diagnostics were truncated/error-limited, warning/error categories, elapsed time and guard outcome. Compiler exit zero plus a newly emitted expected output from this attempt is needed for PASS; stale outputs, zero exit from a wrapper alone or missing error text are insufficient. Do not load or execute the output assembly. On failure or timeout, retain everything and stop; no second compiler invocation, source fixes, dependency-install loops or broad solution build.

Recheck original conversion/source/config/scan bytes/status afterwards. Preserve existing unknown-wrapper annotations and analysis limitations. No TraceMap build, rescan, workspace recollection, SQL reparse, conversion rewrite or change to prior evidence. Work is bounded to 30 tool calls / 15 minutes, whichever comes first.

## Return a short sanitized report

- Sample/artifact binding and actual inclusion of the annotated method: verified / not verified.
- Harness change: evidence-backed global imports added; configuration source/applicability and any unresolved context settings, using aliases rather than private identifiers.
- Compiler selection: version/target compatibility established or unknown; whether the prior compiler was retained. Keep its private full path in the local note. Invocation counts: discovery/help/version probes versus actual source compilations.
- Compile result: PASS / FAIL / NOT VERIFIED. If attempted: compiler exit, fresh-output check, diagnostic truncation/error-limit status, guard result and duration.
- If failed: categorical remaining errors and whether they concern harness context, dependencies, the method or are not yet attributable. Do not call an error prefix exhaustive or a missing diagnostic proof of correctness.
- Original artifact/source/config/scan inputs and previous attempt preserved: yes/no/not verified.
- Boundary: compile result only; wrapper TODOs remain unresolved; no runtime/database execution or source-to-DLL correspondence claim.

Keep exact commands, imports, source references and diagnostics in the private note, not the sanitized response. If blocked, name the smallest missing prerequisite and stop. Do not ask to restart scanning or produce another annotated conversion.
