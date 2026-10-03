# Site Web Forms Guided Setup Tasks

Status: specification complete; implementation not started
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

- [ ] Re-fetch `origin/main`, recheck #803 ancestry, and record the exact
      implementation base before changing public wording.
- [ ] Add `/webforms/` under `site/src/` with visible terminal-not-GUI,
      shipped/demo, Windows-only, static-evidence, and operator-consent
      boundaries.
- [ ] Present folder/solution/C#/VB/projectless target selection and explicit
      website/project choice without implying automatic solution-wide
      onboarding.
- [ ] Present all/subset form selection, bounded `forms.txt` pause, exit code 2,
      `--continue`, saved-state revalidation, and fail-closed selection states.
- [ ] Present project build consent, external projectless ASP.NET compilation,
      `ready` attestation limits, `--add-project`, and isolated repair.
- [ ] Add stop conditions and owner-routing for changed inputs, missing tools,
      corrupt state, ambiguous targets, declined consent, failed publication,
      unverifiable provenance, and partial/unverified results.
- [ ] Link the #806 proof story only when its route exists; link the existing
      Web Forms article, native docs, evidence, gap, reduced-coverage, and
      static-vs-runtime guidance.
- [ ] Update capabilities, docs, roadmap claim ledger, legacy .NET/modernization,
      limitations, proof paths, and navigation surfaces with narrowly scoped
      Web Forms wording.
- [ ] Add `/webforms/` to `site/src/_site/pages.json` and add discovery metadata
      with explicit limitations and non-claims.
- [ ] Add a focused validator, negative tests, and general-validator wiring for
      route content, links, metadata, claim boundaries, #803 exclusion, and
      private-data/overclaim safety.
- [ ] Run focused site tests, `npm test`, `npm run validate`, and `npm run build`
      from `site/`.
- [ ] Complete and record desktop/mobile browser checks for layout, wrapping,
      focus, interactions, console output, and horizontal overflow.
- [ ] Run `./scripts/check-private-paths.sh` and `git diff --check`.
- [ ] Update this checklist and `implementation-state.md` with exact routes,
      claim levels, commit evidence, validation results, browser results, and
      remaining limitations before opening the implementation PR.
