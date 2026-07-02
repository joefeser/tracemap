import Foundation
import SwiftParser
import SwiftParserDiagnostics
import SwiftSyntax

struct SwiftSyntaxExtraction {
    let declarations: [SwiftDeclarationEvidence]
    let imports: [SwiftImportEvidence]
    let calls: [SwiftCallEvidence]
    let constructions: [SwiftConstructionEvidence]
    let relationships: [SwiftRelationshipEvidence]
    let gaps: [CoverageGap]
}

struct SwiftDeclarationEvidence {
    let symbolId: String
    let kind: String
    let name: String
    let displaySignature: String
    let moduleName: String
    let filePath: String
    let startLine: Int
    let endLine: Int
    let startOffset: Int
    let endOffset: Int
    let containingSymbolId: String?
    let parameterLabels: [String]
    let parameterTypeSyntaxes: [String]
    let genericArity: Int
    let isAsync: Bool
    let isThrows: Bool
    let isOverride: Bool
    let syntaxHash: String
    let conditionalCompilation: Bool

    func replacing(symbolId: String, containingSymbolId: String?) -> SwiftDeclarationEvidence {
        SwiftDeclarationEvidence(
            symbolId: symbolId,
            kind: kind,
            name: name,
            displaySignature: displaySignature,
            moduleName: moduleName,
            filePath: filePath,
            startLine: startLine,
            endLine: endLine,
            startOffset: startOffset,
            endOffset: endOffset,
            containingSymbolId: containingSymbolId,
            parameterLabels: parameterLabels,
            parameterTypeSyntaxes: parameterTypeSyntaxes,
            genericArity: genericArity,
            isAsync: isAsync,
            isThrows: isThrows,
            isOverride: isOverride,
            syntaxHash: syntaxHash,
            conditionalCompilation: conditionalCompilation
        )
    }
}

struct SwiftRelationshipEvidence {
    let sourceSymbolId: String
    let sourceSymbolKind: String
    let sourceSymbolDisplayName: String
    let targetSymbolId: String
    let targetSymbolKind: String
    let targetSymbolDisplayName: String
    let relationshipKind: String
    let swiftRelationshipDisplayKind: String
    let filePath: String
    let startLine: Int
    let endLine: Int
    let syntaxHash: String
    let conditionalCompilation: Bool
}

private struct SwiftRelationshipCandidate {
    let sourceSymbolId: String?
    let sourceTypeSyntax: String?
    let targetTypeSyntax: String
    let sourceKind: String
    let contextKind: String
    let filePath: String
    let startLine: Int
    let endLine: Int
    let syntaxHash: String
    let conditionalCompilation: Bool

    func replacingSourceSymbolId(_ replacement: String) -> SwiftRelationshipCandidate {
        SwiftRelationshipCandidate(
            sourceSymbolId: replacement,
            sourceTypeSyntax: sourceTypeSyntax,
            targetTypeSyntax: targetTypeSyntax,
            sourceKind: sourceKind,
            contextKind: contextKind,
            filePath: filePath,
            startLine: startLine,
            endLine: endLine,
            syntaxHash: syntaxHash,
            conditionalCompilation: conditionalCompilation
        )
    }
}

struct SwiftImportEvidence {
    let importedModule: String
    let importKind: String
    let exportedImport: Bool
    let filePath: String
    let startLine: Int
    let endLine: Int
    let syntaxHash: String
    let conditionalCompilation: Bool
}

struct SwiftCallEvidence {
    let callerSymbolId: String?
    let callerDisplayName: String
    let calleeName: String
    let calleeSyntaxKind: String
    let callKind: String
    let argumentLabels: [String]
    let arity: Int
    let filePath: String
    let startLine: Int
    let endLine: Int
    let syntaxHash: String
    let identityDiscriminator: String
    let unsupportedReason: String?
    let conditionalCompilation: Bool
}

struct SwiftConstructionEvidence {
    let callerSymbolId: String?
    let callerDisplayName: String
    let createdTypeSyntax: String
    let argumentLabels: [String]
    let filePath: String
    let startLine: Int
    let endLine: Int
    let syntaxHash: String
    let identityDiscriminator: String
    let conditionalCompilation: Bool
}

