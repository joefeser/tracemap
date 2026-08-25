# Implementation state

- Branch: `codex/webforms-static-composition`
- Base: `origin/dev` at `ec12d179c4000a272e84c39b7cdc436321040929`
- Merge base: `ec12d179c4000a272e84c39b7cdc436321040929`
- Tracking: #708, part of #651
- Bookkeeping: #706 closed as completed by #707; #651 completed child-story checkboxes refreshed and #708 added as the next composition slice.
- Scope decision: reuse the existing Web Forms markup/code-behind pass and identities; add no MSBuild prerequisite, runtime execution, browser rendering, or downstream packet redesign.
- Privacy decision: script payloads, postback targets, and binding field arguments are represented only by length/hash plus categorical shape. Existing safe control identities may be referenced when exactly resolved on one surface.
- Implemented: four new rule-backed candidate families cover bounded `!IsPostBack` lifecycle context, literal client-script registration, literal `__doPostBack` targets, exact same-surface `DataSourceID`, and literal `Eval`/`Bind` expressions. Unsupported and ambiguous shapes fail closed with owning-rule gaps.
- Preservation: the end-to-end fixture is a deliberately non-compiling, non-SDK .NET Framework 4.5 Web Application and asserts that existing markup-event, handler-resolution, `.ashx`, redirect/navigation, and reduced-analysis evidence remains available.
- Privacy: client-script bodies, postback targets, and binding-field literals are never persisted; only length, digest, categorical shape, and already-supported control identities are emitted.
- Validation: 39/39 focused `LegacyWebFormsExtractorTests` passed; the focused rule/report/Web Forms filter passed; the 3/3 adversarial Web Forms fixtures passed; the full solution built with 0 warnings/errors and all 1,694 tests passed; 13/13 legacy-codebase validation tests passed; the private-path guard and `git diff --check` passed. `dotnet format --verify-no-changes` continues to report the existing 254-whitespace-warning repository baseline, with no findings in files changed by this slice.
- Deferred: full legacy workspace reconstruction, dynamic JavaScript/postback analysis, runtime lifecycle behavior, rendered control trees, and Classic ASP.
