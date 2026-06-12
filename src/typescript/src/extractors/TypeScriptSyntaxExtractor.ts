import fs from "node:fs/promises";
import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, FileInventoryItem, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";

export async function extractSyntaxFacts(manifest: ScanManifest, inventory: readonly FileInventoryItem[]): Promise<CodeFact[]> {
  const facts: CodeFact[] = [];
  for (const item of inventory.filter((file) => !file.skipped && (file.relativePath.endsWith(".ts") || file.relativePath.endsWith(".tsx") || file.relativePath.endsWith(".d.ts")))) {
    const text = await fs.readFile(item.absolutePath, "utf8");
    const sourceFile = ts.createSourceFile(item.absolutePath, text, ts.ScriptTarget.Latest, true, item.relativePath.endsWith(".tsx") ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
    const parseDiagnostics = (sourceFile as ts.SourceFile & { parseDiagnostics?: readonly ts.DiagnosticWithLocation[] }).parseDiagnostics ?? [];
    for (const diagnostic of parseDiagnostics) {
      const line = sourceFile.getLineAndCharacterOfPosition(diagnostic.start ?? 0).line + 1;
      facts.push(
        createFact(
          manifest,
          FactTypes.AnalysisGap,
          RuleIds.TypeScriptSyntaxDeclarations,
          EvidenceTiers.Tier4Unknown,
          evidence(sourceFile, item.relativePath, sourceFile, line, line),
          { properties: { category: "syntax-parse", diagnosticCode: diagnostic.code } }
        )
      );
    }
    if (isBoilerplatePath(item.relativePath)) {
      facts.push(
        createFact(
          manifest,
          FactTypes.InfrastructureBoilerplate,
          RuleIds.TypeScriptSyntaxLogicShape,
          EvidenceTiers.Tier2Structural,
          createEvidence(item.relativePath, 1, 1, "typescript-syntax", ScannerVersions.TypeScriptSyntaxExtractor),
          { targetSymbol: item.relativePath, properties: { name: item.relativePath, category: "path" } }
        )
      );
    }
    visit(sourceFile, sourceFile, item.relativePath, manifest, facts);
  }
  return facts;
}

function visit(node: ts.Node, sourceFile: ts.SourceFile, filePath: string, manifest: ScanManifest, facts: CodeFact[]): void {
  addDeclarationFact(node, sourceFile, filePath, manifest, facts);
  if (ts.isPropertyAccessExpression(node)) {
    const name = node.name.getText(sourceFile);
    facts.push(
      createFact(
        manifest,
        FactTypes.MemberAccessName,
        RuleIds.TypeScriptSyntaxMemberAccess,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile),
        {
          targetSymbol: name,
          contractElement: name,
          properties: {
            name,
            memberName: name,
            propertyName: name,
            receiverHash: hash(node.expression.getText(sourceFile))
          }
        }
      )
    );
  }
  if (ts.isCallExpression(node)) {
    const invocationName = callName(node.expression, sourceFile);
    const containing = containingFunctionName(node);
    facts.push(
      createFact(
        manifest,
        FactTypes.InvocationName,
        RuleIds.TypeScriptSyntaxInvocation,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile),
        {
          sourceSymbol: containing,
          targetSymbol: invocationName,
          contractElement: invocationName,
          properties: { name: invocationName, methodName: invocationName, containingType: containing ?? "" }
        }
      )
    );
    facts.push(
      createFact(
        manifest,
        FactTypes.CallEdge,
        RuleIds.TypeScriptSyntaxCallGraph,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile),
        {
          sourceSymbol: containing,
          targetSymbol: invocationName,
          contractElement: invocationName,
          properties: { name: invocationName, methodName: invocationName, callKind: "syntax" }
        }
      )
    );
  }
  if (ts.isNewExpression(node)) {
    const createdType = node.expression.getText(sourceFile);
    facts.push(
      createFact(
        manifest,
        FactTypes.ObjectCreated,
        RuleIds.TypeScriptSyntaxObjectCreation,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile),
        {
          sourceSymbol: containingFunctionName(node),
          targetSymbol: createdType,
          contractElement: createdType,
          properties: { name: createdType, typeName: createdType, argumentCount: node.arguments?.length ?? 0, assignedTo: assignedToName(node, sourceFile) ?? "" }
        }
      )
    );
  }
  if (isCalculation(node)) {
    facts.push(
      createFact(
        manifest,
        FactTypes.CalculationExpression,
        RuleIds.TypeScriptSyntaxLogicShape,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile, undefined, undefined, hash(node.getText(sourceFile))),
        { properties: { expressionHash: hash(node.getText(sourceFile)), operator: ts.SyntaxKind[(node as ts.BinaryExpression).operatorToken.kind] } }
      )
    );
  }
  if (ts.isIfStatement(node) || ts.isSwitchStatement(node) || ts.isConditionalExpression(node)) {
    facts.push(
      createFact(
        manifest,
        FactTypes.BranchingLogic,
        RuleIds.TypeScriptSyntaxLogicShape,
        EvidenceTiers.Tier3SyntaxOrTextual,
        evidence(node, filePath, sourceFile, undefined, undefined, hash(node.getText(sourceFile))),
        { properties: { expressionHash: hash(node.getText(sourceFile)), branchKind: ts.SyntaxKind[node.kind] } }
      )
    );
  }
  ts.forEachChild(node, (child) => visit(child, sourceFile, filePath, manifest, facts));
}

