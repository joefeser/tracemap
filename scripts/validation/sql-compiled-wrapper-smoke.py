#!/usr/bin/env python3
"""Compile-only public corpus replay. Never invokes fixture methods or SQL."""
import argparse
import hashlib
import json
import pathlib
import subprocess


CASES = {
    "Direct": ("Direct", "local-column-cross-boundary-unlinked"),
    "Forwarded": ("Forwarded", "caller-parameter-substitution-unavailable"),
    "FieldObject": ("FieldObject", "constructor-field-forwarding-unavailable"),
    "PropertyObject": ("PropertyObject", "constructor-property-forwarding-unavailable"),
    "Mutated": ("Mutated", "parameter-mutation-unresolved"),
    "Escaped": ("Escaped", "parameter-escape-unresolved"),
    "Branch": ("Branch", "parameter-branch-unresolved"),
    "MergeFresh": ("Direct", "merged-field-schema-unavailable"),
    "MergeExisting": ("Direct", "existing-destination-schema-unavailable"),
    "MergeMutated": ("Direct", "merged-destination-mutation-unavailable"),
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def one(values, label):
    values = list(values)
    if len(values) != 1:
        raise ValueError(f"{label}: expected one item, got {len(values)}")
    return values[0]


def inspect(facts, language, assembly_hashes):
    by_id = {fact["factId"]: fact for fact in facts}
    if len(by_id) != len(facts):
        raise ValueError("duplicate fact identity")
    bodies = [fact for fact in facts if fact["factType"] == "ManagedIlBodyDeclared"]

    def body(role, method):
        return one((fact for fact in bodies
                    if fact["properties"]["rawFileSha256"] == assembly_hashes[role]
                    and f"|method:{len(method)}:{method}|" in fact["targetSymbol"]), f"{role}/{method}")

    shapes = [fact for fact in facts if fact["factType"] == "ManagedIlSqlShapeCandidate"]
    shape = one(shapes, "native shape denominator")
    direct = body("wrapper", "Direct")
    props = shape["properties"]
    if (shape["evidenceTier"] != "Tier3SyntaxOrTextual"
            or shape["ruleId"] != "dotnet.compiled.il-sql-shape.v1"
            or props["sqlShapeSchema"] != "il-sql-shape.v1"
            or props["ilBodyFactId"] != direct["factId"]):
        raise ValueError("wrong shape identity or tier")
    parameter = one(json.loads(props["parameterShapes"]), "parameter denominator")
    expected_name = "str:5:" + hashlib.sha256("@Name".encode("utf-16-le")).hexdigest()
    if parameter["Name"] != {"Kind": "constant-string-hash", "Identity": expected_name}:
        raise ValueError("wrong parameter name evidence")
    if parameter["SqlDbType"] != {"Kind": "constant-int32", "Identity": "12"} or parameter["Size"] != {"Kind": "constant-int32", "Identity": "40"}:
        raise ValueError("wrong parameter shape")
    for call_id in (props["ilCallFactId"], parameter["constructorCallFactId"], parameter["attachmentCallFactId"]):
        call = by_id[call_id]
        if call["factType"] != "ManagedIlCallObserved" or call["properties"]["ilBodyFactId"] != direct["factId"]:
            raise ValueError("wrong shape call link")
        for key in ("rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256"):
            if call["properties"][key] != props[key]:
                raise ValueError("shape provenance mismatch")
    rows = []
    for name, (target, blocker) in CASES.items():
        caller = body("caller", name)
        callee = body("wrapper", target)
        # Encoded assembly/member reference, not deployed identity or runtime dispatch.
        call = one((fact for fact in facts if fact["factType"] == "ManagedIlCallObserved"
                    and fact["properties"]["ilBodyFactId"] == caller["factId"]
                    and f"PublicSql.{language}.wrapper" in fact["properties"]["targetIdentity"]
                    and f"|member:{len(target)}:{target}|" in fact["properties"]["targetIdentity"]), name)
        rows.append({"language": language, "case": name, "callerBodyFactId": caller["factId"],
                     "encodedWrapperCallFactId": call["factId"], "inspectedWrapperBodyFactId": callee["factId"],
                     "localShapeFactId": shape["factId"] if target == "Direct" else None,
                     "wholePathStatus": "UNKNOWN", "firstUnimplementedLeg": blocker,
                     "sqlContractStatus": "NOT_ADMITTED", "runtimeStatus": "NOT_RUN"})
    return rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", required=True, type=pathlib.Path)
    parser.add_argument("--out", required=True, type=pathlib.Path)
    args = parser.parse_args()
    repo = pathlib.Path(__file__).resolve().parents[2]
    cli = args.cli.resolve(strict=True)
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    rows, inputs = [], {}
    for language in ("csharp", "vb"):
        sample = repo / "samples/sql-compiled-wrapper-evidence" / language
        assemblies = {role: sample / role / "bin/Debug/net48" / f"PublicSql.{language}.{role}.dll"
                      for role in ("caller", "wrapper")}
        hashes = {role: digest(path) for role, path in assemblies.items()}
        scan = output / language
        command = ["dotnet", str(cli), "scan", "--repo", str(sample), "--out", str(scan), "--il-body-evidence"]
        for assembly in assemblies.values():
            command += ["--compiled-input", str(assembly)]
        subprocess.run(command, check=True, timeout=300)
        facts_path = scan / "facts.ndjson"
        facts = [json.loads(line) for line in facts_path.read_text(encoding="utf-8-sig").splitlines()]
        rows.extend(inspect(facts, language, hashes))
        inputs[language] = {"assemblies": hashes, "factsSha256": digest(facts_path),
                            "manifestSha256": digest(scan / "scan-manifest.json")}
    report = {"schema": "public-compiled-wrapper-matrix/1", "artifactVisibility": "local-only",
              "generatorSha256": digest(pathlib.Path(__file__)), "cliSha256": digest(cli),
              "boundedInputSha256": hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(",", ":")).encode()).hexdigest(),
              "inputs": inputs, "rows": rows, "coverage": "Partial",
              "summary": {"entryCases": 20, "nativeLocalShapes": 2, "wholePathUnknown": 20},
              "limitations": "Encoded call/member observations are not source/binary/deployed equivalence or runtime proof. Custom-object forwarding and merged-field consumers remain unimplemented. No SQL contract admitted."}
    temporary = output / "matrix.json.tmp"
    temporary.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(output / "matrix.json")
    print("PASS: 20 entry cases; 2 local IL shapes; 20 explicit whole-path unknowns; no SQL execution")


if __name__ == "__main__":
    main()
