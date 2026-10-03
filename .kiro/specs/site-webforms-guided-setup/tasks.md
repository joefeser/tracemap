# Site Web Forms Guided Setup Tasks

Status: implementation validated; commit, PR, and ACK pending
Public claim level: shipped workflow with demo-bounded synthetic proof

## Completed specification work

- [x] Read issue #805 and record its scope, acceptance criteria, dependencies,
      and claim boundary.
- [x] Verify promotion PR #801 at main commit
      `d76358f663ce40532fe0954ca9888612d51a4121`.
- [x] Verify that PR #803 merge commit
      `af05c289a799c896862983fd9f4f73a28d882d0c` is on dev but not main and
      exclude those repairs from shipped claims.
- [x] Inspect the native workflow documentation, wizard acceptance audit,
      implementation/test anchors, existing Web Forms articles, legacy lanes,
      claim ledger, discovery metadata, page registry, and validator patterns.
- [x] Select `/webforms/` as the landing route and document its relationship to
      #806-#808.
- [x] Define shipped/demo claim separation, safety boundaries, metadata,
      validation, and responsive review requirements.

## Future implementation work

- [x] Re-fetch `origin/main`, recheck #803 ancestry, and record the exact
      implementation base before changing public wording.
- [x] Add `/webforms/` under `site/src/` with visible terminal-not-GUI,
      shipped/demo, Windows-only, static-evidence, and operator-consent
      boundaries.
- [x] Present folder/solution/C#/VB/projectless target selection and explicit
      website/project choice without implying automatic solution-wide
      onboarding.
- [x] Present all/subset form selection, bounded `forms.txt` pause, exit code 2,
      `--continue`, saved-state revalidation, and fail-closed selection states.
- [x] Present project build consent, external projectless ASP.NET compilation,
      the Windows-MSBuild requirement for classic/non-SDK/.NET Framework
      projects, `ready` attestation limits, `--add-project`, and isolated
      repair.
- [x] Add stop conditions and owner-routing for changed inputs, missing tools,
      corrupt state, ambiguous targets, declined consent,
      `WINDOWS_BUILD_REQUIRED`, failed publication, unverifiable provenance,
      and partial/unverified results.
- [x] Link the #806 proof story only when its route exists; link the existing
      Web Forms article, native docs, evidence, gap, reduced-coverage, and
      static-vs-runtime guidance.
- [x] Update capabilities, docs, roadmap claim ledger, legacy .NET/modernization,
      limitations, proof paths, and navigation surfaces with narrowly scoped
      Web Forms wording.
- [x] Add `/webforms/` to `site/src/_site/pages.json` and add discovery metadata
      with explicit limitations and non-claims.
- [x] Add a focused validator, negative tests, and general-validator wiring for
      route content, links, metadata, claim boundaries, branch-aware #803
      status, and private-data/overclaim safety.
- [x] Run focused site tests, `npm test`, `npm run validate`, and `npm run build`
      from `site/`.
- [x] Complete and record desktop/mobile browser checks for layout, wrapping,
      focus, interactions, console output, and horizontal overflow.
- [x] Run `./scripts/check-private-paths.sh` and `git diff --check`.
- [x] Update this checklist and `implementation-state.md` with exact routes,
      claim levels, commit evidence, validation results, browser results, and
      remaining limitations before opening the implementation PR.