function addDeclarationFact(node: ts.Node, sourceFile: ts.SourceFile, filePath: string, manifest: ScanManifest, facts: CodeFact[]): void {
  const named = node as { name?: ts.Node };
  if (!named.name || !ts.isIdentifier(named.name)) {
    return;
  }
  const name = named.name.text;
  const typeFact = declarationFactType(node);
  if (!typeFact) {
    return;
  }
  facts.push(
    createFact(
      manifest,
      typeFact,
      RuleIds.TypeScriptSyntaxDeclarations,
      EvidenceTiers.Tier3SyntaxOrTextual,
      evidence(node, filePath, sourceFile),
      {
        targetSymbol: name,
        contractElement: name,
        properties: {
          name,
          typeName: name,
          memberName: name,
          declarationKind: ts.SyntaxKind[node.kind],
          declarationSource: filePath.endsWith(".d.ts") ? "declaration" : "implementation"
        }
      }
    )
  );
}

function declarationFactType(node: ts.Node): string | null {
  if (ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node) || ts.isTypeAliasDeclaration(node)) {
    return FactTypes.TypeDeclared;
  }
  if (ts.isEnumDeclaration(node)) {
    return FactTypes.TypeDeclared;
  }
  if (ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node)) {
    return FactTypes.MethodDeclared;
  }
  if (ts.isPropertyDeclaration(node) || ts.isPropertySignature(node)) {
    return FactTypes.PropertyDeclared;
  }
  if (ts.isParameter(node)) {
    return FactTypes.ParameterDeclared;
  }
  return null;
}

function callName(expression: ts.Expression, sourceFile: ts.SourceFile): string {
  if (ts.isPropertyAccessExpression(expression)) {
    return expression.name.text;
  }
  if (ts.isIdentifier(expression)) {
    return expression.text;
  }
  return expression.getText(sourceFile);
}

function containingFunctionName(node: ts.Node): string | null {
  let current: ts.Node | undefined = node.parent;
  while (current) {
    const named = current as { name?: ts.Node };
    if ((ts.isFunctionDeclaration(current) || ts.isMethodDeclaration(current) || ts.isArrowFunction(current) || ts.isFunctionExpression(current)) && named.name && ts.isIdentifier(named.name)) {
      return named.name.text;
    }
    current = current.parent;
  }
  return null;
}

function assignedToName(node: ts.Node, sourceFile: ts.SourceFile): string | null {
  const parent = node.parent;
  if (ts.isVariableDeclaration(parent) && parent.name) {
    return parent.name.getText(sourceFile);
  }
  if (ts.isBinaryExpression(parent) && parent.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
    return parent.left.getText(sourceFile);
  }
  return null;
}

function isCalculation(node: ts.Node): node is ts.BinaryExpression {
  return ts.isBinaryExpression(node) && [
    ts.SyntaxKind.PlusToken,
    ts.SyntaxKind.MinusToken,
    ts.SyntaxKind.AsteriskToken,
    ts.SyntaxKind.SlashToken,
    ts.SyntaxKind.PercentToken
  ].includes(node.operatorToken.kind);
}

function evidence(
  node: ts.Node,
  filePath: string,
  sourceFile: ts.SourceFile,
  startLine?: number,
  endLine?: number,
  snippetHash: string | null = null
) {
  const start = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
  const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return createEvidence(filePath, startLine ?? start, endLine ?? end, "typescript-syntax", ScannerVersions.TypeScriptSyntaxExtractor, snippetHash);
}

function isBoilerplatePath(filePath: string): boolean {
  return /(^|\/)(generated|dist|build|routes|controllers)(\/|$)/i.test(filePath) || /\.(generated|gen)\.tsx?$/.test(filePath);
}
