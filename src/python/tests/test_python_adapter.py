from __future__ import annotations

import json
import sqlite3
from pathlib import Path

import pytest

from tracemap_py.ast_extractor import extract_python_files
from tracemap_py.engine import make_options, scan
from tracemap_py.fact_factory import create_fact
from tracemap_py.config_extractor import extract_config_files
from tracemap_py.git_metadata import read_git_metadata
from tracemap_py.hashes import sha256_hex
from tracemap_py.inventory import discover_inventory
from tracemap_py.metadata import read_package_metadata
from tracemap_py.models import EvidenceSpan, ScanManifest
from tracemap_py.route import normalize_path_key
from tracemap_py.writers import write_sqlite


ROOT = Path(__file__).resolve().parents[3]


def test_hashes_and_route_keys_are_deterministic() -> None:
    assert sha256_hex("SELECT 1", 12) == "e004ebd5b553"
    assert normalize_path_key("/API/orders/{order_id}?expand=true") == ("/API/orders/{order_id}", "/api/orders/{}")
    assert normalize_path_key("https://example.test/api/status/<status>") == ("/api/status/{status}", "/api/status/{}")


def test_fact_id_ignores_extractor_version() -> None:
    manifest = _manifest("stable")
    first = create_fact(
        manifest,
        "InvocationName",
        "python.ast.invocation.v1",
        "Tier3SyntaxOrTextual",
        EvidenceSpan("app/main.py", 10, 10, None, "PythonAstExtractor", "python-ast/0.1.0"),
        source_symbol="app.main",
        target_symbol="helper",
        contract_element="helper",
        properties={"methodName": "helper"},
    )
    second = create_fact(
        manifest,
        "InvocationName",
        "python.ast.invocation.v1",
        "Tier3SyntaxOrTextual",
        EvidenceSpan("app/main.py", 10, 10, None, "PythonAstExtractor", "python-ast/0.2.0"),
        source_symbol="app.main",
        target_symbol="helper",
        contract_element="helper",
        properties={"methodName": "helper"},
    )
    assert first.fact_id == second.fact_id


def test_fastapi_sample_emits_integration_and_relationship_tables(tmp_path: Path) -> None:
    out = tmp_path / "fastapi"
    manifest, facts = scan(make_options(str(ROOT / "samples/python-fastapi-sample"), str(out)))

    assert manifest.analysis_level == "Level1SemanticAnalysisReduced"
    assert manifest.build_status == "FailedOrPartial"
    assert {path.name for path in out.iterdir()} >= {"scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs"}

    by_type = _fact_counts(out / "index.sqlite")
    assert by_type["HttpRouteBinding"] >= 1
    assert by_type["HttpCallDetected"] >= 1
    assert by_type["SerializerContractMember"] >= 1
    assert by_type["DatabaseColumnMapping"] >= 1
    assert by_type["SqlTextUsed"] >= 2
    assert by_type["ConfigKeyDeclared"] >= 2
    assert by_type["CallEdge"] >= 1
    assert by_type["ObjectCreated"] >= 1
    assert by_type["ArgumentPassed"] >= 1
    assert by_type["FieldAlias"] >= 1
    assert by_type["SymbolRelationship"] >= 1

    con = sqlite3.connect(out / "index.sqlite")
    try:
        assert _scalar(con, "select count(*) from call_edges") >= 1
        assert _scalar(con, "select count(*) from object_creations") >= 1
        assert _scalar(con, "select count(*) from argument_flows") >= 1
        assert _scalar(con, "select count(*) from field_aliases") >= 1
        assert _scalar(con, "select count(*) from parameter_forward_edges") >= 1
        assert _scalar(con, "select count(*) from symbol_relationships") >= 1
        assert _scalar(con, "select count(*) from symbols where language = 'python'") >= 1
        route = con.execute("select properties_json from facts where fact_type='HttpRouteBinding' order by fact_id limit 1").fetchone()[0]
        route_props = json.loads(route)
        assert route_props["normalizedPathKey"] == "/api/orders/{}"
        assert _scalar(con, "select count(*) from facts where fact_type='HttpCallDetected' and target_symbol='requests.post'") == 1
        assert _scalar(con, "select count(*) from facts where fact_type='HttpCallDetected' and target_symbol='httpx.get'") == 1
    finally:
        con.close()

    assert any(fact.evidence.extractor_version for fact in facts)


def test_flask_sample_emits_route_config_and_dynamic_boundaries(tmp_path: Path) -> None:
    out = tmp_path / "flask"
    scan(make_options(str(ROOT / "samples/python-flask-sample"), str(out)))

    by_type = _fact_counts(out / "index.sqlite")
    assert by_type["HttpRouteBinding"] >= 1
    assert by_type["HttpCallDetected"] >= 1
    assert by_type["ConfigKeyDeclared"] >= 1
    assert by_type["SqlTextUsed"] >= 1


def test_broken_sample_records_parse_and_dynamic_gaps(tmp_path: Path) -> None:
    out = tmp_path / "broken"
    manifest, _ = scan(make_options(str(ROOT / "samples/python-broken-sample"), str(out)))

    assert manifest.analysis_level == "Level1SemanticAnalysisReduced"
    assert manifest.build_status == "FailedOrPartial"
    assert any(gap.startswith("PythonParseFailed") for gap in manifest.known_gaps)
    by_type = _fact_counts(out / "index.sqlite")
    assert by_type["AnalysisGap"] >= 1
    assert by_type["MethodDeclared"] >= 1


