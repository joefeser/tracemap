export interface ScanManifest {
  scanId: string;
  repoName: string;
  remoteUrl: string | null;
  branch: string | null;
  commitSha: string;
  scannerVersion: string;
  scannedAt: string;
  analysisLevel: string;
  buildStatus: string;
  solutions: string[];
  projects: string[];
  targetFrameworks: string[];
  knownGaps: string[];
}

export interface EvidenceSpan {
  filePath: string;
  startLine: number;
  endLine: number;
  snippetHash: string | null;
  extractorId: string;
  extractorVersion: string;
}

export interface CodeFact {
  factId: string;
  scanId: string;
  repo: string;
  commitSha: string;
  projectPath: string | null;
  factType: string;
  ruleId: string;
  evidenceTier: string;
  sourceSymbol: string | null;
  targetSymbol: string | null;
  contractElement: string | null;
  evidence: EvidenceSpan;
  properties: Record<string, string>;
}

export interface FileInventoryItem {
  relativePath: string;
  absolutePath: string;
  kind: string;
  sizeBytes: number;
  skipped: boolean;
}

export interface GitMetadata {
  repoName: string;
  remoteUrl: string | null;
  branch: string | null;
  commitSha: string;
  knownGaps: string[];
}

export interface ScanOptions {
  repoPath: string;
  outputPath: string;
  projectPaths: string[];
  includeGlobs: string[];
  excludeGlobs: string[];
  maxFileByteSize: number;
  semantic: boolean;
}

export interface ScanResult {
  manifest: ScanManifest;
  facts: CodeFact[];
  inventory: FileInventoryItem[];
}

export const EvidenceTiers = {
  Tier1Semantic: "Tier1Semantic",
  Tier2Structural: "Tier2Structural",
  Tier3SyntaxOrTextual: "Tier3SyntaxOrTextual",
  Tier4Unknown: "Tier4Unknown"
} as const;

export const FactTypes = {
  RepoScanned: "RepoScanned",
  BuildStatus: "BuildStatus",
  AnalysisGap: "AnalysisGap",
  FileInventoried: "FileInventoried",
  SolutionDeclared: "SolutionDeclared",
  ProjectDeclared: "ProjectDeclared",
  PackageReferenced: "PackageReferenced",
  TargetFrameworkDeclared: "TargetFrameworkDeclared",
  ConfigFileDeclared: "ConfigFileDeclared",
  TypeDeclared: "TypeDeclared",
  MethodDeclared: "MethodDeclared",
  PropertyDeclared: "PropertyDeclared",
  FieldDeclared: "FieldDeclared",
  ParameterDeclared: "ParameterDeclared",
  LocalAlias: "LocalAlias",
  SymbolRelationship: "SymbolRelationship",
  MemberAccessName: "MemberAccessName",
  InvocationName: "InvocationName",
  CallEdge: "CallEdge",
  ObjectCreated: "ObjectCreated",
  ArgumentPassed: "ArgumentPassed",
  CalculationExpression: "CalculationExpression",
  BranchingLogic: "BranchingLogic",
  RetryPolicyLogic: "RetryPolicyLogic",
  SerializationLogic: "SerializationLogic",
  InfrastructureBoilerplate: "InfrastructureBoilerplate",
  PropertyAccessed: "PropertyAccessed",
  MethodInvoked: "MethodInvoked",
  HttpCallDetected: "HttpCallDetected",
  DbChangeSaved: "DbChangeSaved",
  SqlTextUsed: "SqlTextUsed",
  ConfigKeyDeclared: "ConfigKeyDeclared",
  HttpRouteBinding: "HttpRouteBinding",
  DatabaseColumnMapping: "DatabaseColumnMapping",
  SerializerContractMember: "SerializerContractMember"
} as const;
