# Package Upgrade Impact Tasks

- [x] 1. Create Kiro spec artifacts for package upgrade impact. Requirements: 1-10.
- [x] 2. Add package delta parsing and validation. Requirements: 1, 4, 8.
- [x] 3. Read package-config evidence from single and combined indexes read-only. Requirements: 2, 3.
- [x] 4. Match package changes to static package evidence with selectors and caps. Requirements: 4, 5, 10.
- [x] 5. Emit deterministic Markdown and JSON reports with coverage, gaps, sources, and limitations. Requirements: 5-9.
- [x] 6. Wire `tracemap package-impact` CLI, help, and exit-code behavior. Requirements: 1, 9, 10.
- [x] 7. Update rule catalog and docs. Requirements: 5, 8.
- [x] 8. Add focused tests for single index, combined index, reduced-coverage no-match gaps, CLI validation, and unsafe version redaction. Requirements: 1-10.
- [x] 9. Validate with focused tests, full dotnet build/test, private-path guard, CLI sample smoke, and diff whitespace check.
