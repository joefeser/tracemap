# Site Web Forms Guided Setup Requirements

Status: specification complete; implementation not started
Readiness: ready for site implementation after this spec is accepted
Public claim level: shipped workflow with demo-bounded synthetic proof

## Objective

Implement issue #805 as a public Web Forms landing and guided-setup story at
`/webforms/`. The page shall help an evaluator understand the inputs, choices,
pause/resume behavior, consent boundary, repair path, evidence produced, and
limits of TraceMap's native terminal workflow before operating it.

The page describes deterministic static review setup. It is not a browser-based
or native GUI wizard, a customer onboarding service, or evidence that a private
application was compiled, launched, traced, migrated, or proven compatible.

## Current delivery boundary

- PRs #797, #798, #799, #800 and promotion PR #801 are merged to `main` at
  `d76358f663ce40532fe0954ca9888612d51a4121`.
- The public implementation anchors are
  `docs/WEBFORMS_NATIVE_WORKFLOW.md`,
  `.kiro/specs/webforms-resumable-wizard/acceptance-audit.md`, the wizard source
  and tests, the rule catalog, and checked-in synthetic fixtures.
- PR #803 (`af05c289a799c896862983fd9f4f73a28d882d0c`) is merged to `dev` but is
  not an ancestor of the verified `main` head. Its repair changes SHALL NOT be
  presented as shipped on `main` until a later promotion is verified.
- Main-backed capability wording may use `shipped` for the bounded terminal
  workflow. Reproducible fixture examples may use `demo`. Customer, runtime,
  unpublished, or dev-only evidence remains outside both labels.

## Requirements

### 1. Landing route and audience

1. The implementation SHALL add `/webforms/` using `site/src/` as the source
   of truth and the existing site layout and visual system.
2. The route SHALL answer: what an evaluator selects, where the workflow
   pauses, what is saved, what can execute, how a failed project is repaired,
   what evidence remains static, and when an operator must stop.
3. The page SHALL be understandable without copying a complete runnable command
   sequence. Links to the native workflow documentation may carry command-level
   detail.
4. The page SHALL visibly state the public claim level and separate shipped
   terminal workflow claims from demo-bounded synthetic proof.

### 2. Guided terminal setup

1. The page SHALL identify the delivered interface as a terminal wizard backed
   by shared persisted wizard/state implementation. It SHALL NOT call it a GUI,
   native desktop wizard, web setup screen, or automatic onboarding flow.
2. The setup story SHALL cover selection of a website folder, solution,
   explicit C# or VB project, and projectless Web Site mode.
3. Solution and folder discovery SHALL be described as requiring an explicit
   website/project selection where ambiguity exists. It SHALL NOT imply that
   every solution project is scanned or registered automatically.
4. The page SHALL explain `all` versus selected forms, the bounded `forms.txt`
   editing pause, exit code 2 as paused state, and `--continue` as resume from
   saved configuration. Empty selection SHALL NOT be described as all forms.
5. Saved configuration SHALL be framed as private operator state that is
   revalidated on resume, not as public evidence or proof of success.

### 3. Consent, publication, continuation, and repair

1. Project-backed builds SHALL be described as an explicit consent boundary:
   the wizard shows a trusted absolute tool path, version, arguments, and
   working directory before the operator types `build`.
2. Copy SHALL state that MSBuild tasks may execute code, restore dependencies,
   and change build folders or source-controlled content. Declining build
   consent executes no build.
3. Projectless ASP.NET compilation SHALL remain an external Windows operator
   step. Typing `ready` is an operator declaration, not authenticated compiler
   provenance and not proof that publication succeeded.
4. The page SHALL distinguish `--continue`, `--add-project`, and
   `--repair-project`. Isolated repair SHALL be described as resetting one
   selected project's setup state while preserving the other projects and
   historical state described by the documented contract.
5. Copy SHALL state that repair does not fix customer code, corrupt root
   configuration, missing tools, source acquisition, or lost provenance.

### 4. Static evidence and proof boundaries

1. Every capability claim SHALL link to exact main-backed implementation or
   documentation and to an appropriate public-safe proof path.
2. The page SHALL carry rule IDs, evidence tiers, coverage labels, commit and
   extractor/generator context only through public-safe, allowlisted evidence.
3. A checked-in proof asset from issue #806 may support a proof card or outbound
   link, but #805 SHALL NOT depend on private source, raw indexes, customer
   screenshots, local validation folders, or unpublished artifacts.