def test_scans_outside_git_checkout_fail(tmp_path: Path) -> None:
    repo = tmp_path / "not-git"
    repo.mkdir()
    (repo / "app.py").write_text("print('hello')\n", encoding="utf-8")

    with pytest.raises(RuntimeError):
        read_git_metadata(repo)


def test_scan_rejects_destructive_output_paths() -> None:
    repo = ROOT / "samples/python-fastapi-sample"

    with pytest.raises(ValueError):
        scan(make_options(str(repo), str(repo)))

    with pytest.raises(ValueError):
        scan(make_options(str(repo), str(repo.parent)))


def test_inventory_skips_project_paths_outside_repo(tmp_path: Path) -> None:
    repo = ROOT / "samples/python-fastapi-sample"
    outside = tmp_path / "outside.py"
    outside.write_text("print('outside')\n", encoding="utf-8")

    inventory = discover_inventory(repo, make_options(str(repo), str(tmp_path / "out"), project=[str(outside)]))

    assert inventory == []


def test_config_toml_uses_structured_key_paths(tmp_path: Path) -> None:
    config = tmp_path / "config.toml"
    config.write_text("[tool.poetry]\nname = \"demo\"\n", encoding="utf-8")
    manifest = _manifest("toml")
    gaps: list[str] = []

    facts = extract_config_files(tmp_path, manifest, [config], gaps)

    key_paths = {fact.properties.get("keyPath") for fact in facts if fact.fact_type == "ConfigKeyDeclared"}
    assert "tool.poetry.name" in key_paths
    assert "name" not in key_paths
    assert gaps == []


def test_requirements_options_are_not_packages(tmp_path: Path) -> None:
    req = tmp_path / "requirements.txt"
    req.write_text("-r base.txt\n-c constraints.txt\nrequests==2.32.0\n", encoding="utf-8")
    manifest = _manifest("requirements")
    gaps: list[str] = []

    deps, facts = read_package_metadata(tmp_path, manifest, [req], gaps)

    assert deps == {"requests": "==2.32.0"}
    assert all(fact.target_symbol not in {"-r", "-c"} for fact in facts)
    assert gaps == []


def test_annotated_sqlalchemy_tablename_is_detected(tmp_path: Path) -> None:
    repo = tmp_path / "repo"
    repo.mkdir()
    model_file = repo / "models.py"
    model_file.write_text(
        "from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column\n\n"
        "class Base(DeclarativeBase):\n"
        "    pass\n\n"
        "class Order(Base):\n"
        "    __tablename__: str = \"orders\"\n"
        "    status: Mapped[str] = mapped_column()\n",
        encoding="utf-8",
    )
    gaps: list[str] = []

    facts = extract_python_files(repo, _manifest("sqlalchemy-annassign"), [model_file], [repo], {"sqlalchemy": "2.0.0"}, gaps)

    assert any(fact.fact_type == "DatabaseColumnMapping" and fact.properties.get("tableName") == "orders" for fact in facts)
    assert gaps == []


def test_sqlite_symbol_rows_follow_role_properties(tmp_path: Path) -> None:
    index = tmp_path / "index.sqlite"
    manifest = _manifest("sqlite-symbols")
    fact = create_fact(
        manifest,
        "CallEdge",
        "python.ast.callgraph.v1",
        "Tier3SyntaxOrTextual",
        EvidenceSpan("app/main.py", 3, 3, None, "PythonAstExtractor", "python-ast/0.1.0"),
        source_symbol="app.main.handler",
        target_symbol="app.service.call",
        properties={
            "callKind": "Invocation",
            "sourceSymbolId": "py:function:app.main.handler",
            "sourceSymbolLanguage": "python",
            "sourceSymbolKind": "function",
            "sourceSymbolDisplayName": "app.main.handler",
            "targetSymbolId": "py:function:app.service.call",
            "targetSymbolLanguage": "python",
            "targetSymbolKind": "function",
            "targetSymbolDisplayName": "app.service.call",
        },
    )
    write_sqlite(index, manifest, [fact])

    con = sqlite3.connect(index)
    try:
        assert _scalar(con, "select count(*) from symbols where language='python'") == 2
        assert _scalar(con, "select count(*) from call_edges") == 1
    finally:
        con.close()


def _manifest(scan_id: str) -> ScanManifest:
    return ScanManifest(scan_id, "repo", None, "main", "0" * 40, "python-adapter/0.1.0", "2026-06-13T00:00:00+00:00", "Level1SemanticAnalysisReduced", "FailedOrPartial", [], [], ["python"], [])


def _fact_counts(index: Path) -> dict[str, int]:
    con = sqlite3.connect(index)
    try:
        return dict(con.execute("select fact_type, count(*) from facts group by fact_type").fetchall())
    finally:
        con.close()


def _scalar(con: sqlite3.Connection, sql: str) -> int:
    return int(con.execute(sql).fetchone()[0])
