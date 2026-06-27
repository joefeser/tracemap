import Foundation
import SwiftParser
import SwiftParserDiagnostics
import SwiftSyntax

struct SwiftSyntaxExtraction {
    let declarations: [SwiftDeclarationEvidence]
    let imports: [SwiftImportEvidence]
    let calls: [SwiftCallEvidence]
    let constructions: [SwiftConstructionEvidence]
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
    let containingSymbolId: String?
    let parameterLabels: [String]
    let genericArity: Int
    let isAsync: Bool
    let isThrows: Bool
    let syntaxHash: String
    let conditionalCompilation: Bool
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
            gaps += visitor.gaps

            if moduleName(for: item.relativePath).isEmpty {
                gaps.append(CoverageGap(kind: "swift-module-context-unavailable", ruleId: RuleIds.swiftSyntaxAnalysisGap, message: "Swift source file is outside a deterministic Sources/<Module> or Tests/<Module> root; declaration and call identities remain file scoped.", filePath: item.relativePath, startLine: 1, endLine: 1))
            }
        }

        return SwiftSyntaxExtraction(
            declarations: declarations.sorted { $0.symbolId < $1.symbolId },
            imports: imports.sorted { [$0.filePath, String($0.startLine), $0.importedModule].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.importedModule].joined(separator: "|") },
            calls: calls.sorted { [$0.filePath, String($0.startLine), $0.syntaxHash].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.syntaxHash].joined(separator: "|") },
            constructions: constructions.sorted { [$0.filePath, String($0.startLine), $0.syntaxHash].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.syntaxHash].joined(separator: "|") },
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
}

final class EvidenceVisitor: SyntaxVisitor {
    let filePath: String
    let sourceText: String
    let converter: SourceLocationConverter
    var declarations: [SwiftDeclarationEvidence] = []
    var imports: [SwiftImportEvidence] = []
    var calls: [SwiftCallEvidence] = []
    var constructions: [SwiftConstructionEvidence] = []
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
        return .visitChildren
    }

    override func visitPost(_ node: ClassDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: StructDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "struct", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        return .visitChildren
    }

    override func visitPost(_ node: StructDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ActorDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "actor", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        return .visitChildren
    }

    override func visitPost(_ node: ActorDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: EnumDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "enum", name: node.name.text, node: node, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [])
        return .visitChildren
    }

    override func visitPost(_ node: EnumDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ProtocolDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "protocol", name: node.name.text, node: node, genericParameters: [])
        return .visitChildren
    }

    override func visitPost(_ node: ProtocolDeclSyntax) {
        exitDeclaration()
    }

    override func visit(_ node: ExtensionDeclSyntax) -> SyntaxVisitorContinueKind {
        enterDeclaration(kind: "extension", name: safeLabel(node.extendedType.trimmedDescription), node: node, genericParameters: [])
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
            genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [],
            isAsync: node.signature.effectSpecifiers?.asyncSpecifier != nil,
            isThrows: node.signature.effectSpecifiers?.throwsClause != nil
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
            genericParameters: [],
            isAsync: node.signature.effectSpecifiers?.asyncSpecifier != nil,
            isThrows: node.signature.effectSpecifiers?.throwsClause != nil
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
            addDeclaration(kind: "property", name: pattern.identifier.text, node: Syntax(binding), parameterLabels: [], genericParameters: [], isAsync: false, isThrows: false)
        }
        return .visitChildren
    }

    override func visit(_ node: TypeAliasDeclSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "typealias", name: node.name.text, node: Syntax(node), parameterLabels: [], genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [], isAsync: false, isThrows: false)
        return .skipChildren
    }

    override func visit(_ node: AssociatedTypeDeclSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "associatedtype", name: node.name.text, node: Syntax(node), parameterLabels: [], genericParameters: [], isAsync: false, isThrows: false)
        return .skipChildren
    }

    override func visit(_ node: EnumCaseElementSyntax) -> SyntaxVisitorContinueKind {
        addDeclaration(kind: "enum-case", name: node.name.text, node: Syntax(node), parameterLabels: [], genericParameters: [], isAsync: false, isThrows: false)
        return .skipChildren
    }

    override func visit(_ node: SubscriptDeclSyntax) -> SyntaxVisitorContinueKind {
        let labels = node.parameterClause.parameters.map { safeLabel($0.firstName.text) }
        addDeclaration(kind: "subscript", name: "subscript", node: Syntax(node), parameterLabels: labels, genericParameters: node.genericParameterClause?.parameters.map(\.name.text) ?? [], isAsync: false, isThrows: false)
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

    private func enterDeclaration(kind: String, name: String, node: some SyntaxProtocol, parameterLabels: [String] = [], genericParameters: [String], isAsync: Bool = false, isThrows: Bool = false) {
        let declaration = makeDeclaration(kind: kind, name: name, node: Syntax(node), parameterLabels: parameterLabels, genericParameters: genericParameters, isAsync: isAsync, isThrows: isThrows)
        declarations.append(declaration)
        declarationStack.append(declaration)
    }

    private func exitDeclaration() {
        if !declarationStack.isEmpty {
            declarationStack.removeLast()
        }
    }

    private func addDeclaration(kind: String, name: String, node: Syntax, parameterLabels: [String], genericParameters: [String], isAsync: Bool, isThrows: Bool) {
        declarations.append(makeDeclaration(kind: kind, name: name, node: node, parameterLabels: parameterLabels, genericParameters: genericParameters, isAsync: isAsync, isThrows: isThrows))
    }

    private func makeDeclaration(kind: String, name: String, node: Syntax, parameterLabels: [String], genericParameters: [String], isAsync: Bool, isThrows: Bool) -> SwiftDeclarationEvidence {
        let span = lineSpan(node)
        let module = moduleName(for: filePath)
        let safeName = safeLabel(name)
        let containing = declarationStack.last
        let safeLabels = parameterLabels.map(safeLabel)
        let signature = displaySignature(kind: kind, name: safeName, parameterLabels: safeLabels, isAsync: isAsync, isThrows: isThrows)
        let hash = syntaxHash(node)
        let identity = [
            "swift-syntax/v0",
            module,
            filePath,
            containing?.symbolId ?? "",
            kind,
            safeName,
            String(genericParameters.count),
            safeLabels.joined(separator: ","),
            hash
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
            containingSymbolId: containing?.symbolId,
            parameterLabels: safeLabels,
            genericArity: genericParameters.count,
            isAsync: isAsync,
            isThrows: isThrows,
            syntaxHash: hash,
            conditionalCompilation: conditionalDepth > 0
        )
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
