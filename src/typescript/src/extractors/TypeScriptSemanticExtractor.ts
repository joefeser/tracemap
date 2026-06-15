import path from "node:path";
import ts from "typescript";
import { CodeFact, EvidenceTiers, FactTypes, ScanManifest } from "../facts/Models";
import { createEvidence, createFact } from "../facts/FactFactory";
import { RuleIds, ScannerVersions } from "../facts/RuleIds";
import { hash } from "../util/Hash";
import { repoRelative } from "../util/Paths";
import { TypeScriptSymbolIdentityProvider } from "../symbols/TypeScriptSymbolIdentityProvider";
import { LoadedProject } from "./TypeScriptProjectLoader";

export async function extractSemanticFacts(repoPath: string, manifest: ScanManifest, projects: readonly LoadedProject[]): Promise<CodeFact[]> {
  const provider = new TypeScriptSymbolIdentityProvider(repoPath);
  const facts: CodeFact[] = [];
  for (const project of projects) {
    for (const sourceFile of project.sourceFiles) {
      await visit(repoPath, manifest, project, sourceFile, sourceFile, provider, facts);
    }
  }
  return facts;
}

async function visit(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  await addDeclarationFact(repoPath, manifest, project, node, sourceFile, provider, facts);
  await addPropertyAccessFact(repoPath, manifest, project, node, sourceFile, provider, facts);
  await addCallFacts(repoPath, manifest, project, node, sourceFile, provider, facts);
  await addObjectCreationFact(repoPath, manifest, project, node, sourceFile, provider, facts);
  await addLocalAliasFact(repoPath, manifest, project, node, sourceFile, provider, facts);
  await addRelationshipFacts(repoPath, manifest, project, node, sourceFile, provider, facts);
  for (const child of childrenOf(node)) {
    await visit(repoPath, manifest, project, child, sourceFile, provider, facts);
  }
}

async function addDeclarationFact(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  const named = node as { name?: ts.Node };
  if (!named.name || !ts.isIdentifier(named.name)) {
    return;
  }
  const factType = declarationFactType(node);
  if (!factType) {
    return;
  }
  const symbol = project.checker.getSymbolAtLocation(named.name);
  if (!symbol) {
    return;
  }
  const symbolId = await provider.symbolId(symbol, project.checker, sourceFile, node, named.name.text);
  const packageIdentity = await provider.packageFor(sourceFile.fileName);
  const displayName = project.checker.getFullyQualifiedName(symbol).replace(/^".*"\./, "");
  const namespace = namespaceFor(displayName);
  const properties = symbolProperties("target", symbolId, named.name.text, symbolKind(node), displayName, packageIdentity);
  facts.push(
    createFact(
      manifest,
      factType,
      RuleIds.TypeScriptSemanticDeclarations,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile),
      {
        projectPath: project.projectPath,
        targetSymbol: displayName,
        contractElement: named.name.text,
        properties: {
          ...properties,
          name: named.name.text,
          typeName: named.name.text,
          namespace,
          targetSymbol: displayName,
          declarationKind: ts.SyntaxKind[node.kind],
          declarationSource: sourceFile.fileName.endsWith(".d.ts") ? "declaration" : "implementation",
          packageName: packageIdentity.name,
          packageVersion: packageIdentity.version
        }
      }
    )
  );
}

