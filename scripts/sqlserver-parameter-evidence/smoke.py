#!/usr/bin/env python3
"""Offline public synthetic native smoke. Never invokes fixture methods or SQL."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
GENERATOR = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
SCAN_FILES = ("scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log")


def scan_hashes(root, language):
    """Bind public synthetic scan bytes; refuse links at/below the supplied root."""
    hashes = {}
    for name in SCAN_FILES:
        path = root
        for part in (language, "scan", *Path(name).parts):
            if path.is_symlink():
                raise ValueError(f"symlink scan path: {path}")
            path = path / part
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"missing or symlink scan artifact: {path}")
        hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
    return hashes


def verify_scan_hashes(root, receipt):
    expected = receipt.get("scanArtifactSha256")
    actual = {language: scan_hashes(root, language) for language in ("csharp", "vb")}
    if expected != actual:
        raise ValueError("scan artifact digest mismatch or missing receipt bindings")
    return actual
EXPECTED = {
    "Good": "bounded-match", "SizeMismatch": "mismatch", "TypeMismatch": "mismatch",
    "DirectionMismatch": "mismatch", "Max": "bounded-match", "MissingSize": "unknown",
    "Ansi": "unknown", "Decimal": "unknown", "CaseName": "unknown", "InputOutput": "bounded-match",
    "Channels": "unknown", "DynamicName": "unknown", "Conditional": "unknown", "Alias": "unknown",
    "ParameterAlias": "unknown", "Mutation": "unknown", "ParameterReassignment": "unknown",
    "HelperBoundary": "unknown", "Text": "unknown", "DynamicCommand": "unknown", "Duplicate": "unknown",
    "Extra": "mismatch", "Missing": "mismatch", "DefaultOmitted": "unknown",
}


def canonical(value):
    # This public fixture projection contains ASCII identifiers only. The order
    # below mirrors Core.CallProjection; do not hash the original facts file.
    return json.dumps(value, separators=(",", ":"), ensure_ascii=True).encode()


def digest(value):
    return hashlib.sha256(canonical(value)).hexdigest()


def write(path, value):
    path.write_bytes(canonical(value) + b"\n")


def run(*args):
    subprocess.run([str(a) for a in args], cwd=ROOT, check=True, stdout=subprocess.DEVNULL)


def projection(fact):
    span = fact["evidence"]
    return {"factId": fact["factId"], "repo": fact["repo"], "commitSha": fact["commitSha"],
            "ruleId": fact["ruleId"], "evidenceTier": fact["evidenceTier"],
            "filePath": span["filePath"], "startLine": span["startLine"], "endLine": span["endLine"],
            "extractorId": span["extractorId"], "extractorVersion": span["extractorVersion"],
            "state": json.loads(fact["properties"]["sqlServerParameterState"])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="new output directory")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    cli = ROOT / "src/dotnet/TraceMap.Cli/bin/Debug/net10.0/tracemap.dll"
    revision = subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()
    base = json.loads((ROOT / "samples/sqlserver-parameter-evidence/declarations-base.json").read_text())
    rows = []
    public_inputs = []
    scan_artifacts = {}
    for language in ("csharp", "vb"):
        scan = args.output / language / "scan"
        extension = "vbproj" if language == "vb" else "csproj"
        run("dotnet", cli, "scan", "--repo", ROOT / "samples/sqlserver-parameter-evidence", "--project",
            f"{language}/Parameters.{extension}", "--out", scan)
        scan_artifacts[language] = scan_hashes(args.output, language)
        manifest = json.loads((scan / "scan-manifest.json").read_text())
        assert manifest["commitSha"] == revision
        assert manifest["buildStatus"] == "Succeeded"
        calls = [f for line in (scan / "facts.ndjson").read_text().splitlines()
                 if (f := json.loads(line))["factType"] == "DatabaseOperationCandidate"
                 and f["evidenceTier"] == "Tier1Semantic"]
        assert len(calls) == 26
        case_names = [re.search(r"\.([A-Za-z]+)\(", f["sourceSymbol"])[1] for f in calls]
        assert sorted(case_names) == sorted([*EXPECTED, "TwoCommands", "TwoCommands"])
        two_count = 0
        for number, fact in enumerate(sorted(calls, key=lambda f: f["evidence"]["startLine"])):
            name = re.search(r"\.([A-Za-z]+)\(", fact["sourceSymbol"])[1]
            expected = EXPECTED.get(name)
            state = json.loads(fact["properties"]["sqlServerParameterState"])
            if name == "TwoCommands":
                # Independently authored order: second command executes at size 40,
                # followed by the original command at size 20. Never derive the
                # expected status from the extractor's own parameter state.
                expected = "mismatch" if two_count == 0 else "bounded-match"
                two_count += 1
            assert expected is not None, name
            case = args.output / language / f"{number:02}-{name}"
            case.mkdir()
            declarations = copy.deepcopy(base)
            declarations["repo"] = fact["repo"]
            declarations["revision"] = revision
            proc = declarations["procedures"][0]
            proc.update(filePath=f"{language}/{case.name}/declarations.json", startLine=1, endLine=1)
            parameter = proc["parameters"][0]
            parameter["line"] = 1
            if name == "Max": parameter["size"] = -1
            if name == "Ansi": parameter.update(sqlType="VarChar", units="bytes")
            if name == "Decimal": parameter.update(sqlType="Decimal", size=None, units="not-applicable", precision=10, scale=2)
            if name in ("InputOutput", "Channels"): parameter["direction"] = "InputOutput"
            if name == "DefaultOmitted": parameter["hasDefault"] = True
            declarations["provenance"] = {"generatorSha256": GENERATOR,
                                           "boundedInputSha256": digest({"base": base, "case": name, "repo": fact["repo"], "revision": revision})}
            projected = projection(fact)
            public_inputs.append(projected)
            bindings = {"version": "sqlserver-bindings/1", "repo": fact["repo"], "revision": revision,
                        "calls": [{"factId": fact["factId"], "commandText": "dbo.ReadItem", "identity": proc["identity"],
                                   "sourceEvidenceSha256": digest(projected)}],
                        "provenance": {"generatorSha256": GENERATOR, "boundedInputSha256": digest(projected)}}
            write(case / "declarations.json", declarations)
            write(case / "bindings.json", bindings)
            run("dotnet", cli, "sqlserver-schema", "produce", "--declarations", case / "declarations.json", "--out", case / "schema.json")
            common = ["dotnet", cli, "sqlserver-schema", "compare", "--declarations", case / "declarations.json",
                      "--schema", case / "schema.json", "--bindings", case / "bindings.json", "--facts", scan / "facts.ndjson",
                      "--repo", fact["repo"], "--revision", revision]
            for output in ("report", "repeat"):
                run(*common, "--out", case / output)
            report_bytes = (case / "report/parameter-comparison.json").read_bytes()
            assert report_bytes == (case / "repeat/parameter-comparison.json").read_bytes()
            report = json.loads(report_bytes)
            assert report["coverage"] == "partial" and report["verdict"] == "no-aggregate-clean-verdict"
            row = next(r for r in report["comparisons"] if r["factId"] == fact["factId"])
            assert row["status"] == expected, (language, name, row)
            assert len(report["comparisons"]) == 26
            assert all(r["status"] == "unknown" for r in report["comparisons"] if r["factId"] != fact["factId"])
            export = json.loads((case / "schema.json").read_bytes())
            assert export["generatorSha256"] == hashlib.sha256((cli.parent / "TraceMap.Core.dll").read_bytes()).hexdigest()
            rows.append({"language": language, "case": name, "factId": fact["factId"], "status": row["status"],
                         "reportSha256": hashlib.sha256(report_bytes).hexdigest()})
    receipt = {"version": "sqlserver-parameter-smoke/1", "generatorSha256": GENERATOR,
               "boundedInputSha256": digest({"base": base, "calls": public_inputs}), "revision": revision,
               "scanArtifactSha256": scan_artifacts,
               "fixtureSitesPerLanguage": 26, "results": rows, "sqlExecution": "not-run"}
    verify_scan_hashes(args.output, receipt)
    write(args.output / "smoke-receipt.json", receipt)
    counts = {status: sum(r["status"] == status for r in rows) for status in ("bounded-match", "mismatch", "unknown")}
    assert counts == {"bounded-match": 8, "mismatch": 12, "unknown": 32}, counts
    print(f"52 native cases passed at {revision}: {counts}; SQL NOT RUN")


if __name__ == "__main__":
    main()
