# Python Depth Pass Requirements

## Purpose

Improve the Python adapter after the MVP so it can provide better static dependency evidence for endpoint alignment and value-flow review while staying deterministic and reduced-coverage honest.

## Requirements

### 1. Import Alias Resolution

- Resolve common local aliases for imports used by integration extractors.
- Cover `import requests as r`, `import httpx as h`, `from httpx import get`, `from os import getenv`, `from os import environ`, `from fastapi import APIRouter`, and SQLAlchemy column helpers.
- Do not import target code or inspect installed packages.

### 2. FastAPI Router Prefix Composition

- Discover local `APIRouter(prefix=...)` variables.
- Discover static `include_router(router, prefix=...)` calls.
- Compose include prefixes with router prefixes when the router variable can be resolved through same-file or imported-symbol evidence.
- Emit lower-tier route evidence when prefix composition cannot be proven.

### 3. Python Flow Rows

- Emit `FieldAlias` facts for direct assignments from parameters or simple names into `self.<field>`.
- Populate `field_aliases` rows from Python `FieldAlias` facts.
- Populate `parameter_forward_edges` rows from direct Python `ArgumentPassed` parameter-forwarding facts.
- Keep these facts Tier3 unless a future type checker provides stronger symbol evidence.

### 4. Dynamic Boundary Labels

- Preserve `AnalysisGap` facts for parse failures, dynamic SQL, dynamic config keys, and other runtime-only constructs.
- Do not infer runtime DI, decorator side effects, route mutation, importlib targets, monkey patches, or generated members.

## Acceptance

- Python unit tests cover alias imports, composed FastAPI route prefixes, field aliases, and parameter-forward rows.
- Existing local Python samples still scan and reduce successfully.
- Shared .NET reducer/export/combine can read the Python index.
- `dotnet build`, `dotnet test`, Python tests, private-path guard, and diff checks pass.