async function addPropertyAccessFact(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  if (!ts.isPropertyAccessExpression(node)) {
    return;
  }
  const symbol = project.checker.getSymbolAtLocation(node.name);
  if (!symbol) {
    return;
  }
  const propertyName = node.name.text;
  const symbolId = await provider.symbolId(symbol, project.checker, sourceFile, node.name, propertyName);
  const displayName = project.checker.getFullyQualifiedName(symbol).replace(/^".*"\./, "");
  const packageIdentity = await provider.packageFor(sourceFile.fileName);
  const containingType = containingTypeName(symbol, project.checker, node);
  facts.push(
    createFact(
      manifest,
      FactTypes.PropertyAccessed,
      RuleIds.TypeScriptSemanticPropertyAccess,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile),
      {
        projectPath: project.projectPath,
        sourceSymbol: await containingSymbol(repoPath, project, sourceFile, node, provider),
        targetSymbol: displayName,
        contractElement: propertyName,
        properties: {
          ...symbolProperties("target", symbolId, propertyName, "property", displayName, packageIdentity),
          propertyName,
          memberName: propertyName,
          name: propertyName,
          containingType,
          targetSymbol: displayName,
          accessKind: isWriteAccess(node) ? "write" : "read",
          packageName: packageIdentity.name,
          packageVersion: packageIdentity.version
        }
      }
    )
  );
}

async function addCallFacts(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  if (!ts.isCallExpression(node)) {
    return;
  }
  const signature = project.checker.getResolvedSignature(node);
  const signatureDeclaration = signature?.getDeclaration();
  const declarationSymbol = signatureDeclaration ? (signatureDeclaration as ts.Declaration & { symbol?: ts.Symbol }).symbol : undefined;
  const symbol = signatureDeclaration ? project.checker.getSymbolAtLocation(callNameNode(node)) ?? declarationSymbol : project.checker.getSymbolAtLocation(callNameNode(node));
  const methodName = callName(node.expression, sourceFile);
  if (!symbol && !signature) {
    return;
  }
  const declaration = signature?.getDeclaration() ?? node.expression;
  const symbolId = await provider.symbolId(symbol, project.checker, sourceFile, declaration, methodName);
  const displayName = symbol ? project.checker.getFullyQualifiedName(symbol).replace(/^".*"\./, "") : methodName;
  const packageIdentity = await provider.packageFor(sourceFile.fileName);
  const caller = await containingSymbol(repoPath, project, sourceFile, node, provider);
  const containingType = symbol ? containingTypeName(symbol, project.checker, node) : "";
  facts.push(
    createFact(
      manifest,
      FactTypes.MethodInvoked,
      RuleIds.TypeScriptSemanticMethodInvocation,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile),
      {
        projectPath: project.projectPath,
        sourceSymbol: caller,
        targetSymbol: displayName,
        contractElement: methodName,
        properties: {
          ...symbolProperties("target", symbolId, methodName, "method", displayName, packageIdentity),
          methodName,
          memberName: methodName,
          name: methodName,
          containingType,
          targetSymbol: displayName,
          argumentCount: node.arguments.length,
          packageName: packageIdentity.name,
          packageVersion: packageIdentity.version
        }
      }
    )
  );
  facts.push(
    createFact(
      manifest,
      FactTypes.CallEdge,
      RuleIds.TypeScriptSemanticCallGraph,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile),
      {
        projectPath: project.projectPath,
        sourceSymbol: caller,
        targetSymbol: displayName,
        contractElement: methodName,
        properties: {
          ...symbolProperties("target", symbolId, methodName, "method", displayName, packageIdentity),
          methodName,
          callKind: "method",
          containingType,
          packageName: packageIdentity.name,
          packageVersion: packageIdentity.version
        }
      }
    )
  );
  await addArgumentFacts(repoPath, manifest, project, node, sourceFile, provider, facts, signature, caller, displayName, packageIdentity);
}

