# TraceMap

TraceMap is a deterministic C# repository indexer and contract-change reducer. It scans repositories into evidence-backed facts, writes queryable artifacts, and reduces contract deltas into Markdown impact reports.

The repository also includes an MVP deterministic TypeScript scanner under `src/typescript`. It emits the same TraceMap artifacts and writes an `index.sqlite` that the existing .NET reducer can read.

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
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/index-export.json --format json
dotnet run --project src/dotnet/TraceMap.Cli -- export --index .tracemap/index.sqlite --out .tracemap/relationships.mmd --format mermaid
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints --client-index .tracemap-client/index.sqlite --server-index .tracemap-server/index.sqlite --client-label app-client --server-label app-api --out .tracemap-endpoints
scripts/smoke-sample-repos.sh
```

TypeScript scanner:

```bash
cd src/typescript
npm install
npm run build
node dist/src/cli.js scan --repo ../../samples/typescript-modern-sample --out ../../.tracemap-ts
node dist/src/cli.js export --index ../../.tracemap-ts/index.sqlite --out ../../.tracemap-ts/index-export.json --format json
node dist/src/cli.js export --index ../../.tracemap-ts/index.sqlite --out ../../.tracemap-ts/relationships.mmd --format mermaid
cd ../..
dotnet run --project src/dotnet/TraceMap.Cli -- reduce --index .tracemap-ts/index.sqlite --contract-delta samples/contract-deltas/typescript-modern.status.json --out .tracemap-ts/impact-report.md
```

Endpoint alignment compares two existing indexes instead of scanning multiple apps in one command:

```bash
node src/typescript/dist/src/cli.js scan --repo /path/to/App.Api/ClientApp --out /tmp/app-client
dotnet run --project src/dotnet/TraceMap.Cli -- scan --repo /path/to/App --project App.Api/App.Api.csproj --out /tmp/app-server
dotnet run --project src/dotnet/TraceMap.Cli -- endpoints --client-index /tmp/app-client/index.sqlite --server-index /tmp/app-server/index.sqlite --client-label app-client --server-label app-api --out /tmp/app-endpoints
```

Endpoint findings are static, coverage-relative evidence. A client-only row is not proof of a broken call, and a server-only row is not proof of dead code.

## License

This project is licensed under the Apache License 2.0. See [LICENSE](./LICENSE).



Copyright © 2026 Joe Feser. All rights reserved.
