#!/usr/bin/env python3
"""Public build-only HTTP matrix: native CLI, semantic/fallback/IL and retained reports."""
import argparse
import collections
import json
import pathlib
import shutil
import subprocess
import sys
from http_matrix_expectations import load_expectations, assert_calls, assert_compiled_cases

parser = argparse.ArgumentParser()
parser.add_argument("--output", type=pathlib.Path, required=True)
parser.add_argument("--configuration", default="Debug")
args = parser.parse_args()
root = pathlib.Path(__file__).resolve().parents[2]
args.output.mkdir(parents=True, exist_ok=True)
cli = ["dotnet", str(root / f"src/dotnet/TraceMap.Cli/bin/{args.configuration}/net10.0/tracemap.dll")]
outputs = []

def run(command):
    result = subprocess.run(command, cwd=root, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stdout + result.stderr)

for tfm in ["net20", "net35", "net40", "net45", "net452", "net472", "net48", "net10.0"]:
    for language, extension in [("csharp", ".cs"), ("vb", ".vb")]:
        sample = root / "samples/outbound-http-coverage"
        sample = sample / language if tfm == "net10.0" else sample / "legacy" / tfm / language
        cases, control_lines, controls = load_expectations(sample, language, sample.parent / "OVERLOADS.md",
            root / "samples/outbound-http-coverage/CONTROL-EXPECTATIONS.json" if tfm == "net10.0" else None)
        name = ("HttpMatrix.CSharp" if language == "csharp" else "HttpMatrix.VB") if tfm == "net10.0" else f"HttpLegacy.{tfm}.{language}"
        for mode in ["semantic", "projectless", "compiled"]:
            prefix = f"{tfm}-{language}-{mode}".replace(".", "-")
            repo = sample
            options = []
            if mode != "semantic":
                repo = args.output / (prefix + "-source")
                repo.mkdir(exist_ok=True)
                if not (repo / ".git").exists():
                    run(["git", "init", "-q", str(repo)])
                    run(["git", "-C", str(repo), "config", "user.name", "Synthetic Fixture"])
                    run(["git", "-C", str(repo), "config", "user.email", "synthetic@example.invalid"])
                if mode == "projectless":
                    for source in sample.glob("*" + extension):
                        shutil.copyfile(source, repo / source.name)
                else:
                    options = ["--compiled-input", str(sample / f"bin/{args.configuration}/{tfm}/{name}.dll"), "--il-body-evidence"]
                run(["git", "-C", str(repo), "add", "."])
                run(["git", "-C", str(repo), "commit", "-qm", "Public synthetic HTTP fixture", "--allow-empty"])
            pairs = []
            for iteration in ["a", "b"]:
                out = args.output / (prefix + "-" + iteration)
                pairs.append(out)
                outputs.append(str(out))
                run(cli + ["scan", "--repo", str(repo), "--out", str(out)] + options)
            for artifact in ["facts.ndjson", "report.md"]:
                assert (pairs[0] / artifact).read_bytes() == (pairs[1] / artifact).read_bytes(), (prefix, artifact)
            facts = [json.loads(line) for line in (pairs[0] / "facts.ndjson").read_text(encoding="utf-8").splitlines()]
            tiers = collections.Counter(fact["evidenceTier"] for fact in facts if fact["factType"] == "HttpCallDetected")
            if mode == "semantic":
                assert_calls(facts, language, mode, cases, control_lines, controls)
                if language == "csharp":
                    assert_calls(facts, language, "projectless", cases, control_lines, controls)
                else:
                    assert tiers["Tier3SyntaxOrTextual"] == 0, (prefix, tiers)
                assert set(tiers) <= {"Tier1Semantic", "Tier3SyntaxOrTextual"}, (prefix, tiers)
                assert all(fact["properties"].get("methodFamily") == "unresolved" for fact in facts if fact["factType"] == "HttpCallDetected" and fact["evidenceTier"] == "Tier3SyntaxOrTextual"), prefix
                run(cli + ["paths", "--index", str(pairs[0] / "index.sqlite"), "--out", str(args.output / (prefix + "-paths")), "--from-symbol", next(iter(cases)), "--to-surface", "http-client", "--format", "json"])
                report = json.loads((args.output / (prefix + "-paths/paths-report.json")).read_text(encoding="utf-8"))
                assert "bounded-static-http-call" in json.dumps(report), prefix
            elif mode == "compiled":
                assert not tiers, (prefix, tiers)
                assert_compiled_cases(facts, cases)
            elif language == "vb":
                assert_calls(facts, language, mode, cases, control_lines, controls)
                assert set(tiers) == {"Tier3SyntaxOrTextual"}, (prefix, tiers)
                assert any(fact["ruleId"] == "vb.syntax.http-coverage-gap.v1" and fact["factType"] == "AnalysisGap" for fact in facts), prefix
                assert "VisualBasicHttpAnalysisPartialWithoutSemantics" in (pairs[0] / "report.md").read_text(encoding="utf-8"), prefix
                run(cli + ["paths", "--index", str(pairs[0] / "index.sqlite"), "--out", str(args.output / (prefix + "-paths")), "--from-symbol", next(iter(cases)), "--to-surface", "http-client", "--format", "json"])
                report = json.loads((args.output / (prefix + "-paths/paths-report.json")).read_text(encoding="utf-8"))
                assert "reduced-explicit-receiver-candidate" in json.dumps(report), prefix
            else:
                assert set(tiers) == {"Tier3SyntaxOrTextual"}, (prefix, tiers)
                assert_calls(facts, language, mode, cases, control_lines, controls)
            print(prefix, dict(tiers), "deterministic facts/report", flush=True)
run([sys.executable, str(root / "scripts/validate-adapter-artifacts.py")] + outputs)
print(f"Validated {len(outputs)} CLI outputs; no fixture method executed.")