enum SwiftSyntaxEvidenceExtractor {
    static func extract(scanRoot: URL, inventory: [InventoryItem]) -> SwiftSyntaxExtraction {
        var declarations: [SwiftDeclarationEvidence] = []
        var imports: [SwiftImportEvidence] = []
        var calls: [SwiftCallEvidence] = []
        var constructions: [SwiftConstructionEvidence] = []
        var relationshipCandidates: [SwiftRelationshipCandidate] = []
        var gaps: [CoverageGap] = []

        for item in inventory where item.selected && item.kind == "swift-source" {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            guard let text = try? String(contentsOf: url, encoding: .utf8) else {
                gaps.append(CoverageGap(kind: "swift-source-unreadable", ruleId: RuleIds.swiftSyntaxAnalysisGap, message: "Swift source file could not be read as UTF-8.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine))
                continue
            }
            let sourceFile = Parser.parse(source: text)
            let converter = SourceLocationConverter(fileName: item.relativePath, tree: sourceFile)
            gaps += parserDiagnosticGaps(sourceFile: sourceFile, converter: converter, item: item)
            let visitor = EvidenceVisitor(filePath: item.relativePath, sourceText: text, converter: converter)
            visitor.walk(sourceFile)
            declarations += visitor.declarations
            imports += visitor.imports
            calls += visitor.calls
            constructions += visitor.constructions
            relationshipCandidates += visitor.relationshipCandidates
            gaps += visitor.gaps

            if moduleName(for: item.relativePath).isEmpty {
                gaps.append(CoverageGap(kind: "swift-module-identity-unknown", ruleId: RuleIds.swiftSyntaxIdentityGap, message: "Swift source file is outside a deterministic Sources/<Module> or Tests/<Module> root; declaration and call identities use an unknown-module sentinel.", filePath: item.relativePath, startLine: 1, endLine: 1))
            }
        }

        let deduplicated = deduplicateDeclarations(declarations)
        declarations = deduplicated.declarations
        relationshipCandidates = rewriteRelationshipCandidates(relationshipCandidates, using: deduplicated.rewrites)
        gaps += deduplicated.gaps

        let resolved = resolveRelationships(declarations: declarations, candidates: relationshipCandidates)
        gaps += resolved.gaps

        return SwiftSyntaxExtraction(
            declarations: declarations.sorted { $0.symbolId < $1.symbolId },
            imports: imports.sorted { [$0.filePath, String($0.startLine), $0.importedModule].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.importedModule].joined(separator: "|") },
            calls: calls.sorted { [$0.filePath, String($0.startLine), $0.syntaxHash].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.syntaxHash].joined(separator: "|") },
            constructions: constructions.sorted { [$0.filePath, String($0.startLine), $0.syntaxHash].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.syntaxHash].joined(separator: "|") },
            relationships: resolved.relationships.sorted { [$0.sourceSymbolId, $0.relationshipKind, $0.targetSymbolId, $0.filePath, String($0.startLine)].joined(separator: "|") < [$1.sourceSymbolId, $1.relationshipKind, $1.targetSymbolId, $1.filePath, String($1.startLine)].joined(separator: "|") },
            gaps: gaps
        )
    }

    private static func parserDiagnosticGaps(sourceFile: SourceFileSyntax, converter: SourceLocationConverter, item: InventoryItem) -> [CoverageGap] {
        guard sourceFile.hasError else { return [] }
        let diagnostics = ParseDiagnosticsGenerator.diagnostics(for: sourceFile)
        if diagnostics.isEmpty {
            return [
                CoverageGap(
                    kind: "SwiftParseDiagnostics",
                    ruleId: RuleIds.swiftSyntaxAnalysisGap,
                    message: "Swift parser reported recoverable syntax diagnostics; diagnosticMessageHash=\(sha256Hex("unknown-swift-parser-diagnostic")); diagnosticId=unknown.",
                    filePath: item.relativePath,
                    startLine: item.startLine,
                    endLine: item.endLine
                )
            ]
        }
        return diagnostics.prefix(20).map { diagnostic in
            let location = diagnostic.location(converter: converter)
            let diagnosticId = safeLabel(String(describing: diagnostic.diagnosticID))
            return CoverageGap(
                kind: "SwiftParseDiagnostics",
                ruleId: RuleIds.swiftSyntaxAnalysisGap,
                message: "Swift parser reported recoverable syntax diagnostics; diagnosticMessageHash=\(sha256Hex(diagnostic.message)); diagnosticId=\(diagnosticId).",
                filePath: item.relativePath,
                startLine: max(1, location.line),
                endLine: max(1, location.line)
            )
        }
    }

    private struct DeclarationRewrite {
        let oldSymbolId: String
        let newSymbolId: String
        let filePath: String
        let startLine: Int
        let endLine: Int
        let kind: String
        let name: String
    }

    private static func deduplicateDeclarations(_ declarations: [SwiftDeclarationEvidence]) -> (declarations: [SwiftDeclarationEvidence], gaps: [CoverageGap], rewrites: [String: [DeclarationRewrite]]) {
        let groups = Dictionary(grouping: declarations, by: \.symbolId)
        var gaps: [CoverageGap] = []
        for (symbolId, group) in groups where group.count > 1 {
            let first = group.sorted { [$0.filePath, String($0.startLine), $0.name].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.name].joined(separator: "|") }.first!
            gaps.append(CoverageGap(kind: "swift-duplicate-symbol-identity", ruleId: RuleIds.swiftSyntaxIdentityGap, message: "Swift declarations produced a duplicate syntax identity; deterministic body-independent discriminators were applied where available. candidateCount=\(group.count); identityHash=\(sha256Hex(symbolId, length: 24)).", filePath: first.filePath, startLine: first.startLine, endLine: first.endLine))
        }
        let duplicateIds = Set(groups.filter { $0.value.count > 1 }.map(\.key))
        guard !duplicateIds.isEmpty else { return (declarations, [], [:]) }
        let duplicateOrdinalSuffixesByLocator: [String: Int] = groups
            .filter { $0.value.count > 1 }
            .flatMap { _, group -> [(String, Int)] in
                group
                    .enumerated()
                    .map { (declaration: $0.element, originalIndex: $0.offset) }
                    .reduce(into: [String: [(declaration: SwiftDeclarationEvidence, originalIndex: Int)]]()) { result, item in
                        result[stableDuplicateDiscriminator(item.declaration), default: []].append(item)
                    }
                    .values
                    .filter { $0.count > 1 }
                    .flatMap { sameStableKey -> [(String, Int)] in
                        sameStableKey
                            .sorted { lhs, rhs in
                                duplicateSourceOrderKey(lhs.declaration, originalIndex: lhs.originalIndex) < duplicateSourceOrderKey(rhs.declaration, originalIndex: rhs.originalIndex)
                            }
                            .enumerated()
                            .map { ordinal, item in (declarationLocator(item.declaration), ordinal) }
                    }
            }
            .reduce(into: [:]) { result, item in result[item.0] = item.1 }

        func replacementSymbolId(for declaration: SwiftDeclarationEvidence) -> String {
            var discriminator = stableDuplicateDiscriminator(declaration)
            if let ordinal = duplicateOrdinalSuffixesByLocator[declarationLocator(declaration)] {
                discriminator += "|ordinal:\(ordinal)"
            }
            return "swift-syntax:v0:" + sha256Hex([declaration.symbolId, discriminator].joined(separator: "\n"))
        }
        var rewrites: [String: [DeclarationRewrite]] = [:]
        let rewritten = declarations.map { declaration -> SwiftDeclarationEvidence in
            let replacement = duplicateIds.contains(declaration.symbolId) ? replacementSymbolId(for: declaration) : declaration.symbolId
            if replacement != declaration.symbolId {
                rewrites[declaration.symbolId, default: []].append(DeclarationRewrite(
                    oldSymbolId: declaration.symbolId,
                    newSymbolId: replacement,
                    filePath: declaration.filePath,
                    startLine: declaration.startLine,
                    endLine: declaration.endLine,
                    kind: declaration.kind,
                    name: declaration.name
                ))
            }
            var containing = declaration.containingSymbolId
            if let containingSymbolId = declaration.containingSymbolId,
               duplicateIds.contains(containingSymbolId),
               let containingDeclaration = groups[containingSymbolId]?.first(where: { candidate in
                   candidate.filePath == declaration.filePath
                       && candidate.startLine <= declaration.startLine
                       && candidate.endLine >= declaration.endLine
               }) {
                containing = replacementSymbolId(for: containingDeclaration)
            }
            return declaration.replacing(symbolId: replacement, containingSymbolId: containing)
        }
        return (rewritten, gaps, rewrites)
    }

    private static func stableDuplicateDiscriminator(_ declaration: SwiftDeclarationEvidence) -> String {
        [
            declaration.filePath,
            declaration.kind,
            declaration.name,
            String(declaration.genericArity),
            declaration.parameterLabels.joined(separator: ","),
            declaration.parameterTypeSyntaxes.joined(separator: ",")
        ].joined(separator: "|")
    }

    private static func duplicateSourceOrderKey(_ declaration: SwiftDeclarationEvidence, originalIndex: Int) -> String {
        [
            declaration.filePath,
            String(declaration.startLine),
            String(declaration.endLine),
            declaration.kind,
            declaration.name,
            String(declaration.genericArity),
            declaration.parameterLabels.joined(separator: ","),
            declaration.parameterTypeSyntaxes.joined(separator: ","),
            String(originalIndex)
        ].joined(separator: "|")
    }

    private static func declarationLocator(_ declaration: SwiftDeclarationEvidence) -> String {
        [
            declaration.symbolId,
            declaration.filePath,
            String(declaration.startLine),
            String(declaration.endLine),
            declaration.kind,
            declaration.name
        ].joined(separator: "|")
    }

    private static func rewriteRelationshipCandidates(_ candidates: [SwiftRelationshipCandidate], using rewrites: [String: [DeclarationRewrite]]) -> [SwiftRelationshipCandidate] {
        guard !rewrites.isEmpty else { return candidates }
        return candidates.map { candidate in
            guard let sourceSymbolId = candidate.sourceSymbolId,
                  let candidates = rewrites[sourceSymbolId] else {
                return candidate
            }
            let matches = candidates.filter { rewrite in
                rewrite.filePath == candidate.filePath
                    && rewrite.startLine == candidate.startLine
                    && rewrite.endLine == candidate.endLine
                    && rewrite.kind == candidate.sourceKind
            }
            guard matches.count == 1, let match = matches.first else {
                return candidate
            }
            return candidate.replacingSourceSymbolId(match.newSymbolId)
        }
    }

    private static func resolveRelationships(declarations: [SwiftDeclarationEvidence], candidates: [SwiftRelationshipCandidate]) -> (relationships: [SwiftRelationshipEvidence], gaps: [CoverageGap]) {
        let typeKinds: Set<String> = ["class", "struct", "enum", "actor", "protocol"]
        let typeDeclarations = declarations.filter { typeKinds.contains($0.kind) }
        let declarationsById = Dictionary(grouping: declarations, by: \.symbolId)
            .compactMapValues { grouped -> SwiftDeclarationEvidence? in
                grouped.count == 1 ? grouped.first : nil
            }
        let typesByModuleAndName = Dictionary(grouping: typeDeclarations) { declaration in
            "\(declaration.moduleName)|\(declaration.name)"
        }
        var relationships: [SwiftRelationshipEvidence] = []
        var gaps: [CoverageGap] = []

        func resolveType(_ rawType: String, module: String, candidate: SwiftRelationshipCandidate) -> [SwiftDeclarationEvidence] {
            let name = normalizedTypeLookupName(rawType)
            guard !name.isEmpty else { return [] }
            let key = "\(module)|\(name)"
            return typesByModuleAndName[key] ?? []
        }

        func resolveTypeInAnyModule(_ rawType: String) -> [SwiftDeclarationEvidence] {
            let name = normalizedTypeLookupName(rawType)
            guard !name.isEmpty else { return [] }
            return typeDeclarations.filter { $0.name == name }
        }

        for candidate in candidates {
            let sourceCandidates: [SwiftDeclarationEvidence]
            if let sourceSymbolId = candidate.sourceSymbolId, let source = declarationsById[sourceSymbolId] {
                sourceCandidates = [source]
            } else if let sourceTypeSyntax = candidate.sourceTypeSyntax {
                let module = moduleName(for: candidate.filePath)
                sourceCandidates = module.isEmpty
                    ? resolveTypeInAnyModule(sourceTypeSyntax)
                    : resolveType(sourceTypeSyntax, module: safeLabel(module), candidate: candidate)
            } else {
                sourceCandidates = []
            }

            guard sourceCandidates.count == 1, let source = sourceCandidates.first else {
                let gapKind = candidate.sourceSymbolId == nil && moduleName(for: candidate.filePath).isEmpty
                    ? "swift-module-identity-unknown"
                    : (sourceCandidates.isEmpty ? "swift-unresolved-external-symbol" : "swift-ambiguous-symbol-identity")
                gaps.append(relationshipGap(kind: gapKind, candidate: candidate, message: "Swift relationship source identity could not be resolved to exactly one source-local symbol.", candidateCount: sourceCandidates.count))
                continue
            }

            let targetCandidates = resolveType(candidate.targetTypeSyntax, module: source.moduleName, candidate: candidate)
            guard targetCandidates.count == 1, let target = targetCandidates.first else {
                gaps.append(relationshipGap(kind: targetCandidates.isEmpty ? "swift-unresolved-external-symbol" : "swift-ambiguous-symbol-identity", candidate: candidate, message: "Swift relationship target identity could not be resolved to exactly one source-local symbol.", candidateCount: targetCandidates.count))
                continue
            }

            guard let relationshipKind = relationshipKind(sourceKind: source.kind, targetKind: target.kind, contextKind: candidate.contextKind) else {
                gaps.append(relationshipGap(kind: "swift-relationship-kind-unsupported", candidate: candidate, message: "Swift relationship syntax was visible, but v0 could not classify it as a canonical source-local relationship kind.", candidateCount: targetCandidates.count))
                continue
            }

            relationships.append(SwiftRelationshipEvidence(
                sourceSymbolId: source.symbolId,
                sourceSymbolKind: source.kind,
                sourceSymbolDisplayName: displayName(for: source),
                targetSymbolId: target.symbolId,
                targetSymbolKind: target.kind,
                targetSymbolDisplayName: displayName(for: target),
                relationshipKind: relationshipKind,
                swiftRelationshipDisplayKind: displayRelationshipKind(relationshipKind),
                filePath: candidate.filePath,
                startLine: candidate.startLine,
                endLine: candidate.endLine,
                syntaxHash: candidate.syntaxHash,
                conditionalCompilation: candidate.conditionalCompilation
            ))
        }

        let inheritanceGroups = Dictionary(grouping: relationships.filter { $0.relationshipKind == "InheritsFrom" }, by: \.sourceSymbolId)
        for (_, relationships) in inheritanceGroups {
            let targets = Set(relationships.map(\.targetSymbolId))
            guard targets.count > 1,
                  let first = relationships.sorted(by: { [$0.filePath, String($0.startLine), $0.targetSymbolId].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.targetSymbolId].joined(separator: "|") }).first else {
                continue
            }
            gaps.append(CoverageGap(kind: "swift-ambiguous-symbol-identity", ruleId: RuleIds.swiftSyntaxIdentityGap, message: "Swift class inheritance resolved to multiple source-local superclass targets; override resolution for that type was skipped. candidateCount=\(targets.count).", filePath: first.filePath, startLine: first.startLine, endLine: first.endLine))
        }

        let inheritanceBySource = inheritanceGroups
            .compactMapValues { relationships -> String? in
                let targets = Set(relationships.map(\.targetSymbolId))
                return targets.count == 1 ? targets.first : nil
            }
        for declaration in declarations where declaration.isOverride {
            guard let containingSymbolId = declaration.containingSymbolId,
                  let superclassSymbolId = inheritanceBySource[containingSymbolId] else {
                gaps.append(CoverageGap(kind: "swift-override-target-unresolved", ruleId: RuleIds.swiftSyntaxIdentityGap, message: "Swift override modifier was visible, but no source-local superclass relationship was available for this containing type.", filePath: declaration.filePath, startLine: declaration.startLine, endLine: declaration.endLine))
                continue
            }
            let matches = declarations.filter { candidate in
                candidate.containingSymbolId == superclassSymbolId
                    && candidate.kind == declaration.kind
                    && candidate.name == declaration.name
                    && candidate.parameterLabels == declaration.parameterLabels
            }
            guard matches.count == 1, let target = matches.first else {
                gaps.append(CoverageGap(kind: "swift-override-target-unresolved", ruleId: RuleIds.swiftSyntaxIdentityGap, message: "Swift override modifier was visible, but the overridden target member could not be resolved to exactly one source-local declaration.", filePath: declaration.filePath, startLine: declaration.startLine, endLine: declaration.endLine))
                continue
            }
            relationships.append(SwiftRelationshipEvidence(
                sourceSymbolId: declaration.symbolId,
                sourceSymbolKind: declaration.kind,
                sourceSymbolDisplayName: displayName(for: declaration),
                targetSymbolId: target.symbolId,
                targetSymbolKind: target.kind,
                targetSymbolDisplayName: displayName(for: target),
                relationshipKind: "Overrides",
                swiftRelationshipDisplayKind: "OverrideCandidate",
                filePath: declaration.filePath,
                startLine: declaration.startLine,
                endLine: declaration.endLine,
                syntaxHash: declaration.syntaxHash,
                conditionalCompilation: declaration.conditionalCompilation
            ))
        }

        return (relationships, gaps)
    }

    private static func relationshipKind(sourceKind: String, targetKind: String, contextKind: String) -> String? {
        if contextKind == "extension-protocol-adoption" {
            return targetKind == "protocol" ? "ImplementsInterface" : nil
        }
        if sourceKind == "class" {
            if targetKind == "class" { return "InheritsFrom" }
            if targetKind == "protocol" { return "ImplementsInterface" }
        }
        if sourceKind == "protocol" {
            return targetKind == "protocol" ? "ExtendsInterface" : nil
        }
        if ["struct", "enum", "actor"].contains(sourceKind) {
            return targetKind == "protocol" ? "ImplementsInterface" : nil
        }
        return nil
    }

    private static func displayRelationshipKind(_ relationshipKind: String) -> String {
        switch relationshipKind {
        case "InheritsFrom": return "ClassInheritance"
        case "ImplementsInterface": return "ProtocolConformance"
        case "ExtendsInterface": return "ProtocolInheritance"
        case "Overrides": return "OverrideCandidate"
        default: return safeLabel(relationshipKind)
        }
    }

    private static func relationshipGap(kind: String, candidate: SwiftRelationshipCandidate, message: String, candidateCount: Int) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.swiftSyntaxIdentityGap, message: "\(message) relationshipKind=\(candidate.contextKind); candidateCount=\(candidateCount); targetHash=\(sha256Hex(candidate.targetTypeSyntax, length: 24)).", filePath: candidate.filePath, startLine: candidate.startLine, endLine: candidate.endLine)
    }
}

