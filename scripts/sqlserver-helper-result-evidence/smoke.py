#!/usr/bin/env python3
"""Offline one-level helper -> returned DataTable/DataSet consumer native smoke over public synthetic C#/VB scans.

Never invokes fixture methods or SQL. Reuses the #912 result smoke helpers (and through them the parameter smoke helpers)
and the DataSet smoke's shape producer without changing their bytes, so retained receipts and denominators stay
comparable. Each call site of a bounded helper is bound and compared separately.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

if sys.flags.optimize:
    raise RuntimeError("Helper-result smoke requires Python optimization disabled")


def require(condition, detail):
    if not condition:
        raise ValueError(f"Helper-result smoke validation failed: {detail}")


ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
SPEC = importlib.util.spec_from_file_location("result_smoke", ROOT / "scripts/sqlserver-result-column-evidence/smoke.py")
RESULT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(RESULT)
DATASET_SPEC = importlib.util.spec_from_file_location("dataset_smoke", ROOT / "scripts/sqlserver-dataset-evidence/smoke.py")
DATASET = importlib.util.module_from_spec(DATASET_SPEC)
DATASET_SPEC.loader.exec_module(DATASET)
HELPERS = RESULT.HELPERS
SAMPLE = ROOT / "samples/sqlserver-helper-result-evidence"
SHAPES = RESULT.SAMPLE  # JSON base declarations, ddl/ and ddl-table/ templates are reused unchanged.
M, X, U = RESULT.M, RESULT.X, RESULT.U
BINDINGS_VERSION = "sqlserver-helper-result-bindings/1"
STATE_VERSION = "sqlserver-helper-result/2"
RULE = "database.sqlserver.helper-result-column-comparison.v1"
REPORT = "helper-result-column-comparison.json"
ORIGINS = DATASET.ORIGINS
ORIGIN_LABELS = DATASET.ORIGIN_LABELS
SITES_PER_LANGUAGE = 32
BOUNDED_HELPERS = ("ReadTable", "ReadSet", "ReadNamedSet", "ReadTableText", "ReadTableUsing")
MEMBER = re.compile(r"\.([A-Za-z]+)(?:<[^>]*>|\(Of [^)]*\))?\(")

# Independently authored before any extractor output (design.md). Core caller cells run against all three origins.
CORE_CASES = {
    "TableName": ([M], None), "TableOrdinal": ([M], None), "TableForeach": ([M], None), "TableRowsGuard": ([M], None),
    "OtherProcedure": ([M], None), "TwoCallers#0": ([M], None), "TwoCallers#1": ([M], None), "SetIndex": ([M], None),
    "SetDefaultName": ([M], None), "SetSourceName": ([M], None), "SetTableLocalGuards": ([M], None), "TextHelper": ([M], None),
    "ConnectionHelper": ([M], None), "ConstProcedure": ([M], None),
    "MissingName": ([X], None), "OrdinalOutOfRange": ([X], None), "OtherProcedureMissing": ([X], None), "SetMissing": ([X], None),
    "Renamed": ([X], "renamed"), "CaseOnly": ([U], None), "DynamicName": ([U], None), "AbsentBinding": ([U], None),
    "Incomplete": ([U], "incomplete"),
}
# Helper state cells (table-backed DDL only), keyed by the Fill's enclosing method: exact gap, or ("bounded", reason).
HELPER_CASES = {
    "PublicHelper": "helper-accessibility-unsupported", "InternalHelper": "helper-accessibility-unsupported",
    "InstanceHelper": "helper-static-required", "VirtualHelper": "helper-virtual-or-interface-unsupported",
    "GenericHelper": "helper-generic-unsupported", "OverloadedHelper#0": "helper-overload-ambiguous", "OverloadedHelper#1": "helper-overload-ambiguous",
    "ExtensionHelper": "helper-extension-unsupported", "PartialHelper": "helper-partial-type-unsupported", "RefHelper": "helper-parameter-unsupported",
    "OptionalHelper": "helper-parameter-unsupported", "TwoStringHelper": "helper-parameter-unsupported", "ObjectHelper": "helper-return-type-unsupported",
    "RecursiveHelper": "helper-recursion", "DelegateHelper": "helper-reference-escape", "MutatingHelper": "datatable-mutation",
    "GuardedHelper": "unsupported-control-flow", "ExecutingHelper": "command-executed-outside-fill", "MultipleReturnHelper": "helper-multiple-returns",
    "ConstantTextHelper": "helper-command-text-parameter-required", "ParameterWriteHelper": "helper-parameter-mutation",
    "CallingHelper": "helper-body-unsupported", "DisposedHelper": "helper-result-disposed", "UncalledHelper": ("bounded", "helper-caller-unavailable"),
    "TextCommandHelper": ("bounded", "text-command-outside-scope"), "InlineFill": "helper-return-required", "CallerRefill": "helper-return-required",
}
# Caller state cells (table-backed DDL only), keyed by the calling method: exact gap, or ("bounded", reason).
CALLER_CASES = {
    "CallerNamedArgument": "helper-named-argument-unsupported", "CallerDynamicProcedure": "helper-procedure-argument-nonconstant",
    "CallerNullProcedure": "helper-procedure-argument-invalid", "CallerDiscarded": "helper-result-local-required",
    "CallerChained": "helper-result-local-required", "CallerPassed": "helper-result-local-required", "CallerObjectLocal": "helper-result-local-required",
    "CallerRowWrite": "datatable-mutation", "CallerRefill": "repeated-fill", "CallerReassigned#0": "datatable-reassignment",
    "CallerReassigned#1": "helper-result-local-required", "CallerAlias": "datatable-alias-or-escape", "CallerTryCatch": "unsupported-control-flow",
    "CallerLambda": "unsupported-control-flow", "CallerLaterTable": "dataset-later-table-unsupported", "CallerSetMutation": "dataset-mutation",
    "CallerSourceNameMismatch": "dataset-table-name-unbound", "CallerConnectionArgument": "helper-connection-argument-unsupported",
    "CallerNoReads": ("bounded", "no-bounded-column-access"),
}
OTHER_PROCEDURE = {"OtherProcedure", "TwoCallers#1", "OtherProcedureMissing", "CallerReassigned#1"}
EXPECTED_TOTALS = {M: 42, X: 15, U: 58}  # Per language; C# and VB double it.
REFUSAL_COUNT = 24


def expected_totals():
    """Derive the per-language denominator from the authored matrices only."""
    rows = [s for statuses, _ in CORE_CASES.values() for s in statuses] * len(ORIGINS)
    rows += [U] * (len(HELPER_CASES) + len(CALLER_CASES))
    return {s: rows.count(s) for s in (M, X, U)}


def case_plan():
    """Every (kind, case key, origin, variant, expected statuses) cell in a stable order."""
    plan = [("caller", key, origin, variant, list(statuses)) for key, (statuses, variant) in CORE_CASES.items() for origin in ORIGINS]
    plan += [("helper", key, "ddl-table-result", None, [U]) for key in HELPER_CASES]
    return plan + [("caller", key, "ddl-table-result", None, [U]) for key in CALLER_CASES]


def member(symbol):
    return MEMBER.search(symbol)[1]


def keyed(items):
    """(name, item) pairs in source order; repeated names get #n suffixes."""
    items = list(items)
    counts = {}
    for name, _ in items:
        counts[name] = counts.get(name, 0) + 1
    seen, result = {}, []
    for name, item in items:
        number = seen.get(name, 0)
        seen[name] = number + 1
        result.append((f"{name}#{number}" if counts[name] > 1 else name, item))
    return result


