# TraceMap Python Adapter

`tracemap-py` scans Python repositories into TraceMap-compatible artifacts:

```text
scan-manifest.json
facts.ndjson
index.sqlite
report.md
logs/analyzer.log
```

Install locally from source:

```bash
cd src/python
python -m pip install -e '.[dev]'
tracemap-py scan --repo ../../samples/python-fastapi-sample --out ../../.tracemap-python
```

Use the existing .NET CLI for reducer/export/combine/endpoint workflows:

```bash
dotnet run --project ../dotnet/TraceMap.Cli -- reduce \
  --index ../../.tracemap-python/index.sqlite \
  --contract-delta ../../samples/contract-deltas/python-fastapi.order-status.json \
  --out ../../.tracemap-python/impact-report.md
```

The MVP uses AST/package/config evidence only. It does not import target modules, run framework startup, execute decorators, install dependencies, or claim full semantic coverage.