final class EvidenceVisitor: SyntaxVisitor {
    let filePath: String
    let sourceText: String
    let converter: SourceLocationConverter
    var declarations: [SwiftDeclarationEvidence] = []
    var imports: [SwiftImportEvidence] = []
    var calls: [SwiftCallEvidence] = []
    var constructions: [SwiftConstructionEvidence] = []
    fileprivate var relationshipCandidates: [SwiftRelationshipCandidate] = []
    var gaps: [CoverageGap] = []
    private var declarationStack: [SwiftDeclarationEvidence] = []
    private var conditionalDepth = 0

    init(filePath: String, sourceText: String, converter: SourceLocationConverter) {
        self.filePath = filePath
        self.sourceText = sourceText
        self.converter = converter
        super.init(viewMode: .sourceAccurate)
    }

    override func visit(_ node: ImportDeclSyntax) -> SyntaxVisitorContinueKind {
        let span = lineSpan(node)
        let module = node.path.trimmedDescription
        let kind = node.importKindSpecifier?.text ?? "module"
        let exported = node.attributes.contains { attribute in
            attribute.trimmedDescription.contains("@_exported")
        }
        imports.append(SwiftImportEvidence(
            importedModule: safeImportPath(module),
            importKind: safeLabel(kind),
            exportedImport: exported,
            filePath: filePath,
            startLine: span.start,
            endLine: span.end,
            syntaxHash: syntaxHash(node),
            conditionalCompilation: conditionalDepth > 0
        ))
        return .skipChildren
    }

