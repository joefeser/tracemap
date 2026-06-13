# Python Depth Pass Design

## Overview

The adapter remains a reduced-coverage static scanner. The depth pass adds a bounded file prepass before per-file AST extraction:

1. Parse all selected Python files.
2. Build per-module import alias maps.
3. Collect local FastAPI router variables and static include-router prefixes.
4. Feed resolved router prefixes and aliases into the normal AST visitor.

No project code is imported, no decorators execute, and no dependencies are installed.

## Import Aliases

Each visitor receives a map from local names to qualified static names:

- `r -> requests`
- `client -> httpx`
- `http_get -> httpx.get`
- `getenv -> os.getenv`
- `environ -> os.environ`
- `APIRouter -> fastapi.APIRouter`
- `mapped_column -> sqlalchemy.orm.mapped_column`

Call and attribute classification resolves the first name segment through this map before matching integration rules.

## FastAPI Prefixes

The prepass records router symbols as `<module>.<variable>` with their local prefix. It also records static include prefixes keyed by the resolved router symbol. During extraction, each file maps local router variables to the composed prefix:

```text
include prefix + router prefix + decorator path
```

If a router cannot be resolved across modules, the extractor keeps the local router prefix only.

## Flow Tables

Python emits:

- `FieldAlias` when `self.field = parameter_or_name` is directly visible.
- `ArgumentPassed` when a current function parameter is directly passed by name.

The SQLite writer inserts `field_aliases` rows from `FieldAlias` facts and derives `parameter_forward_edges` rows from direct `ArgumentPassed` facts. These rows support flow/export/combine queries while preserving Tier3 limitations.

## Limitations

- No type checker, interprocedural alias analysis, mutation modeling, collection-content tracking, branch feasibility, async/callback scheduling, or runtime route registration is claimed.
- Prefix composition handles static names and direct imports only.
- Parameter names for Python call targets are ordinal placeholders unless statically known in the call expression.