async function addArgumentFacts(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.CallExpression,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[],
  signature: ts.Signature | undefined,
  caller: string | null,
  callee: string,
  packageIdentity: { name: string; version: string }
): Promise<void> {
  const parameters = signature?.parameters ?? [];
  for (let index = 0; index < node.arguments.length; index++) {
    const argument = node.arguments[index];
    const parameter = parameters[index];
    const argumentSymbol = project.checker.getSymbolAtLocation(argument);
    const argumentSymbolId = argumentSymbol ? await provider.symbolId(argumentSymbol, project.checker, sourceFile, argument, argumentSymbol.getName()) : null;
    const parameterName = parameter?.getName() ?? `arg${index}`;
    const parameterType = parameter ? project.checker.typeToString(project.checker.getTypeOfSymbolAtLocation(parameter, argument)) : "";
    const parameterSymbolId = parameter ? await provider.symbolId(parameter, project.checker, sourceFile, argument, parameterName) : null;
    const argumentDisplayName = argumentSymbol ? project.checker.getFullyQualifiedName(argumentSymbol).replace(/^".*"\./, "") : "";
    const parameterDisplayName = parameter ? project.checker.getFullyQualifiedName(parameter).replace(/^".*"\./, "") : "";
    const roleProperties = {
      ...(argumentSymbol && argumentSymbolId ? symbolProperties("argument", argumentSymbolId, argumentSymbol.getName(), "symbol", argumentDisplayName, packageIdentity) : {}),
      ...(parameter && parameterSymbolId ? symbolProperties("parameter", parameterSymbolId, parameterName, "parameter", parameterDisplayName, packageIdentity) : {})
    };
    facts.push(
      createFact(
        manifest,
        FactTypes.ArgumentPassed,
        RuleIds.TypeScriptSemanticValueFlow,
        EvidenceTiers.Tier1Semantic,
        evidence(argument, repoPath, sourceFile, undefined, undefined, hash(argument.getText(sourceFile))),
        {
          projectPath: project.projectPath,
          sourceSymbol: caller,
          targetSymbol: callee,
          contractElement: parameterName,
          properties: {
            ...roleProperties,
            parameterName,
            parameterType,
            parameterOrdinal: index,
            argumentOrdinal: index,
            argumentExpressionKind: ts.SyntaxKind[argument.kind],
            argumentExpressionHash: hash(argument.getText(sourceFile)),
            argumentSymbol: argumentSymbol?.getName() ?? "",
            argumentSymbolId: argumentSymbolId ?? "",
            argumentSymbolKind: argumentSymbol ? "symbol" : "",
            argumentType: project.checker.typeToString(project.checker.getTypeAtLocation(argument)),
            packageName: packageIdentity.name,
            packageVersion: packageIdentity.version
          }
        }
      )
    );
  }
}

async function addObjectCreationFact(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  if (!ts.isNewExpression(node)) {
    return;
  }
  const type = project.checker.getTypeAtLocation(node.expression);
  const symbol = type.symbol ?? project.checker.getSymbolAtLocation(node.expression);
  const createdType = safeExpressionName(node.expression) ?? `new-${ts.SyntaxKind[node.expression.kind]}`;
  const symbolId = await provider.symbolId(symbol, project.checker, sourceFile, node.expression, createdType);
  const packageIdentity = await provider.packageFor(sourceFile.fileName);
  const target = symbol ? project.checker.getFullyQualifiedName(symbol).replace(/^".*"\./, "") : createdType;
  facts.push(
    createFact(
      manifest,
      FactTypes.ObjectCreated,
      RuleIds.TypeScriptSemanticObjectCreation,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile),
      {
        projectPath: project.projectPath,
        sourceSymbol: await containingSymbol(repoPath, project, sourceFile, node, provider),
        targetSymbol: target,
        contractElement: createdType,
        properties: {
          ...symbolProperties("target", symbolId, createdType, "type", target, packageIdentity),
          name: createdType,
          typeName: createdType,
          expressionHash: hash(node.expression.getText(sourceFile)),
          assignedTo: assignedToName(node, sourceFile) ?? "",
          argumentCount: node.arguments?.length ?? 0,
          packageName: packageIdentity.name,
          packageVersion: packageIdentity.version
        }
      }
    )
  );
}

