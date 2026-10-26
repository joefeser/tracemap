#!/usr/bin/env python3
"""Offline SqlDataAdapter.Fill(DataSet) -> first-table DataRow native smoke over public synthetic C#/VB scans.

Never invokes fixture methods or SQL. Reuses the #912 result smoke helpers (and through them the parameter
smoke helpers) without changing their bytes, so retained receipts and denominators stay comparable. The Phase 2
DataTable smoke is not imported: its generator, matrix and denominator stay independent.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys

if sys.flags.optimize:
    raise RuntimeError("DataSet smoke requires Python optimization disabled")


def require(condition, detail):
    if not condition:
        raise ValueError(f"DataSet smoke validation failed: {detail}")


ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
SPEC = importlib.util.spec_from_file_location("result_smoke", ROOT / "scripts/sqlserver-result-column-evidence/smoke.py")
RESULT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RESULT)
HELPERS = RESULT.HELPERS
SAMPLE = ROOT / "samples/sqlserver-dataset-evidence"
SHAPES = RESULT.SAMPLE  # JSON base declarations, ddl/ and ddl-table/ templates are reused unchanged.
M, X, U = RESULT.M, RESULT.X, RESULT.U
BINDINGS_VERSION = "sqlserver-dataset-bindings/1"
STATE_VERSION = "sqlserver-dataset/1"
RULE = "database.sqlserver.dataset-column-comparison.v1"
DATATABLE_BINDINGS_VERSION = "sqlserver-datatable-bindings/1"
REPORT = "dataset-column-comparison.json"
ORIGINS = ("result-schema", "ddl-result", "ddl-table-result")
ORIGIN_LABELS = {"result-schema": "independent-synthetic-result-declarations", "ddl-result": "public-synthetic-tsql-ddl-result",
                 "ddl-table-result": "public-synthetic-tsql-ddl-table-result"}
SITES_PER_LANGUAGE = 99

# Independently authored before any extractor output (design.md): ordered statuses of every report row for the
# selected Fill, and the shape variant. Core cells run against all three origins; #n is the nth Fill site.
CORE_CASES = {
    "IndexZero": ([M], None), "DefaultName": ([M], None), "SourceTableName": ([M], None), "SourceTableIndexZero": ([M], None),
    "SelectCommandProperty": ([M], None), "DiscardFill": ([M], None), "TableLocal": ([M], None), "RowLocalOrdinal": ([M], None),
    "ForeachRows": ([M, M], None), "TableLocalForeach": ([M], None), "TablesCountGuard": ([M], None), "RowsCountGuard": ([M], None),
    "NestedGuards": ([M], None), "IsNullName": ([M], None), "IsNullOrdinal": ([M], None), "BangName": ([M], None),
    "UsingLifetime": ([M], None), "WithParameter": ([M], None), "TwoFills#0": ([M], None), "TwoFills#1": ([M], None),
    "MixedSelectors": ([M, M], None), "ConvertedOrdinal": ([M], None), "Reordered": ([M, M], "reordered"),
    "MissingName": ([X], None), "OrdinalOutOfRange": ([X], None), "IsNullMissing": ([X], None), "Renamed": ([X], "renamed"),
    "CaseOnly": ([U], None), "DuplicateDeclared": ([U], "duplicate"), "DuplicateRenamed": ([U], "duplicate"), "DynamicName": ([U], None),
    "DynamicOrdinal": ([U], None), "NonIdentifierName": ([U], None), "Incomplete": ([U], "incomplete"), "AbsentBinding": ([U], None),
}
# State cells (table-backed DDL only): exact expected state gap; "bounded" cells name the comparison reason instead.
STATE_CASES = {
    "FillDataTable": "fill-overload-unsupported", "FillRange": "fill-overload-unsupported", "FillResultUsed": "fill-result-use-unsupported",
    "DynamicSourceTable": "dynamic-source-table-unsupported", "NonIdentifierSourceTable": "source-table-outside-identifier-vocabulary",
    "EmptySourceTable": "invalid-source-table", "SecondTableIndex": "dataset-later-table-unsupported", "NegativeTableIndex": "invalid-table-index",
    "DynamicTableIndex": "dynamic-table-selector-unsupported", "DynamicTableName": "dynamic-table-selector-unsupported",
    "LaterTableName": "dataset-table-name-unbound", "CaseOnlyTableName": "dataset-table-name-case-unknown",
    "SourceNameMismatch": "dataset-table-name-unbound", "TableNamespace": "dataset-table-namespace-unsupported",
    "TablesCountUse": "dataset-tables-count-unsupported", "TablesCountWrongGuard": "unsupported-control-flow", "TablesAdd": "dataset-mutation",
    "TablesAlias": "dataset-tables-alias-or-escape", "TablesMember": "unsupported-dataset-tables-member", "TablesForeach": "unsupported-control-flow",
    "MergeDataSet": "dataset-mutation", "ReadXml": "dataset-mutation", "Relations": "dataset-relations-unsupported",
    "EnforceConstraints": "dataset-mutation", "CaseSensitiveDataSet": "dataset-mutation", "CopyDataSet": "ambiguous-row-ownership",
    "DataSetMember": "unsupported-dataset-member", "NamedDataSet": "dataset-constructor-unsupported",
    "DataSetInitializer": "dataset-initializer-unsupported", "DataSetReassigned": "dataset-reassignment", "DataSetAlias": "dataset-alias-or-escape",
    "DataSetEscape": "dataset-alias-or-escape", "RefDataSet": "dataset-alias-or-escape", "UseBeforeFill": "dataset-use-before-fill",
    "SharedDataSet": "fresh-dataset-required", "TypedDataSet": "fresh-dataset-local-required", "HelperFill": "fresh-sqldataadapter-local-required",
    "RepeatedFill#0": "repeated-fill", "RepeatedFill#1": "repeated-fill", "AdapterReused#0": "adapter-reused", "AdapterReused#1": "adapter-reused",
    "FillSchemaFirst": "fill-schema-unsupported", "TableMappings": "adapter-table-mappings-unsupported",
    "MissingSchemaAction": "adapter-missing-schema-action-unsupported", "MissingMappingAction": "adapter-missing-mapping-action-unsupported",
    "CommandTypeAfterAdapter": "command-mutation-after-adapter-association", "TextCommand": ("bounded", "text-command-outside-scope"),
    "TableLocalReassigned": "datatable-reassignment", "TableLocalEscape": "datatable-alias-or-escape",
    "TableColumns": "datatable-schema-access-unsupported", "TableSelect": "ambiguous-row-ownership", "LinqRows": "ambiguous-row-ownership",
    "TableClear": "datatable-mutation", "RowsAdd": "datatable-mutation", "RowWrite": "datatable-mutation",
    "FieldExtension": "datarow-field-extension-unsupported", "DynamicRowIndex": "dynamic-row-index-unsupported", "RowAlias": "row-alias-or-escape",
    "ForeachObjectRow": "foreach-row-type-unsupported", "GuardElse": "unsupported-control-flow", "TablesAndRowsGuard": "unsupported-control-flow",
    "OtherDataSetGuard": "unsupported-control-flow", "TryCatch": "unsupported-control-flow", "NoReads": ("bounded", "no-bounded-column-access"),
}
DDL_VARIANTS = {None: "base.sql", "renamed": "renamed.sql", "reordered": "reordered.sql", "duplicate": "duplicate.sql", "incomplete": "if.sql"}
EXPECTED_TOTALS = {M: 78, X: 12, U: 88}  # Per language; C# and VB double it.
REFUSAL_COUNT = 24


def expected_totals():
    """Derive the per-language denominator from the authored matrices only."""
    rows = [s for statuses, _ in CORE_CASES.values() for s in statuses] * len(ORIGINS)
    rows += [U] * len(STATE_CASES)
    return {s: rows.count(s) for s in (M, X, U)}


def case_plan():
    """Every (case key, origin, variant, expected statuses) cell in a stable order."""
    plan = [(key, origin, variant, list(statuses)) for key, (statuses, variant) in CORE_CASES.items() for origin in ORIGINS]
    return plan + [(key, "ddl-table-result", None, [U]) for key in STATE_CASES]


def case_keys(facts):
    """Fill facts in source order; repeated method names get #n suffixes (shared #912 keying)."""
    return RESULT.case_keys(facts)


