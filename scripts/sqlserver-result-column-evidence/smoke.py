#!/usr/bin/env python3
"""Offline result-column native smoke over public synthetic C#/VB scans. Never invokes fixture methods or SQL."""
import argparse
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
SPEC = importlib.util.spec_from_file_location("json_smoke", ROOT / "scripts/sqlserver-parameter-evidence/smoke.py")
HELPERS = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(HELPERS)
SAMPLE = ROOT / "samples/sqlserver-result-column-evidence"
M, X, U = "bounded-match", "mismatch", "unknown"

# Independently authored before any extractor output was inspected: the ordered statuses of every
# report row for the one selected execution in each case. Keys with #n are the nth execution site.
SOURCE_CASES = {
    "NameMatch": [M], "MissingName": [X], "Renamed": [X], "OrdinalValid": [M], "OrdinalOutOfRange": [X],
    "Reordered": [M, M], "GetOrdinalDirect": [M, M], "GetOrdinalLocal": [M, M], "GetOrdinalMissing": [X, X],
    "CaseOnly": [U], "DuplicateDeclared": [U], "DynamicName": [U], "DynamicOrdinal": [U], "AbsentBinding": [U],
    "ReaderAlias": [U], "ReaderReassignment": [U], "CommandAlias": [U], "OrdinalAlias": [M, U],
    "OrdinalReassignment": [M, U], "HelperEscape": [U], "TwoReaders#0": [M], "TwoReaders#1": [M],
    "ReadLoop": [M, M], "ReadGuard": [M], "UsingLifetime": [M], "NextResult": [U], "ScalarChannel": [U],
    "NonQueryChannel": [U], "RecordsAffected": [U], "Incomplete": [U], "TextCommand": [U], "Conditional": [U],
    "WithParameter": [M], "NullCheck": [M],
}
JSON_VARIANTS = {"Renamed": "renamed", "Reordered": "reordered", "DuplicateDeclared": "duplicate", "Incomplete": "incomplete"}
DDL_TEMPLATES = {"Renamed": "renamed.sql", "Reordered": "reordered.sql", "DuplicateDeclared": "duplicate.sql", "Incomplete": "if.sql"}
# A supported-looking projection inside each unsupported body must never establish completeness.
BODY_MATRIX = ("if.sql", "nested.sql", "dynamic.sql", "multiple.sql", "star.sql", "from.sql", "union.sql",
               "assignment.sql", "settings.sql", "return.sql", "noalias.sql")
EXPECTED_TOTALS = {"json": {M: 18, X: 5, U: 18}, "ddl": {M: 18, X: 5, U: 29}}  # Per language; C# and VB double it.
SITES_PER_LANGUAGE = 34
IDENTITY = {"server": "SyntheticServer", "database": "SyntheticDatabase", "schema": "dbo", "procedure": "ReadItems"}


def expected_totals():
    """Derive the denominator from the authored matrix only."""
    json_rows = [s for statuses in SOURCE_CASES.values() for s in statuses]
    ddl_rows = json_rows + [U] * len(BODY_MATRIX)
    count = lambda rows: {s: rows.count(s) for s in (M, X, U)}
    return {"json": count(json_rows), "ddl": count(ddl_rows)}


def case_keys(facts):
    """Map execution facts to authored case keys in source order; repeated methods get #n suffixes."""
    seen = {}
    keys = []
    for fact in sorted(facts, key=lambda f: f["evidence"]["startLine"]):
        name = re.search(r"\.([A-Za-z]+)\(", fact["sourceSymbol"])[1]
        number = seen.get(name, 0)
        seen[name] = number + 1
        keys.append((fact, name, number))
    repeated = {n for n, c in seen.items() if c > 1}
    return [(f, f"{n}#{i}" if n in repeated else n) for f, n, i in keys]


def case_gaps(observed):
    """A missing or unexpected case is an explicit gap, never a renamed pass."""
    observed = list(observed)
    gaps = [f"missing:{k}" for k in SOURCE_CASES if k not in observed]
    gaps += [f"unexpected:{k}" for k in observed if k not in SOURCE_CASES]
    gaps += [f"duplicate:{k}" for k in set(observed) if observed.count(k) > 1]
    return sorted(gaps)


