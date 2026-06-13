from __future__ import annotations

from pathlib import Path

from .models import FileInventoryItem, ScanOptions
from .pathing import matches_any, relative_to

EXCLUDED_DIRS = {
    ".git",
    ".mypy_cache",
    ".pytest_cache",
    ".ruff_cache",
    "__pycache__",
    ".venv",
    "venv",
    "env",
    "site-packages",
    "build",
    "dist",
    "node_modules",
}

CONFIG_EXTENSIONS = {".env", ".ini", ".cfg", ".toml", ".yaml", ".yml", ".json"}
SQL_EXTENSIONS = {".sql"}
METADATA_NAMES = {"pyproject.toml", "setup.cfg", "setup.py", "Pipfile"}


def discover_inventory(repo: Path, options: ScanOptions) -> list[FileInventoryItem]:
    repo = repo.resolve()
    output = Path(options.output_path).resolve()
    items: list[FileInventoryItem] = []
    roots = _scope_roots(repo, options.project_paths)
    for root in roots:
        if root.is_file():
            candidates = [root]
        else:
            candidates = [path for path in root.rglob("*") if path.is_file()]
        for path in candidates:
            resolved = path.resolve()
            if output == resolved or output in resolved.parents:
                continue
            rel = relative_to(resolved, repo)
            if _excluded(resolved, repo):
                continue
            if options.include_globs and not matches_any(rel, options.include_globs):
                continue
            if options.exclude_globs and matches_any(rel, options.exclude_globs):
                continue
            kind = classify(path)
            if not kind:
                continue
            size = path.stat().st_size
            items.append(FileInventoryItem(rel, str(resolved), kind, size, size > options.max_file_byte_size))
    return sorted({item.relative_path: item for item in items}.values(), key=lambda item: item.relative_path)


def classify(path: Path) -> str | None:
    name = path.name
    suffix = path.suffix.lower()
    if suffix == ".py":
        return "PythonSource"
    if suffix == ".pyi":
        return "PythonStub"
    if name in METADATA_NAMES or name.startswith("requirements") and name.endswith(".txt"):
        return "PythonMetadata"
    if suffix in SQL_EXTENSIONS:
        return "SqlFile"
    if suffix in CONFIG_EXTENSIONS or name == ".env":
        return "ConfigFile"
    return None


def create_scan_id(repo_identity: str, commit_sha: str, inventory: list[FileInventoryItem], hash_fn) -> str:
    signature = "\n".join(f"{item.relative_path}|{item.kind}|{item.size_bytes}" for item in inventory)
    return "scan-" + hash_fn(f"{repo_identity}|{commit_sha}|{signature}", 20)


def package_roots(repo: Path, inventory: list[FileInventoryItem]) -> list[Path]:
    roots: set[Path] = set()
    src = repo / "src"
    if src.is_dir():
        roots.add(src.resolve())
    for item in inventory:
        path = repo / item.relative_path
        if path.name == "__init__.py":
            roots.add(path.parent.resolve())
    return sorted(roots or {repo.resolve()}, key=lambda p: str(p))


def module_name(path: Path, repo: Path, roots: list[Path]) -> str:
    resolved = path.resolve()
    for root in sorted(roots, key=lambda p: len(str(p)), reverse=True):
        try:
            rel = resolved.relative_to(root)
        except ValueError:
            continue
        parts = list(rel.with_suffix("").parts)
        if parts and parts[-1] == "__init__":
            parts = parts[:-1]
        return ".".join(parts)
    rel = resolved.relative_to(repo.resolve()).with_suffix("")
    parts = [part for part in rel.parts if part != "__init__"]
    return ".".join(parts)


def _scope_roots(repo: Path, project_paths: list[str]) -> list[Path]:
    if not project_paths:
        return [repo]
    roots = []
    for value in project_paths:
        path = Path(value)
        if not path.is_absolute():
            path = repo / path
        roots.append(path.resolve())
    return roots


def _excluded(path: Path, repo: Path) -> bool:
    try:
        rel_parts = path.relative_to(repo).parts
    except ValueError:
        return True
    return any(part in EXCLUDED_DIRS for part in rel_parts)