    override func visit(_ node: IfConfigDeclSyntax) -> SyntaxVisitorContinueKind {
        let span = lineSpan(node)
        gaps.append(CoverageGap(kind: "ConditionalCompilationAmbiguous", ruleId: RuleIds.swiftSyntaxAnalysisGap, message: "Swift conditional compilation block was parsed without selecting a build configuration; nested evidence is conditional syntax only.", filePath: filePath, startLine: span.start, endLine: span.end))
        if node.trimmedDescription.contains("canImport") {
            gaps.append(CoverageGap(kind: "CanImportConditionalAmbiguous", ruleId: RuleIds.swiftSyntaxAnalysisGap, message: "Swift canImport conditional was parsed without resolving toolchain module availability; nested evidence is conditional syntax only.", filePath: filePath, startLine: span.start, endLine: span.end))
        }
        conditionalDepth += 1
        return .visitChildren
    }

    override func visitPost(_ node: IfConfigDeclSyntax) {
        if conditionalDepth > 0 {
            conditionalDepth -= 1
        }
    }

    override func visit(_ node: ClassDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "class", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        recordInheritanceCandidates(source: declarationStack.last, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), contextKind: "type-inheritance", node: node)
        return .visitChildren
    }

    override func visitPost(_ node: ClassDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: StructDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "struct", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        recordInheritanceCandidates(source: declarationStack.last, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), contextKind: "type-inheritance", node: node)
        return .visitChildren
    }

    override func visitPost(_ node: StructDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ActorDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "actor", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        recordInheritanceCandidates(source: declarationStack.last, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), contextKind: "type-inheritance", node: node)
        return .visitChildren
    }

    override func visitPost(_ node: ActorDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: EnumDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "enum", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        recordInheritanceCandidates(source: declarationStack.last, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), contextKind: "type-inheritance", node: node)
        return .visitChildren
    }

    override func visitPost(_ node: EnumDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ProtocolDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "protocol", name: node.name.text, node: node, genericParameters: [])
        recordInheritanceCandidates(source: declarationStack.last, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), contextKind: "type-inheritance", node: node)
        return .visitChildren
    }

    override func visitPost(_ node: ProtocolDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ExtensionDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "extension", name: safeLabel(node.extendedType.trimmedDescription), node: node, genericParameters: [])
        recordExtensionProtocolCandidates(extendedType: node.extendedType.trimmedDescription, inheritedTypes: inheritedTypeDescriptions(node.inheritanceClause), node: node)
        return .visitChildren
    }

    override func visitPost(_ node: ExtensionDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: FunctionDeclSyntax) -> SyntaxVisitorContinueKind {
        let labels = node.signature.parameterClause.parameters.map { parameter in
            let first = parameter.firstName.text
            return safeLabel(first)
        }
        enterDeclaration(
            kind: "function",
            name: node.name.text,
            node: node,
            parameterLabels: labels,
            parameterTypeSyntaxes: node.signature.parameterClause.parameters.map { safeTypeSyntax($0.type.trimmedDescription) },
            genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [],
            isAsync: node.signature.effectSpecifiers?.asyncSpecifier != nil,
            isThrows: node.signature.effectSpecifiers?.throwsClause != nil,
            isOverride: hasOverrideModifier(node.modifiers)
        )
        return .visitChildren
    }

    override func visitPost(_ node: FunctionDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: InitializerDeclSyntax) -> SyntaxVisitorContinueKind {
        let labels = node.signature.parameterClause.parameters.map { safeLabel($0.firstName.text) }
        enterDeclaration(
            kind: "initializer",
            name: "init",
            node: node,
            parameterLabels: labels,
            parameterTypeSyntaxes: node.signature.parameterClause.parameters.map { safeTypeSyntax($0.type.trimmedDescription) },
            genericParameters: [],
            isAsync: node.signature.effectSpecifiers?.asyncSpecifier != nil,
            isThrows: node.signature.effectSpecifiers?.throwsClause != nil,
            isOverride: hasOverrideModifier(node.modifiers)
        )
        return .visitChildren
    }

    override func visitPost(_ node: InitializerDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: VariableDeclSyntax) -> SyntaxVisitorContinueKind {
        if declarationStack.contains(where: { ["function", "initializer", "subscript"].contains($0.kind) }) {
            return .visitChildren
        }
        for binding in node.bindings {
            guard let pattern = binding.pattern.as(IdentifierPatternSyntax.self) else { continue }
            let declarationNode = node.bindings.count == 1 ? Syntax(node) : Syntax(binding)
            addDeclaration(kind: "property", name: pattern.identifier.text, node: declarationNode, parameterLabels: [], parameterTypeSyntaxes: [], genericParameters: [], isAsync: false, isThrows: false, isOverride: hasOverrideModifier(node.modifiers))
        }
        return .visitChildren
    }

    override func visit(_ node: TypeAliasDeclSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "typealias", name: node.name.text, node: Syntax(node), parameterLabels: [], parameterTypeSyntaxes: [], genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [], isAsync: false, isThrows: false, isOverride: false)
        return .skipChildren
    }

    override func visit(_ node: AssociatedTypeDeclSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "associatedtype", name: node.name.text, node: Syntax(node), parameterLabels: [], parameterTypeSyntaxes: [], genericParameters: [], isAsync: false, isThrows: false, isOverride: false)
        return .skipChildren
    }

    override func visit(_ node: EnumCaseElementSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "enum-case", name: node.name.text, node: Syntax(node), parameterLabels: [], parameterTypeSyntaxes: [], genericParameters: [], isAsync: false, isThrows: false, isOverride: false)
        return .skipChildren
    }

    override func visit(_ node: SubscriptDeclSyntax) -> SyntaxVisitorContinueKind {
        let labels = node.parameterClause.parameters.map { safeLabel($0.firstName.text) }
        addDeclaration(kind: "subscript", name: "subscript", node: Syntax(node), parameterLabels: labels, parameterTypeSyntaxes: node.parameterClause.parameters.map { safeTypeSyntax($0.type.trimmedDescription) }, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [], isAsync: false, isThrows: false, isOverride: hasOverrideModifier(node.modifiers))
        return .visitChildren
    }

    override func visit(_ node: FunctionCallExprSyntax) -> SyntaxVisitorContinueKind {
        let span = lineSpan(node)
        let labels = node.arguments.map { argument in
            safeLabel(argument.label?.text ?? "_")
        }
        let called = calleeDescription(node.calledExpression)
        let unsupported = unsupportedCallReason(node.calledExpression)
        let caller = declarationStack.last
        calls.append(SwiftCallEvidence(
            callerSymbolId: caller?.symbolId,
            callerDisplayName: caller?.displaySignature ?? "",
            calleeName: called.name,
            calleeSyntaxKind: called.kind,
            callKind: called.callKind,
            argumentLabels: labels,
            arity: labels.count,
            filePath: filePath,
            startLine: span.start,
            endLine: span.end,
            syntaxHash: syntaxHash(node),
            identityDiscriminator: syntaxIdentityDiscriminator(node),
            unsupportedReason: unsupported,
            conditionalCompilation: conditionalDepth > 0
        ))
        if called.isConstructionCandidate {
            constructions.append(SwiftConstructionEvidence(
                callerSymbolId: caller?.symbolId,
                callerDisplayName: caller?.displaySignature ?? "",
                createdTypeSyntax: called.name,
                argumentLabels: labels,
                filePath: filePath,
                startLine: span.start,
                endLine: span.end,
                syntaxHash: syntaxHash(node),
                identityDiscriminator: syntaxIdentityDiscriminator(node),
                conditionalCompilation: conditionalDepth > 0
            ))
        }
        if let unsupported {
            gaps.append(CoverageGap(kind: unsupported, ruleId: RuleIds.swiftSyntaxAnalysisGap, message: "Swift call syntax is not resolved by v0 syntax extraction.", filePath: filePath, startLine: span.start, endLine: span.end))
        }
        return .visitChildren
    }

    private func enterDeclaration(kind: String, name: String, node: some SyntaxProtocol, parameterLabels: [String] = [], parameterTypeSyntaxes: [String] = [], genericParameters: [String], isAsync: Bool = false, isThrows: Bool = false, isOverride: Bool = false) {
        let declaration = makeDeclaration(kind: kind, name: name, node: Syntax(node), parameterLabels: parameterLabels, parameterTypeSyntaxes: parameterTypeSyntaxes, genericParameters: genericParameters, isAsync: isAsync, isThrows: isThrows, isOverride: isOverride)
        declarations.append(declaration)
        declarationStack.append(declaration)
    }

    private func exitDeclaration() {
        if !declarationStack.isEmpty {
            declarationStack.removeLast()
        }
    }

    private func addDeclaration(kind: String, name: String, node: Syntax, parameterLabels: [String], parameterTypeSyntaxes: [String], genericParameters: [String], isAsync: Bool, isThrows: Bool, isOverride: Bool) {
        declarations.append(makeDeclaration(kind: kind, name: name, node: node, parameterLabels: parameterLabels, parameterTypeSyntaxes: parameterTypeSyntaxes, genericParameters: genericParameters, isAsync: isAsync, isThrows: isThrows, isOverride: isOverride))
    }

    private func makeDeclaration(kind: String, name: String, node: Syntax, parameterLabels: [String], parameterTypeSyntaxes: [String], genericParameters: [String], isAsync: Bool, isThrows: Bool, isOverride: Bool) -> SwiftDeclarationEvidence {
        let span = lineSpan(node)
        let startOffset = node.positionAfterSkippingLeadingTrivia.utf8Offset
        let endOffset = node.endPositionBeforeTrailingTrivia.utf8Offset
        let rawModule = moduleName(for: filePath)
        let module = rawModule.isEmpty ? "unknown-module" : safeLabel(rawModule)
        let moduleDiscriminator = rawModule.isEmpty ? filePath : ""
        let safeName = safeLabel(name)
        let containing = declarationStack.last
        let safeLabels = parameterLabels.map(safeLabel)
        let safeParameterTypes = parameterTypeSyntaxes.map(safeTypeSyntax)
        let signature = displaySignature(kind: kind, name: safeName, parameterLabels: safeLabels, isAsync: isAsync, isThrows: isThrows)
        let hash = syntaxHash(node)
        let identity = [
            "swift-syntax/v0",
            module,
            moduleDiscriminator,
            containing?.symbolId ?? "",
            kind,
            safeName,
            String(genericParameters.count),
            safeLabels.joined(separator: ",")
        ].joined(separator: "\n")
        return SwiftDeclarationEvidence(
            symbolId: "swift-syntax:v0:" + sha256Hex(identity),
            kind: kind,
            name: safeName,
            displaySignature: signature,
            moduleName: safeLabel(module),
            filePath: filePath,
            startLine: span.start,
            endLine: span.end,
            startOffset: startOffset,
            endOffset: endOffset,
            containingSymbolId: containing?.symbolId,
            parameterLabels: safeLabels,
            parameterTypeSyntaxes: safeParameterTypes,
            genericArity: genericParameters.count,
            isAsync: isAsync,
            isThrows: isThrows,
            isOverride: isOverride,
            syntaxHash: hash,
            conditionalCompilation: conditionalDepth > 0
        )
    }

    private func recordInheritanceCandidates(source: SwiftDeclarationEvidence?, inheritedTypes: [String], contextKind: String, node: some SyntaxProtocol) {
        guard let source else { return }
        let span = lineSpan(node)
        for inheritedType in inheritedTypes {
            relationshipCandidates.append(SwiftRelationshipCandidate(
                sourceSymbolId: source.symbolId,
                sourceTypeSyntax: nil,
                targetTypeSyntax: inheritedType,
                sourceKind: source.kind,
                contextKind: contextKind,
                filePath: filePath,
                startLine: span.start,
                endLine: span.end,
                syntaxHash: syntaxHash(node),
                conditionalCompilation: conditionalDepth > 0
            ))
        }
    }

    private func recordExtensionProtocolCandidates(extendedType: String, inheritedTypes: [String], node: some SyntaxProtocol) {
        let span = lineSpan(node)
        guard !inheritedTypes.isEmpty else { return }
        for inheritedType in inheritedTypes {
            relationshipCandidates.append(SwiftRelationshipCandidate(
                sourceSymbolId: nil,
                sourceTypeSyntax: extendedType,
                targetTypeSyntax: inheritedType,
                sourceKind: "extension",
                contextKind: "extension-protocol-adoption",
                filePath: filePath,
                startLine: span.start,
                endLine: span.end,
                syntaxHash: syntaxHash(node),
                conditionalCompilation: conditionalDepth > 0
            ))
        }
    }

    private func lineSpan(_ node: some SyntaxProtocol) -> (start: Int, end: Int) {
        let start = node.startLocation(converter: converter, afterLeadingTrivia: true).line
        let end = node.endLocation(converter: converter, afterTrailingTrivia: false).line
        return (max(1, start), max(max(1, start), end))
    }

    private func syntaxHash(_ node: some SyntaxProtocol) -> String {
        sha256Hex(normalizeSwiftSyntaxForHash(node.trimmedDescription))
    }

    private func syntaxIdentityDiscriminator(_ node: some SyntaxProtocol) -> String {
        String(node.positionAfterSkippingLeadingTrivia.utf8Offset)
    }
}

