import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";

export class AnalysisGapCollector {
  private readonly gaps: CodeFact[] = [];
  private readonly knownGaps = new Set<string>();

  add(
    manifest: ScanManifest,
    category: string,
    message: string,
    filePath = ".",
    startLine = 1,
    properties: Record<string, string | number | boolean | null | undefined> = {}
  ): void {
    this.knownGaps.add(`${category}: ${message}`);
    this.gaps.push(
      createFact(
        manifest,
        FactTypes.AnalysisGap,
        RuleIds.RepoManifest,
        EvidenceTiers.Tier4Unknown,
        createEvidence(filePath, startLine, startLine, "typescript-gap", ScannerVersions.TraceMapTypeScript),
        {
          properties: {
            gapKind: category,
            category,
            messageHash: hashMessage(message),
            ...properties
          }
        }
      )
    );
  }

  facts(): CodeFact[] {
    return [...this.gaps];
  }

  messages(): string[] {
    return [...this.knownGaps].sort();
  }
}

function hashMessage(value: string): string {
  let hash = 0;
  for (let index = 0; index < value.length; index++) {
    hash = (hash * 31 + value.charCodeAt(index)) >>> 0;
  }
  return hash.toString(16);
}