def fill_keys(facts):
    """Fill facts keyed by their enclosing method in source order."""
    return keyed((member(f["sourceSymbol"]), f) for f in sorted(facts, key=lambda f: f["evidence"]["startLine"]))


def caller_keys(fills):
    """Call sites of the bounded helpers keyed by their calling member in source order: (key, (fact, index, caller))."""
    sites = []
    for key, fact in fills:
        if key not in BOUNDED_HELPERS:
            continue
        for index, caller in enumerate(json.loads(fact["properties"]["sqlServerHelperResultState"])["callers"]):
            sites.append((member(caller["caller"]), (fact, index, caller)))
    return keyed(sorted(sites, key=lambda s: s[1][2]["line"]))


def case_gaps(fill_observed, caller_observed):
    """A missing or unexpected case is an explicit gap, never a renamed pass."""
    gaps = []
    for kind, known, observed in (("helper", set(BOUNDED_HELPERS) | set(HELPER_CASES), list(fill_observed)),
                                  ("caller", set(CORE_CASES) | set(CALLER_CASES), list(caller_observed))):
        gaps += [f"missing:{kind}:{k}" for k in sorted(known) if k not in observed]
        gaps += [f"unexpected:{kind}:{k}" for k in observed if k not in known]
        gaps += [f"duplicate:{kind}:{k}" for k in sorted(set(observed)) if observed.count(k) > 1]
    return sorted(gaps)


