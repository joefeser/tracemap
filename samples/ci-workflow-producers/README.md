# ci-workflow-producers sample

A minimal public fixture for the opt-in CI workflow producer evidence lane.

`src/Sample.csproj` declares no `PackageId` and no `Version` — the project-file
lane alone cannot prove any produced package. `.github/workflows/pack.yml`
packs it with CI property overrides, so the package identity and version are
ci-defined.

Smoke:

```sh
dotnet run --project src/dotnet/TraceMap.Cli -- scan \
  --repo samples/ci-workflow-producers --out <outside-output> --index-ci-producers
```

The scan emits one `PackageProduced` fact with
`sourceKind=ci-workflow`, `manifestKind=github-workflow`,
`package=Contoso.Sample`, `version=0.1.0`, `workflowPath=.github/workflows/pack.yml`,
rule `project.file.v1`, tier `Tier2Structural`, extractor `ci-workflow/0.1.0`,
and evidence lines pointing at the `dotnet pack` step. Without the flag the
fact output is unchanged from a default scan.

Evidence contract and limitations: see `rules/rule-catalog.yml`
(`project.file.v1`) and `docs/VALIDATION.md` ("Opt-in CI workflow producer
evidence"). These facts prove a pack definition exists in CI — never a build
or a published artifact.
