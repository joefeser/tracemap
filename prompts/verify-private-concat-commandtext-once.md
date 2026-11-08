# Verify the private compiled string-array SQL case

Use this handoff on the authorized work machine for the original inline UPDATE case. The decision to make is whether the exact Concat return reaches the command's CommandText. Use the saved private configuration, compiled artifacts, method selector, and existing resource guard. Keep all identifiers, literals, hashes, receipts, paths and SQL on that machine. No application, handler or database execution.

**Reuse first.** Inspect completed existing runs. Reuse one if its tool identity matches the pinned build below, its scan succeeded with the same inputs, scope and limits, and its index covers the target compiled method. Go directly to the evidence checks. The allocation-site-only historical baseline cannot answer this question.

**If a new scan is needed**, this handoff calls for one fresh preflight and at most one guarded execution. Use a clean checkout of `dde147264505118428f024e4d754f2fa4215c5b3` (or a docs-only descendant with identical build inputs). Use a separate checkout if there are unpublished changes. Build the Debug CLI once. Verify IL extractor 0.2.70 and policy explicit-il-body-evidence.v71. Record exact CLI/Core SHA-256 locally. Use the same built DLL throughout, without a rebuild. Confirm the existing guard can wrap the actual review run with the previous time, memory and cleanup limits; if unavailable stop with the specific dependency. Do not substitute a bare scan, widen limits or retry.

1. With the original saved fresh-operation config, prepare a NEW durable run directory, leaving all previous runs intact. Command shape (PowerShell):

   ```powershell
   dotnet $cliDll webforms-review preflight --config $savedConfig --out $newRun
   if ($LASTEXITCODE -ne 0) { throw 'Fresh preflight failed.' }
   ```

2. Compare the new manifest's compiled inputs to the previously identified case by actual artifact hashes, module identity and method token. Check source commit, selectors, scope, limits and other evidence inputs against the saved baseline, apart from the intentional tool update. Stop on unverified input identity or scope changes. Preserve the old manifest. With the existing approved external resource guard invoke the same DLL with arguments `webforms-review run --run $newRun`. This is the single scan. Require a completed attempt and a fresh index with SQLite integrity OK before attributing any absence to the extractor. On guard stop, mismatch or failure, report the stage and stop.

3. Bind the exact module, compiled caller, method token and IL call offset. Locate the framework String.Concat(String[]) overload and join ManagedIlCallValuesObserved through ilCallFactId. Examine the ordered string-array-elements argument snapshot. Record the element count and origin-kind counts. Check the result origin and its use by the exact wrapper call or command setter. Require supporting IDs, IL locations, rule/tier, source commit and extractor identity for each hop. Display-name similarity, an unrelated Concat, and a count of all arrays in the index are insufficient.

4. Use the existing exact root with the bounded native wrapper query:

   ```powershell
   python scripts/sql-wrapper-report.py --index $newIndex --cli $cliDll --root $exactRoot --out $freshReport
   ```

   Inspect the terminal commandBinding.commandTextFromPath and supporting path facts. Accept symbolic-string-composition only when the chain traces this Concat result to that command instance. If only the array is recovered, distinguish missing result propagation from missing terminal binding; report the first unproven hop and any associated gap. Check the previously observed bulk-copy destination hash for preservation. Parameter-loop attachment and shared transaction identity remain separate.

Return **one sanitized verdict**: (1) verified through CommandText, (2) array recovered, downstream binding unresolved, (3) array unresolved, or (4) verification blocked. Include tool/input identity checks, element count and origin kinds, propagation, command binding, guard completion and the first gap. Keep exact receipts locally. A blocked or partial run is not evidence of an extractor failure.
