#!/usr/bin/env python3
"""Offline table-backed DDL result-shape native smoke over public synthetic C#/VB scans.

Never invokes fixture methods or SQL. Reuses the #912 result smoke helpers without changing that
smoke's bytes, so the retained result receipts and denominators stay directly comparable.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
SPEC = importlib.util.spec_from_file_location("result_smoke", ROOT / "scripts/sqlserver-result-column-evidence/smoke.py")
RESULT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RESULT)
HELPERS = RESULT.HELPERS
SAMPLE = RESULT.SAMPLE
TEMPLATES = SAMPLE / "ddl-table"
M, X, U = RESULT.M, RESULT.X, RESULT.U
INPUT_VERSION = "sqlserver-ddl-table-result-input/1"
ORIGIN = "public-synthetic-tsql-ddl-table-result"
RULE = "database.sqlserver.ddl-table-result-shape.v1"

# Replay: the 34 #912 execution sites per language against table-backed templates. The base shape
# (Id, Name, Total from dbo.Items, which also has Price and ItemCode) equals the JSON base shape, so
# the independently authored #912 per-case expectations apply unchanged.
REPLAY_TEMPLATES = {"Renamed": "renamed.sql", "Reordered": "reordered.sql", "DuplicateDeclared": "duplicate.sql", "Incomplete": "if.sql"}

# Additional cases, authored before any extractor output: (source case, procedure template, table file
# or None, expected statuses of the selected execution, expected ReadItems shape reason or "complete").
EXTRA_CASES = (
    ("NameMatch", "alias-table.sql", "tables.sql", [M], "complete"),
    ("NameMatch", "alias-output.sql", "tables.sql", [M], "complete"),
    ("NameMatch", "bracketed.sql", "tables.sql", [M], "complete"),
    ("OrdinalOutOfRange", "wide.sql", "tables.sql", [M], "complete"),
    ("NameMatch", "base.sql", None, [U], "table-dependency-missing"),
    ("NameMatch", "view.sql", "tables.sql", [U], "table-dependency-missing"),
    ("NameMatch", "missing-column.sql", "tables.sql", [U], "column-dependency-missing"),
    ("NameMatch", "wrong-schema.sql", "tables.sql", [U], "table-dependency-missing"),
    ("NameMatch", "wrong-database.sql", "tables.sql", [U], "cross-database-or-server-reference-unsupported"),
    ("NameMatch", "unqualified.sql", "tables.sql", [U], "table-reference-schema-required"),
    ("NameMatch", "case-table.sql", "tables.sql", [U], "table-reference-case-policy-unknown"),
    ("NameMatch", "case-column.sql", "tables.sql", [U], "column-case-policy-unknown"),
    ("NameMatch", "case-output.sql", "tables.sql", [U], "duplicate-or-case-only-output-name-unsupported"),
    ("NameMatch", "qualifier-unbound.sql", "tables.sql", [U], "column-qualifier-unbound"),
    ("NameMatch", "qualifier-no-alias.sql", "tables.sql", [U], "column-qualifier-without-alias-unsupported"),
    ("NameMatch", "star.sql", "tables.sql", [U], "select-star-unsupported"),
    ("NameMatch", "join.sql", "tables.sql", [U], "join-unsupported"),
    ("NameMatch", "cte.sql", "tables.sql", [U], "cte-unsupported"),
    ("NameMatch", "subquery.sql", "tables.sql", [U], "subquery-unsupported"),
    ("NameMatch", "union.sql", "tables.sql", [U], "set-operation-unsupported"),
    ("NameMatch", "temp.sql", "tables.sql", [U], "temporary-table-unsupported"),
    ("NameMatch", "dynamic.sql", "tables.sql", [U], "exec-or-dynamic-sql-unsupported"),
    ("NameMatch", "exec.sql", "tables.sql", [U], "exec-or-dynamic-sql-unsupported"),
    ("NameMatch", "if.sql", "tables.sql", [U], "conditional-body-unsupported"),
    ("NameMatch", "multiple.sql", "tables.sql", [U], "body-statement-count-unsupported"),
    ("NameMatch", "where.sql", "tables.sql", [U], "select-clause-unsupported"),
    ("NameMatch", "expression.sql", "tables.sql", [U], "non-column-projection-unsupported"),
    ("NameMatch", "hint.sql", "tables.sql", [U], "table-hint-unsupported"),
    ("NameMatch", "base.sql", "tables-computed.sql", [U], "table-definition-unsupported"),
    ("NameMatch", "base.sql", "tables-case.sql", [U], "table-definition-unsupported"),
)
REPLAY_SHAPES = {"base.sql": "complete", "renamed.sql": "complete", "reordered.sql": "complete",
                 "duplicate.sql": "duplicate-or-case-only-output-name-unsupported", "if.sql": "conditional-body-unsupported"}
# Independently authored output names and backing table columns for the complete ReadItems shapes.
EXPECTED_PROJECTIONS = {
    "base.sql": (["Id", "Name", "Total"], ["Id", "Name", "Total"]),
    "renamed.sql": (["Id", "DisplayName", "Total"], ["Id", "Name", "Total"]),
    "reordered.sql": (["Total", "Name", "Id"], ["Total", "Name", "Id"]),
    "alias-table.sql": (["Id", "Name", "Total"], ["Id", "Name", "Total"]),
    "alias-output.sql": (["Id", "Name", "Total"], ["Id", "ItemCode", "Price"]),
    "bracketed.sql": (["Id", "Name", "Total"], ["Id", "Name", "Total"]),
    "wide.sql": (["Id", "Name", "Total", "Price"], ["Id", "Name", "Total", "Price"]),
}
EXPECTED_TOTALS = {M: 22, X: 5, U: 44}  # Per language; C# and VB double it.


def expected_totals():
    """Derive the denominator from the authored matrices only."""
    rows = [s for statuses in RESULT.SOURCE_CASES.values() for s in statuses]
    rows += [s for _, _, _, statuses, _ in EXTRA_CASES for s in statuses]
    return {s: rows.count(s) for s in (M, X, U)}


def case_plan():
    """Every (source case, template, table file, expected statuses, expected shape) cell, in a stable order."""
    plan = [(key, REPLAY_TEMPLATES.get(key, "base.sql"), "tables.sql", list(statuses),
             REPLAY_SHAPES[REPLAY_TEMPLATES.get(key, "base.sql")]) for key, statuses in RESULT.SOURCE_CASES.items()]
    return plan + [(k, t, f, list(s), r) for k, t, f, s, r in EXTRA_CASES]


def manifest(repo, revision, files, hashes):
    """Field order mirrors Core SqlServerDdlInput so the receipt can recompute the bounded input digest."""
    source = {"repo": repo, "revision": revision, "files": files, "sha256": [hashes[f] for f in files]}
    return {"version": INPUT_VERSION, "repo": repo, "revision": revision, "server": RESULT.IDENTITY["server"],
            "database": RESULT.IDENTITY["database"], "publicSynthetic": True,
            "files": [{"path": f, "sha256": hashes[f]} for f in sorted(files)],
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(source)}}


def check_export(export, template, tables, expected_shape, input_doc, core_hash, parser_hash):
    assert export["version"] == "sqlserver-ddl-table-result-schema/1" and export["ruleId"] == RULE
    assert export["generatorSha256"] == core_hash and export["parserSha256"] == parser_hash
    assert export["boundedInputSha256"] == HELPERS.digest(input_doc)
    assert export["declarations"]["origin"] == ORIGIN and export["tier"] == "Tier2Structural"
    items = next(s for s in export["declarations"]["shapes"] if s["identity"]["procedure"] == "ReadItems")
    observed = "complete" if items["completeness"] == "complete" else items["unknownReason"]
    assert observed == expected_shape, (template, tables, observed, expected_shape)
    if items["completeness"] != "complete":
        assert not items["columns"], template  # Never a partial column list.
        assert not [p for p in export["projections"] if p["procedure"]["procedure"] == "ReadItems"], template
        return
    outputs, sources = EXPECTED_PROJECTIONS[template]
    assert [c["name"] for c in items["columns"]] == outputs, (template, items)
    projection = next(p for p in export["projections"] if p["procedure"]["procedure"] == "ReadItems")
    assert [c["sourceColumn"] for c in projection["columns"]] == sources, (template, projection)
    assert projection["table"] == {"server": RESULT.IDENTITY["server"], "database": RESULT.IDENTITY["database"], "schema": "dbo", "table": "Items"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="new output directory")
    args = parser.parse_args()
    assert expected_totals() == EXPECTED_TOTALS, expected_totals()
    args.output.mkdir(parents=True, exist_ok=False)
    cli = ROOT / "src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll"
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    core_hash = hashlib.sha256((cli.parent / "TraceMap.Core.dll").read_bytes()).hexdigest()
    parser_hash = hashlib.sha256((cli.parent / "Microsoft.SqlServer.TransactSql.ScriptDom.dll").read_bytes()).hexdigest()
    hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(TEMPLATES.glob("*.sql"))}
    results, public_inputs, gaps, scan_artifacts, selected = [], [], [], {}, {}
    plan = case_plan()
    for language in ("csharp", "vb"):
        scan = args.output / language / "scan"
        extension = "vbproj" if language == "vb" else "csproj"
        RESULT.run_ok("dotnet", cli, "scan", "--repo", SAMPLE, "--project", f"{language}/Results.{extension}", "--out", scan)
        scan_artifacts[language] = HELPERS.scan_hashes(args.output, language)
        scan_manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        assert scan_manifest["commitSha"] == revision and scan_manifest["buildStatus"] == "Succeeded"
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"]
        keyed = dict((k, f) for f, k in RESULT.case_keys(calls))
        gaps += [f"{language}:{g}" for g in RESULT.case_gaps(k for _, k in RESULT.case_keys(calls))]
        assert len(calls) == RESULT.SITES_PER_LANGUAGE or gaps, len(calls)
        for number, (key, template, tables, expected, shape_reason) in enumerate(plan):
            fact = keyed.get(key)
            if fact is None:
                continue
            projected = RESULT.reader_projection(fact)
            public_inputs.append(projected)
            selected[(language, key)] = fact
            files = [template] + ([tables] if tables else [])
            case = args.output / language / f"{number:02}-{key.replace('#', '-')}-{template[:-4]}-{tables[:-4] if tables else 'no-tables'}"
            case.mkdir(parents=True)
            input_doc = manifest(fact["repo"], revision, files, hashes)
            HELPERS.write(case / "input.json", input_doc)
            HELPERS.write(case / "bindings.json", RESULT.binding(fact, key, revision, projected))
            RESULT.run_ok("dotnet", cli, "sqlserver-ddl-table-result", "produce", "--input", case / "input.json", "--source-root", TEMPLATES,
                          "--out", case / "schema.json")
            common = ["dotnet", cli, "sqlserver-ddl-table-result", "compare", "--input", case / "input.json", "--source-root", TEMPLATES,
                      "--schema", case / "schema.json", "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson",
                      "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                RESULT.run_ok(*common, "--out", case / output)
            wrapper, report, statuses, digest = RESULT.check_report(case, fact, expected, "ddl")
            assert wrapper["version"] == "sqlserver-ddl-table-result-comparison/1"
            assert report["version"] == "sqlserver-ddl-table-result-column-comparison/1" and report["origin"] == ORIGIN
            assert wrapper["generatorSha256"] == core_hash and report["generatorSha256"] == core_hash
            check_export(wrapper["ddlEvidence"], template, tables, shape_reason, input_doc, core_hash, parser_hash)
            markdown = (case / "report/report.md").read_text()
            if shape_reason != "complete":
                assert shape_reason in markdown, (case.name, shape_reason)
            results.append({"language": language, "case": key, "template": template, "tables": tables, "factId": fact["factId"],
                            "statuses": statuses, "shape": shape_reason, "reportSha256": digest})
    refusals = check_refusals(args.output, cli, revision, selected, hashes) if not gaps else []
    counts = {s: sum(r["statuses"].count(s) for r in results) for s in (M, X, U)}
    expected_counts = {s: 2 * n for s, n in EXPECTED_TOTALS.items()}
    receipt = {"version": "sqlserver-table-result-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"templates": hashes, "calls": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash,
               "resultHelperGeneratorSha256": RESULT.GENERATOR, "helperGeneratorSha256": HELPERS.GENERATOR,
               "scanArtifactSha256": scan_artifacts, "sitesPerLanguage": RESULT.SITES_PER_LANGUAGE, "casesPerLanguage": len(plan),
               "expectedCounts": expected_counts, "observedCounts": counts, "gaps": gaps, "refusals": refusals, "results": results,
               "sqlExecution": "not-run"}
    HELPERS.verify_scan_hashes(args.output, receipt)
    HELPERS.write(args.output / "smoke-receipt.json", receipt)
    assert not gaps, gaps
    assert counts == expected_counts, counts
    assert refusals and all(r["refused"] for r in refusals), refusals
    print(f"table-result native smoke passed at {revision}: {len(plan)} cases per language, {counts}; "
          f"{len(refusals)} refusals; SQL NOT RUN")


def check_refusals(root, cli, revision, selected, hashes):
    """Whole-input and wrong-origin failures must exit non-zero and publish nothing."""
    work = root / "refusals"
    work.mkdir()
    fact = selected[("csharp", "NameMatch")]
    scan = root / "csharp/scan/facts.ndjson"
    projected = RESULT.reader_projection(fact)
    good = RESULT.binding(fact, "NameMatch", revision, projected)
    HELPERS.write(work / "bindings.json", good)
    sources = work / "sources"
    sources.mkdir()
    for name in ("tables.sql", "base.sql"):
        (sources / name).write_bytes((TEMPLATES / name).read_bytes())
    table_input = manifest(fact["repo"], revision, ["base.sql", "tables.sql"], hashes)
    HELPERS.write(work / "input.json", table_input)
    RESULT.run_ok("dotnet", cli, "sqlserver-ddl-table-result", "produce", "--input", work / "input.json", "--source-root", sources,
                  "--out", work / "schema.json")
    # Constant-route and parameter-route artifacts over constant-only public SQL.
    constant_sources = work / "constant-sources"
    constant_sources.mkdir()
    constant_sql = SAMPLE / "ddl/base.sql"
    (constant_sources / "base.sql").write_bytes(constant_sql.read_bytes())
    constant_hash = hashlib.sha256(constant_sql.read_bytes()).hexdigest()
    constant_input = {"version": "sqlserver-ddl-result-input/1", "repo": fact["repo"], "revision": revision,
                      "server": RESULT.IDENTITY["server"], "database": RESULT.IDENTITY["database"], "publicSynthetic": True,
                      "files": [{"path": "base.sql", "sha256": constant_hash}]}
    HELPERS.write(work / "constant-input.json", constant_input)
    RESULT.run_ok("dotnet", cli, "sqlserver-ddl-result", "produce", "--input", work / "constant-input.json", "--source-root", constant_sources,
                  "--out", work / "constant-schema.json")
    checks = []

    def record(name, *command, output):
        ok, why = RESULT.refused(*command, output=output)
        checks.append({"case": name, "refused": ok, "message": why})

    def table_run(name, files=None, input_doc=None, schema=None, bindings=None, rev=revision, link=False, route="sqlserver-ddl-table-result"):
        """files maps relative names to SQL text; the manifest is re-hashed unless input_doc is supplied."""
        root_dir = work / f"src-{name}"
        root_dir.mkdir()
        files = files or {n: (TEMPLATES / n).read_text() for n in ("base.sql", "tables.sql")}
        for file_name, text in files.items():
            target = root_dir / file_name
            if link and file_name == "tables.sql":
                (root_dir / "actual.sql").write_text(text)
                target.symlink_to(root_dir / "actual.sql")
            else:
                target.write_text(text)
        local = {n: hashlib.sha256((root_dir / n).read_bytes()).hexdigest() for n in files}
        doc = input_doc or manifest(fact["repo"], revision, sorted(files), local)
        HELPERS.write(work / f"input-{name}.json", doc)
        out = work / f"out-{name}"
        command = ["dotnet", cli, route]
        command += (["compare", "--schema", schema, "--bindings", bindings or work / "bindings.json", "--facts", scan,
                     "--repo", fact["repo"], "--revision", rev] if schema else ["produce"])
        command += ["--input", work / f"input-{name}.json", "--source-root", root_dir, "--out", out]
        record(name, *command, output=out)

    base_sql = (TEMPLATES / "base.sql").read_text()
    tables_sql = (TEMPLATES / "tables.sql").read_text()
    table_run("stale-table-digest", input_doc=dict(table_input, files=[{"path": "base.sql", "sha256": hashes["base.sql"]},
                                                                    {"path": "tables.sql", "sha256": "f" * 64}]))
    table_run("duplicate-table-definition", files={"base.sql": base_sql, "tables.sql": tables_sql,
                                                   "tables2.sql": "CREATE TABLE dbo.Items (Id int NOT NULL);\n"})
    table_run("case-only-table-identity", files={"base.sql": base_sql, "tables.sql": tables_sql.replace("dbo.Items", "DBO.items")
                                                 + "\nCREATE TABLE dbo.Items (Id int NOT NULL);\n"})
    table_run("table-procedure-identity-collision", files={"base.sql": base_sql, "tables.sql": tables_sql + "\nCREATE TABLE dbo.ReadItems (Id int);\n"})
    table_run("alter-table-migration", files={"base.sql": base_sql, "tables.sql": tables_sql + "\nALTER TABLE dbo.Items ADD Extra int NULL;\n"})
    table_run("create-view", files={"base.sql": base_sql, "tables.sql": tables_sql + "\nCREATE VIEW dbo.ActiveItems AS SELECT Id FROM dbo.Items;\n"})
    table_run("unqualified-table-definition", files={"base.sql": base_sql, "tables.sql": "CREATE TABLE Items (Id int NOT NULL);\n"})
    table_run("temporary-table-definition", files={"base.sql": base_sql, "tables.sql": "CREATE TABLE #Items (Id int NOT NULL);\n"})
    table_run("malformed-table-sql", files={"base.sql": base_sql, "tables.sql": "CREATE TABLE dbo.Items (Id int NOT NULL PRIMARY);\n"})
    table_run("sqlcmd-in-table", files={"base.sql": base_sql, "tables.sql": tables_sql + "\n-- :setvar Owner dbo\n"})
    table_run("symlinked-table-source", link=True)
    table_run("constant-manifest-version", input_doc=dict(table_input, version="sqlserver-ddl-result-input/1"))
    table_run("parameter-manifest-version", input_doc=dict(table_input, version="sqlserver-ddl-input/1"))
    table_run("table-manifest-in-constant-route", input_doc=table_input, route="sqlserver-ddl-result")
    table_run("table-manifest-in-parameter-route", input_doc=table_input, route="sqlserver-ddl")
    table_run("constant-export-as-table-schema", schema=work / "constant-schema.json")
    mutated = json.loads((work / "schema.json").read_bytes())
    mutated["declarations"]["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-schema.json", mutated)
    table_run("mutated-table-export", schema=work / "mutated-schema.json")
    widened = json.loads((work / "schema.json").read_bytes())
    widened["tables"][0]["columns"] = widened["tables"][0]["columns"][:2]
    HELPERS.write(work / "mutated-table-columns.json", widened)
    table_run("mutated-table-definition-in-export", schema=work / "mutated-table-columns.json")
    # Stale export: produced from the original table, compared after the table file changed (manifest re-hashed).
    table_run("stale-export-after-table-change", files={"base.sql": base_sql, "tables.sql": tables_sql.replace("Total decimal", "Amount decimal")},
              schema=work / "schema.json")
    table_run("stale-revision", schema=work / "schema.json", rev="2" * 40)
    stale = copy.deepcopy(good)
    stale["calls"][0]["readerEvidenceSha256"] = "0" * 64
    HELPERS.write(work / "stale-digest.json", stale)
    table_run("stale-reader-digest", schema=work / "schema.json", bindings=work / "stale-digest.json")
    old = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
           "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItems", "identity": RESULT.IDENTITY,
                      "sourceEvidenceSha256": HELPERS.digest(projected)}]}
    HELPERS.write(work / "parameter-bindings.json", old)
    table_run("parameter-bindings-as-result-bindings", schema=work / "schema.json", bindings=work / "parameter-bindings.json")
    # The constant result route refuses the table export; it never reinterprets it.
    out = work / "out-table-export-in-constant-route"
    record("table-export-in-constant-route", "dotnet", cli, "sqlserver-ddl-result", "compare", "--input", work / "constant-input.json",
           "--source-root", constant_sources, "--schema", work / "schema.json", "--bindings", work / "bindings.json", "--facts", scan,
           "--repo", fact["repo"], "--revision", revision, "--out", out, output=out)
    existing = work / "existing"
    existing.mkdir()
    (existing / "keep.txt").write_bytes(b"prior winner\n")
    result = RESULT.run_captured_cli("dotnet", cli, "sqlserver-ddl-table-result", "compare", "--input", work / "input.json",
                                     "--source-root", sources, "--schema", work / "schema.json", "--bindings", work / "bindings.json",
                                     "--facts", scan, "--repo", fact["repo"], "--revision", revision, "--out", existing)
    checks.append({"case": "existing-output-not-replaced", "refused": result.returncode != 0
                   and sorted(p.name for p in existing.iterdir()) == ["keep.txt"] and (existing / "keep.txt").read_bytes() == b"prior winner\n",
                   "message": []})
    return checks


if __name__ == "__main__":
    main()
