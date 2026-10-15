# Diagnose remaining families from the accepted scan — no build or rescan

Fetch `origin/codex/evidence-admission-hardening` and use the fetched revision
without overwriting local changes. Reuse the latest accepted saved scan from this
conversation (vb-syntax/0.4.17, legacy-webforms/0.13.9), not the older 266-gap scan.
Ask only if its location is missing or ambiguous. Preserve all saved artifacts.

This change improves retained diagnostics; it does not change extraction. Do not
build, publish, run tests, rescan, restore packages, load MSBuild projects, execute
application code, or change source/configuration. Do not chase the single lambda.

1. Run the committed `scripts/summarize-retained-gaps.py` once with `--scan` set to
   the accepted directory and `--out` set to a new local JSON filename outside
   the scan and checkout. Use the existing Python installation. Stop on missing
   prerequisites or limits; do not raise limits or create ad-hoc parsers.
2. Compare retained totals with the accepted summary. The expected baseline is
   242 other-known-rule gap occurrences, 6,416 compiled gap occurrences and 7,680
   unresolved calls. These are separate denominators; unchanged input must retain
   unchanged totals. Verify saved report still records four candidate operation
   groups and five grouped parameter facts; no new extraction is being claimed.
3. Return the generated `retainedDiagnostics`. The semantic/HTTP overlap counts
   compare exact retained file paths locally, not hashes or source contents.
   Shared files suggest correlated coverage notices, NOT a demonstrated common
   root cause or duplicate defects. Missing file evidence remains unavailable.
4. Inspect only the accepted scan manifest, retained semantic/workspace gap
   evidence and saved project configuration. Determine whether VB semantic
   analysis was not attempted (projectless/outside selected project scope),
   attempted but failed, or unknown. Distinguish absent project metadata from a
   failed build. If already available, inspect the relevant checked-in project
   declarations for exclusions or missing references; do not search the disk or
   execute a build. Report only the generic cause supported by retained evidence.
   If logs or facts do not establish a cause, say unavailable.
5. Examine the 25 retained control-registration gap occurrences together. Use
   their local evidence locations to inspect only implicated markup Register
   directives and ancestor web.config controls sections within the SAME accepted
   source root. Bound source inspection to 40 files and 5 minutes; stop and report
   partial if exceeded. Do not search outside the root or follow external includes.
   Separate: no declaration found in inspected scope; declaration in excluded or
   external config; dynamic/unsupported Src; namespace/assembly registration;
   missing target; ambiguity; clear/remove/location scoping; unavailable evidence.
   Treat registrations as unresolved until target/scope evidence supports a join.
   Inspect all occurrences within the budget, grouping repeated generic shapes.
6. Return one concise handoff: unchanged baseline totals; generated diagnostics;
   semantic attempt/cause evidence; registration counts by inspected generic
   cause; inspected/remaining occurrence counts; and generic synthetic shapes
   needed to reproduce each supported failure. Do not include private paths,
   names, source, configuration values, assembly identities, logs, raw hashes or
   fact IDs. Do not write an ad-hoc machine-readable report. The committed summary
   is the only derived JSON output; any additional findings are bounded prose.

No second scan. No claimed resolution from better labels. If evidence is missing,
identify the minimum missing input rather than proposing another generic patch.
