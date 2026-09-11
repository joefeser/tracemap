# Run one focused Web Forms monorepo scan

```text
Run one private, owner-controlled TraceMap local review over exactly three
repository-relative application folders:

1. the projectless or project-owned Web Forms application;
2. its backend code;
3. its shared/custom Web Forms controls.

Purpose:

- replace the prior portfolio-scale run with one bounded application snapshot;
- retain cross-folder static evidence among the Web Forms application, controls,
  and backend;
- exclude unrelated repository areas and generated/churn files;
- produce a Web Forms modernization packet from the focused scan;
- return only a small categorical/count summary.

This is a validation task, not a product-code task.

Authorization:

- You may inspect only the owner-identified private Git repository and the three
  owner-identified folders.
- You may inspect directory names and project/solution metadata inside those
  folders to determine project ownership.
- You may run the checked-out TraceMap CLI and its local-review workflow.
- You may create one new output directory outside the private repository and
  outside the TraceMap repository.
- Keep the complete output private and local for later review.

Input discovery:

1. Confirm the TraceMap checkout contains this prompt and record its HEAD
   locally. Do not return the SHA.
2. If the private repository root and the three repository-relative folders are
   not already present in task context, ask the owner for only those four
   values. Do not search unrelated directories or drives.
3. Verify the supplied repository root with:
   `git -C <root> rev-parse --show-toplevel`.
4. Require a clean private worktree. If it is dirty, stop with
   `SOURCE_WORKTREE_DIRTY`; do not modify or clean it.
5. Record the private repository commit locally. Do not return it.
6. Verify that each of the three supplied folders is beneath that same Git root.
7. Inspect only those three folder trees for `.csproj`, `.vbproj`, `.sln`,
   `.aspx`, `.ascx`, `.master`, `.ashx`, code-behind, and designer file shapes.
   Do not read application source merely to perform this boundary check.
8. Classify the Web Forms folder as `web-application-project`,
   `projectless-web-site`, or `unknown`:
   - checked-in Web Forms markup without an owning project is a valid
     `projectless-web-site`;
   - never substitute a sibling backend or controls project as the Web Forms
     owner;
   - if the classification is `unknown`, stop with
     `WEBFORMS_PROJECT_SHAPE_UNKNOWN`.

Focused scan configuration:

- Use the private Git root as `--repo` so all evidence shares one repository and
  commit identity.
- Add exactly one `--include <repository-relative-folder>/**` for each of the
  three owner-identified folders. Do not include the repository root broadly.
- Add these exclusions explicitly:
  - `.vs/**`
  - `**/bin/**`
  - `**/obj/**`
  - `**/node_modules/**`
  - `**/dist/**`
  - `**/coverage/**`
  - `**/TestResults/**`
  - `**/.angular/**`
  - `**/.next/**`
- Add `--project` only for project files located inside the backend or controls
  folder and for an actual owning Web Forms project when one exists.
- When multiple project files exist within one of those bounded folders, include
  each genuine application project. Do not include test, sample, generated,
  migration-only, or unrelated projects merely because they are nearby.
- Do not pass a whole-repository solution merely to make project discovery
  easier.
- Do not pass `--restore`; the guided local-review command intentionally has no
  restore or network behavior.

Execution:

1. Choose a new, empty output path outside both repositories.
2. Run the current TraceMap CLI's guided workflow with the assembled arguments:

   `dotnet run --project <TraceMapRoot>/src/dotnet/TraceMap.Cli -- local-review run --repo <PrivateGitRoot> --out <NewOutputRoot> <three include arguments> <bounded project arguments> <exclusion arguments> --webforms-modernization`

3. Do not run the multi-source interaction runner for this pass. It answers a
   different question and would reintroduce unrelated scope noise.
4. Do not retry automatically after a failure.
5. Preserve the private output even when the workflow truthfully reports reduced
   or partial coverage.
6. Verify that `local-review-result.json`, the `scan` directory, and—when the
   scan is compatible—the `webforms` directory are recorded by the workflow.
7. Verify from the scan manifest that the repository identity and commit are
   present and that the scan is not represented as full coverage when build or
   analysis gaps exist.

Private review:

From only the retained focused output, calculate aggregate counts for:

- inventoried files;
- Web Forms markup, code-behind, designer, handler, application, and site-map
  inventory shapes;
- positive Web Forms inventory facts;
- declared event bindings;
- resolved handlers;
- projected event flows;
- classic ASP.NET surface and handler facts;
- backend/service boundary facts when the existing rules expose them;
- analysis gaps, grouped only by rule ID;
- analyzer capability diagnostics, grouped only by categorical state;
- Web Forms modernization surfaces, event chains, downstream boundaries,
  candidate links, and packet gaps.

Do not treat an absent fact as proof that the feature does not exist. Do not
claim runtime execution, complete feature coverage, correct behavior, migration
safety, endpoint alignment, or production readiness.

Classify `nextAction` as exactly one of:

- `review-focused-evidence`: useful focused evidence and packet were produced;
- `restore-semantic-prerequisites`: scoped inventory exists but semantic/build
  coverage is the primary limitation;
- `patch-webforms-extractor`: relevant files are inventoried but their expected
  structural facts are absent;
- `correct-three-folder-scope`: the three focused folders still contain no
  relevant Web Forms/classic ASP.NET inventory;
- `inspect-focused-failure`: the guided workflow stopped before a trustworthy
  focused result was retained.

Return only this compact sanitized block:

focused-webforms-scan=<completed|boundary-stop>
failureCode=<categorical-or-none>
projectShape=<web-application-project|projectless-web-site|unknown>
selectedFolderCount=<count>
selectedProjectCount=<count>
analysisLevel=<categorical-value-or-unavailable>
buildStatus=<categorical-value-or-unavailable>
fileInventory=<count>
markupInventory=<count>
codeBehindInventory=<count>
designerInventory=<count>
handlerInventory=<count>
webFormsInventoryFacts=<count>
eventBindings=<count>
resolvedHandlers=<count>
eventFlows=<count>
aspNetSurfaceFacts=<count>
aspNetHandlerFacts=<count>
analysisGaps=<count>
modernizationSurfaces=<count-or-unavailable>
modernizationEventChains=<count-or-unavailable>
modernizationBoundaries=<count-or-unavailable>
modernizationCandidates=<count-or-unavailable>
modernizationGaps=<count-or-unavailable>
nextAction=<review-focused-evidence|restore-semantic-prerequisites|patch-webforms-extractor|correct-three-folder-scope|inspect-focused-failure>

Privacy and mutation boundary:

- Do not return or commit private repository names, folder names, paths,
  filenames, project names, routes, URLs, symbols, labels, source values,
  diagnostic text, SQL, configuration values, credentials, connection material,
  logs, run IDs, commit SHAs, or hashes derived from private values.
- Do not quote raw JSON, NDJSON, Markdown report content, source, or config.
- Do not modify the private source repository, TraceMap source, Git metadata, or
  the owner's existing configuration.
- Do not commit, push, publish, open a pull request, or use network services.
- Before responding, search the proposed response for every private name, path
  fragment, label, run ID, commit SHA, and identifier encountered during the
  task. Remove any match and repeat the check.
```
