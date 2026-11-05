# Check framework source versus compiled DLL — read-only, once

The owner intended two source repositories (website/application and framework),
plus compiled website/dependency evidence and SQL. The completed run's review
reported one source repository, folder-mode syntax analysis / build NotRun,
and admitted compiled assemblies. The owner says the wizard never asked for
the framework SOURCE folder. Determine the actual configuration and coverage;
do not treat the earlier review's "no blocking discrepancies" as proof.

Use the SAME completed workspace run and owning workspace root already known
in this session. If unavailable, ask only for that folder. Read AGENTS.md.
This is a READ-ONLY diagnosis: no scan, workspace verify/collect, MSBuild/solution
load, build/restore, application/SQL execution, config changes, wizard restart,
receipt/attestation changes, commits, PR or merge. Do not reproduce the solution-
root crash. No agents, external uploads or outbound messages.

## Establish recorded versus current state

1. Read the completed run's START-HERE.md and workspace-run.local.json. Resolve
   recorded source/native/config paths, schemas and identities. Locate the owning
   workspace.local.json, saved workspace-answers.local.json and project config
   through explicit links or the immediate documented workspace-root layout;
   don't crawl disks/repos. Compare hashes where a recorded commitment establishes
   the mapping. A changed current config is not proof of what the completed run used.
2. Inspect workspace `repositories`, the run record's `sources`, per-source scan
   manifests and the combined index's source roster. Inspect SQLite schema before
   queries; open with URI mode=ro and PRAGMA query_only=ON. Record for each SOURCE
   alias: root role, project/solution/folder mode, selected/excluded state, target,
   source snapshot/inventory and fact/symbol contributions, analysis level and
   build status. Keep private paths/names local.

## Answer whether the framework SOURCE was scanned

3. Is a second source repository explicitly configured? Is it selected by any
   scope filter used for THIS run? Is there a matching source scan manifest,
   source snapshot, source roster entry and attributable extracted evidence?
   Configured-but-excluded is different from absent; configured-but-unscanned
   is different from scanned with reduced coverage.
4. If no explicit second source channel exists, inspect retained manifests/facts
   for evidence that framework source was considered transitively through a
   project reference. Check actual source paths and contributions locally under
   that source root; don't infer coverage from a project-reference edge alone.
   Identify whether the retained evidence proves source analysis, only a reference,
   or nothing. Do not call transitive partial evidence a complete second-repo scan.
5. Separately inspect native configuration, publication/dependency roster and
   compiled admission evidence for the framework DLL: actual input role (primary
   or dependency/context), admission/binding status and recorded identity checks.
   Source-correspondence receipts, PDB document names and DLL admission do not
   establish that the framework source repository was scanned. Don't assume an
   admitted primary assembly is a selected source repository.

## Explain the missing wizard question without blaming the owner

6. Inspect the saved answers' FrameworkRoot and FrameworkTarget (JSON camelCase
   when applicable). Distinguish missing/null, empty-string skip state, and a
   nonempty path/target. An empty value proves saved state, NOT that the owner
   knowingly chose skip. Look for an existing terminal log or recorded answers
   history before attributing that decision to the owner; otherwise origin UNKNOWN.
7. Compare behavior with the ACTUAL recorded tool revision when established.
   Current WebFormsWizardWorkspace.RunAsync has two relevant branches to inspect:
   - An existing workspace.local.json goes directly to verify/collect/later,
     before asking workspace setup questions.
   - The framework-source question is asked only while FrameworkRoot is null;
     a saved skip/path is reused on continuation.
   Check saved state, initialization arguments and existing logs to establish
   whether either explains this run. Current code alone is not proof of past UI
   behavior. Do not infer that csproj-versus-sln selection caused the omission.
8. Confirm whether the configured framework location, if any, is its SOURCE repo
   and intended csproj/sln, or merely a publication/bin/DLL path. Read project XML
   only if explicitly linked and necessary; never load the sln/MSBuild or execute it.

## Verdict and proposed correction; then stop

Answer TWO distinct questions:
- Does the configuration describe the evidence actually collected?
- Does it meet the owner's intended TWO-source-repository scope?

Use a precise outcome: framework source explicitly scanned; source represented
only transitively/partially; framework configured but excluded/unscanned;
framework DLL only; or UNKNOWN due to missing retained evidence. Do not label
"one source + compiled DLL" good for the requested two-source scope.

If correction is needed, propose the smallest supported extension that preserves
existing source/native/SQL choices and this completed run. Identify the exact
local config field/workflow privately, without editing anything. Check the real
CLI/help/source before proposing a command. If the wizard cannot add the second
source after workspace creation, report that tool gap instead of recommending
reset, deletion, hand-edited signed answers, relabeling DLL coverage, or starting
over. Any future collection is a separate owner decision and fresh run.

One pass: <=25 tool calls / 10 minutes. Mark unavailable checks NOT CHECKED.
Write only an optional uniquely named private Markdown note outside the run;
no generated JSON/database or modifications to evidence/configuration.

Return <=25 SANITIZED lines:
- Completed run versus current config identity: matched/changed/unknown.
- Intended source repos=2; configured=N; selected=N; evidenced source scans=N.
- SOURCE-1 / SOURCE-2 mode, inclusion, analysis/build and contribution status.
- Framework SOURCE verdict, with evidence artifact/table/property categories.
- Framework DLL role/admission/binding, explicitly separate from source coverage.
- FrameworkRoot saved state (null/skip/nonempty/missing), target type and whether
  actual logs establish why the source question was not asked (otherwise unknown).
- Config matches collected scope yes/no/unknown; meets intended scope yes/no/unknown.
- Exact categorical discrepancy, smallest proposed correction, and any tool gap.
- Existing run preserved=yes; no scan/edit performed; local private-note existence.

No private repo/project/assembly/type names, paths, source, SQL, fingerprints,
connection strings or receipts in the shared response. Use SOURCE-1, SOURCE-2
and FRAMEWORK-DLL aliases. Keep detailed local evidence private.
