#!/usr/bin/env python3
"""Bounded, privacy-projected inventory of an existing scan. No scanner execution."""
import argparse
from collections import Counter
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import sys
import time

RULE = "workflow.retained-gap-inventory.v1"
MAX_BYTES = 512 * 1024 * 1024
MAX_LINE = 1024 * 1024
MAX_FACTS = 1_000_000
REASONS = frozenset("""SemanticAnalysisUnavailable NoVisualBasicProjectOrSolution
SyntaxFileReadFailed SyntaxParseDiagnostic SyntaxFallbackBudgetExhausted
VisualBasicSyntaxFallbackPhaseFailed VisualBasicHttpAnalysisPartialWithoutSemantics
WebFormsVbImportContextRejected CompilationDiagnostic UncategorizedWorkspaceFailure
UnsupportedWebFormsEventAttribute CompiledIlTargetUnavailable CompiledIlTargetAmbiguous
CompiledIlFrameworkVersionAmbiguous CompiledIlVirtualDispatchUnproven
UnboundManagedInput ManagedInputProvenanceMismatch StaleManagedInput
AmbiguousManagedBindingReceipt""".split())
# Reviewed public literals from LegacyDataMetadataExtractor, LegacyWebFormsExtractor,
# LegacyAspNetExtractor and VisualBasicSyntaxExtractor. Never infer labels from
# private messages or accept arbitrary identifier-shaped strings.
EXPANDED_REASONS = frozenset("""ConfigTransformPresent EncryptedConfigSection ExternalConfigInclude
MalformedLegacyDataMetadata LegacyDataMetadataTooLarge UnsupportedLegacyDataMetadataVersion
UnsupportedLegacyOrmMappingShape AmbiguousLegacyDataModelIdentity
MalformedWebFormsDirective UnreadableWebFormsMarkup UnresolvedWebFormsPageType
MissingWebFormsCodeBehind UnsupportedWebFormsTitle UnsupportedWebFormsMasterPageReference
MissingWebFormsMasterPage UnsupportedWebFormsMasterPageTarget UnsupportedWebFormsUserControlRegistration
MissingWebFormsUserControl UnresolvedWebFormsContentPlaceholder UnresolvedWebFormsContentMaster
UnresolvedWebFormsControlRegistration AmbiguousWebFormsUserControlRegistration
UnresolvedWebFormsAssemblyControlRegistration AmbiguousWebFormsAssemblyControlRegistration
WebFormsAssemblyTypeUnavailable WebFormsAssemblyProjectUnavailable
ClientWebFormsEventAttribute NonIdentifierWebFormsEventValue DynamicWebFormsEventSubscription
UnsupportedWebFormsEventSubscription UnknownWebFormsEventSubscriptionReceiver
AmbiguousWebFormsEventSubscriptionReceiver AmbiguousVisualBasicWebFormsEventSubscriptionReceiver
UnsupportedWebFormsIsPostBackCondition AmbiguousWebFormsIsPostBackReceiver
DynamicWebFormsClientScriptRegistration AmbiguousWebFormsClientScriptRegistrationReceiver
UnresolvedWebFormsPostBackTarget AmbiguousWebFormsPostBackTarget DynamicWebFormsPostBackTarget
UnresolvedWebFormsDataSourceId AmbiguousWebFormsDataSourceId AmbiguousWebFormsDataBindingSource
DynamicWebFormsDataSourceId DynamicWebFormsDataBindingExpression
UnprovenCrossFileWebFormsHandler MissingWebFormsHandler AmbiguousWebFormsHandler AmbiguousAutoEventWireupHandler
UnsupportedVisualBasicEventReceiver UnsupportedVisualBasicEventHandlerDelegate
UnsupportedVisualBasicEventName InvalidVisualBasicHandlesBinding
UnreadableAspNetApplicationFile MalformedAspNetApplicationDirective UnreadableAspNetCodeFile
MalformedAspNetCodeFile""".split())
REASONS = REASONS | EXPANDED_REASONS
COMPILED_REASONS = frozenset("""IlValueCallEffectsUnmodeled IlValueCallShapeUnavailable
IlValueControlFlowBoundary IlValueControlFlowUnavailable IlValueExceptionFlowUnavailable
IlValueInstructionUnavailable IlValueReturnBoundary IlValueStackLimit IlValueStackUnavailable
IlCommandAdapterBindingUnavailable IlCommandAdapterConstructorUnsupported IlCommandBindingWorkLimit
IlCommandControlFlowUnavailable IlCommandOperandsUnavailable IlCommandParameterFlowUnavailable
IlCommandTextBindingUnavailable IlCommandTextCallerBindingRequired IlCommandTextReturnBindingUnavailable
IlCommandTypeBindingUnavailable IlCommandUnknownCallEffects IlCommandValueEvidenceUnavailable""".split())
REASONS = REASONS | COMPILED_REASONS
TIERS = frozenset(("Tier1Semantic", "Tier2Structural", "Tier3SyntaxOrTextual", "Tier4Unknown"))
REGISTRATION_STATES = frozenset(("register-directive-unavailable",
    "AmbiguousWebFormsAssemblyControlRegistration", "WebFormsAssemblyTypeUnavailable",
    "WebFormsAssemblyProjectUnavailable", "UnresolvedWebFormsAssemblyControlRegistration"))