def case_gaps(observed):
    """A missing or unexpected case is an explicit gap, never a renamed pass."""
    observed = list(observed)
    known = set(CORE_CASES) | set(STATE_CASES)
    gaps = [f"missing:{k}" for k in sorted(known) if k not in observed]
    gaps += [f"unexpected:{k}" for k in observed if k not in known]
    gaps += [f"duplicate:{k}" for k in set(observed) if observed.count(k) > 1]
    return sorted(gaps)


def dataset_projection(fact):
    """Mirror of Core SqlServerDataSetEvidence.Projection (ASCII public fixture values only)."""
    span = fact["evidence"]
    state = fact["properties"].get("sqlServerDataSetState")
    return {"contract": BINDINGS_VERSION, "factId": fact["factId"], "repo": fact["repo"], "commitSha": fact["commitSha"],
            "ruleId": fact["ruleId"], "evidenceTier": fact["evidenceTier"],
            "filePath": span["filePath"], "startLine": span["startLine"], "endLine": span["endLine"],
            "extractorId": span["extractorId"], "extractorVersion": span["extractorVersion"],
            "dataSetState": None if state is None else json.loads(state)}


def datatable_projection(fact):
    """Mirror of Core SqlServerDataTableEvidence.Projection, used only to prove DataTable digests never substitute."""
    projected = dataset_projection(fact)
    state = fact["properties"].get("sqlServerDataTableState")
    projected.pop("dataSetState")
    projected["contract"] = DATATABLE_BINDINGS_VERSION
    projected["dataTableState"] = None if state is None else json.loads(state)
    return projected


