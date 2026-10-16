# One namespace-only App_Code registration acceptance

Fetch `origin/codex/evidence-admission-hardening` and select the fetched revision
without overwriting local changes. Reuse the latest accepted source configuration
and saved baseline (242 noncompiled gap occurrences, 6,416 compiled occurrences,
four candidate operation groups, five parameter facts). Ask only if ambiguous.

Build once into a NEW isolated tool directory outside the source/scan/checkout
roots; preserve old outputs. Use the new binary explicitly and create a new
preflight/run with the same source configuration. Never resume old tool receipts.
Confirm the exact `ScannerVersions.LegacyWebFormsExtractor` value in
`src/dotnet/TraceMap.Core/Models.cs` at the selected tool revision; VB syntax remains `vb-syntax/0.4.18`.
Do not run the full test suite, execute application methods, connect to SQL/HTTP,
apply transforms, manufacture a project file, or load unapproved assemblies.

Run ONE scan. Stop on failure with the categorical error; no repeated retries.
Inspect the previously diagnosed namespace-only registration and its 24 usage
sites together. Check that any new UsesRegisteredAppCodeControlCandidate:

- remains Tier3SyntaxOrTextual, not a compiler or assembly-binding claim;
- references the retained namespace registration and a unique inventoried class
  declaration beneath the selected web root's App_Code directory;
- does not infer an assembly merely from App_Code.dll being present;
- leaves missing/duplicate/out-of-scope targets unresolved.

Do not assume all 25 gaps disappear. Report accepted candidate count and remaining
generic refusal categories. Confirm four operation groups still retain candidate
paths, five parameter facts remain grouped, and framework uncertainty remains
separate. Semantic/projectless notices are not fixed by these source joins.

Run `scripts/summarize-retained-gaps.py` on the new scan to a new local output
outside the checkout/scan. Compare against the accepted baseline only if private
source commit and scope are unchanged. Keep compiled and other-rule totals
separate. Return only approved aggregate counts and generic refusal shapes, not
private identifiers, paths, source, hashes or artifacts. Do not create ad-hoc JSON.

The public synthetic matrix covers 89 combinations and refusal/interaction cases;
it is not proof of private compatibility or universal ASP.NET support. No second
scan or full-suite validation on the work machine.
