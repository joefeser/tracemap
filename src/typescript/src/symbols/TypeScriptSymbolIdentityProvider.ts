import path from "node:path";
import ts from "typescript";
import { PackageIdentity, findNearestPackageIdentity } from "../extractors/PackageJsonExtractor";
import { normalizePath, repoRelative } from "../util/Paths";

export class TypeScriptSymbolIdentityProvider {
  private readonly packageCache = new Map<string, PackageIdentity>();

  constructor(private readonly repoPath: string) {}

  async packageFor(filePath: string): Promise<PackageIdentity> {
    const directory = path.dirname(filePath);
    const cached = this.packageCache.get(directory);
    if (cached) {
      return cached;
    }
    const identity = await findNearestPackageIdentity(this.repoPath, filePath);
    this.packageCache.set(directory, identity);
    return identity;
  }

  async symbolId(symbol: ts.Symbol | undefined, checker: ts.TypeChecker, sourceFile: ts.SourceFile, node: ts.Node, fallbackName: string): Promise<string> {
    const name = symbol?.getName() ?? fallbackName;
    if (!symbol || name === "__function" || name === "__object") {
      return this.localSymbolKey(sourceFile, node, name);
    }
    const declaration = symbol.valueDeclaration ?? symbol.declarations?.[0] ?? node;
    const declarationSourceFile = declaration.getSourceFile();
    const identitySourceFile = isInsidePath(this.repoPath, declarationSourceFile.fileName) ? declarationSourceFile : sourceFile;
    const identity = await this.packageFor(identitySourceFile.fileName);
    const modulePath = normalizePath(path.relative(identity.rootPath, identitySourceFile.fileName).replace(/\.(tsx?|d\.ts)$/, ""));
    const descriptor = descriptorFor(symbol, checker, declaration, name);
    return `typescript package ${identity.name} ${identity.version} ${modulePath} ${descriptor}`;
  }

  localSymbolKey(sourceFile: ts.SourceFile, node: ts.Node, name: string): string {
    const position = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
    return `typescript local ${repoRelative(this.repoPath, sourceFile.fileName)}:${position.line + 1}:${position.character + 1}:${name}`;
  }
}

export function localSymbolKey(repoPath: string, sourceFile: ts.SourceFile, node: ts.Node, name: string): string {
  const position = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
  return `typescript local ${repoRelative(repoPath, sourceFile.fileName)}:${position.line + 1}:${position.character + 1}:${name}`;
}

function descriptorFor(symbol: ts.Symbol, checker: ts.TypeChecker, declaration: ts.Node, name: string): string {
  if (ts.isParameter(declaration)) {
    return parameterDescriptor(declaration);
  }
  if (ts.isClassDeclaration(declaration) || ts.isInterfaceDeclaration(declaration) || ts.isTypeAliasDeclaration(declaration) || ts.isEnumDeclaration(declaration)) {
    return `${name}#`;
  }
  if (ts.isMethodDeclaration(declaration) || ts.isMethodSignature(declaration) || ts.isFunctionDeclaration(declaration) || ts.isConstructorDeclaration(declaration) || ts.isCallSignatureDeclaration(declaration)) {
    const type = checker.getTypeOfSymbolAtLocation(symbol, declaration);
    const signatures = checker.getSignaturesOfType(type, ts.SignatureKind.Call);
    const signature = signatures[0];
    const params = signature ? signature.parameters.map((param) => checker.typeToString(checker.getTypeOfSymbolAtLocation(param, declaration))).join(",") : "";
    return `${name}(${params}).`;
  }
  return `${name}.`;
}

function parameterDescriptor(declaration: ts.ParameterDeclaration): string {
  const owner = declaration.parent;
  const ownerName = callableOwnerName(owner);
  const ordinal = "parameters" in owner ? owner.parameters.findIndex((parameter) => parameter === declaration) : -1;
  const parameterName = ts.isIdentifier(declaration.name) ? declaration.name.text : `binding-${ordinal}`;
  return `${ownerName} parameter ${ordinal}:${parameterName}`;
}

function callableOwnerName(node: ts.Node): string {
  if (ts.isConstructorDeclaration(node)) {
    const className = ts.isClassDeclaration(node.parent) && node.parent.name ? node.parent.name.text : "anonymous-class";
    return `${className}.constructor`;
  }

  const named = node as { name?: ts.Node };
  const localName = named.name && ts.isIdentifier(named.name) ? named.name.text : ts.SyntaxKind[node.kind];
  const parent = node.parent;
  if ((ts.isMethodDeclaration(node) || ts.isMethodSignature(node)) && parent && (ts.isClassDeclaration(parent) || ts.isInterfaceDeclaration(parent)) && parent.name) {
    return `${parent.name.text}.${localName}`;
  }

  return localName;
}

function isInsidePath(rootPath: string, filePath: string): boolean {
  const root = path.resolve(rootPath);
  const candidate = path.resolve(filePath);
  return candidate === root || candidate.startsWith(root + path.sep);
}
