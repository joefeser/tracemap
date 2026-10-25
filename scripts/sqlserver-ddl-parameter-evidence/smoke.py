#!/usr/bin/env python3
"""Offline DDL CLI smoke over committed-head public synthetic C#/VB scans; no SQL execution."""
import argparse
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
# Expected outcomes are authored independently of the DDL parser and source extractor.
EXPECTED = {
    "Good": "bounded-match", "SizeMismatch": "mismatch", "TypeMismatch": "mismatch",
    "DirectionMismatch": "mismatch", "Max": "bounded-match", "MissingSize": "unknown",
    "Ansi": "unknown", "Decimal": "unknown", "CaseName": "unknown", "InputOutput": "bounded-match",
    "Channels": "unknown", "DynamicName": "unknown", "Conditional": "unknown", "Alias": "unknown",
    "ParameterAlias": "unknown", "Mutation": "unknown", "ParameterReassignment": "unknown",
    "HelperBoundary": "unknown", "Text": "unknown", "DynamicCommand": "unknown", "Duplicate": "unknown",
    "Extra": "mismatch", "Missing": "mismatch", "DefaultOmitted": "unknown",
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json-smoke", type=Path, required=True, help="completed same-head JSON-origin smoke output")
    parser.add_argument("--output", type=Path, required=True, help="new output directory")
    args = parser.parse_args()
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    old_receipt = json.loads((args.json_smoke / "smoke-receipt.json").read_bytes())
    assert old_receipt["revision"] == revision and old_receipt["generatorSha256"] == HELPERS.GENERATOR
    assert len(old_receipt["results"]) == 52
    assert {s: sum(r["status"] == s for r in old_receipt["results"]) for s in ("bounded-match", "mismatch", "unknown")} == {
        "bounded-match": 8, "mismatch": 12, "unknown": 32}
    scan_artifacts = HELPERS.verify_scan_hashes(args.json_smoke, old_receipt)
    args.output.mkdir(parents=True, exist_ok=False)
    cli = ROOT / "src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll"
    core_hash = hashlib.sha256((cli.parent / "TraceMap.Core.dll").read_bytes()).hexdigest()
    parser_hash = hashlib.sha256((cli.parent / "Microsoft.SqlServer.TransactSql.ScriptDom.dll").read_bytes()).hexdigest()
    templates = ROOT / "samples/sqlserver-ddl-parameter-evidence"
    public_inputs = []
    results = []
    template_hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(templates.glob("*.sql"))}
    for language in ("csharp", "vb"):
        scan = args.json_smoke / language / "scan"
        manifest = json.loads((scan / "scan-manifest.json").read_bytes())
        assert manifest["commitSha"] == revision and manifest["buildStatus"] == "Succeeded"
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate" and f["evidenceTier"] == "Tier1Semantic"]
        assert len(calls) == 26
        names = [re.search(r"\.([A-Za-z]+)\(", f["sourceSymbol"])[1] for f in calls]
        assert sorted(names) == sorted([*EXPECTED, "TwoCommands", "TwoCommands"])
        two_count = 0
        for number, fact in enumerate(sorted(calls, key=lambda f: f["evidence"]["startLine"])):
            name = re.search(r"\.([A-Za-z]+)\(", fact["sourceSymbol"])[1]
            expected = EXPECTED.get(name)
            if name == "TwoCommands":
                expected = "mismatch" if two_count == 0 else "bounded-match"
                two_count += 1
            template = {"Max": "max.sql", "Ansi": "ansi.sql", "Decimal": "decimal.sql", "DefaultOmitted": "default.sql",
                        "InputOutput": "output.sql", "Channels": "output.sql"}.get(name, "base.sql")
            case = args.output / language / f"{number:02}-{name}"
            case.mkdir(parents=True)
            projected = HELPERS.projection(fact)
            public_inputs.append(projected)
            source_projection = {"repo": fact["repo"], "revision": revision, "file": template, "sha256": template_hashes[template]}
            input_doc = {"version": "sqlserver-ddl-input/1", "repo": fact["repo"], "revision": revision,
                         "server": "SyntheticServer", "database": "SyntheticDatabase", "publicSynthetic": True,
                         "files": [{"path": template, "sha256": template_hashes[template]}],
                         "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(source_projection)}}
            binding = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
                       "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItem",
                                  "identity": {"server": "SyntheticServer", "database": "SyntheticDatabase", "schema": "dbo", "procedure": "ReadItem"},
                                  "sourceEvidenceSha256": HELPERS.digest(projected)}],
                       "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": HELPERS.digest(projected)}}
            HELPERS.write(case / "input.json", input_doc)
            HELPERS.write(case / "bindings.json", binding)
            HELPERS.run("dotnet", cli, "sqlserver-ddl", "produce", "--input", case / "input.json", "--source-root", templates, "--out", case / "schema.json")
            common = ["dotnet", cli, "sqlserver-ddl", "compare", "--input", case / "input.json", "--source-root", templates,
                      "--schema", case / "schema.json", "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson",
                      "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                HELPERS.run(*common, "--out", case / output)
            for filename in ("parameter-comparison.json", "report.md"):
                assert (case / "report" / filename).read_bytes() == (case / "repeat" / filename).read_bytes()
            raw = (case / "report/parameter-comparison.json").read_bytes()
            wrapper = json.loads(raw)
            ddl = wrapper["ddlEvidence"]
            report = wrapper["comparison"]
            assert wrapper["generatorSha256"] == core_hash
            assert ddl["generatorSha256"] == core_hash and ddl["parserSha256"] == parser_hash
            assert ddl["boundedInputSha256"] == HELPERS.digest(input_doc)
            assert ddl["declarations"]["origin"] == "public-synthetic-tsql-ddl"
            assert ddl["tier"] == "Tier2Structural" and ddl["ruleId"] == "database.sqlserver.ddl-parameters.v1"
            assert report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict"
            row = next(r for r in report["comparisons"] if r["factId"] == fact["factId"])
            assert row["status"] == expected, (language, name, row)
            assert len(report["comparisons"]) == 26
            assert all(r["status"] == "unknown" and "binding-missing" in r["reasons"] for r in report["comparisons"] if r["factId"] != fact["factId"])
            assert "No aggregate clean verdict" in (case / "report/report.md").read_text()
            results.append({"language": language, "case": name, "factId": fact["factId"], "status": row["status"],
                            "reportSha256": hashlib.sha256(raw).hexdigest()})
    counts = {s: sum(r["status"] == s for r in results) for s in ("bounded-match", "mismatch", "unknown")}
    assert counts == {"bounded-match": 8, "mismatch": 12, "unknown": 32}, counts
    receipt = {"version": "sqlserver-ddl-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": HELPERS.digest({"templates": template_hashes, "calls": public_inputs}),
               "revision": revision, "coreSha256": core_hash, "parserSha256": parser_hash,
               "helperGeneratorSha256": HELPERS.GENERATOR,
               "scanArtifactSha256": scan_artifacts,
               "fixtureSitesPerLanguage": 26, "results": results, "sqlExecution": "not-run"}
    HELPERS.verify_scan_hashes(args.json_smoke, old_receipt)
    HELPERS.write(args.output / "smoke-receipt.json", receipt)
    print(f"52 DDL native cases passed at {revision}: {counts}; SQL NOT RUN")


if __name__ == "__main__":
    main()