async function addLocalAliasFact(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  if (!ts.isVariableDeclaration(node) || !node.initializer || !ts.isIdentifier(node.name)) {
    return;
  }
  const aliasSymbol = project.checker.getSymbolAtLocation(node.name);
  const originSymbol = project.checker.getSymbolAtLocation(node.initializer);
  if (!aliasSymbol || !originSymbol) {
    return;
  }
  const aliasId = await provider.symbolId(aliasSymbol, project.checker, sourceFile, node.name, node.name.text);
  const originId = await provider.symbolId(originSymbol, project.checker, sourceFile, node.initializer, originSymbol.getName());
  facts.push(
    createFact(
      manifest,
      FactTypes.LocalAlias,
      RuleIds.TypeScriptSemanticLocalAlias,
      EvidenceTiers.Tier1Semantic,
      evidence(node, repoPath, sourceFile, undefined, undefined, hash(node.initializer.getText(sourceFile))),
      {
        projectPath: project.projectPath,
        sourceSymbol: await containingSymbol(repoPath, project, sourceFile, node, provider),
        targetSymbol: aliasSymbol.getName(),
        contractElement: aliasSymbol.getName(),
        properties: {
          aliasSymbol: aliasSymbol.getName(),
          aliasSymbolId: aliasId,
          targetSymbolId: aliasId,
          aliasSymbolKind: "local",
          aliasType: project.checker.typeToString(project.checker.getTypeAtLocation(node.name)),
          originSymbol: originSymbol.getName(),
          originSymbolId: originId,
          originSymbolKind: "symbol",
          originType: project.checker.typeToString(project.checker.getTypeAtLocation(node.initializer)),
          expressionHash: hash(node.initializer.getText(sourceFile))
        }
      }
    )
  );
}

async function addRelationshipFacts(
  repoPath: string,
  manifest: ScanManifest,
  project: LoadedProject,
  node: ts.Node,
  sourceFile: ts.SourceFile,
  provider: TypeScriptSymbolIdentityProvider,
  facts: CodeFact[]
): Promise<void> {
  if (!ts.isClassDeclaration(node) && !ts.isInterfaceDeclaration(node)) {
    return;
  }
  if (!node.name) {
    return;
  }
  const sourceSymbol = project.checker.getSymbolAtLocation(node.name);
  if (!sourceSymbol) {
    return;
  }
  const sourceId = await provider.symbolId(sourceSymbol, project.checker, sourceFile, node.name, node.name.text);
  for (const clause of node.heritageClauses ?? []) {
    const relationshipKind = clause.token === ts.SyntaxKind.ExtendsKeyword
      ? ts.isInterfaceDeclaration(node)
        ? "ExtendsInterface"
        : "ExtendsClass"
      : "ImplementsInterface";
    for (const typeNode of clause.types) {
      const targetSymbol = project.checker.getSymbolAtLocation(typeNode.expression);
      if (!targetSymbol) {
        continue;
      }
      const targetName = safeExpressionName(typeNode.expression) ?? targetSymbol.getName();
      const targetId = await provider.symbolId(targetSymbol, project.checker, sourceFile, typeNode.expression, targetName);
      facts.push(
        createFact(
          manifest,
          FactTypes.SymbolRelationship,
          RuleIds.TypeScriptSemanticSymbolRelationship,
          EvidenceTiers.Tier1Semantic,
          evidence(typeNode, repoPath, sourceFile),
          {
            projectPath: project.projectPath,
            sourceSymbol: sourceSymbol.getName(),
            targetSymbol: targetSymbol.getName(),
            contractElement: targetSymbol.getName(),
            properties: {
              relationshipKind,
              sourceSymbol: sourceSymbol.getName(),
              targetSymbol: targetSymbol.getName(),
              sourceSymbolId: sourceId,
              targetSymbolId: targetId,
              name: targetSymbol.getName()
            }
          }
        )
      );
    }
  }
  if (ts.isClassDeclaration(node)) {
    for (const member of node.members) {
      const modifiers = ts.getModifiers(member as ts.HasModifiers) ?? [];
      if (!modifiers.some((modifier) => modifier.kind === ts.SyntaxKind.OverrideKeyword)) {
        continue;
      }
      const named = member as { name?: ts.Node };
      if (!named.name || !ts.isIdentifier(named.name)) {
        continue;
      }
      const memberSymbol = project.checker.getSymbolAtLocation(named.name);
      if (!memberSymbol) {
        continue;
      }
      const memberId = await provider.symbolId(memberSymbol, project.checker, sourceFile, named.name, named.name.text);
      facts.push(
        createFact(
          manifest,
          FactTypes.SymbolRelationship,
          RuleIds.TypeScriptSemanticSymbolRelationship,
          EvidenceTiers.Tier1Semantic,
          evidence(member, repoPath, sourceFile),
          {
            projectPath: project.projectPath,
            sourceSymbol: memberSymbol.getName(),
            targetSymbol: memberSymbol.getName(),
            contractElement: memberSymbol.getName(),
            properties: {
              relationshipKind: "Overrides",
              sourceSymbol: memberSymbol.getName(),
              targetSymbol: memberSymbol.getName(),
              sourceSymbolId: memberId,
              targetSymbolId: memberId,
              name: memberSymbol.getName()
            }
          }
        )
      );
    }
  }
}

