# Site Web Forms Guided Setup Design

Status: specification complete; implementation not started
Public claim level: shipped workflow with demo-bounded synthetic proof

## Route decision

Use `/webforms/` as the durable landing route. It is short, leaves room for the
related proof/demo/workbench routes, and gives the existing concept article a
single product-oriented destination instead of creating another article that
repeats the same evidence overview.

Planned route family:

- `/webforms/` — issue #805, terminal setup and resumability orientation;
- `/webforms/source-plus-compiled-proof/` — issue #806, readable synthetic
  source/compiled proof story;
- `/webforms/local-demo/` — issue #807, reproducible local demo and artifact
  map; and
- `/webforms/review-workbench/` — issue #808, reviewer walkthrough.

Only routes present in the implementation branch or its base may be linked.
This avoids broken placeholders while preserving the intended information
architecture.

## Evidence architecture

The page uses three visibly separate evidence layers:

| Layer | Allowed claim | Primary source | Required boundary |
| --- | --- | --- | --- |
| Main-backed workflow | `shipped` | PRs #797-#801, native workflow docs, wizard source/tests, rule catalog | Bounded terminal interface only; no #803 repair claim |
| Public synthetic proof | `demo` | checked-in fixtures and approved #806 proof projection | Static and partial where labeled; Windows publication proof stays Windows-only |
| Operator/customer state | not public evidence | private configuration, selected source/publication, local run output | Never publish, summarize as success, or use to claim compatibility |

The page-level label may say `Public claim level: shipped` because the terminal
workflow is on main. Each synthetic example or proof card must retain `demo`
wording. This avoids using a single label to imply that a demonstration proves
customer outcomes.

## Main/dev boundary

The implementation starts from main commit
`d76358f663ce40532fe0954ca9888612d51a4121`, the merge commit of promotion PR
#801. PR #803's merge commit
`af05c289a799c896862983fd9f4f73a28d882d0c` is on dev and not main. #803 changed
wizard store and related extractor, model, documentation, rule, script, and test
files. The future site page must not generalize those repairs into main-backed
claims. Before implementation or later wording refresh, recheck ancestry; only
then may the status record change.

## Page structure

1. **Hero and boundary**
   - Name the terminal guided setup.
   - Display shipped versus demo labels.
   - State that TraceMap neither launches the site nor executes SQL.
   - Offer links to the native workflow document and source-plus-compiled proof
     only when that proof route exists.
2. **Choose the website input**
   - Compare website folder, solution, C# project, VB project, and projectless
     Web Site choices.
   - Show that ambiguous solutions/folders require explicit selection.
   - State that automatic whole-solution onboarding is absent.
3. **Choose the review scope**
   - Explain all versus selected forms.
   - Model `forms.txt` as the single intended human-edited selection file.
   - Explain paused exit code 2 and `--continue` without presenting a copyable
     end-to-end customer command block.
4. **Cross the execution boundary deliberately**
   - Separate a project-backed build-consent card from the external
     projectless Windows compilation card.
   - Label classic, non-SDK, and .NET Framework project targets that require
     Windows MSBuild; `WINDOWS_BUILD_REQUIRED` is a stop on non-Windows hosts.
   - Keep tool path/version/arguments/working directory preview and the exact
     `build` consent concept visible.
   - Label `ready` as a declaration, not proof.
5. **Resume, add, or repair**
   - Contrast normal continuation, explicit project addition, and isolated
     project repair.
   - Show what is preserved and what repair cannot restore.
6. **Read the resulting static evidence**
   - Link to the #806 proof story and existing evidence/gap/limitation routes.
   - Explain rule IDs, tiers, coverage, provenance, retained reports, and
     explicit gaps without embedding private output.
7. **Stop conditions and owner handoff**
   - Surface declined consent, changed inputs, missing/blank/invalid selection,
     ambiguous target, corrupt root state, missing Windows publication,
     unverifiable provenance, failed/partial analysis, and unverified retained
     reports.
   - Route each condition to operator, repository owner, build owner, or review
     owner rather than promising automated resolution.
8. **Limitations and next routes**
   - Repeat runtime, compatibility, migration, cross-service, and publication
     non-claims.
   - Link to static-vs-runtime and reduced-coverage guidance.

## Existing page integration

The implementation should make narrow changes to existing source pages:

