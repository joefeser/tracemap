# VB.NET Adapter Foundation Tasks

Issue: [#736](https://github.com/joefeser/tracemap/issues/736)

- [x] 1. Confirm the exact `origin/dev` base and document the implementation
  branch and scope decisions.
- [x] 2. Add VB inventory/classification and protect `.vb` inputs in snapshot
  capture and verification.
- [x] 3. Add the pinned Roslyn Visual Basic packages, extractor versions, and
  cataloged VB rule IDs with limitation text.
- [x] 4. Implement VB project selection and semantic loading without
  regressing C# or mixed-language solutions.
- [x] 5. Emit compiler-backed VB declarations, symbol occurrences,
  references, calls, construction, arguments, and direct relationships.
- [x] 6. Implement per-file VB syntax fallback with explicit gaps and reduced
  coverage.
- [x] 7. Merge VB results into shared artifacts with deterministic ordering and
  backing-fact integrity.
- [ ] 8. Add synthetic semantic, fallback, mixed-language, generated-source,
  determinism, snapshot-mutation, and public-safety tests.
- [ ] 9. Select and pin an open-source VB.NET smoke repository, document the
  expected commit and coverage, and validate its artifacts.
- [ ] 10. Update the rule catalog, adapter documentation, acceptance guidance,
  and `docs/VALIDATION.md` with exact commands.
- [ ] 11. Run focused and full validation, update implementation state, and
  open a PR to `dev`.

## Validation floor

```bash
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
python3 scripts/validate-adapter-artifacts.py <vb-scan-output>
./scripts/check-private-paths.sh
git diff --check
```