4. Any new derived machine-readable asset introduced while implementing #805
   SHALL record the exact generator SHA-256 and a SHA-256 of its bounded,
   privacy-projected input. It SHALL NOT hash a private source artifact for a
   shareable public identity.
5. Windows publication validation SHALL be labeled Windows-only. A macOS/Linux
   static or test pass SHALL NOT be presented as satisfying that gate.
6. Partial, reduced, unresolved, unsupported, paused, declined, failed, and
   external-step states SHALL remain distinct from completed static report
   verification.

### 5. Required limitations and non-claims

The route, discovery entry, claim ledger entry, and focused validator SHALL
preserve these boundaries:

- no runtime page execution, page launch, event firing, branch feasibility,
  database or SQL execution, service reachability, production usage, deployment
  state, migration parity, release approval, or safety claim;
- no automatic solution-wide onboarding, arbitrary source-server or PDB
  acquisition, external publication, dependency discovery, customer
  compatibility guarantee, or cross-service tracing claim;
- no claim that selected publication came from the declared source merely
  because an operator attested it or a hash matched later;
- no private source, raw snippets, raw SQL, configuration values, credentials,
  connection material, local paths, private identities, raw SQLite/fact streams,
  analyzer output, or customer screenshots; and
- no LLM calls, embeddings, vector databases, or prompt-based classification.

### 6. Existing surface integration

1. `/webforms/` SHALL become the Web Forms orientation route rather than
   duplicating the concept article at
   `/blog/modernizing-web-forms-without-running-it/`.
2. The concept article SHALL link forward to the shipped setup route, while the
   setup route links back for the static-evidence overview.
3. The route SHALL link to the source-plus-compiled proof story from #806 and
   reserve clear onward paths for #807's local demo and #808's review
   workbench. Missing downstream routes SHALL not be linked until they exist in
   the same branch or base.
4. Implementation SHALL update the relevant capability, docs, limitations,
   proof-path, legacy .NET/modernization, roadmap claim-ledger, and navigation
   surfaces without upgrading all legacy .NET rows as a group.
5. The broad hidden/concept legacy rows SHALL remain conservative. A distinct
   Web Forms terminal workflow row may move to shipped only with the exact
   main-backed proof and limitations recorded here.

### 7. Discovery, metadata, and validation

1. Implementation SHALL register `/webforms/` in `site/src/_site/pages.json`
   and `site/src/_site/discovery.json`; generated sitemap and navigation output
   SHALL come from the build rather than manual edits to `site/dist` or
   `site/output`.
2. Discovery metadata SHALL include explicit limitations and non-claims,
   Windows-only publication scope, the terminal-not-GUI distinction, and the
   external projectless compilation boundary.
3. A focused validator and negative tests SHALL enforce the route, metadata,
   required concepts, link targets, claim labels, main/dev boundary, private
   data exclusions, and forbidden overclaims.
4. Implementation validation SHALL include `cd site && npm run build`,
   `cd site && npm test`, `cd site && npm run validate`, focused validator
   tests, `./scripts/check-private-paths.sh`, and `git diff --check`.
5. Desktop and mobile browser checks SHALL cover the landing route, long setup
   labels, consent/stop callouts, links, focus states, wrapping, and horizontal
   overflow. The implementation record SHALL state the tested viewport sizes
   and observed results.

## Acceptance criteria

- `/webforms/` presents the exact bounded terminal workflow delivered on main.
- Website/solution/project/projectless selection, all/subset forms,
  `forms.txt`, `--continue`, saved configuration, build consent, external
  Windows compilation, `--add-project`, and isolated repair are visible.
- #803-only repairs are not claimed as main-backed behavior.
- Public claims are traceable to exact implementation/docs and synthetic proof;
  gaps and stop conditions are at least as prominent as the happy path.
- The claim ledger distinguishes this bounded shipped workflow from broader
  hidden/concept legacy evidence.
- Focused/full validation and desktop/mobile review pass without manually
  editing generated output.

## Dependencies

- Required implementation evidence: PRs #797-#801 on `main`.
- First-wave companion: #806, which supplies the source-plus-compiled public
  proof story used by later routes.
- Downstream consumers: #807, #808, and #809.
- Dev-only repair boundary: #803, excluded from shipped claims until promoted.
