# One bounded gap-family acceptance

Fetch `origin/codex/evidence-admission-hardening` and select the fetched revision
without overwriting local changes. This is production extractor work, not another
summary-catalog update. Do not run the full test suite on the work machine.

Scope: VB bracket-escaped event receiver identifiers and Boolean-literal
IsPostBack comparisons (including Not and parentheses, bounded to 32 wrappers).
Scoped configuration registrations and dynamic postback refusal are regression
coverage, not newly implemented resolution. Do not promise that any particular
private gap matches these supported forms.

1. Reuse the operator-confirmed source configuration and saved baseline scan from
   this conversation. Ask only if missing or ambiguous. Preserve that baseline.
2. The previous attempt aborted during runtime inventory before producing facts.
   It is not an accepted scan and must not be resumed with the changed runtime.
   Build this revision once into a NEW, empty, isolated output directory outside
   both the checkout and private source/scan roots, using `dotnet build
   src/dotnet/TraceMap.Cli/TraceMap.Cli.csproj -c Release -o <new-tool-directory>`.
   Do not delete old output, trim DLLs/localizations, or copy new binaries over
   the failed run's runtime. Run this new binary explicitly (not a PATH alias).
   Runtime inventory now admits all qualifying files within the existing 4,096
   total-entry bound; the 256-MiB per-file and 2-GiB aggregate hash bounds remain.
   Using the same saved source configuration, create a new run/preflight and run
   ONE fresh scan to a new output directory. Do not reuse stale preflight or tool
   identity receipts. Confirm the runtime-inventory phase completes and extractor
   versions `vb-syntax/0.4.16` and `legacy-webforms/0.13.8`. Never execute application
   methods, SQL, HTTP requests, or apply configuration transforms. If build or
   scan fails, report the failure without repeated retries or broad test runs.
   A runtime-inventory failure again means no gap comparison is available.
3. Run `scripts/summarize-retained-gaps.py` against the new scan with a new output
   filename outside the checkout and scan directory. Compare its privacy-safe
   aggregate groups to the existing summary. This is a changed extractor, so
   counts need not stay identical. Compare only if private source commit/scope
   are unchanged; otherwise report the comparison as unavailable.
4. Verify the accepted four operation groups still have candidate entry paths,
   five parameters remain grouped, and framework uncertainty remains separate.
   Do not equate observed event paths with verified WebForms boundaries/routes.
5. Return all gap-family counts together, including unchanged/increased counts.
   Inspect locally at most one example per remaining family and describe only
   its generic syntax category: escaped/simple/chained/invocation event receiver;
   Boolean literal/compound/shadowed lifecycle condition; absent/ambiguous/config
   registration; literal/dynamic postback. Do not expose private names, paths,
   source, configuration, payloads or raw fact IDs. Do not create an ad-hoc
   machine-readable derived artifact; use the committed summary generator.

No second rescan or full-suite run. If the fixes do not match the private shapes,
say so. The 266 historic gap occurrences and 7,680 unresolved calls are separate
denominators, not unique defects. Semantic prerequisites, config transforms,
runtime targets and unresolved-call reason classification are not fixed here.