def reader_projection(fact):
    """Mirror of Core SqlServerResultEvidence.ReaderProjection (ASCII public fixture values only)."""
    span = fact["evidence"]
    state = fact["properties"].get("sqlServerReaderState")
    return {"factId": fact["factId"], "repo": fact["repo"], "commitSha": fact["commitSha"],
            "ruleId": fact["ruleId"], "evidenceTier": fact["evidenceTier"],
            "filePath": span["filePath"], "startLine": span["startLine"], "endLine": span["endLine"],
            "extractorId": span["extractorId"], "extractorVersion": span["extractorVersion"],
            "state": None if state is None else json.loads(state)}


def declarations(base, variant, repo, revision, path):
    d = copy.deepcopy(base)
    d.update(repo=repo, revision=revision)
    for shape in d["shapes"]:
        shape.update(filePath=path, startLine=1, endLine=1)
        for column in shape["columns"]:
            column["line"] = 1
    items = d["shapes"][0]
    if variant == "renamed":
        items["columns"][1]["name"] = "DisplayName"
    elif variant == "reordered":
        items["columns"] = [dict(c, ordinal=i) for i, c in enumerate(reversed(items["columns"]))]
    elif variant == "duplicate":
        items["columns"][2] = dict(items["columns"][1], ordinal=2)
    elif variant == "incomplete":
        # Columns stay listed but an unknown shape can never establish a match or a missing column.
        items.update(completeness="unknown", unknownReason="authored-incomplete")
    d["provenance"] = {"generatorSha256": GENERATOR,
                       "boundedInputSha256": HELPERS.digest({"base": base, "variant": variant, "repo": repo, "revision": revision})}
    return d