class InventoryError(Exception):
    pass


def canonical(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()


def digest(data):
    return hashlib.sha256(data).hexdigest()


def bounded_lines(path, limit, line_limit, deadline):
    # Refuse leaf links and nonregular inputs. A renamed regular file remains
    # pinned by the open descriptor; parent-directory races are not authenticity.
    if path.is_symlink():
        raise InventoryError("LINKED_INPUT")
    fd = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0) | getattr(os, "O_NONBLOCK", 0))
    with os.fdopen(fd, "rb") as stream:
        before = os.fstat(stream.fileno())
        if not stat.S_ISREG(before.st_mode) or before.st_size > limit:
            raise InventoryError("INPUT_LIMIT_OR_TYPE")
        consumed = 0
        while True:
            if time.monotonic() > deadline:
                raise InventoryError("TIME_LIMIT")
            line = stream.readline(line_limit + 1)
            if not line:
                break
            consumed += len(line)
            if consumed > limit or len(line) > line_limit:
                raise InventoryError("INPUT_LIMIT")
            yield line
        after = os.fstat(stream.fileno())
        if (before.st_size, before.st_mtime_ns, before.st_ctime_ns) != (after.st_size, after.st_mtime_ns, after.st_ctime_ns):
            raise InventoryError("INPUT_CHANGED")


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise InventoryError("DUPLICATE_JSON_KEY")
        result[key] = value
    return result


