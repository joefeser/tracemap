"""Independent checked-in fixture oracle; never infer expectations from scanner output."""
import collections
import json
import re


def methods(text, language):
    pattern = (r"\bpublic void (\w+)\(" if language == "csharp"
               else r"\bPublic Sub (\w+)\(")
    result = {}
    for match in re.finditer(pattern, text):
        name = match.group(1)
        if name in result:
            raise ValueError(f"Duplicate fixture method: {name}")
        result[name] = text.count("\n", 0, match.start()) + 1
    return result


def load_expectations(sample, language, inventory, controls_file):
    extension = ".cs" if language == "csharp" else ".vb"
    cases = re.findall(r"^\| (Case\d+) \|", inventory.read_text(encoding="utf-8"), re.MULTILINE)
    if not cases or len(cases) != len(set(cases)):
        raise ValueError("Missing or duplicate inventory cases")
    declarations = methods((sample / ("Matrix" + extension)).read_text(encoding="utf-8"), language)
    if set(declarations) != set(cases):
        raise ValueError("Inventory and source case identities differ")
    controls_path = sample / ("Controls" + extension)
    controls = {}
    control_lines = {}
    if controls_file is not None:
        if not controls_path.exists():
            raise ValueError("Required control fixture is missing")
        controls = json.loads(controls_file.read_text(encoding="utf-8"))["controls"]
        text = controls_path.read_text(encoding="utf-8")
        # The synthetic Controls class precedes the fake/inherited type declarations.
        marker = "public class InheritedClient" if language == "csharp" else "Public Class InheritedClient"
        control_lines = methods(text.split(marker)[0], language)
        if set(control_lines) != set(controls):
            raise ValueError("Control source and hand-authored oracle identities differ")
        for expectations in controls.values():
            if set(expectations) != {"semantic", "csharp-projectless", "vb-projectless"} or any(
                    type(value) is not int or value < 0 for value in expectations.values()):
                raise ValueError("Invalid control expectations")
    elif controls_path.exists():
        raise ValueError("Control fixture has no independent oracle")
    return declarations, control_lines, controls


def assert_calls(facts, language, mode, cases, control_lines, controls):
    tier = "Tier1Semantic" if mode == "semantic" else "Tier3SyntaxOrTextual"
    extension = ".cs" if language == "csharp" else ".vb"
    observed = collections.Counter()
    for fact in facts:
        if fact["factType"] != "HttpCallDetected" or fact["evidenceTier"] != tier:
            continue
        evidence = fact["evidence"]
        filename = evidence["filePath"].replace("\\", "/").rsplit("/", 1)[-1]
        roster = cases if filename == "Matrix" + extension else control_lines if filename == "Controls" + extension else {}
        owners = [name for name, line in roster.items() if line <= evidence["startLine"]]
        if not owners:
            raise AssertionError(f"Unmapped HTTP callsite: {filename}:{evidence['startLine']}")
        owner = max(owners, key=roster.get)
        observed[owner] += 1
    key = "semantic" if mode == "semantic" else language + "-projectless"
    expected = collections.Counter({name: 1 for name in cases})
    expected.update({name: values[key] for name, values in controls.items()})
    assert observed == expected, (mode, language, observed, expected)


def assert_compiled_cases(facts, cases):
    observed = set()
    for fact in facts:
        if fact["factType"] == "ManagedIlCallObserved":
            match = re.search(r"\|method:\d+:(Case\d+)\|", fact.get("targetSymbol") or "")
            if match:
                observed.add(match.group(1))
    assert observed == set(cases), (observed, set(cases))
