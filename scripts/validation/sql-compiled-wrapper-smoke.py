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
SUPPORTED_OBJECT_PATHS = frozenset(("Direct", "Forwarded", "FieldObject", "PropertyObject", "MergeFresh"))
TABLE_CASES = ("First", "Second", "Conditional", "Mutated", "UnknownHelper", "RepeatedMerge",
               "ClearMerge", "Existing", "Exceptional", "DuplicateName", "OutOfRange", "DynamicSlot", "ThirdTable")


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

    def body(role, method, owner=None):
        return one((fact for fact in bodies
                    if fact["properties"]["rawFileSha256"] == assembly_hashes[role]
                    and (owner is None or f"|names:{len(owner)}:{owner}|arity:" in fact["targetSymbol"])
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
    object_paths = [fact for fact in facts if fact["factType"] == "ManagedIlSqlObjectPathCandidate"]
    if len(object_paths) != 7:
        raise ValueError("wrong object path denominator")
    for name, (target, blocker) in CASES.items():
        caller = body("caller", name, "Caller")
        callee = body("wrapper", target)
        # Encoded assembly/member reference, not deployed identity or runtime dispatch.
        call = one((fact for fact in facts if fact["factType"] == "ManagedIlCallObserved"
                    and fact["properties"]["ilBodyFactId"] == caller["factId"]
                    and f"PublicSql.{language}.wrapper" in fact["properties"]["targetIdentity"]
                    and f"|member:{len(target)}:{target}|" in fact["properties"]["targetIdentity"]), name)
        candidates = [fact for fact in object_paths if fact["targetSymbol"] == caller["targetSymbol"]]
        if name in SUPPORTED_OBJECT_PATHS:
            candidate = one(candidates, name + " object path")
            evidence = candidate["properties"]
            if (candidate["evidenceTier"] != "Tier3SyntaxOrTextual"
                    or candidate["ruleId"] != "dotnet.compiled.il-sql-object-path.v1"
                    or evidence["rootBodyFactId"] != caller["factId"]
                    or evidence["sqlObjectPathSchema"] != "il-sql-object-path.v1"):
                raise ValueError("wrong object path identity/tier")
            for key in ("rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256"):
                if evidence[key] != caller["properties"][key]:
                    raise ValueError("object path provenance mismatch")
            parameter_shape = one(json.loads(evidence["parameterShapes"]), "object parameter")
            for key in ("Name", "SqlDbType", "Size"):
                if parameter_shape[key] != parameter[key]:
                    raise ValueError("wrong forwarded parameter shape")
            for key, value in (("commandText", "public.lookup"), ("columnIdentity", "Name")):
                expected = {"Kind": "constant-string-hash", "Identity": f"str:{len(value)}:" + hashlib.sha256(value.encode("utf-16-le")).hexdigest()}
                if json.loads(evidence[key]) != expected:
                    raise ValueError("wrong object scalar identity")
            sites = json.loads(evidence["sites"])
            one((site for site in sites if site["Action"] == "fill"), "one Fill execution")
            one((site for site in sites if site["Action"] == "column-consumer"), "one consumer")
            if {site["BinarySha256"] for site in sites} != set(assembly_hashes.values()):
                raise ValueError("missing cross-binary path")
            for site in sites:
                linked = by_id[site["bodyFactId"]]
                if (linked["factType"] != "ManagedIlBodyDeclared" or linked["targetSymbol"] != site["BodyIdentity"]
                        or linked["properties"]["rawFileSha256"] != site["BinarySha256"]):
                    raise ValueError("wrong object body link")
            merge = name == "MergeFresh"
            if ((evidence["filledDataSetIdentity"] != evidence["consumerDataSetIdentity"]) != merge
                    or evidence["consumerRelation"] != ("fresh-empty-schema-union-candidate" if merge else "same-returned-dataset-candidate")):
                raise ValueError("wrong Merge identity/relation")
            status, path_id, gap = "BOUNDED_STATIC_CANDIDATE", candidate["factId"], None
        else:
            if candidates:
                raise ValueError("unsafe object path accepted")
            gap_fact = one((fact for fact in facts if fact["factType"] == "AnalysisGap"
                            and fact["ruleId"] == "dotnet.compiled.il-sql-object-path.v1"
                            and fact["targetSymbol"] == caller["targetSymbol"]), name + " refusal")
            status, path_id, gap = "UNKNOWN", None, gap_fact["contractElement"]
        rows.append({"language": language, "case": name, "callerBodyFactId": caller["factId"],
                     "encodedWrapperCallFactId": call["factId"], "inspectedWrapperBodyFactId": callee["factId"],
                     "localShapeFactId": shape["factId"] if target == "Direct" else None,
                     "wholePathStatus": status, "objectPathFactId": path_id, "objectPathGap": gap,
                     "foundationBlocker": blocker,
                     "sqlContractStatus": "NOT_ADMITTED", "runtimeStatus": "NOT_RUN"})
    table_rows = []
    for name in TABLE_CASES:
        caller = body("caller", name, "TableFlow")
        candidates = [fact for fact in object_paths if fact["targetSymbol"] == caller["targetSymbol"]]
        if name in ("First", "Second"):
            candidate = one(candidates, name)
            evidence = candidate["properties"]
            if (candidate["evidenceTier"] != "Tier3SyntaxOrTextual"
                    or candidate["ruleId"] != "dotnet.compiled.il-sql-object-path.v1"
                    or evidence["rootBodyFactId"] != caller["factId"]
                    or evidence["consumerRelation"] != "fresh-empty-table-schema-union-candidate"
                    or evidence["filledTableIdentity"] == evidence["consumerTableIdentity"]
                    or evidence["filledDataSetIdentity"] == evidence["consumerDataSetIdentity"]):
                raise ValueError("wrong named-table identity/relation")
            expected_command = "public." + name.lower()
            expected = {"Kind": "constant-string-hash", "Identity": f"str:{len(expected_command)}:" + hashlib.sha256(expected_command.encode("utf-16-le")).hexdigest()}
            if json.loads(evidence["commandText"]) != expected:
                raise ValueError("selected wrong table execution")
            for key in ("rawFileSha256", "ilGeneratorSha256", "ilBoundedInputSha256"):
                if evidence[key] != caller["properties"][key]:
                    raise ValueError("table path provenance mismatch")
            sites = json.loads(evidence["sites"])
            for action in ("fill", "fresh-named-table", "fresh-empty-table-schema-union"):
                if sum(site["Action"] == action for site in sites) != 2:
                    raise ValueError("wrong table flow operation denominator")
            one((site for site in sites if site["Action"] == "fill"
                 and f'{site["Frame"]}:{site["Offset"]}' == evidence["executionIdentity"]), "selected execution")
            for site in sites:
                linked = by_id[site["bodyFactId"]]
                if (linked["factType"] != "ManagedIlBodyDeclared" or linked["targetSymbol"] != site["BodyIdentity"]
                        or linked["properties"]["rawFileSha256"] != site["BinarySha256"]):
                    raise ValueError("wrong table body link")
            status, path_id, gap = "BOUNDED_STATIC_CANDIDATE", candidate["factId"], None
        else:
            if candidates:
                raise ValueError("unsafe table path accepted")
            refusal = one((fact for fact in facts if fact["factType"] == "AnalysisGap"
                           and fact["ruleId"] == "dotnet.compiled.il-sql-object-path.v1"
                           and fact["targetSymbol"] == caller["targetSymbol"]), name)
            status, path_id, gap = "UNKNOWN", None, refusal["contractElement"]
        table_rows.append({"language": language, "case": name, "callerBodyFactId": caller["factId"],
                           "wholePathStatus": status, "objectPathFactId": path_id, "objectPathGap": gap,
                           "sqlContractStatus": "NOT_ADMITTED", "runtimeStatus": "NOT_RUN"})
    return rows, table_rows


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cli", required=True, type=pathlib.Path)
    parser.add_argument("--out", required=True, type=pathlib.Path)
    args = parser.parse_args()
    repo = pathlib.Path(__file__).resolve().parents[2]
    cli = args.cli.resolve(strict=True)
    output = args.out.resolve()
    output.mkdir(parents=True, exist_ok=False)
    rows, table_rows, inputs = [], [], {}
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
        original, tables = inspect(facts, language, hashes)
        rows.extend(original)
        table_rows.extend(tables)
        inputs[language] = {"assemblies": hashes, "factsSha256": digest(facts_path),
                            "manifestSha256": digest(scan / "scan-manifest.json")}
    report = {"schema": "public-compiled-wrapper-matrix/3", "artifactVisibility": "local-only",
              "generatorSha256": digest(pathlib.Path(__file__)), "cliSha256": digest(cli),
              "boundedInputSha256": hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(",", ":")).encode()).hexdigest(),
              "inputs": inputs, "rows": rows, "tableRows": table_rows, "coverage": "Partial",
              "summary": {"entryCases": 20, "nativeLocalShapes": 2, "wholePathStaticCandidates": 10, "wholePathUnknown": 10},
              "tableSummary": {"entryCases": 26, "wholePathStaticCandidates": 4, "wholePathUnknown": 22},
              "limitations": "Encoded static paths are not source/binary/deployed equivalence, SQL contract matches or runtime proof. Fresh-empty Merge is a distinct schema-union candidate, never original result identity. Complex DDL and server/database identity remain unadmitted."}
    temporary = output / "matrix.json.tmp"
    temporary.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    temporary.replace(output / "matrix.json")
    print("PASS: original 20 cases (10 paths/10 unknown); table 26 cases (4 paths/22 unknown); 2 local IL shapes; no SQL execution")


if __name__ == "__main__":
    main()