def helper_projection(fact, index):
    """Mirror of Core SqlServerHelperResultEvidence.Projection for one call site (ASCII public fixture values only)."""
    span = fact["evidence"]
    state = json.loads(fact["properties"]["sqlServerHelperResultState"])
    callers = state["callers"]
    caller = callers[index] if 0 <= index < len(callers) else None
    state["callers"] = []
    return {"contract": BINDINGS_VERSION, "factId": fact["factId"], "repo": fact["repo"], "commitSha": fact["commitSha"],
            "ruleId": fact["ruleId"], "evidenceTier": fact["evidenceTier"],
            "filePath": span["filePath"], "startLine": span["startLine"], "endLine": span["endLine"],
            "extractorId": span["extractorId"], "extractorVersion": span["extractorVersion"],
            "callerIndex": index, "helperState": state, "caller": caller}


def binding(fact, index, key, revision, projected):
    identity = dict(RESULT.IDENTITY)
    if key in OTHER_PROCEDURE:
        identity["procedure"] = "ReadOther"
    if key == "AbsentBinding":
        identity["database"] = "OtherDatabase"  # Exact four-part identity absent from every admitted shape.
    calls = [] if index is None else [{"factId": fact["factId"], "callerIndex": index,
                                       "commandText": f"{identity['schema']}.{identity['procedure']}", "identity": identity,
                                       "helperResultEvidenceSha256": HELPERS.digest(projected)}]
    return {"version": BINDINGS_VERSION, "repo": fact["repo"], "revision": revision, "calls": calls,
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(projected)}}


def check_helper_state(key, fact):
    """Helper-level source state against the authored matrix, plus the unchanged Phase 2/3 sibling states."""
    state = json.loads(fact["properties"]["sqlServerHelperResultState"])
    require(state["version"] == STATE_VERSION, (key, state["version"]))
    table = json.loads(fact["properties"]["sqlServerDataTableState"])
    dataset = json.loads(fact["properties"]["sqlServerDataSetState"])
    if key in BOUNDED_HELPERS:
        require(state["coverage"] == "bounded" and state["gap"] is None and state["commandType"] == "StoredProcedure", (key, state["gap"]))
        expected = ("fill-overload-unsupported", "dataset-alias-or-escape") if "Set" in key else ("datatable-alias-or-escape", "fill-overload-unsupported")
        require((table["gap"], dataset["gap"]) == expected, (key, table["gap"], dataset["gap"]))
        return None
    if key == "InlineFill":
        require(table["coverage"] == "bounded", (key, table["gap"]))
    expected = HELPER_CASES[key]
    if isinstance(expected, tuple):
        require(state["coverage"] == "bounded" and state["gap"] is None, (key, state["gap"]))
        return expected[1]
    require(state["coverage"] == "unknown" and state["gap"] == expected and not state["callers"], (key, state["gap"], expected))
    return expected


