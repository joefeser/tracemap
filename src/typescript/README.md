# TraceMap TypeScript Scanner

`tracemap-ts` scans TypeScript repositories into TraceMap artifacts:

```bash
npm install
npm run build
node dist/src/cli.js scan --repo ../../samples/typescript-modern-sample --out ../../.tracemap-ts
```

Reduction is performed by the existing .NET reducer:

```bash
dotnet run --project ../dotnet/TraceMap.Cli -- reduce \
  --index ../../.tracemap-ts/index.sqlite \
  --contract-delta ../../samples/contract-deltas/typescript-modern.status.json \
  --out ../../.tracemap-ts/impact-report.md
```

The SQLite writer uses `sql.js`, a pure JavaScript/WASM SQLite implementation. It avoids native addon build requirements while producing a normal SQLite database readable by the .NET reducer.