function declarationFactType(node: ts.Node): string | null {
  if (ts.isClassDeclaration(node) || ts.isInterfaceDeclaration(node) || ts.isTypeAliasDeclaration(node) || ts.isEnumDeclaration(node)) {
    return FactTypes.TypeDeclared;
  }
  if (ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node) || ts.isMethodSignature(node)) {
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

function symbolProperties(
  role: "source" | "target" | "argument" | "parameter" | "origin",
  symbolId: string,
  symbolName: string,
  symbolKind: string,
  displayName: string,
  packageIdentity: { name: string; version: string }
): Record<string, string> {
  return {
    [`${role}SymbolId`]: symbolId,
    [`${role}Symbol`]: symbolName,
    [`${role}SymbolLanguage`]: "typescript",
    [`${role}SymbolKind`]: symbolKind,
    [`${role}SymbolDisplayName`]: displayName,
    [`${role}DisplayName`]: displayName,
    [`${role}AssemblyName`]: packageIdentity.name,
    [`${role}AssemblyVersion`]: packageIdentity.version
  };
}

function symbolKind(node: ts.Node): string {
  if (ts.isClassDeclaration(node)) return "class";
  if (ts.isInterfaceDeclaration(node)) return "interface";
  if (ts.isTypeAliasDeclaration(node)) return "type";
  if (ts.isEnumDeclaration(node)) return "enum";
  if (ts.isFunctionDeclaration(node) || ts.isMethodDeclaration(node) || ts.isMethodSignature(node)) return "method";
  if (ts.isPropertyDeclaration(node) || ts.isPropertySignature(node)) return "property";
  if (ts.isParameter(node)) return "parameter";
  return "symbol";
}

function namespaceFor(displayName: string): string {
  const index = displayName.lastIndexOf(".");
  return index > 0 ? displayName.slice(0, index) : "";
}

function containingTypeName(symbol: ts.Symbol, checker: ts.TypeChecker, node: ts.Node): string {
  const parent = symbol.valueDeclaration?.parent;
  if (parent && (ts.isClassDeclaration(parent) || ts.isInterfaceDeclaration(parent)) && parent.name) {
    return parent.name.text;
  }
  const type = checker.getTypeAtLocation(node);
  return checker.typeToString(type);
}

async function containingSymbol(
  repoPath: string,
  project: LoadedProject,
  sourceFile: ts.SourceFile,
  node: ts.Node,
  provider: TypeScriptSymbolIdentityProvider
): Promise<string | null> {
  let current: ts.Node | undefined = node.parent;
  while (current) {
    const named = current as { name?: ts.Node };
    if ((ts.isFunctionDeclaration(current) || ts.isMethodDeclaration(current) || ts.isConstructorDeclaration(current) || ts.isArrowFunction(current) || ts.isFunctionExpression(current)) && named.name && ts.isIdentifier(named.name)) {
      const symbol = project.checker.getSymbolAtLocation(named.name);
      return symbol ? provider.symbolId(symbol, project.checker, sourceFile, named.name, named.name.text) : named.name.text;
    }
    if (ts.isConstructorDeclaration(current)) {
      const containingClass = current.parent;
      if (ts.isClassDeclaration(containingClass) && containingClass.name) {
        const symbol = project.checker.getSymbolAtLocation(containingClass.name);
        const name = `${containingClass.name.text}.constructor`;
        return symbol ? `${await provider.symbolId(symbol, project.checker, sourceFile, containingClass.name, containingClass.name.text)} constructor` : name;
      }
    }
    if ((ts.isArrowFunction(current) || ts.isFunctionExpression(current)) && ts.isVariableDeclaration(current.parent) && ts.isIdentifier(current.parent.name)) {
      const symbol = project.checker.getSymbolAtLocation(current.parent.name);
      return symbol ? provider.symbolId(symbol, project.checker, sourceFile, current.parent.name, current.parent.name.text) : current.parent.name.text;
    }
    current = current.parent;
  }
  return null;
}

function callNameNode(node: ts.CallExpression): ts.Node {
  return ts.isPropertyAccessExpression(node.expression) ? node.expression.name : node.expression;
}

function callName(expression: ts.Expression, sourceFile: ts.SourceFile): string {
  if (ts.isPropertyAccessExpression(expression)) return expression.name.text;
  if (ts.isIdentifier(expression)) return expression.text;
  return ts.SyntaxKind[expression.kind];
}

function isWriteAccess(node: ts.PropertyAccessExpression): boolean {
  const parent = node.parent;
  return ts.isBinaryExpression(parent) && parent.left === node && parent.operatorToken.kind === ts.SyntaxKind.EqualsToken;
}

function assignedToName(node: ts.Node, sourceFile: ts.SourceFile): string | null {
  const parent = node.parent;
  if (ts.isVariableDeclaration(parent)) {
    return bindingName(parent.name);
  }
  if (ts.isBinaryExpression(parent) && parent.operatorToken.kind === ts.SyntaxKind.EqualsToken) {
    return safeExpressionName(parent.left);
  }
  return null;
}

function bindingName(name: ts.BindingName): string {
  if (ts.isIdentifier(name)) {
    return name.text;
  }
  return ts.isObjectBindingPattern(name) ? "object-binding-pattern" : "array-binding-pattern";
}

function safeExpressionName(expression: ts.Expression): string | null {
  if (ts.isIdentifier(expression)) {
    return expression.text;
  }
  if (ts.isPropertyAccessExpression(expression)) {
    const receiver = safeExpressionName(expression.expression);
    return receiver ? `${receiver}.${expression.name.text}` : expression.name.text;
  }
  if (expression.kind === ts.SyntaxKind.ThisKeyword) {
    return "this";
  }
  if (expression.kind === ts.SyntaxKind.SuperKeyword) {
    return "super";
  }
  return null;
}

function evidence(node: ts.Node, repoPath: string, sourceFile: ts.SourceFile, startLine?: number, endLine?: number, snippetHash: string | null = null) {
  const start = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile)).line + 1;
  const end = sourceFile.getLineAndCharacterOfPosition(node.getEnd()).line + 1;
  return createEvidence(repoRelative(repoPath, sourceFile.fileName), startLine ?? start, endLine ?? end, "typescript-semantic", ScannerVersions.TypeScriptSemanticExtractor, snippetHash);
}

function childrenOf(node: ts.Node): ts.Node[] {
  const children: ts.Node[] = [];
  ts.forEachChild(node, (child) => {
    children.push(child);
  });
  return children;
}