def binding(fact, key, revision, projected):
    identity = dict(RESULT.IDENTITY)
    if key == "TwoFills#1":
        identity["procedure"] = "ReadOther"
    if key == "AbsentBinding":
        identity["database"] = "OtherDatabase"  # Exact four-part identity absent from every admitted shape.
    return {"version": BINDINGS_VERSION, "repo": fact["repo"], "revision": revision,
            "calls": [{"factId": fact["factId"], "commandText": f"{identity['schema']}.{identity['procedure']}",
                       "identity": identity, "dataSetEvidenceSha256": HELPERS.digest(projected)}],
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(projected)}}


def ddl_manifest(version, repo, revision, files, hashes):
    """Field order mirrors Core SqlServerDdlInput so the export's bounded input digest can be recomputed."""
    source = {"repo": repo, "revision": revision, "files": sorted(files), "sha256": [hashes[f] for f in sorted(files)]}
    return {"version": version, "repo": repo, "revision": revision, "server": RESULT.IDENTITY["server"],
            "database": RESULT.IDENTITY["database"], "publicSynthetic": True,
            "files": [{"path": f, "sha256": hashes[f]} for f in sorted(files)],
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(source)}}


class Shapes:
    """Produces each (origin, variant) shape export once through the unchanged origin route."""

    def __init__(self, root, cli, repo, revision):
        self.root, self.cli, self.repo, self.revision = root / "shapes", cli, repo, revision
        self.root.mkdir()
        self.base = json.loads((SHAPES / "result-declarations-base.json").read_bytes())
        self.hashes = {d: {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((SHAPES / d).glob("*.sql"))}
                       for d in ("ddl", "ddl-table")}
        self.cache = {}

    def get(self, origin, variant):
        """Returns (compare origin options, schema path, admitted input document)."""
        key = (origin, variant)
        if key in self.cache:
            return self.cache[key]
        work = self.root / f"{origin}-{variant or 'base'}"
        work.mkdir()
        if origin == "result-schema":
            decl = RESULT.declarations(self.base, variant, self.repo, self.revision, f"shapes/{work.name}/declarations.json")
            HELPERS.write(work / "declarations.json", decl)
            RESULT.run_ok("dotnet", self.cli, "sqlserver-result-schema", "produce", "--declarations", work / "declarations.json",
                          "--out", work / "schema.json")
            entry = (["--declarations", work / "declarations.json"], work / "schema.json", decl)
        else:
            folder = "ddl" if origin == "ddl-result" else "ddl-table"
            files = [DDL_VARIANTS[variant]] + (["tables.sql"] if origin == "ddl-table-result" else [])
            version = "sqlserver-ddl-result-input/1" if origin == "ddl-result" else "sqlserver-ddl-table-result-input/1"
            doc = ddl_manifest(version, self.repo, self.revision, files, self.hashes[folder])
            HELPERS.write(work / "input.json", doc)
            RESULT.run_ok("dotnet", self.cli, "sqlserver-" + origin, "produce", "--input", work / "input.json",
                          "--source-root", SHAPES / folder, "--out", work / "schema.json")
            entry = (["--input", work / "input.json", "--source-root", SHAPES / folder], work / "schema.json", doc)
        self.cache[key] = entry
        return entry


