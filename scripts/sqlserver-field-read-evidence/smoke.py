#!/usr/bin/env python3
"""Offline Field<T> / Field(Of T) read native smoke over public synthetic multi-document C#/VB scans.

Never invokes fixture methods or SQL. Reuses the #912 result smoke helpers, the DataSet smoke's shape producer and the
Phase 4/5 helper smokes' keying and projection helpers without changing their bytes, so retained receipts and denominators
stay comparable. Each field-read site (a direct DataTable/DataSet Fill, or one call site of a bounded private or internal
helper) is bound and compared separately; the type argument is recorded, never compared.
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
    raise RuntimeError("Field-read smoke requires Python optimization disabled")


def require(condition, detail):
    if not condition:
        raise ValueError(f"Field-read smoke validation failed: {detail}")


ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


RESULT = load("result_smoke", "scripts/sqlserver-result-column-evidence/smoke.py")
DATASET = load("dataset_smoke", "scripts/sqlserver-dataset-evidence/smoke.py")
PHASE4 = load("helper_result_smoke", "scripts/sqlserver-helper-result-evidence/smoke.py")
PHASE5 = load("internal_helper_result_smoke", "scripts/sqlserver-internal-helper-result-evidence/smoke.py")
HELPERS = RESULT.HELPERS
SAMPLE = ROOT / "samples/sqlserver-field-read-evidence"
M, X, U = RESULT.M, RESULT.X, RESULT.U
BINDINGS_VERSION = "sqlserver-field-read-bindings/1"
STATE_VERSION = "sqlserver-field-read/1"
STATE = "sqlServerFieldReadState"
RULE = "database.sqlserver.field-read-column-comparison.v1"
REPORT = "field-read-column-comparison.json"
ROUTE = "sqlserver-field-read"
ORIGINS = DATASET.ORIGINS
ORIGIN_LABELS = DATASET.ORIGIN_LABELS
SITES_PER_LANGUAGE = 26
FIELD_GAP = "datarow-field-extension-unsupported"
# Bounded helper Fill sites: (origin, call sites in order).
HELPER_FILLS = {
    "ReadPrivate": ("helper", ["HelperFieldName", "HelperFieldOtherProcedure", "HelperFieldMissing", "HelperFieldColumn"]),
    "ReadShared": ("internal-helper", ["InternalFieldName", "InternalFieldOrdinal", "InternalFieldMissing", "InternalFieldVersion", "InternalNoField"]),
    "ReadSharedSet": ("internal-helper", ["InternalFieldSet"]),
}

# Independently authored before any extractor output (design.md). Core cells run against all three origins.
# Fill-keyed cells (direct DataTable/DataSet Fill sites) and site-keyed cells (helper call sites, by calling member).
CORE_FILL_CASES = {
    "TableFieldName": ([M], None), "TableFieldOrdinal": ([M], None), "TableFieldRowLocal": ([M], None), "TableFieldForeach": ([M], None),
    "TableFieldMixed": ([M, M], None), "TableFieldMissing": ([X], None), "TableFieldOrdinalOutOfRange": ([X], None),
    "TableFieldCaseOnly": ([U], None), "TableFieldDynamic": ([U], None), "TableFieldRenamed": ([X], "renamed"),
    "TableFieldIncomplete": ([U], "incomplete"),
    "SetFieldName": ([M], None), "SetFieldTableLocal": ([M], None), "SetFieldMissing": ([X], None),
}
CORE_SITE_CASES = {
    "HelperFieldName": ([M], None), "HelperFieldOtherProcedure": ([M], None), "HelperFieldMissing": ([X], None),
    "InternalFieldName": ([M], None), "InternalFieldOrdinal": ([M], None), "InternalFieldSet": ([M], None), "InternalFieldMissing": ([X], None),
}
# State cells (table-backed DDL only), keyed by the Fill's enclosing method: the expected origin gap, or ("site", reason) when the state
# is bounded but its one site carries no bound Field read. BASE is the unchanged Phase 2/3/4 gap for the same input (None = bounded).
STATE_CASES = {
    "TableFieldColumn": "datatable:datacolumn-access-unsupported", "TableFieldVersion": "datatable:row-version-access-unsupported",
    "TableSetField": "datatable:datatable-mutation", "TableFieldAsEnumerable": "datatable:ambiguous-row-ownership",
    "TableFieldStatic": "datatable:datarow-field-static-call-unsupported", "TableFieldUnbound": ("site", "field-read-absent"),
    "TableNoField": ("site", "field-read-absent"), "SetFieldLaterTable": "dataset:dataset-later-table-unsupported",
    "ReadBody": "helper:helper-body-unsupported",
}
BASE = {"TableSetField": "datatable-mutation", "TableFieldAsEnumerable": "ambiguous-row-ownership", "TableFieldUnbound": None, "TableNoField": None,
        "SetFieldLaterTable": "dataset-later-table-unsupported"}
# Site state cells (table-backed DDL only), keyed by the calling member: the exact site gap.
SITE_CASES = {"HelperFieldColumn": "datacolumn-access-unsupported", "InternalFieldVersion": "row-version-access-unsupported",
              "InternalNoField": "field-read-absent"}
SITE_BASE = {"InternalNoField": None}  # Phase 4/5 caller gap for the same call site; every other Field caller keeps FIELD_GAP.
OTHER_PROCEDURE = {"HelperFieldOtherProcedure"}
EXPECTED_TOTALS = {M: 39, X: 18, U: 21}  # Per language; C# and VB double it.
REFUSAL_COUNT = 26


def expected_totals():
    """Derive the per-language denominator from the authored matrices only."""
    rows = [s for cases in (CORE_FILL_CASES, CORE_SITE_CASES) for statuses, _ in cases.values() for s in statuses] * len(ORIGINS)
    rows += [U] * (len(STATE_CASES) + len(SITE_CASES))
    return {s: rows.count(s) for s in (M, X, U)}


def case_plan():
    """Every (kind, case key, origin, variant, expected statuses) cell in a stable order."""
    plan = [(kind, key, origin, variant, list(statuses)) for kind, cases in (("fill", CORE_FILL_CASES), ("site", CORE_SITE_CASES))
            for key, (statuses, variant) in cases.items() for origin in ORIGINS]
    plan += [("fill", key, "ddl-table-result", None, [U]) for key in STATE_CASES]
    return plan + [("site", key, "ddl-table-result", None, [U]) for key in SITE_CASES]


def fill_keys(facts):
    """Fill facts keyed by their enclosing method in (path, line) order."""
    ordered = sorted(facts, key=lambda f: (f["evidence"]["filePath"], f["evidence"]["startLine"]))
    return PHASE4.keyed((PHASE4.member(f["sourceSymbol"]), f) for f in ordered)


def site_keys(fills):
    """Sites of the bounded helper Fills keyed by calling member: (key, (fact, index, site))."""
    sites = []
    for key, fact in fills:
        if key not in HELPER_FILLS:
            continue
        for index, site in enumerate(json.loads(fact["properties"][STATE])["sites"]):
            sites.append((PHASE4.member(site["caller"]), (fact, index, site)))
    return PHASE4.keyed(sites)


def case_gaps(fill_observed, site_observed):
    """A missing or unexpected case is an explicit gap, never a renamed pass."""
    gaps = []
    for kind, known, observed in (("fill", set(CORE_FILL_CASES) | set(STATE_CASES) | set(HELPER_FILLS), list(fill_observed)),
                                  ("site", set(CORE_SITE_CASES) | set(SITE_CASES), list(site_observed))):
        gaps += [f"missing:{kind}:{k}" for k in sorted(known) if k not in observed]
        gaps += [f"unexpected:{kind}:{k}" for k in observed if k not in known]
        gaps += [f"duplicate:{kind}:{k}" for k in sorted(set(observed)) if observed.count(k) > 1]
    return sorted(gaps)


def field_projection(fact, index):
    """Mirror of Core SqlServerFieldReadEvidence.Projection for one site (ASCII public fixture values only)."""
    span = fact["evidence"]
    state = json.loads(fact["properties"][STATE])
    sites = state["sites"]
    site = sites[index] if 0 <= index < len(sites) else None
    state["sites"] = []
    return {"contract": BINDINGS_VERSION, "factId": fact["factId"], "repo": fact["repo"], "commitSha": fact["commitSha"],
            "ruleId": fact["ruleId"], "evidenceTier": fact["evidenceTier"],
            "filePath": span["filePath"], "startLine": span["startLine"], "endLine": span["endLine"],
            "extractorId": span["extractorId"], "extractorVersion": span["extractorVersion"],
            "siteIndex": index, "fieldState": state, "site": site}


def binding(fact, index, key, revision, projected):
    identity = dict(RESULT.IDENTITY)
    if key in OTHER_PROCEDURE:
        identity["procedure"] = "ReadOther"
    calls = [] if index is None else [{"factId": fact["factId"], "siteIndex": index,
                                       "commandText": f"{identity['schema']}.{identity['procedure']}", "identity": identity,
                                       "fieldReadEvidenceSha256": HELPERS.digest(projected)}]
    return {"version": BINDINGS_VERSION, "repo": fact["repo"], "revision": revision, "calls": calls,
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(projected)}}


def has_field(site):
    return any(a["member"] == "Field" for a in site["accesses"])


def check_fill_state(key, fact):
    """Field-read source state against the authored matrix, plus the unchanged Phase 2/3/4 sibling states. Returns (site index, reason)."""
    state = json.loads(fact["properties"][STATE])
    require(state["version"] == STATE_VERSION, (key, state["version"]))
    table = json.loads(fact["properties"]["sqlServerDataTableState"])
    dataset = json.loads(fact["properties"]["sqlServerDataSetState"])
    phase4 = json.loads(fact["properties"]["sqlServerHelperResultState"])
    if key in HELPER_FILLS:
        origin, callers = HELPER_FILLS[key]
        require(state["coverage"] == "bounded" and state["origin"] == origin and len(state["sites"]) == len(callers), (key, state["gap"], state["originGaps"]))
        require([PHASE4.member(s["caller"]) for s in state["sites"]] == callers, (key, [s["caller"] for s in state["sites"]]))
        expected_phase4 = "bounded" if origin == "helper" else "unknown"
        require(phase4["coverage"] == expected_phase4, (key, phase4["gap"]))
        return None, None
    origin = "dataset" if key.startswith("Set") else "datatable"
    base = dataset if origin == "dataset" else table
    expected = STATE_CASES.get(key)
    if key == "ReadBody":
        require(phase4["gap"] == FIELD_GAP, (key, phase4["gap"]))
    elif BASE.get(key, FIELD_GAP) is None:
        require(base["coverage"] == "bounded", (key, base["gap"]))
    else:
        require(base["coverage"] == "unknown" and base["gap"] == BASE.get(key, FIELD_GAP), (key, base["gap"]))
    if expected is None or isinstance(expected, tuple):
        require(state["coverage"] == "bounded" and state["gap"] is None and state["origin"] == origin and not state["originGaps"]
                and len(state["sites"]) == 1 and state["commandType"] == "StoredProcedure", (key, state["gap"], state["originGaps"]))
        site = state["sites"][0]
        require(site["path"] is None and site["caller"] and PHASE4.member(site["caller"]) == key, (key, site))
        if expected is None:
            require(site["coverage"] == "bounded" and site["gap"] is None and has_field(site) and site["commandTextHash"], (key, site))
            require(all(a["typeArgument"] for a in site["accesses"] if a["member"] == "Field"), (key, site))
            return 0, None
        require(site["coverage"] == "unknown" and site["gap"] == expected[1] and not site["accesses"], (key, site))
        return 0, expected[1]
    require(state["coverage"] == "unknown" and state["gap"] == "field-read-origin-unbound" and not state["sites"] and expected in state["originGaps"]
            and [g.split(":")[0] for g in state["originGaps"]] == ["datatable", "dataset", "helper", "internal-helper"], (key, state["gap"], state["originGaps"]))
    return None, expected


def check_site_state(key, fact, index, site, language):
    origin = json.loads(fact["properties"][STATE])["origin"]
    extension = "vb" if language == "vb" else "cs"
    require(site["path"] == (f"{language}/Callers.{extension}" if origin == "internal-helper" else None), (key, site["path"]))
    sibling = json.loads(fact["properties"]["sqlServerInternalHelperResultState" if origin == "internal-helper" else "sqlServerHelperResultState"])
    caller = sibling["callers"][index]
    require(caller["line"] == site["line"] and caller["caller"] == site["caller"], (key, caller, site))
    base = SITE_BASE.get(key, FIELD_GAP)
    require((caller["coverage"], caller["gap"]) == (("bounded", None) if base is None else ("unknown", base)), (key, caller["gap"]))
    expected = SITE_CASES.get(key)
    if expected is None:
        require(site["coverage"] == "bounded" and site["gap"] is None and has_field(site) and site["commandTextHash"], (key, site))
        return None
    require(site["coverage"] == "unknown" and site["gap"] == expected and not site["accesses"], (key, site["gap"], expected))
    return expected


def check_report(case, fact, index, expected, origin, reason, core_hash, schema_path, site_path):
    for name in (REPORT, "report.md"):
        require((case / "report" / name).read_bytes() == (case / "repeat" / name).read_bytes(), (case, name))
    raw = (case / "report" / REPORT).read_bytes()
    report = json.loads(raw)
    require(report["version"] == "sqlserver-field-read-comparison/1" and report["consumerVersion"] == STATE_VERSION
            and report["bindingsVersion"] == BINDINGS_VERSION, "report contract")
    require(report["schemaRoute"] == origin and report["origin"] == ORIGIN_LABELS[origin], (origin, report["origin"]))
    require(report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict", "coverage/verdict")
    require(report["generatorSha256"] == core_hash, "report generator")
    require(report["schemaEvidenceSha256"] == HELPERS.digest(json.loads(schema_path.read_bytes())), "admitted export digest")
    rows = [r for r in report["comparisons"] if r["factId"] == fact["factId"] and r["siteIndex"] == index]
    statuses = [r["status"] for r in rows]
    require(statuses == expected, (case.name, statuses, expected, rows))
    if reason is not None:
        require(len(rows) == 1 and reason in rows[0]["reasons"], (case.name, reason, rows))
    for row in rows:
        require(row["sitePath"] == site_path, (case.name, site_path, row))
        if row["access"] and row["access"]["member"] == "Field":
            require(f"field-type-argument-not-compared:{row['access']['typeArgument']}" in row["uncomparedFacets"], (case.name, row))
    for row in report["comparisons"]:
        if row not in rows:
            require(row["status"] == U, row)
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
        RESULT.run_ok("dotnet", cli, "scan", "--repo", SAMPLE, "--project", f"{language}/FieldReads.{extension}", "--out", scan)
        scan_artifacts[language] = HELPERS.scan_hashes(args.output, language)
        manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        require(manifest["commitSha"] == revision and manifest["buildStatus"] == "Succeeded", (language, manifest["buildStatus"]))
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"
                 and f["properties"].get("operationKind") == "data-adapter-fill"]
        fills = fill_keys(calls)
        sites = site_keys(fills)
        gaps += [f"{language}:{g}" for g in case_gaps([k for k, _ in fills], [k for k, _ in sites])]
        require(len(calls) == SITES_PER_LANGUAGE or gaps, len(calls))
        reasons, paths = {}, {}
        for key, fact in fills:
            index, reasons[("fill", key)] = check_fill_state(key, fact)
            if key not in HELPER_FILLS:
                selected[(language, "fill", key)] = (fact, index)
            for site in range(len(json.loads(fact["properties"][STATE])["sites"])):
                public_inputs.append(field_projection(fact, site))
        for key, (fact, index, site) in sites:
            reasons[("site", key)] = check_site_state(key, fact, index, site, language)
            selected[(language, "site", key)] = (fact, index)
            paths[key] = site["path"]
        if shapes is None and fills:
            shapes = DATASET.Shapes(args.output, cli, fills[0][1]["repo"], revision)
        for number, (kind, key, origin, variant, expected) in enumerate(plan):
            if (language, kind, key) not in selected:
                continue
            fact, index = selected[(language, kind, key)]
            case = args.output / language / f"{number:03}-{kind}-{key}-{origin}-{variant or 'base'}"
            case.mkdir(parents=True)
            projected = field_projection(fact, -1 if index is None else index)
            HELPERS.write(case / "bindings.json", binding(fact, index, key, revision, projected))
            options, schema, _ = shapes.get(origin, variant)
            common = ["dotnet", cli, ROUTE, "compare", "--origin", origin, *options, "--schema", schema,
                      "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson", "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                RESULT.run_ok(*common, "--out", case / output)
            statuses, digest = check_report(case, fact, index, expected, origin, reasons[(kind, key)], core_hash, schema, paths.get(key))
            results.append({"language": language, "kind": kind, "case": key, "origin": origin, "variant": variant, "factId": fact["factId"],
                            "siteIndex": index, "sitePath": paths.get(key), "statuses": statuses, "reportSha256": digest})
    refusals = check_refusals(args.output, cli, revision, selected, shapes) if not gaps else []
    counts = {s: sum(r["statuses"].count(s) for r in results) for s in (M, X, U)}
    expected_counts = {s: 2 * n for s, n in EXPECTED_TOTALS.items()}
    hashes = shapes.hashes if shapes else {}
    receipt = {"version": "sqlserver-field-read-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"base": shapes.base if shapes else None, "templates": hashes, "sites": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash,
               "resultHelperGeneratorSha256": RESULT.GENERATOR, "datasetHelperGeneratorSha256": DATASET.GENERATOR,
               "phase4HelperGeneratorSha256": PHASE4.GENERATOR, "phase5HelperGeneratorSha256": PHASE5.GENERATOR,
               "helperGeneratorSha256": HELPERS.GENERATOR, "scanArtifactSha256": scan_artifacts, "sitesPerLanguage": SITES_PER_LANGUAGE,
               "casesPerLanguage": len(plan), "expectedCounts": expected_counts, "observedCounts": counts, "gaps": gaps,
               "refusals": refusals, "results": results, "sqlExecution": "not-run"}
    publish_receipt(args.output, receipt)
    print(f"Field-read native smoke passed at {revision}: {len(plan)} cases per language, {counts}; "
          f"{len(refusals)} refusals; SQL NOT RUN")


def check_refusals(root, cli, revision, selected, shapes):
    """Site, route, digest and type-argument substitution and whole-input failures must exit non-zero and publish nothing."""
    work = root / "refusals"
    work.mkdir()
    fact, first = selected[("csharp", "site", "InternalFieldName")]
    same, second = selected[("csharp", "site", "InternalFieldOrdinal")]
    require(same["factId"] == fact["factId"] and second != first, "two sites of one helper")
    scan = root / "csharp/scan/facts.ndjson"
    good = binding(fact, first, "InternalFieldName", revision, field_projection(fact, first))
    HELPERS.write(work / "bindings.json", good)
    json_options, json_schema, _ = shapes.get("result-schema", None)
    table_options, table_schema, _ = shapes.get("ddl-table-result", None)
    checks = []

    def record(name, *command, output):
        ok, why = RESULT.refused(*command, output=output)
        checks.append({"case": name, "refused": ok, "message": why})

    def compare(name, origin="result-schema", options=None, schema=None, bindings=None, facts=scan, rev=revision, route=ROUTE):
        out = work / f"out-{name}"
        record(name, "dotnet", cli, route, "compare", "--origin", origin, *(options or json_options),
               "--schema", schema or json_schema, "--bindings", bindings or work / "bindings.json", "--facts", facts,
               "--repo", fact["repo"], "--revision", rev, "--out", out, output=out)

    def write_bindings(name, document):
        HELPERS.write(work / f"{name}.json", document)
        return work / f"{name}.json"

    def changed(name, **fields):
        document = copy.deepcopy(good)
        document["calls"][0].update(fields)
        return write_bindings(name, document)

    def as_route(name, version, field, projected, keep_index=None):
        other = copy.deepcopy(good)
        other["version"] = version
        call = {k: v for k, v in other["calls"][0].items() if k not in ("siteIndex", "fieldReadEvidenceSha256")}
        if keep_index is not None:
            call["callerIndex"] = keep_index
        call[field] = HELPERS.digest(projected)
        other["calls"] = [call]
        compare(f"{name}-bindings-as-field-bindings", bindings=write_bindings(f"{name}-bindings", other))

    datatable_projection = DATASET.datatable_projection(fact)
    internal_projection = PHASE5.internal_projection(fact, first)
    as_route("datatable", "sqlserver-datatable-bindings/1", "dataTableEvidenceSha256", datatable_projection)
    as_route("dataset", "sqlserver-dataset-bindings/1", "dataSetEvidenceSha256", DATASET.dataset_projection(fact))
    as_route("helper", PHASE4.BINDINGS_VERSION, "helperResultEvidenceSha256", PHASE4.helper_projection(fact, first), keep_index=first)
    as_route("internal-helper", PHASE5.BINDINGS_VERSION, "internalHelperResultEvidenceSha256", internal_projection, keep_index=first)
    reader = RESULT.binding(fact, "NameMatch", revision, RESULT.reader_projection(fact))
    compare("reader-bindings-as-field-bindings", bindings=write_bindings("reader-bindings", reader))
    parameter = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
                 "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItems", "identity": RESULT.IDENTITY,
                            "sourceEvidenceSha256": HELPERS.digest(HELPERS.projection(fact))}]}
    compare("parameter-bindings-as-field-bindings", bindings=write_bindings("parameter-bindings", parameter))
    compare("datatable-digest-substitution", bindings=changed("datatable-digest", fieldReadEvidenceSha256=HELPERS.digest(datatable_projection)))
    compare("internal-helper-digest-substitution", bindings=changed("internal-digest", fieldReadEvidenceSha256=HELPERS.digest(internal_projection)))
    compare("stale-field-digest", bindings=changed("stale-digest", fieldReadEvidenceSha256="0" * 64))
    # Site 0's binding retargeted to site 1 (same digest), site 1's digest at index 0, and an absent index.
    compare("site-index-substitution", bindings=changed("site-index", siteIndex=second))
    compare("site-digest-substitution", bindings=changed("site-digest", fieldReadEvidenceSha256=HELPERS.digest(field_projection(fact, second))))
    compare("out-of-range-site-index", bindings=changed("out-of-range", siteIndex=999))
    duplicate = copy.deepcopy(good)
    duplicate["calls"].append(copy.deepcopy(duplicate["calls"][0]))
    compare("duplicate-binding", bindings=write_bindings("duplicate-binding", duplicate))
    compare("stale-revision", rev="2" * 40)
    for name, mutate in (("mutated-site-access", lambda s: s["accesses"][0].update(name="Title")),
                         ("mutated-type-argument", lambda s: s["accesses"][0].update(typeArgument="System.Int32")),
                         ("mutated-site-procedure-hash", lambda s: s.update(commandTextHash="f" * 64))):
        tampered = [json.loads(line) for line in scan.read_text().splitlines()]
        for item in tampered:
            if item["factId"] == fact["factId"]:
                state = json.loads(item["properties"][STATE])
                mutate(state["sites"][first])
                item["properties"][STATE] = json.dumps(state, separators=(",", ":"))
        (work / f"{name}.ndjson").write_text("".join(json.dumps(i, separators=(",", ":")) + "\n" for i in tampered))
        compare(name, facts=work / f"{name}.ndjson")
    for route in ("sqlserver-datatable-result", "sqlserver-dataset-result", "sqlserver-helper-result", "sqlserver-internal-helper-result"):
        compare(f"field-bindings-in-{route.removeprefix('sqlserver-').removesuffix('-result')}-route", route=route)
    out = work / "out-field-bindings-in-reader-route"
    record("field-bindings-in-reader-route", "dotnet", cli, "sqlserver-result-schema", "compare", *json_options, "--schema", json_schema,
           "--bindings", work / "bindings.json", "--facts", scan, "--repo", fact["repo"], "--revision", revision, "--out", out, output=out)
    compare("table-export-as-result-schema", schema=table_schema)
    compare("unknown-origin", origin="reader")
    (work / "oversized-bindings.json").write_bytes(b" " * (4 * 1024 * 1024 + 1))
    compare("oversized-bindings", bindings=work / "oversized-bindings.json")
    existing = work / "existing"
    existing.mkdir()
    (existing / "keep.txt").write_bytes(b"prior winner\n")
    result = RESULT.run_captured_cli("dotnet", cli, ROUTE, "compare", "--origin", "result-schema", *json_options,
                                     "--schema", json_schema, "--bindings", work / "bindings.json", "--facts", scan,
                                     "--repo", fact["repo"], "--revision", revision, "--out", existing)
    checks.append({"case": "existing-output-not-replaced", "refused": result.returncode != 0
                   and sorted(p.name for p in existing.iterdir()) == ["keep.txt"] and (existing / "keep.txt").read_bytes() == b"prior winner\n",
                   "message": []})
    return checks


if __name__ == "__main__":
    main()
