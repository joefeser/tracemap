# Python Indexer Implementation State

Status: implemented MVP with post-MVP backlog open.

Branch/PR:

- Implemented across the Python adapter feature work already merged into `dev`.

Scope Implemented:

- `src/python` package and `tracemap-py scan` CLI.
- Deterministic repo metadata, inventory, manifest, facts, SQLite, and report output.
- Python AST extraction for declarations, imports, invocations, call edges, object creation, direct argument flow, aliases, inheritance relationships, and syntax boundaries.
- FastAPI and Flask route facts.
- Pydantic, dataclass, SQLAlchemy, direct SQL, config/env, package metadata, and HTTP client facts.
- Reduced coverage labeling for parse/dynamic/static-analysis gaps.
- Reducer-compatible fact shapes and downstream `.NET` command compatibility.
- Python sample repos and contract deltas under `samples/`.

Validation:

- Python tests are expected to run from a fresh temporary venv, for example:
  - `python3 -m venv /tmp/tracemap-python-venv`
  - `/tmp/tracemap-python-venv/bin/python -m pip install -e "src/python[dev]"`
  - `/tmp/tracemap-python-venv/bin/python -m pytest src/python/tests`
- Python smoke commands are documented in `docs/VALIDATION.md`.

Open Follow-Ups:

- Type-checker-backed Tier1 facts.
- Real Python `PropertyAccessed` and `MethodInvoked` facts.
- Cross-module route/include expansion and additional frameworks.
- Lockfile parsing and richer business-logic/flow facts.
- Public OSS smoke target automation beyond the committed samples.

