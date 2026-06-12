# TraceMap

TraceMap is a deterministic C# repository indexer and contract-change reducer. It scans repositories into evidence-backed facts, writes queryable artifacts, and reduces contract deltas into Markdown impact reports.

Start here:

- [Product requirements](docs/PRD.md)
- [Acceptance plan](docs/ACCEPTANCE.md)
- [Decision log](docs/DECISIONS.md)
- [Build milestones](MILESTONES.md)
- [Rule catalog](rules/rule-catalog.yml)

TraceMap is not an AI impact-analysis tool. The scanner and reducer do not use LLM calls, embeddings, vector databases, or prompt-based classification.

## Commands

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo samples/modern-sample --out .tracemap
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap/index.sqlite --contract-delta samples/contract-deltas/modern-sample.customer-profile.json --out .tracemap/impact-report.md
dotnet run --project src/dotnet/TraceMap.Cli -- flow --index .tracemap/index.sqlite --symbol request --out .tracemap/flow-report.md
scripts/smoke-sample-repos.sh
```

## License

This project is licensed under the Apache License 2.0. See [LICENSE](./LICENSE).



Copyright © 2026 Joe Feser. All rights reserved.