def check_state(key, fact):
    """Source state against the authored matrix before any comparison runs."""
    state = json.loads(fact["properties"]["sqlServerDataSetState"])
    require(state["version"] == STATE_VERSION, (key, state["version"]))
    # Sibling property: only the Fill(DataTable) site has bounded DataTable state; DataSet overloads stay refused there.
    table = json.loads(fact["properties"]["sqlServerDataTableState"])
    require(table["coverage"] == "bounded" if key == "FillDataTable" else table["gap"] == "fill-overload-unsupported", (key, table["gap"]))
    expected = STATE_CASES.get(key)
    if expected is None or isinstance(expected, tuple):
        require(state["coverage"] == "bounded" and state["gap"] is None, (key, state["coverage"], state["gap"]))
        if expected is None:
            require(state["commandType"] == "StoredProcedure" and state["accesses"], (key, state))
        return expected[1] if expected else None
    require(state["coverage"] == "unknown" and state["gap"] == expected and not state["accesses"], (key, state["gap"], expected))
    return expected


def check_report(case, fact, expected, origin, reason, core_hash, schema_path):
    for name in (REPORT, "report.md"):
        require((case / "report" / name).read_bytes() == (case / "repeat" / name).read_bytes(), (case, name))
    raw = (case / "report" / REPORT).read_bytes()
    report = json.loads(raw)
    require(report["version"] == "sqlserver-dataset-comparison/1" and report["consumerVersion"] == STATE_VERSION
            and report["bindingsVersion"] == BINDINGS_VERSION, "report contract")
    require(report["schemaRoute"] == origin and report["origin"] == ORIGIN_LABELS[origin], (origin, report["origin"]))
    require(report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict", "coverage/verdict")
    require(report["generatorSha256"] == core_hash, "report generator")
    require(report["schemaEvidenceSha256"] == HELPERS.digest(json.loads(schema_path.read_bytes())), "admitted export digest")
    rows = [r for r in report["comparisons"] if r["factId"] == fact["factId"]]
    statuses = [r["status"] for r in rows]
    require(statuses == expected, (case.name, statuses, expected, rows))
    if reason is not None:
        require(len(rows) == 1 and reason in rows[0]["reasons"], (case.name, reason, rows))
    for row in report["comparisons"]:
        if row["factId"] != fact["factId"]:
            require(row["status"] == U and "binding-missing" in row["reasons"], row)
        require(row["ruleId"] == RULE, row)
        require(row["tier"] == ("Tier4Unknown" if row["status"] == U else "Tier2Structural"), row)
        require(row["sourceTier"] == "Tier1Semantic", row)
    markdown = (case / "report/report.md").read_text()
    require("No aggregate clean verdict" in markdown, "markdown verdict")
    for row in rows:
        for why in row["reasons"]:
            require(why in markdown, (case.name, why))
    return statuses, hashlib.sha256(raw).hexdigest()


def publish_receipt(root, receipt):
    require(not receipt["gaps"], receipt["gaps"])
    require(receipt["observedCounts"] == receipt["expectedCounts"], "aggregate counts")
    require(len(receipt["refusals"]) == REFUSAL_COUNT and all(r["refused"] for r in receipt["refusals"]), "refusals")
    HELPERS.verify_scan_hashes(root, receipt)
    HELPERS.write(root / "smoke-receipt.json", receipt)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="new output directory")
    args = parser.parse_args()
    require(expected_totals() == EXPECTED_TOTALS, expected_totals())
    args.output.mkdir(parents=True, exist_ok=False)
    cli = ROOT / "src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll"
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    core_hash = hashlib.sha256((cli.parent / "TraceMap.Core.dll").read_bytes()).hexdigest()
    parser_hash = hashlib.sha256((cli.parent / "Microsoft.SqlServer.TransactSql.ScriptDom.dll").read_bytes()).hexdigest()
    results, public_inputs, gaps, scan_artifacts, selected = [], [], [], {}, {}
    shapes = None
    plan = case_plan()
    for language in ("csharp", "vb"):
        scan = args.output / language / "scan"
        extension = "vbproj" if language == "vb" else "csproj"
        RESULT.run_ok("dotnet", cli, "scan", "--repo", SAMPLE, "--project", f"{language}/DataSets.{extension}", "--out", scan)
        scan_artifacts[language] = HELPERS.scan_hashes(args.output, language)
        manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        require(manifest["commitSha"] == revision and manifest["buildStatus"] == "Succeeded", (language, manifest["buildStatus"]))
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"
                 and f["properties"].get("operationKind") == "data-adapter-fill"]
        keyed = dict((k, f) for f, k in case_keys(calls))
        gaps += [f"{language}:{g}" for g in case_gaps(k for _, k in case_keys(calls))]
        require(len(calls) == SITES_PER_LANGUAGE or gaps, len(calls))
        reasons = {}
        for key, fact in keyed.items():
            reasons[key] = check_state(key, fact)
            projected = dataset_projection(fact)
            public_inputs.append(projected)
            selected[(language, key)] = fact
        if shapes is None and keyed:
            shapes = Shapes(args.output, cli, next(iter(keyed.values()))["repo"], revision)
        for number, (key, origin, variant, expected) in enumerate(plan):
            fact = keyed.get(key)
            if fact is None:
                continue
            options, schema, _ = shapes.get(origin, variant)
            case = args.output / language / f"{number:03}-{key.replace('#', '-')}-{origin}-{variant or 'base'}"
            case.mkdir(parents=True)
            HELPERS.write(case / "bindings.json", binding(fact, key, revision, dataset_projection(fact)))
            common = ["dotnet", cli, "sqlserver-dataset-result", "compare", "--origin", origin, *options, "--schema", schema,
                      "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson", "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                RESULT.run_ok(*common, "--out", case / output)
            statuses, digest = check_report(case, fact, expected, origin, reasons[key], core_hash, schema)
            results.append({"language": language, "case": key, "origin": origin, "variant": variant, "factId": fact["factId"],
                            "statuses": statuses, "reportSha256": digest})
    refusals = check_refusals(args.output, cli, revision, selected, shapes) if not gaps else []
    counts = {s: sum(r["statuses"].count(s) for r in results) for s in (M, X, U)}
    expected_counts = {s: 2 * n for s, n in EXPECTED_TOTALS.items()}
    hashes = shapes.hashes if shapes else {}
    receipt = {"version": "sqlserver-dataset-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"base": shapes.base if shapes else None, "templates": hashes, "calls": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash,
               "resultHelperGeneratorSha256": RESULT.GENERATOR, "helperGeneratorSha256": HELPERS.GENERATOR,
               "scanArtifactSha256": scan_artifacts, "sitesPerLanguage": SITES_PER_LANGUAGE, "casesPerLanguage": len(plan),
               "expectedCounts": expected_counts, "observedCounts": counts, "gaps": gaps, "refusals": refusals, "results": results,
               "sqlExecution": "not-run"}
    publish_receipt(args.output, receipt)
    print(f"DataSet native smoke passed at {revision}: {len(plan)} cases per language, {counts}; "
          f"{len(refusals)} refusals; SQL NOT RUN")


def check_refusals(root, cli, revision, selected, shapes):
    """Substitution, wrong-origin and whole-input failures must exit non-zero and publish nothing."""
    work = root / "refusals"
    work.mkdir()
    fact = selected[("csharp", "IndexZero")]
    scan = root / "csharp/scan/facts.ndjson"
    projected = dataset_projection(fact)
    good = binding(fact, "IndexZero", revision, projected)
    HELPERS.write(work / "bindings.json", good)
    json_options, json_schema, json_decl = shapes.get("result-schema", None)
    ddl_options, ddl_schema, ddl_input = shapes.get("ddl-result", None)
    table_options, table_schema, table_input = shapes.get("ddl-table-result", None)
    checks = []

    def record(name, *command, output):
        ok, why = RESULT.refused(*command, output=output)
        checks.append({"case": name, "refused": ok, "message": why})

    def compare(name, origin="result-schema", options=None, schema=None, bindings=None, facts=scan, rev=revision):
        out = work / f"out-{name}"
        record(name, "dotnet", cli, "sqlserver-dataset-result", "compare", "--origin", origin, *(options or json_options),
               "--schema", schema or json_schema, "--bindings", bindings or work / "bindings.json", "--facts", facts,
               "--repo", fact["repo"], "--revision", rev, "--out", out, output=out)

    def write_bindings(name, document):
        HELPERS.write(work / f"{name}.json", document)
        return work / f"{name}.json"

    reader = RESULT.binding(fact, "NameMatch", revision, RESULT.reader_projection(fact))
    table_binding = copy.deepcopy(good)
    table_binding["version"] = DATATABLE_BINDINGS_VERSION
    table_binding["calls"][0] = {k: v for k, v in table_binding["calls"][0].items() if k != "dataSetEvidenceSha256"}
    table_binding["calls"][0]["dataTableEvidenceSha256"] = HELPERS.digest(datatable_projection(fact))
    compare("datatable-bindings-as-dataset-bindings", bindings=write_bindings("datatable-bindings", table_binding))
    compare("reader-bindings-as-dataset-bindings", bindings=write_bindings("reader-bindings", reader))
    parameter = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
                 "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItems", "identity": RESULT.IDENTITY,
                            "sourceEvidenceSha256": HELPERS.digest(HELPERS.projection(fact))}]}
    compare("parameter-bindings-as-dataset-bindings", bindings=write_bindings("parameter-bindings", parameter))
    for name, digest in (("datatable-digest-substitution", HELPERS.digest(datatable_projection(fact))),
                         ("reader-digest-substitution", HELPERS.digest(RESULT.reader_projection(fact))),
                         ("parameter-digest-substitution", HELPERS.digest(HELPERS.projection(fact))),
                         ("stale-dataset-digest", "0" * 64)):
        changed = copy.deepcopy(good)
        changed["calls"][0]["dataSetEvidenceSha256"] = digest
        compare(name, bindings=write_bindings(name, changed))
    duplicate = copy.deepcopy(good)
    duplicate["calls"].append(copy.deepcopy(duplicate["calls"][0]))
    compare("duplicate-binding", bindings=write_bindings("duplicate-binding", duplicate))
    compare("stale-revision", rev="2" * 40)
    tampered = [json.loads(line) for line in scan.read_text().splitlines()]
    for item in tampered:
        if item["factId"] == fact["factId"]:
            state = json.loads(item["properties"]["sqlServerDataSetState"])
            state["accesses"][0]["name"] = "Title"
            item["properties"]["sqlServerDataSetState"] = json.dumps(state, separators=(",", ":"))
    (work / "mutated-facts.ndjson").write_text("".join(json.dumps(i, separators=(",", ":")) + "\n" for i in tampered))
    compare("mutated-source-facts", facts=work / "mutated-facts.ndjson")
    compare("table-export-as-result-schema", schema=table_schema)
    compare("constant-export-as-table-schema", origin="ddl-table-result", options=table_options, schema=ddl_schema)
    compare("table-manifest-in-ddl-result-origin", origin="ddl-result", options=table_options, schema=table_schema)
    compare("constant-manifest-in-ddl-table-origin", origin="ddl-table-result", options=ddl_options, schema=ddl_schema)
    compare("unknown-origin", origin="reader")
    compare("origin-option-mismatch", options=table_options)
    mutated = copy.deepcopy(json_decl)
    mutated["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-declarations.json", mutated)
    compare("mutated-json-declarations", options=["--declarations", work / "mutated-declarations.json"])
    export = json.loads(table_schema.read_bytes())
    export["declarations"]["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-table-export.json", export)
    compare("mutated-table-export", origin="ddl-table-result", options=table_options, schema=work / "mutated-table-export.json")
    stale_input = dict(table_input, files=[dict(f, sha256="f" * 64) if f["path"] == "tables.sql" else f for f in table_input["files"]])
    HELPERS.write(work / "stale-table-input.json", stale_input)
    compare("stale-table-source-digest", origin="ddl-table-result", options=["--input", work / "stale-table-input.json",
            "--source-root", SHAPES / "ddl-table"], schema=table_schema)
    linked = work / "linked-sources"
    linked.mkdir()
    (linked / "actual.sql").write_bytes((SHAPES / "ddl/base.sql").read_bytes())
    (linked / "base.sql").symlink_to(linked / "actual.sql")
    compare("symlinked-ddl-source", origin="ddl-result", options=["--input", ddl_options[1], "--source-root", linked], schema=ddl_schema)
    out = work / "out-dataset-bindings-in-datatable-route"
    record("dataset-bindings-in-datatable-route", "dotnet", cli, "sqlserver-datatable-result", "compare", "--origin", "result-schema", *json_options,
           "--schema", json_schema, "--bindings", work / "bindings.json", "--facts", scan, "--repo", fact["repo"], "--revision", revision,
           "--out", out, output=out)
    out = work / "out-dataset-bindings-in-reader-route"
    record("dataset-bindings-in-reader-route", "dotnet", cli, "sqlserver-result-schema", "compare", *json_options, "--schema", json_schema,
           "--bindings", work / "bindings.json", "--facts", scan, "--repo", fact["repo"], "--revision", revision, "--out", out, output=out)
    (work / "oversized-bindings.json").write_bytes(b" " * (4 * 1024 * 1024 + 1))
    compare("oversized-bindings", bindings=work / "oversized-bindings.json")
    existing = work / "existing"
    existing.mkdir()
    (existing / "keep.txt").write_bytes(b"prior winner\n")
    result = RESULT.run_captured_cli("dotnet", cli, "sqlserver-dataset-result", "compare", "--origin", "result-schema", *json_options,
                                     "--schema", json_schema, "--bindings", work / "bindings.json", "--facts", scan,
                                     "--repo", fact["repo"], "--revision", revision, "--out", existing)
    checks.append({"case": "existing-output-not-replaced", "refused": result.returncode != 0
                   and sorted(p.name for p in existing.iterdir()) == ["keep.txt"] and (existing / "keep.txt").read_bytes() == b"prior winner\n",
                   "message": []})
    return checks


if __name__ == "__main__":
    main()