- `/blog/modernizing-web-forms-without-running-it/`: retain the concept
  overview and link to `/webforms/` for shipped terminal setup.
- `/capabilities/`: add a distinct bounded Web Forms terminal workflow entry;
  do not rewrite broad legacy support language.
- `/docs/`: add the public setup guide route and link the repository-native
  workflow document.
- `/roadmap/#claim-ledger`: add a main-backed Web Forms workflow row while
  keeping the broad legacy-validation row conservative.
- `/legacy-dotnet/evidence/` and `/legacy-modernization/evidence-map/`: link the
  supported workflow/proof route from the Web Forms row, but preserve hidden or
  concept status for unsupported legacy questions.
- `/limitations/`, `/limitations/reduced-coverage/`, `/proof-paths/`, and
  `/static-vs-runtime/`: add contextual links where they help the evaluator
  interpret the workflow's gaps and non-claims.
- Shared navigation/footer: add the landing route only if the existing
  navigation budget and hierarchy support it; otherwise place it under docs or
  capabilities and test discoverability.

## Public-safe content model

The landing page itself does not require a new JSON asset. If implementation
adds a derived asset, it must use a narrow allowlist and include:

- public repository and exact commit;
- schema/generator identity and exact generator SHA-256;
- bounded privacy-projected input identity and SHA-256;
- rule IDs, evidence tiers, coverage labels, extractor versions, and safe
  repository-relative spans only when those values are supported; and
- explicit gaps and limitations.

The model excludes customer paths, copied configuration, raw code/markup, raw
SQL, binary contents, connection material, private names, raw fact streams,
SQLite, analyzer logs, and local validation output.

## Metadata design

Add `/webforms/` to `site/src/_site/pages.json` with monthly change frequency
and a priority consistent with a top-level evidence lane. Add one discovery
record with:

- `sourceType: "site-page"`;
- `publicClaimLevel: "shipped"` for the main-backed workflow;
- a concise static terminal-setup summary;
- an existing proof/evidence route as `preferredProofPath` until #806 is
  present, then the #806 route;
- limitations covering bounded source/compiled evidence, Windows-required
  legacy project builds, external Windows projectless compilation, and partial
  coverage; and
- non-claims covering runtime, compatibility, migration, publication success,
  cross-service tracing, private data, and AI analysis.

The build owns sitemap output. No generated output is edited or committed.

## Validator design

Add a focused module and test, following existing route validators. It should
validate source/dist as appropriate and be called by the general validator.
The validator must check:

- canonical/OG metadata, page registry, discovery entry, sitemap, and inbound
  links;
- visible shipped/demo distinction and terminal-not-GUI language;
- website folder, solution, C#/VB, projectless, explicit selection, all/subset,
  `forms.txt`, pause, `--continue`, saved state, consent, Windows-required
  legacy builds, Windows projectless publication, add-project, repair, stop
  conditions, and owner handoff concepts;
- the exact revalidated implementation-base anchor and a branch-aware #803
  assertion: require dev-only exclusion while #803 is absent from that main
  base, but accept a later verified promotion without demanding stale copy;
- required limitations/non-claims and absence of forbidden success/runtime
  language;
- absence of private/local paths, raw source/SQL/configuration, secrets,
  connection strings, raw indexes, logs, and customer identities; and
- negative fixtures that independently remove or corrupt each key field so the
  focused validator cannot pass from incidental text.

Use semantic markers sparingly for stable tests. Validator rules should inspect
normalized visible text so tag splitting cannot bypass claim checks.

## Responsive and accessibility design

- Use existing cards/detail lists and avoid a desktop-only wide table for the
  setup sequence.
- Preserve heading order, landmark structure, keyboard focus, link labels, and
  sufficient color contrast.
- On narrow screens, selection and consent comparisons stack into one column.
- Long tokens such as commit IDs, flags, and projectless labels must wrap
  without horizontal overflow.
- Desktop and mobile review must include interaction/focus checks as well as
  screenshots or computed overflow checks; visual inspection is not proof of
  workflow execution.

## Implementation validation

Run focused validator tests first, then:

```text
cd site && npm test
cd site && npm run validate
cd site && npm run build
./scripts/check-private-paths.sh
git diff --check
```

Perform desktop and mobile browser checks for `/webforms/` and any existing
page whose layout or interaction changed. Record exact viewports, console state,
overflow results, and any intentionally deferred browser coverage in
`implementation-state.md`.
