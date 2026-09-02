# Run the Web Forms monorepo correction

```text
Complete the Web Forms monorepo correction and validation in one private,
owner-controlled session.

Context:

- The private repository is a monorepo containing the Web Forms application,
  backend, and UI components in different folders.
- The previous retained interaction run reviewed nine .NET scopes but emitted
  zero positive Web Forms or classic ASP.NET evidence.
- One previous scope had full semantic coverage, so reduced compilation alone
  does not explain the absence.
- The TraceMap interaction runner requires each `repositoryPath` to be the Git
  repository root. A nested application folder must be selected with a
  repository-relative `include` entry and, only when one exists, a `projects`
  entry.
- The target may be a projectless ASP.NET Web Site. Absence of a `.csproj` is
  not a failure and must not cause the agent to select a sibling backend or
  custom-control project as the Web Forms owner.

Authorization:

- You may inspect the private repository only to identify its Git root, whether
  the Web Forms folder has an owning project file, and the folder containing
  checked-in `.aspx`, `.ascx`, `.master`, or `.ashx` files.
- You may update only the local interaction configuration and create one new
  local interaction output directory.
- You may run committed TraceMap validation and interaction-run tooling.
- Keep every private name, path, value, and artifact local.

Procedure:

1. Confirm the TraceMap checkout contains this prompt and record its HEAD
   locally. Do not return the SHA.
2. Ask the owner for the exact private monorepo location and interaction
   configuration file only if they are not already in task context. Do not
   search unrelated directories or drives.
3. Determine the monorepo Git root with Git.
4. Locate the bounded Web Forms application folder by the presence of checked-in
   `.aspx`, `.ascx`, `.master`, or `.ashx` files. Determine whether that folder
   has an owning `.csproj`. Classify it locally as
   `web-application-project`, `projectless-web-site`, or `unknown`. Do not read
   application source contents merely to identify file types and boundaries.
5. Update only the intended Web Forms source entry in the local interaction
   configuration:
   - keep `repositoryPath` equal to the Git root;
   - set `kind` to `dotnet`;
   - for `web-application-project`, set `projects` to the repository-relative
     Web Forms `.csproj` and clear `solutions` unless project loading requires
     the solution;
   - for `projectless-web-site`, set both `projects` and `solutions` to empty
     arrays; do not substitute a sibling backend or custom-control project;
   - for `unknown`, stop with `WEBFORMS_PROJECT_SHAPE_UNKNOWN`;
   - set `include` to the repository-relative Web Forms folder followed by
     `/**`;
   - preserve any owner-required exclusions; the committed runner will also
     add its standard generated/churn exclusions.
6. Do not alter any other configured source unless a schema-valid unique label
   is required for this Web Forms scope. Do not expose the label.
7. Run the committed interaction runner in `ValidateOnly` mode using the
   corrected local configuration.
8. If validation fails, do not improvise. Return the sanitized boundary-stop
   line defined below.
9. If validation succeeds, run one new interaction review into a new output
   directory outside every source repository and outside the TraceMap
   repository.
10. Do not retry automatically if the run fails.
11. If the run completes or produces a retained failed/partial result, inspect
    only the selected Web Forms source's:
    - `scan-manifest.json`;
    - `facts.ndjson`.
12. Count, without returning identities or raw rows:
    - `FileInventoried` facts whose `kind` is `WebFormsMarkup`;
    - `FileInventoried` facts whose `kind` is `WebFormsCodeBehind`;
    - `FileInventoried` facts whose `kind` is `WebFormsDesigner`;
    - `FileInventoried` facts whose `kind` is `AspNetHandler`;
    - `FileInventoried` facts whose `kind` is `AspNetApplication`;
    - `FileInventoried` facts whose `kind` is `AspNetSiteMap`;
    - positive non-gap facts under `legacy.webforms.inventory.v1`;
    - positive `WebFormsEventBindingDeclared` facts;
    - positive `WebFormsHandlerResolved` facts;
    - positive `WebFormsEventFlowProjected` facts;
    - positive facts under `legacy.aspnet.surface.v1`;
    - positive facts under `legacy.aspnet.handler.v1`.
13. Classify `nextAction`:
    - all Web Forms and classic ASP.NET inventory counts are zero =>
      `correct-scope`;
    - Web Forms markup inventory is positive but Web Forms inventory facts are
      zero, or ASP.NET handler inventory is positive but ASP.NET handler facts
      are zero =>
      `patch-tracemap-extractor`;
    - inventory facts are positive but semantic/build coverage is reduced =>
      `restore-semantic-prerequisites`;
    - otherwise => `review-evidence`.

Return only one line:

webforms-rerun=<completed|boundary-stop>;failureCode=<categorical-or-none>;projectShape=<web-application-project|projectless-web-site|unknown>;analysisLevel=<value-or-unavailable>;buildStatus=<value-or-unavailable>;markupInventory=<count>;codeBehindInventory=<count>;designerInventory=<count>;handlerInventory=<count>;applicationInventory=<count>;siteMapInventory=<count>;webFormsInventoryFacts=<count>;eventBindings=<count>;resolvedHandlers=<count>;eventFlows=<count>;aspNetSurfaceFacts=<count>;aspNetHandlerFacts=<count>;nextAction=<review-evidence|restore-semantic-prerequisites|patch-tracemap-extractor|correct-scope>

Privacy and mutation boundary:

- Do not return or commit source labels, repository names, paths, filenames,
  project names, routes, URLs, symbols, source values, diagnostic messages,
  SQL, configuration values, credentials, connection material, logs, run IDs,
  commit SHAs, or hashes derived from private values.
- Do not quote raw JSON or NDJSON rows.
- Do not modify application source, TraceMap source, or Git metadata.
- Do not commit, push, publish, open a pull request, or use network services.
- Preserve the corrected private configuration and the new output locally for
  owner review.
- Before responding, search the proposed line for every private label, name,
  path fragment, run ID, and commit SHA encountered during the task. Remove any
  match and repeat the check.
```
