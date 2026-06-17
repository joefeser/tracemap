import fs from "node:fs/promises";
import path from "node:path";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

interface TemplateOwner {
  componentClass: string;
  componentFile: string;
  templateOrigin: "templateUrl" | "inline";
}

interface TemplateInput {
  filePath: string;
  text: string;
  origin: "templateUrl" | "inline";
  componentClass: string;
}

export async function extractAngularTemplateFacts(manifest: ScanManifest, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const owners = await collectTemplateOwners(inventory);
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => !file.skipped && file.relativePath.endsWith(".html")).sort(byPath)) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    const owner = owners.get(item.relativePath);
    facts.push(...extractTemplate(manifest, {
      filePath: item.relativePath,
      text,
      origin: "templateUrl",
      componentClass: owner?.componentClass ?? ""
    }));
  }

  for (const item of inventory.filter((file) => !file.skipped && file.relativePath.endsWith(".ts")).sort(byPath)) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    for (const inline of inlineTemplates(text)) {
      const componentClass = componentClassNear(text, inline.index) ?? "";
      facts.push(...extractTemplate(manifest, {
        filePath: item.relativePath,
        text: inline.text,
        origin: "inline",
        componentClass
      }, inline.startLine));
    }
  }

  return facts
    .sort((left, right) =>
      left.evidence.filePath.localeCompare(right.evidence.filePath)
      || left.evidence.startLine - right.evidence.startLine
      || left.factType.localeCompare(right.factType)
      || left.factId.localeCompare(right.factId)
    );
}

async function collectTemplateOwners(inventory: readonly FileInventoryItem[]): Promise<Map<string, TemplateOwner>> {
  const owners = new Map<string, TemplateOwner>();
  for (const item of inventory.filter((file) => !file.skipped && file.relativePath.endsWith(".ts")).sort(byPath)) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    const componentClass = componentClassNear(text, 0);
    if (!componentClass) {
      continue;
    }
    const templateUrl = /templateUrl\s*:\s*["']([^"']+)["']/.exec(text)?.[1];
    if (!templateUrl || templateUrl.includes("://")) {
      continue;
    }
    const normalized = path.posix.normalize(path.posix.join(path.posix.dirname(item.relativePath), templateUrl.replaceAll("\\", "/")));
    owners.set(normalized, {
      componentClass,
      componentFile: item.relativePath,
      templateOrigin: "templateUrl"
    });
  }
  return owners;
}

function extractTemplate(manifest: ScanManifest, input: TemplateInput, baseLine = 1): CodeFact[] {
  const facts: CodeFact[] = [];
  const starts = lineStarts(input.text);
  const addGap = (index: number, expression: string, gapKind: string, message: string) => {
    const line = baseLine + lineFor(starts, index) - 1;
    facts.push(createFact(
      manifest,
      FactTypes.UiBindingGap,
      RuleIds.TypeScriptAngularBindingGap,
      EvidenceTiers.Tier4Unknown,
      createEvidence(input.filePath, line, line, "typescript-angular-template", ScannerVersions.TypeScriptAngularTemplateExtractor),
      {
        sourceSymbol: input.componentClass || null,
        properties: common(input, {
          gapKind,
          expressionHash: hash(expression, 32),
          expressionKind: "dynamic",
          message
        })
      }
    ));
  };

  for (const match of input.text.matchAll(/\{\{\s*([^}]+?)\s*\}\}/g)) {
    const expression = match[1].trim();
    if (!isStaticPropertyPath(expression)) {
      addGap(match.index ?? 0, expression, "dynamic-template-expression", "Interpolation expression is not a static property path.");
      continue;
    }
    facts.push(bindingFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "interpolation", expression, "read"));
  }

  for (const match of input.text.matchAll(/\[\(([\w.-]+)\)\]\s*=\s*["']([^"']+)["']/g)) {
    const expression = match[2].trim();
    if (!isStaticPropertyPath(expression)) {
      addGap(match.index ?? 0, expression, "dynamic-two-way-binding", "Two-way binding expression is not a static property path.");
      continue;
    }
    facts.push(bindingFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "two-way", expression, "read"));
    facts.push(bindingFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "two-way", expression, "write"));
  }

  for (const match of input.text.matchAll(/\[([\w.-]+)\]\s*=\s*["']([^"']+)["']/g)) {
    const expression = match[2].trim();
    if (!isStaticPropertyPath(expression)) {
      addGap(match.index ?? 0, expression, "dynamic-property-binding", "Property binding expression is not a static property path.");
      continue;
    }
    facts.push(bindingFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "property", expression, "read", match[1]));
  }

  for (const match of input.text.matchAll(/\(([\w.-]+)\)\s*=\s*["']([^"']+)["']/g)) {
    const handler = /^([A-Za-z_$][\w$]*)/.exec(match[2].trim())?.[1];
    if (!handler) {
      addGap(match.index ?? 0, match[2], "dynamic-event-binding", "Event binding handler is not a static method name.");
      continue;
    }
    facts.push(createFact(
      manifest,
      FactTypes.UiEventBinding,
      RuleIds.TypeScriptAngularEventBinding,
      EvidenceTiers.Tier2Structural,
      span(input.filePath, starts, baseLine, match.index ?? 0, match[0].length),
      {
        sourceSymbol: input.componentClass || null,
        targetSymbol: handler,
        contractElement: handler,
        properties: common(input, {
          bindingKind: "event",
          eventName: match[1],
          handlerName: handler,
          expressionHash: hash(match[2], 32),
          expressionKind: "handler-call"
        })
      }
    ));
  }

  for (const match of input.text.matchAll(/\bformControlName\s*=\s*["']([^"']+)["']/g)) {
    facts.push(controlFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "form-control", match[1], { formControlName: match[1] }));
  }
  for (const match of input.text.matchAll(/\bform(Group|ArrayName)\s*=\s*["']([^"']+)["']/g)) {
    facts.push(controlFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, match[1] === "Group" ? "form-group" : "form-array", match[2], { formGroupName: match[2] }));
  }
  for (const match of input.text.matchAll(/\bname\s*=\s*["']([^"']+)["'][^>]*\bngModel\b/g)) {
    facts.push(controlFact(manifest, input, starts, baseLine, match.index ?? 0, match[0].length, "template-driven-control", match[1], { controlName: match[1] }));
  }
  for (const match of input.text.matchAll(/#([A-Za-z_$][\w$]*)(?:\s*=\s*["']([^"']+)["'])?/g)) {
    facts.push(createFact(
      manifest,
      FactTypes.UiTemplateVariable,
      RuleIds.TypeScriptAngularTemplateVariable,
      EvidenceTiers.Tier3SyntaxOrTextual,
      span(input.filePath, starts, baseLine, match.index ?? 0, match[0].length),
      {
        sourceSymbol: input.componentClass || null,
        targetSymbol: match[1],
        contractElement: match[1],
        properties: common(input, {
          bindingKind: "template-variable",
          templateVariableName: match[1],
          templateVariableExport: match[2] ?? "",
          expressionKind: "template-variable"
        })
      }
    ));
  }

  return facts;
}