def summarize(scan, catalog, *, max_seconds=30):
    deadline = time.monotonic() + max_seconds
    catalog_bytes = b"".join(bounded_lines(catalog, 4 * 1024 * 1024, MAX_LINE, deadline))
    # Only IDs from the shipped public catalog may cross the privacy boundary.
    rules = set(re.findall(rb"^  - id: ([a-z0-9][a-z0-9.-]*)\s*$", catalog_bytes, re.M))
    rules = {value.decode("ascii") for value in rules}
    if not rules:
        raise InventoryError("CATALOG_INVALID")
    groups = Counter()
    registration_states = Counter()
    semantic_files, http_files = set(), set()
    missing_semantic_paths = missing_http_paths = 0
    total = 0
    for raw in bounded_lines(scan / "facts.ndjson", MAX_BYTES, MAX_LINE, deadline):
        total += 1
        if total > MAX_FACTS:
            raise InventoryError("FACT_LIMIT")
        fact = json.loads(raw, object_pairs_hook=unique_object)
        if not isinstance(fact, dict) or not isinstance(fact.get("factType"), str):
            raise InventoryError("FACT_INVALID")
        if fact["factType"] != "AnalysisGap":
            continue
        props = fact.get("properties")
        if not isinstance(props, dict):
            raise InventoryError("GAP_INVALID")
        rule = fact.get("ruleId")
        tier = fact.get("evidenceTier")
        reason = props.get("gapKind")
        if reason is None or reason == "":
            reason = props.get("classification")
        if not all(value is None or isinstance(value, str) for value in (rule, tier, reason)):
            raise InventoryError("GAP_INVALID")
        if rule == "legacy.webforms.composition.v1":
            state = props.get("registrationState")
            registration_states[state if isinstance(state, str) and state in REGISTRATION_STATES
                                else "other-or-unavailable"] += 1
        semantic = rule == "vb.semantic.workspace.v1" and reason == "SemanticAnalysisUnavailable"
        http = rule == "vb.syntax.http-coverage-gap.v1" and reason == "VisualBasicHttpAnalysisPartialWithoutSemantics"
        if semantic or http:
            evidence = fact.get("evidence")
            path = evidence.get("filePath") if isinstance(evidence, dict) else None
            if isinstance(path, str) and path not in ("", "."):
                (semantic_files if semantic else http_files).add(path)
            elif semantic:
                missing_semantic_paths += 1
            else:
                missing_http_paths += 1
        groups[(rule if rule in rules else "other-rule",
                reason if reason in REASONS else "other-or-missing-reason",
                tier if tier in TIERS else "unknown-tier")] += 1

    matches = []
    pattern = re.compile(rb"^Path matching: bounded; ([0-9]{1,10}) calls without a unique supported method target\. Analysis gaps in scan: ([0-9]{1,10})\.")
    report_path = scan / "report.md"
    if report_path.exists() or report_path.is_symlink():
        for line in bounded_lines(report_path, 16 * 1024 * 1024, MAX_LINE, deadline):
            match = pattern.match(line)
            if match:
                matches.append(tuple(map(int, match.groups())))
    gap_count = sum(groups.values())
    if len(matches) > 1:
        raise InventoryError("REPORT_AMBIGUOUS")
    if matches and matches[0][1] != gap_count:
        raise InventoryError("REPORT_GAP_COUNT_MISMATCH")
    projection = {
        "factsRead": total,
        "analysisGapFacts": gap_count,
        "retainedDiagnostics": {
            "registrationStates": dict(sorted(registration_states.items())),
            "semanticHttpFileOverlap": {
                "comparison": "exact-retained-file-path-only-not-root-cause",
                "semanticFiles": len(semantic_files), "httpFiles": len(http_files),
                "sharedFiles": len(semantic_files & http_files),
                "semanticOnlyFiles": len(semantic_files - http_files),
                "httpOnlyFiles": len(http_files - semantic_files),
                "semanticFactsWithoutFile": missing_semantic_paths,
                "httpFactsWithoutFile": missing_http_paths,
            },
        },
        "gapDomainCounts": {
            "compiled": sum(n for (r, _, _), n in groups.items() if r.startswith("dotnet.compiled.")),
            "otherKnownRules": sum(n for (r, _, _), n in groups.items()
                                   if r != "other-rule" and not r.startswith("dotnet.compiled.")),
            "unknownRule": sum(n for (r, _, _), n in groups.items() if r == "other-rule"),
        },
        "gapGroups": [dict(ruleId=r, reason=k, evidenceTier=t, count=n)
                      for (r, k, t), n in sorted(groups.items())],
        "reasonCoverage": {
            "recognizedFacts": sum(n for (_, reason, _), n in groups.items() if reason in REASONS),
            "otherOrMissingFacts": sum(n for (_, reason, _), n in groups.items() if reason not in REASONS),
        },
        "unresolvedCalls": {"count": matches[0][0] if matches else None,
                            "status": "retained-report-total" if matches else "unavailable",
                            "reasonBreakdown": "unavailable-without-retained-resolver-diagnostics"},
        "sqlHttpImpact": "unavailable-without-operation-specific-evidence-joins",
    }
    return {
        "schemaVersion": "retained-gap-inventory.v1", "ruleId": RULE,
        "evidenceTier": "Tier4Unknown", "coverage": "bounded-retained-artifacts-only",
        "generatorSha256": digest(Path(__file__).read_bytes()),
        "publicCatalogSha256": digest(catalog_bytes),
        # Never hash private facts, report bytes, names, or source identities.
        "boundedInputSha256": digest(canonical(projection)),
        "boundedInputKind": "privacy-projected-aggregate-counts",
        "limits": {"factsBytes": MAX_BYTES, "lineBytes": MAX_LINE, "facts": MAX_FACTS, "seconds": max_seconds},
        "inventory": projection,
        "limitations": ["Counts are fact occurrences, not unique bugs or complete application coverage.",
                        "Unknown labels are collapsed, never echoed or hashed individually.",
                        "Matching gap counts do not prove the report and facts share a scan identity.",
                        "No source scan, resolver replay, runtime proof, or before/after comparison was performed."],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scan", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    try:
        repo = Path(__file__).resolve().parents[1]
        destination = args.out.resolve()
        if destination.is_relative_to(args.scan.resolve()) or destination.is_relative_to(repo):
            raise InventoryError("OUTPUT_LOCATION_UNSAFE")
        result = summarize(args.scan, repo / "rules/rule-catalog.yml")
        # Exclusive creation prevents overwriting source artifacts or an earlier receipt.
        with args.out.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(json.dumps(result, indent=2, sort_keys=True) + "\n")
        print("RETAINED_GAP_INVENTORY_WRITTEN")
        return 0
    except InventoryError as error:
        print("RETAINED_GAP_INVENTORY_" + str(error), file=sys.stderr)
    except (OSError, ValueError, TypeError, RecursionError):
        print("RETAINED_GAP_INVENTORY_INPUT_OR_OUTPUT_INVALID", file=sys.stderr)
    return 1


if __name__ == "__main__":
    sys.exit(main())