def binding(fact, key, revision, projected):
    identity = dict(IDENTITY)
    if key == "TwoReaders#1":
        identity["procedure"] = "ReadOther"
    if key == "AbsentBinding":
        identity["database"] = "OtherDatabase"  # Exact four-part identity absent from every admitted shape.
    return {"version": "sqlserver-result-bindings/1", "repo": fact["repo"], "revision": revision,
            "calls": [{"factId": fact["factId"], "commandText": f"{identity['schema']}.{identity['procedure']}",
                       "identity": identity, "readerEvidenceSha256": HELPERS.digest(projected)}],
            "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(projected)}}


def run_ok(*args):
    HELPERS.run(*args)


def run_captured_cli(*args):
    if not args or args[0] != "dotnet":
        raise ValueError("Only the local dotnet CLI is admitted")
    # Fixed executable, literal argv, no shell: paths/metacharacters are data.
    # nosemgrep: python.lang.security.audit.dangerous-subprocess-use-audit
    return subprocess.run(["dotnet", *[str(a) for a in args[1:]]], cwd=ROOT, capture_output=True, text=True, shell=False)


def refused(*args, output):
    result = run_captured_cli(*args)
    return result.returncode != 0 and not Path(output).exists(), result.stderr.strip().splitlines()[-1:] if result.stderr else []


def check_report(case, fact, expected, origin):
    for name in ("result-column-comparison.json", "report.md"):
        assert (case / "report" / name).read_bytes() == (case / "repeat" / name).read_bytes(), (case, name)
    raw = (case / "report/result-column-comparison.json").read_bytes()
    wrapper = json.loads(raw)
    report = wrapper["comparison"] if origin == "ddl" else wrapper
    assert report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict"
    rows = [r for r in report["comparisons"] if r["factId"] == fact["factId"]]
    statuses = [r["status"] for r in rows]
    assert statuses == expected, (origin, case.name, statuses, expected, rows)
    for row in report["comparisons"]:
        if row["factId"] != fact["factId"]:
            assert row["status"] == U and "binding-missing" in row["reasons"], row
        assert row["tier"] == ("Tier4Unknown" if row["status"] == U else "Tier2Structural")
        assert row["sourceTier"] == "Tier1Semantic"
    markdown = (case / "report/report.md").read_text()
    assert "No aggregate clean verdict" in markdown
    for row in rows:  # Unknown and mismatch reasons survive into human output.
        for reason in row["reasons"]:
            assert reason in markdown, (case.name, reason)
    return wrapper, report, statuses, hashlib.sha256(raw).hexdigest()


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
    base_bytes = (SAMPLE / "result-declarations-base.json").read_bytes()
    base = json.loads(base_bytes)
    templates = SAMPLE / "ddl"
    template_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(templates.glob("*.sql"))}
    results, public_inputs, gaps, scan_artifacts = [], [], [], {}
    selected = {}
    for language in ("csharp", "vb"):
        scan = args.output / language / "scan"
        extension = "vbproj" if language == "vb" else "csproj"
        run_ok("dotnet", cli, "scan", "--repo", SAMPLE, "--project", f"{language}/Results.{extension}", "--out", scan)
        scan_artifacts[language] = HELPERS.scan_hashes(args.output, language)
        manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        assert manifest["commitSha"] == revision and manifest["buildStatus"] == "Succeeded"
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"]
        keyed = case_keys(calls)
        gaps += [f"{language}:{g}" for g in case_gaps(k for _, k in keyed)]
        assert len(calls) == SITES_PER_LANGUAGE or gaps, len(calls)
        for number, (fact, key) in enumerate(keyed):
            expected = SOURCE_CASES.get(key)
            if expected is None:
                continue
            projected = reader_projection(fact)
            public_inputs.append(projected)
            selected[(language, key)] = fact
            bindings = binding(fact, key, revision, projected)
            case_names = [("json", None), ("ddl", DDL_TEMPLATES.get(key, "base.sql"))]
            if key == "NameMatch":
                case_names += [("ddl", t) for t in BODY_MATRIX]
            for origin, template in case_names:
                case = args.output / language / origin / f"{number:02}-{key.replace('#', '-')}" / (template or "declarations")
                case.mkdir(parents=True)
                HELPERS.write(case / "bindings.json", bindings)
                if origin == "json":
                    HELPERS.write(case / "declarations.json", declarations(base, JSON_VARIANTS.get(key), fact["repo"], revision,
                                                                       f"{language}/json/{case.parent.name}/declarations.json"))
                    run_ok("dotnet", cli, "sqlserver-result-schema", "produce", "--declarations", case / "declarations.json", "--out", case / "schema.json")
                    common = ["dotnet", cli, "sqlserver-result-schema", "compare", "--declarations", case / "declarations.json"]
                else:
                    source = {"repo": fact["repo"], "revision": revision, "file": template, "sha256": template_hashes[template]}
                    input_doc = {"version": "sqlserver-ddl-result-input/1", "repo": fact["repo"], "revision": revision,
                                 "server": IDENTITY["server"], "database": IDENTITY["database"], "publicSynthetic": True,
                                 "files": [{"path": template, "sha256": template_hashes[template]}],
                                 "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(source)}}
                    HELPERS.write(case / "input.json", input_doc)
                    run_ok("dotnet", cli, "sqlserver-ddl-result", "produce", "--input", case / "input.json", "--source-root", templates,
                           "--out", case / "schema.json")
                    common = ["dotnet", cli, "sqlserver-ddl-result", "compare", "--input", case / "input.json", "--source-root", templates]
                common += ["--schema", case / "schema.json", "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson",
                           "--repo", fact["repo"], "--revision", revision]
                for output in ("report", "repeat"):
                    run_ok(*common, "--out", case / output)
                want = [U] if template in BODY_MATRIX else expected
                wrapper, report, statuses, digest = check_report(case, fact, want, origin)
                assert wrapper["generatorSha256"] == core_hash
                if origin == "ddl":
                    ddl = wrapper["ddlEvidence"]
                    assert ddl["generatorSha256"] == core_hash and ddl["parserSha256"] == parser_hash
                    assert ddl["boundedInputSha256"] == HELPERS.digest(input_doc)
                    assert ddl["declarations"]["origin"] == "public-synthetic-tsql-ddl-result"
                    assert ddl["ruleId"] == "database.sqlserver.ddl-result-shape.v1" and ddl["tier"] == "Tier2Structural"
                    if template in BODY_MATRIX:
                        assert all(s["completeness"] == "unknown" and not s["columns"] for s in ddl["declarations"]["shapes"]), template
                else:
                    assert report["origin"] == "independent-synthetic-result-declarations"
                    assert report["schemaRuleId"] == "database.sqlserver.synthetic-result-shape.v1"
                results.append({"language": language, "origin": origin, "case": key, "template": template, "factId": fact["factId"],
                                "statuses": statuses, "reportSha256": digest})
    refusals = check_refusals(args.output, cli, revision, base, selected, templates, template_hashes) if not gaps else []
    counts = {origin: {s: sum(r["statuses"].count(s) for r in results if r["origin"] == origin) for s in (M, X, U)} for origin in ("json", "ddl")}
    expected_counts = {origin: {s: 2 * n for s, n in totals.items()} for origin, totals in EXPECTED_TOTALS.items()}
    receipt = {"version": "sqlserver-result-column-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"base": base, "templates": template_hashes, "calls": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash, "helperGeneratorSha256": HELPERS.GENERATOR,
               "scanArtifactSha256": scan_artifacts, "sitesPerLanguage": SITES_PER_LANGUAGE, "expectedCounts": expected_counts,
               "observedCounts": counts, "gaps": gaps, "refusals": refusals, "results": results, "sqlExecution": "not-run"}
    HELPERS.verify_scan_hashes(args.output, receipt)
    HELPERS.write(args.output / "smoke-receipt.json", receipt)
    assert not gaps, gaps
    assert counts == expected_counts, counts
    assert refusals and all(r["refused"] for r in refusals), refusals
    print(f"result-column native smoke passed at {revision}: {counts}; {len(refusals)} refusals; SQL NOT RUN")


def check_refusals(root, cli, revision, base, selected, templates, template_hashes):
    """Whole-input failures must exit non-zero and publish nothing."""
    work = root / "refusals"
    work.mkdir()
    fact = selected[("csharp", "NameMatch")]
    scan = root / "csharp/scan/facts.ndjson"
    projected = reader_projection(fact)
    good = binding(fact, "NameMatch", revision, projected)
    decl = declarations(base, None, fact["repo"], revision, "refusals/declarations.json")
    HELPERS.write(work / "declarations.json", decl)
    HELPERS.write(work / "bindings.json", good)
    run_ok("dotnet", cli, "sqlserver-result-schema", "produce", "--declarations", work / "declarations.json", "--out", work / "schema.json")
    sources = work / "sources"
    sources.mkdir()
    (sources / "base.sql").write_bytes((templates / "base.sql").read_bytes())
    ddl_input = {"version": "sqlserver-ddl-result-input/1", "repo": fact["repo"], "revision": revision, "server": IDENTITY["server"],
                 "database": IDENTITY["database"], "publicSynthetic": True, "files": [{"path": "base.sql", "sha256": template_hashes["base.sql"]}]}
    HELPERS.write(work / "ddl-input.json", ddl_input)
    run_ok("dotnet", cli, "sqlserver-ddl-result", "produce", "--input", work / "ddl-input.json", "--source-root", sources, "--out", work / "ddl-schema.json")
    # Pre-#911 parameter artifacts must never be reinterpreted as result-shape evidence.
    run_ok("dotnet", cli, "sqlserver-schema", "produce", "--declarations", ROOT / "samples/sqlserver-parameter-evidence/declarations-base.json",
           "--out", work / "parameter-schema.json")
    checks = []

    def json_compare(name, declarations_path=None, schema=None, bindings_path=None, facts=scan, rev=revision):
        out = work / f"out-{name}"
        ok, why = refused("dotnet", cli, "sqlserver-result-schema", "compare", "--declarations", declarations_path or work / "declarations.json",
                          "--schema", schema or work / "schema.json", "--bindings", bindings_path or work / "bindings.json",
                          "--facts", facts, "--repo", fact["repo"], "--revision", rev, "--out", out, output=out)
        checks.append({"case": name, "refused": ok, "message": why})

    def ddl_run(name, sql=None, input_doc=None, schema=None, link=False):
        root_dir = work / f"src-{name}"
        root_dir.mkdir()
        text = (templates / "base.sql").read_bytes() if sql is None else sql.encode()
        doc = copy.deepcopy(input_doc or ddl_input)
        if sql is not None and input_doc is None:
            doc["files"][0]["sha256"] = hashlib.sha256(text).hexdigest()
        if link:
            (root_dir / "actual.sql").write_bytes(text)
            (root_dir / "base.sql").symlink_to(root_dir / "actual.sql")
        else:
            (root_dir / "base.sql").write_bytes(text)
        HELPERS.write(work / f"input-{name}.json", doc)
        out = work / f"out-{name}"
        command = ["dotnet", cli, "sqlserver-ddl-result"]
        command += (["compare", "--schema", schema, "--bindings", work / "bindings.json", "--facts", scan, "--repo", fact["repo"],
                     "--revision", revision] if schema else ["produce"])
        command += ["--input", work / f"input-{name}.json", "--source-root", root_dir, "--out", out]
        ok, why = refused(*command, output=out)
        checks.append({"case": name, "refused": ok, "message": why})

    json_compare("stale-revision", rev="2" * 40)
    stale = copy.deepcopy(good)
    stale["calls"][0]["readerEvidenceSha256"] = "0" * 64
    HELPERS.write(work / "stale-digest.json", stale)
    json_compare("stale-reader-digest", bindings_path=work / "stale-digest.json")
    mutated = copy.deepcopy(decl)
    mutated["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-declarations.json", mutated)
    json_compare("mutated-declaration-bytes", declarations_path=work / "mutated-declarations.json")
    export = json.loads((work / "schema.json").read_bytes())
    export["generatorSha256"] = "0" * 64
    HELPERS.write(work / "mutated-schema.json", export)
    json_compare("mutated-export-bytes", schema=work / "mutated-schema.json")
    json_compare("parameter-export-as-result-schema", schema=work / "parameter-schema.json")
    old = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
           "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItems", "identity": IDENTITY, "sourceEvidenceSha256": HELPERS.digest(projected)}]}
    HELPERS.write(work / "parameter-bindings.json", old)
    json_compare("parameter-bindings-as-result-bindings", bindings_path=work / "parameter-bindings.json")
    tampered = [json.loads(line) for line in scan.read_text().splitlines()]
    for item in tampered:
        if item["factId"] == fact["factId"]:
            state = json.loads(item["properties"]["sqlServerReaderState"])
            state["accesses"][0]["name"] = "Title"
            item["properties"]["sqlServerReaderState"] = json.dumps(state, separators=(",", ":"))
    (work / "mutated-facts.ndjson").write_text("".join(json.dumps(i, separators=(",", ":")) + "\n" for i in tampered))
    json_compare("mutated-source-facts", facts=work / "mutated-facts.ndjson")
    (work / "oversized.json").write_bytes(b" " * (4 * 1024 * 1024 + 1))
    json_compare("oversized-declarations", declarations_path=work / "oversized.json")
    existing = work / "existing"
    existing.mkdir()
    (existing / "keep.txt").write_bytes(b"prior winner\n")
    result = run_captured_cli("dotnet", cli, "sqlserver-result-schema", "compare", "--declarations", work / "declarations.json",
                             "--schema", work / "schema.json", "--bindings", work / "bindings.json", "--facts", scan,
                             "--repo", fact["repo"], "--revision", revision, "--out", existing)
    checks.append({"case": "existing-output-not-replaced", "refused": result.returncode != 0
                   and sorted(p.name for p in existing.iterdir()) == ["keep.txt"] and (existing / "keep.txt").read_bytes() == b"prior winner\n",
                   "message": []})
    ddl_run("mutated-ddl-source", input_doc=dict(ddl_input, files=[{"path": "base.sql", "sha256": "f" * 64}]))
    ddl_run("malformed-sql", sql="CREATE PROCEDURE dbo.ReadItems AS SELECT FROM;")
    ddl_run("sqlcmd-in-comment", sql="CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id; -- :r other.sql\n")
    ddl_run("sqlcmd-in-string", sql="CREATE PROCEDURE dbo.ReadItems AS SELECT N'$(Owner)' AS Name;\n")
    ddl_run("oversized-sql", sql="CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id;" + " " * (256 * 1024))
    ddl_run("symlinked-source", link=True)
    ddl_run("parameter-manifest-version", input_doc=dict(ddl_input, version="sqlserver-ddl-input/1"))
    ddl_run("ambiguous-identity", sql="CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id;\nGO\nCREATE PROCEDURE DBO.readitems AS SELECT 2 AS Id;\n")
    ddl_export = json.loads((work / "ddl-schema.json").read_bytes())
    ddl_export["declarations"]["shapes"][0]["columns"][1]["name"] = "Title"
    HELPERS.write(work / "mutated-ddl-schema.json", ddl_export)
    ddl_run("mutated-ddl-export", schema=work / "mutated-ddl-schema.json")
    ddl_run("json-export-as-ddl-schema", schema=work / "schema.json")
    parameter_input = dict(ddl_input, version="sqlserver-ddl-input/1")
    HELPERS.write(work / "parameter-ddl-input.json", parameter_input)
    run_ok("dotnet", cli, "sqlserver-ddl", "produce", "--input", work / "parameter-ddl-input.json", "--source-root", sources,
           "--out", work / "parameter-ddl-schema.json")
    ddl_run("parameter-ddl-export-as-result-schema", schema=work / "parameter-ddl-schema.json")
    return checks


if __name__ == "__main__":
    main()