function bindingFact(
  manifest: ScanManifest,
  input: TemplateInput,
  starts: readonly number[],
  baseLine: number,
  index: number,
  length: number,
  bindingKind: string,
  propertyPath: string,
  direction: string,
  targetName = ""
): CodeFact {
  const memberName = propertyPath.split(".").at(-1) ?? propertyPath;
  return createFact(
    manifest,
    FactTypes.UiTemplateBinding,
    RuleIds.TypeScriptAngularTemplateBinding,
    EvidenceTiers.Tier2Structural,
    span(input.filePath, starts, baseLine, index, length),
    {
      sourceSymbol: input.componentClass || null,
      targetSymbol: propertyPath,
      contractElement: memberName,
      properties: common(input, {
        bindingKind,
        direction,
        targetName,
        propertyPath,
        memberName,
        propertyName: memberName,
        expressionKind: "property-path",
        expressionHash: hash(propertyPath, 32)
      })
    }
  );
}

function controlFact(
  manifest: ScanManifest,
  input: TemplateInput,
  starts: readonly number[],
  baseLine: number,
  index: number,
  length: number,
  bindingKind: string,
  controlName: string,
  extra: Record<string, string>
): CodeFact {
  return createFact(
    manifest,
    FactTypes.UiFormControlBinding,
    RuleIds.TypeScriptAngularFormBinding,
    EvidenceTiers.Tier2Structural,
    span(input.filePath, starts, baseLine, index, length),
    {
      sourceSymbol: input.componentClass || null,
      targetSymbol: controlName,
      contractElement: controlName,
      properties: common(input, {
        bindingKind,
        controlName,
        propertyName: controlName,
        expressionKind: "control-name",
        ...extra
      })
    }
  );
}

function common(input: TemplateInput, values: Record<string, string>): Record<string, string> {
  return {
    uiFramework: "angular",
    templateOrigin: input.origin,
    componentClass: input.componentClass,
    valueStored: "safe-metadata-only",
    ...values
  };
}

function isStaticPropertyPath(expression: string): boolean {
  return /^[A-Za-z_$][\w$]*(?:\??\.[A-Za-z_$][\w$]*)*$/.test(expression)
    && !/[()[\]|]/.test(expression);
}

function inlineTemplates(text: string): { text: string; index: number; startLine: number }[] {
  const result: { text: string; index: number; startLine: number }[] = [];
  const starts = lineStarts(text);
  for (const match of text.matchAll(/template\s*:\s*`([\s\S]*?)`/g)) {
    result.push({ text: match[1], index: match.index ?? 0, startLine: lineFor(starts, match.index ?? 0) });
  }
  return result;
}

function componentClassNear(text: string, index: number): string | null {
  const after = text.slice(index);
  const match = /export\s+class\s+([A-Za-z_$][\w$]*)/.exec(after) ?? /class\s+([A-Za-z_$][\w$]*)/.exec(after);
  return match?.[1] ?? null;
}

function span(filePath: string, starts: readonly number[], baseLine: number, index: number, length: number) {
  const start = baseLine + lineFor(starts, index) - 1;
  const end = baseLine + lineFor(starts, index + length) - 1;
  return createEvidence(filePath, start, end, "typescript-angular-template", ScannerVersions.TypeScriptAngularTemplateExtractor);
}

function lineStarts(text: string): number[] {
  const starts = [0];
  for (let index = 0; index < text.length; index++) {
    if (text[index] === "\n") {
      starts.push(index + 1);
    }
  }
  return starts;
}

function lineFor(starts: readonly number[], index: number): number {
  let line = 0;
  for (let candidate = 0; candidate < starts.length; candidate++) {
    if (starts[candidate] > index) {
      break;
    }
    line = candidate;
  }
  return line + 1;
}

function byPath(left: FileInventoryItem, right: FileInventoryItem): number {
  return left.relativePath.localeCompare(right.relativePath);
}