def check_caller_state(key, caller):
    expected = CALLER_CASES.get(key)
    if expected is None or isinstance(expected, tuple):
        require(caller["coverage"] == "bounded" and caller["gap"] is None and caller["commandTextHash"], (key, caller["gap"]))
        if expected is None:
            require(caller["accesses"], (key, caller))
        return expected[1] if expected else None
    require(caller["coverage"] == "unknown" and caller["gap"] == expected and not caller["accesses"], (key, caller["gap"], expected))
    return expected


def check_report(case, fact, index, expected, origin, reason, core_hash, schema_path):
    for name in (REPORT, "report.md"):
        require((case / "report" / name).read_bytes() == (case / "repeat" / name).read_bytes(), (case, name))
    raw = (case / "report" / REPORT).read_bytes()
    report = json.loads(raw)
    require(report["version"] == "sqlserver-helper-result-comparison/1" and report["consumerVersion"] == STATE_VERSION
            and report["bindingsVersion"] == BINDINGS_VERSION, "report contract")
    require(report["schemaRoute"] == origin and report["origin"] == ORIGIN_LABELS[origin], (origin, report["origin"]))
    require(report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict", "coverage/verdict")
    require(report["generatorSha256"] == core_hash, "report generator")
    require(report["schemaEvidenceSha256"] == HELPERS.digest(json.loads(schema_path.read_bytes())), "admitted export digest")
    rows = [r for r in report["comparisons"] if r["factId"] == fact["factId"] and (index is None or r["callerIndex"] == index)]
    statuses = [r["status"] for r in rows]
    require(statuses == expected, (case.name, statuses, expected, rows))
    if reason is not None:
        require(len(rows) == 1 and reason in rows[0]["reasons"], (case.name, reason, rows))
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
        RESULT.run_ok("dotnet", cli, "scan", "--repo", SAMPLE, "--project", f"{language}/Helpers.{extension}", "--out", scan)
        scan_artifacts[language] = HELPERS.scan_hashes(args.output, language)
        manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        require(manifest["commitSha"] == revision and manifest["buildStatus"] == "Succeeded", (language, manifest["buildStatus"]))
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"
                 and f["properties"].get("operationKind") == "data-adapter-fill"]
        fills = fill_keys(calls)
        sites = caller_keys(fills)
        gaps += [f"{language}:{g}" for g in case_gaps([k for k, _ in fills], [k for k, _ in sites])]
        require(len(calls) == SITES_PER_LANGUAGE or gaps, len(calls))
        reasons = {}
        for key, fact in fills:
            reasons[("helper", key)] = check_helper_state(key, fact)
            selected[(language, "helper", key)] = (fact, None)
            for index in range(len(json.loads(fact["properties"]["sqlServerHelperResultState"])["callers"])):
                public_inputs.append(helper_projection(fact, index))
        for key, (fact, index, caller) in sites:
            reasons[("caller", key)] = check_caller_state(key, caller)
            selected[(language, "caller", key)] = (fact, index)
        if shapes is None and fills:
            shapes = DATASET.Shapes(args.output, cli, fills[0][1]["repo"], revision)
        for number, (kind, key, origin, variant, expected) in enumerate(plan):
            if (language, kind, key) not in selected:
                continue
            fact, index = selected[(language, kind, key)]
            case = args.output / language / f"{number:03}-{kind}-{key.replace('#', '-')}-{origin}-{variant or 'base'}"
            case.mkdir(parents=True)
            projected = helper_projection(fact, -1 if index is None else index)
            HELPERS.write(case / "bindings.json", binding(fact, index, key, revision, projected))
            options, schema, _ = shapes.get(origin, variant)
            common = ["dotnet", cli, "sqlserver-helper-result", "compare", "--origin", origin, *options, "--schema", schema,
                      "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson", "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                RESULT.run_ok(*common, "--out", case / output)
            statuses, digest = check_report(case, fact, index, expected, origin, reasons[(kind, key)], core_hash, schema)
            results.append({"language": language, "kind": kind, "case": key, "origin": origin, "variant": variant, "factId": fact["factId"],
                            "callerIndex": index, "statuses": statuses, "reportSha256": digest})
    refusals = check_refusals(args.output, cli, revision, selected, shapes) if not gaps else []
    counts = {s: sum(r["statuses"].count(s) for r in results) for s in (M, X, U)}
    expected_counts = {s: 2 * n for s, n in EXPECTED_TOTALS.items()}
    hashes = shapes.hashes if shapes else {}
    receipt = {"version": "sqlserver-helper-result-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"base": shapes.base if shapes else None, "templates": hashes, "callers": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash,
               "resultHelperGeneratorSha256": RESULT.GENERATOR, "datasetHelperGeneratorSha256": DATASET.GENERATOR,
               "helperGeneratorSha256": HELPERS.GENERATOR, "scanArtifactSha256": scan_artifacts, "sitesPerLanguage": SITES_PER_LANGUAGE,
               "casesPerLanguage": len(plan), "expectedCounts": expected_counts, "observedCounts": counts, "gaps": gaps,
               "refusals": refusals, "results": results, "sqlExecution": "not-run"}
    publish_receipt(args.output, receipt)
    print(f"Helper-result native smoke passed at {revision}: {len(plan)} cases per language, {counts}; "
          f"{len(refusals)} refusals; SQL NOT RUN")


def check_refusals(root, cli, revision, selected, shapes):
    """Caller, route and digest substitution and whole-input failures must exit non-zero and publish nothing."""
    work = root / "refusals"
    work.mkdir()
    fact, first = selected[("csharp", "caller", "TwoCallers#0")]
    same, second = selected[("csharp", "caller", "TwoCallers#1")]
    require(same["factId"] == fact["factId"] and second != first, "two callers of one helper")
    scan = root / "csharp/scan/facts.ndjson"
    good = binding(fact, first, "TwoCallers#0", revision, helper_projection(fact, first))
    HELPERS.write(work / "bindings.json", good)
    json_options, json_schema, json_decl = shapes.get("result-schema", None)
    ddl_options, ddl_schema, _ = shapes.get("ddl-result", None)
    table_options, table_schema, _ = shapes.get("ddl-table-result", None)
    checks = []

    def record(name, *command, output):
        ok, why = RESULT.refused(*command, output=output)
        checks.append({"case": name, "refused": ok, "message": why})

    def compare(name, origin="result-schema", options=None, schema=None, bindings=None, facts=scan, rev=revision, route="sqlserver-helper-result"):
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

    dataset_projection = DATASET.dataset_projection(fact)
    datatable_projection = DATASET.datatable_projection(fact)
    for name, version, field, projected in (("datatable", "sqlserver-datatable-bindings/1", "dataTableEvidenceSha256", datatable_projection),
                                            ("dataset", "sqlserver-dataset-bindings/1", "dataSetEvidenceSha256", dataset_projection)):
        other = copy.deepcopy(good)
        other["version"] = version
        call = {k: v for k, v in other["calls"][0].items() if k not in ("callerIndex", "helperResultEvidenceSha256")}
        call[field] = HELPERS.digest(projected)
        other["calls"] = [call]
        compare(f"{name}-bindings-as-helper-bindings", bindings=write_bindings(f"{name}-bindings", other))
    reader = RESULT.binding(fact, "NameMatch", revision, RESULT.reader_projection(fact))
    compare("reader-bindings-as-helper-bindings", bindings=write_bindings("reader-bindings", reader))
    parameter = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
                 "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItems", "identity": RESULT.IDENTITY,
                            "sourceEvidenceSha256": HELPERS.digest(HELPERS.projection(fact))}]}
    compare("parameter-bindings-as-helper-bindings", bindings=write_bindings("parameter-bindings", parameter))
    compare("datatable-digest-substitution", bindings=changed("datatable-digest", helperResultEvidenceSha256=HELPERS.digest(datatable_projection)))
    compare("dataset-digest-substitution", bindings=changed("dataset-digest", helperResultEvidenceSha256=HELPERS.digest(dataset_projection)))
    compare("stale-helper-digest", bindings=changed("stale-digest", helperResultEvidenceSha256="0" * 64))
    # Caller 0's binding retargeted to caller 1 (same digest), caller 1's digest at index 0, and an absent index.
    compare("caller-index-substitution", bindings=changed("caller-index", callerIndex=second))
    compare("caller-digest-substitution", bindings=changed("caller-digest", helperResultEvidenceSha256=HELPERS.digest(helper_projection(fact, second))))
    compare("out-of-range-caller-index", bindings=changed("out-of-range", callerIndex=999))
    duplicate = copy.deepcopy(good)
    duplicate["calls"].append(copy.deepcopy(duplicate["calls"][0]))
    compare("duplicate-binding", bindings=write_bindings("duplicate-binding", duplicate))
    compare("stale-revision", rev="2" * 40)
    for name, mutate in (("mutated-caller-access", lambda c: c["accesses"][0].update(name="Title")),
                         ("mutated-caller-procedure-hash", lambda c: c.update(commandTextHash="f" * 64))):
        tampered = [json.loads(line) for line in scan.read_text().splitlines()]
        for item in tampered:
            if item["factId"] == fact["factId"]:
                state = json.loads(item["properties"]["sqlServerHelperResultState"])
                mutate(state["callers"][first])
                item["properties"]["sqlServerHelperResultState"] = json.dumps(state, separators=(",", ":"))
        (work / f"{name}.ndjson").write_text("".join(json.dumps(i, separators=(",", ":")) + "\n" for i in tampered))
        compare(name, facts=work / f"{name}.ndjson")
    compare("table-export-as-result-schema", schema=table_schema)
    compare("constant-export-as-table-schema", origin="ddl-table-result", options=table_options, schema=ddl_schema)
    compare("unknown-origin", origin="reader")
    compare("origin-option-mismatch", options=table_options)
    mutated = copy.deepcopy(json_decl)
    mutated["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-declarations.json", mutated)
    compare("mutated-json-declarations", options=["--declarations", work / "mutated-declarations.json"])
    for route in ("sqlserver-datatable-result", "sqlserver-dataset-result"):
        compare(f"helper-bindings-in-{route.split('-')[1]}-route", route=route)
    out = work / "out-helper-bindings-in-reader-route"
    record("helper-bindings-in-reader-route", "dotnet", cli, "sqlserver-result-schema", "compare", *json_options, "--schema", json_schema,
           "--bindings", work / "bindings.json", "--facts", scan, "--repo", fact["repo"], "--revision", revision, "--out", out, output=out)
    (work / "oversized-bindings.json").write_bytes(b" " * (4 * 1024 * 1024 + 1))
    compare("oversized-bindings", bindings=work / "oversized-bindings.json")
    existing = work / "existing"
    existing.mkdir()
    (existing / "keep.txt").write_bytes(b"prior winner\n")
    result = RESULT.run_captured_cli("dotnet", cli, "sqlserver-helper-result", "compare", "--origin", "result-schema", *json_options,
                                     "--schema", json_schema, "--bindings", work / "bindings.json", "--facts", scan,
                                     "--repo", fact["repo"], "--revision", revision, "--out", existing)
    checks.append({"case": "existing-output-not-replaced", "refused": result.returncode != 0
                   and sorted(p.name for p in existing.iterdir()) == ["keep.txt"] and (existing / "keep.txt").read_bytes() == b"prior winner\n",
                   "message": []})
    return checks


if __name__ == "__main__":
    main()