private func inheritedTypeDescriptions(_ clause: InheritanceClauseSyntax?) -> [String] {
    clause?.inheritedTypes.map { $0.type.trimmedDescription } ?? []
}

private func hasOverrideModifier(_ modifiers: DeclModifierListSyntax) -> Bool {
    modifiers.contains { $0.name.text == "override" }
}

private func normalizedTypeLookupName(_ value: String) -> String {
    var normalized = value.trimmed()
    normalized = normalized.replacingOccurrences(of: #"<.*>"#, with: "", options: .regularExpression)
    normalized = normalized.replacingOccurrences(of: "?", with: "")
    normalized = normalized.replacingOccurrences(of: "!", with: "")
    guard normalized.range(of: #"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)?$"#, options: .regularExpression) != nil else {
        return ""
    }
    guard !normalized.contains(".") else { return "" }
    return safeLabel(normalized)
}

private func displayName(for declaration: SwiftDeclarationEvidence) -> String {
    let module = declaration.moduleName.isEmpty ? "unknown-module" : declaration.moduleName
    return "\(module).\(declaration.displaySignature)"
}

private struct CalleeDescription {
    let name: String
    let kind: String
    let callKind: String
    let isConstructionCandidate: Bool
}

private func calleeDescription(_ expression: ExprSyntax) -> CalleeDescription {
    if let declRef = expression.as(DeclReferenceExprSyntax.self) {
        let name = safeLabel(declRef.baseName.text)
        return CalleeDescription(name: name, kind: "identifier", callKind: "simple-call", isConstructionCandidate: startsLikeTypeName(name))
    }
    if let member = expression.as(MemberAccessExprSyntax.self) {
        let base = member.base?.trimmedDescription ?? ""
        let name = safeMemberPath(base: base, member: member.declName.baseName.text)
        let isInit = member.declName.baseName.text == "init"
        return CalleeDescription(name: name, kind: "member-access", callKind: isInit ? "initializer-call" : "member-call", isConstructionCandidate: isInit)
    }
    if let optional = expression.as(OptionalChainingExprSyntax.self) {
        let inner = calleeDescription(optional.expression)
        return CalleeDescription(name: inner.name, kind: "optional-chaining", callKind: "optional-chain-call", isConstructionCandidate: false)
    }
    let raw = expression.trimmedDescription
    return CalleeDescription(name: "sha256:\(sha256Hex(raw, length: 24))", kind: "unsupported-expression", callKind: "unsupported-call", isConstructionCandidate: false)
}

private func unsupportedCallReason(_ expression: ExprSyntax) -> String? {
    if expression.is(OptionalChainingExprSyntax.self) || expression.trimmedDescription.contains("?.") {
        return "swift-call-optional-chaining-unresolved"
    }
    if expression.is(DeclReferenceExprSyntax.self) || expression.is(MemberAccessExprSyntax.self) {
        return nil
    }
    return "swift-call-unsupported-shape"
}

private func displaySignature(kind: String, name: String, parameterLabels: [String], isAsync: Bool, isThrows: Bool) -> String {
    var value = "\(kind) \(name)"
    if kind == "function" || kind == "initializer" || kind == "subscript" {
        value += "(\(parameterLabels.joined(separator: ",")))"
    }
    if isAsync { value += " async" }
    if isThrows { value += " throws" }
    return value
}

private func moduleName(for path: String) -> String {
    let parts = path.split(separator: "/").map(String.init)
    guard parts.count >= 3 else { return "" }
    if parts[0] == "Sources" || parts[0] == "Tests" {
        return safeLabel(parts[1])
    }
    return ""
}

private func safeImportPath(_ value: String) -> String {
    value.split(separator: ".").map { safeLabel(String($0)) }.joined(separator: ".")
}

private func safeTypeSyntax(_ value: String) -> String {
    let normalized = normalizeSwiftSyntaxForHash(value)
    guard !normalized.isEmpty else { return "" }
    return "sha256:\(sha256Hex(normalized, length: 24))"
}

private func safeMemberPath(base: String, member: String) -> String {
    let safeMember = safeLabel(member)
    guard !base.isEmpty else { return safeMember }
    let safeBase = base
        .split(separator: ".")
        .map { safeLabel(String($0)) }
        .joined(separator: ".")
    return safeBase.isEmpty ? safeMember : "\(safeBase).\(safeMember)"
}

private func startsLikeTypeName(_ value: String) -> Bool {
    value.first.map { String($0).range(of: #"[A-Z]"#, options: .regularExpression) != nil } ?? false
}

private func normalizeSwiftSyntaxForHash(_ text: String) -> String {
    var normalized = stripSwiftCommentsAndStringLiterals(text)
    normalized = normalized.replacingOccurrences(of: #"\b[0-9]+(?:\.[0-9]+)?\b"#, with: "<number>", options: .regularExpression)
    normalized = normalized.replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
    return normalized.trimmed()
}
