import CryptoKit
import Foundation

public enum TraceMapSwiftVersion {
    public static let scanner = "tracemap-swift/0.1.0"
    public static let extractorId = "swift.scaffold"
    public static let extractorVersion = "0.1.0"
    public static let scanIdPrefix = "swift-scan/v1"
    public static let defaultMaxFileByteSize = 1_048_576
}

public enum TraceMapSwiftCLI {
    public static let usage = """
    Usage:
      tracemap-swift scan --repo <path> --out <path> [options]
      tracemap-swift --version
      tracemap-swift --help

    Options:
      --repo <path>                 Git-backed repository or scan root.
      --out <path>                  Output directory to rebuild.
      --project <path>              Repeatable project or sub-scope filter.
      --include <glob>              Repeatable include glob.
      --exclude <glob>              Repeatable exclude glob.
      --max-file-byte-size <bytes>  Maximum selected file size. Default: 1048576.
      --version                     Print deterministic adapter version.
      --help                        Print this help.
    """

    public static func run(arguments: [String]) -> Int {
        do {
            if arguments.isEmpty || arguments.contains("--help") || arguments.contains("-h") {
                print(usage)
                return 0
            }
            if arguments == ["--version"] || arguments == ["version"] {
                print(TraceMapSwiftVersion.scanner)
                return 0
            }
            guard arguments.first == "scan" else {
                throw ScanError.invalidArguments("expected `scan`, `--help`, or `--version`")
            }
            let options = try parseScanOptions(Array(arguments.dropFirst()))
            let result = try SwiftScanEngine.scan(options: options)
            print("TraceMap Swift scan complete: \(result.facts.count) facts, \(result.manifest.analysisLevel), \(result.manifest.buildStatus)")
            return 0
        } catch {
            fputs("tracemap-swift: \(error.localizedDescription)\n", stderr)
            return 1
        }
    }

    public static func parseScanOptions(_ arguments: [String]) throws -> SwiftScanOptions {
        var repo: String?
        var out: String?
        var projects: [String] = []
        var includes: [String] = []
        var excludes: [String] = []
        var maxFileByteSize = TraceMapSwiftVersion.defaultMaxFileByteSize
        var index = 0
        while index < arguments.count {
            let arg = arguments[index]
            func nextValue() throws -> String {
                guard index + 1 < arguments.count else {
                    throw ScanError.invalidArguments("missing value for \(arg)")
                }
                index += 1
                return arguments[index]
            }
            switch arg {
            case "--repo":
                repo = try nextValue()
            case "--out":
                out = try nextValue()
            case "--project":
                projects.append(try nextValue())
            case "--include":
                includes.append(try nextValue())
            case "--exclude":
                excludes.append(try nextValue())
            case "--max-file-byte-size":
                let raw = try nextValue()
                guard let parsed = Int(raw), parsed > 0 else {
                    throw ScanError.invalidArguments("--max-file-byte-size must be a positive integer")
                }
                maxFileByteSize = parsed
            case "--version":
                throw ScanError.invalidArguments("use `tracemap-swift --version`")
            default:
                throw ScanError.invalidArguments("unknown option \(arg)")
            }
            index += 1
        }
        guard let repo else {
            throw ScanError.invalidArguments("scan requires --repo <path>")
        }
        guard let out else {
            throw ScanError.invalidArguments("scan requires --out <path>")
        }
        return SwiftScanOptions(
            repoPath: URL(fileURLWithPath: repo),
            outputPath: URL(fileURLWithPath: out),
            projectFilters: projects,
            includeGlobs: includes,
            excludeGlobs: excludes,
            maxFileByteSize: maxFileByteSize
        )
    }
}

public struct SwiftScanOptions: Equatable {
    public let repoPath: URL
    public let outputPath: URL
    public let projectFilters: [String]
    public let includeGlobs: [String]
    public let excludeGlobs: [String]
    public let maxFileByteSize: Int

    public init(
        repoPath: URL,
        outputPath: URL,
        projectFilters: [String] = [],
        includeGlobs: [String] = [],
        excludeGlobs: [String] = [],
        maxFileByteSize: Int = TraceMapSwiftVersion.defaultMaxFileByteSize
    ) {
        self.repoPath = repoPath
        self.outputPath = outputPath
        self.projectFilters = projectFilters
        self.includeGlobs = includeGlobs
        self.excludeGlobs = excludeGlobs
        self.maxFileByteSize = maxFileByteSize
    }
}

public struct SwiftScanResult {
    public let manifest: ScanManifest
    public let facts: [CodeFact]
    public let inventory: [InventoryItem]
}

public enum SwiftScanEngine {
    public static func scan(options: SwiftScanOptions) throws -> SwiftScanResult {
        let repo = options.repoPath.standardizedFileURL
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: repo.path, isDirectory: &isDirectory), isDirectory.boolValue else {
            throw ScanError.invalidArguments("repo path does not exist: \(repo.path)")
        }
        let git = try GitMetadata.load(scanRoot: repo)
        let inventory = try InventoryBuilder.build(scanRoot: repo, gitRoot: git.gitRoot, options: options)
        let scanId = stableScanId(git: git, options: options, inventory: inventory)
        var gaps = CoverageGap.defaults(inventory: inventory)
        if !Toolchain.swiftAvailable() {
            gaps.append(CoverageGap(kind: "swift-toolchain-unavailable", ruleId: RuleIds.toolchainUnavailable, message: "Swift toolchain unavailable; emitted scaffold inventory only."))
        }
        if inventory.contains(where: { $0.kind == "swiftpm-manifest" }) {
            gaps.append(CoverageGap(kind: "swiftpm-load-deferred", ruleId: RuleIds.projectLoadFailed, message: "SwiftPM semantic package loading is deferred in the scaffold slice."))
        }
        if inventory.contains(where: { $0.kind == "xcode-project" || $0.kind == "xcode-workspace" }) {
            gaps.append(CoverageGap(kind: "xcode-load-deferred", ruleId: RuleIds.projectLoadFailed, message: "Xcode project/workspace semantic loading is deferred in the scaffold slice."))
        }
        gaps.append(CoverageGap(kind: "swift-semantic-extractor-deferred", ruleId: RuleIds.dynamicBoundary, message: "SwiftSyntax, SourceKit, protocol dispatch, Objective-C bridging, UI, storage, and runtime analysis are out of scope for this scaffold."))

        let manifest = ScanManifest(
            scanId: scanId,
            repoName: git.repoName,
            remoteUrl: git.remoteUrl,
            branch: git.branch,
            commitSha: git.commitSha,
            scannerVersion: TraceMapSwiftVersion.scanner,
            scannedAt: isoNow(),
            analysisLevel: inventory.hasOnlyTextualEvidence ? "Level3SyntaxAnalysis" : "Level1SemanticAnalysisReduced",
            buildStatus: "FailedOrPartial",
            solutions: [],
            projects: inventory.projectIdentifiers,
            targetFrameworks: [],
            knownGaps: gaps.map(\.message).sorted(),
            scanRootRelativePath: relativePath(from: git.gitRoot, to: repo),
            scanRootPathHash: sha256Hex(repo.path),
            gitRootHash: sha256Hex(git.gitRoot.path),
            extractorVersions: [TraceMapSwiftVersion.extractorId: TraceMapSwiftVersion.extractorVersion]
        )

        let facts = FactFactory.facts(manifest: manifest, inventory: inventory, gaps: gaps)
        try OutputWriter.write(outputPath: options.outputPath, manifest: manifest, facts: facts, inventory: inventory)
        return SwiftScanResult(manifest: manifest, facts: facts, inventory: inventory)
    }

    private static func stableScanId(git: GitMetadata, options: SwiftScanOptions, inventory: [InventoryItem]) -> String {
        let optionSignature = [
            "project=\(options.projectFilters.sorted().joined(separator: ","))",
            "include=\(options.includeGlobs.sorted().joined(separator: ","))",
            "exclude=\(options.excludeGlobs.sorted().joined(separator: ","))",
            "max=\(options.maxFileByteSize)"
        ].joined(separator: ";")
        let inventorySignature = inventory
            .map { "\($0.relativePath)|\($0.kind)|\($0.sizeBytes)|\($0.skippedReason ?? "selected")" }
            .sorted()
            .joined(separator: "\n")
        return "swift-" + sha256Hex([
            TraceMapSwiftVersion.scanIdPrefix,
            git.repoIdentity,
            git.commitSha,
            optionSignature,
            inventorySignature
        ].joined(separator: "\n"), length: 32)
    }
}

public enum ScanError: LocalizedError {
    case invalidArguments(String)
    case gitMetadata(String)
    case io(String)

    public var errorDescription: String? {
        switch self {
        case .invalidArguments(let message), .gitMetadata(let message), .io(let message):
            return message
        }
    }
}

public struct GitMetadata {
    public let repoName: String
    public let remoteUrl: String?
    public let branch: String?
    public let commitSha: String
    public let gitRoot: URL

    var repoIdentity: String {
        remoteUrl?.isEmpty == false ? remoteUrl! : repoName
    }

    static func load(scanRoot: URL) throws -> GitMetadata {
        let root = try runGit(scanRoot, ["rev-parse", "--show-toplevel"]).trimmed()
        let commit = try runGit(scanRoot, ["rev-parse", "HEAD"]).trimmed()
        guard commit.range(of: #"^[0-9a-fA-F]{40}$"#, options: .regularExpression) != nil else {
            throw ScanError.gitMetadata("unable to resolve concrete git commit SHA")
        }
        let branch = try? runGit(scanRoot, ["rev-parse", "--abbrev-ref", "HEAD"]).trimmed()
        let remote = try? runGit(scanRoot, ["config", "--get", "remote.origin.url"]).trimmed()
        let gitRoot = URL(fileURLWithPath: root).standardizedFileURL
        let repoName = gitRoot.lastPathComponent.replacingOccurrences(of: ".git", with: "")
        return GitMetadata(repoName: repoName, remoteUrl: remote?.nilIfEmpty, branch: branch?.nilIfEmpty, commitSha: commit.lowercased(), gitRoot: gitRoot)
    }

    private static func runGit(_ repo: URL, _ args: [String]) throws -> String {
        try runProcess(executable: "/usr/bin/git", arguments: ["-C", repo.path] + args)
    }
}

public struct InventoryItem: Codable, Equatable {
    public let relativePath: String
    public let kind: String
    public let sizeBytes: Int
    public let startLine: Int
    public let endLine: Int
    public let skippedReason: String?

    var selected: Bool { skippedReason == nil }
}

enum InventoryBuilder {
    static let excludedSegmentNames: Set<String> = [
        ".git", ".build", ".swiftpm", ".tracemap-demo", ".tmp", "DerivedData"
    ]

    static func build(scanRoot: URL, gitRoot: URL, options: SwiftScanOptions) throws -> [InventoryItem] {
        let keys: [URLResourceKey] = [.isDirectoryKey, .fileSizeKey, .isRegularFileKey]
        guard let enumerator = FileManager.default.enumerator(
            at: scanRoot,
            includingPropertiesForKeys: keys,
            options: [.skipsHiddenFiles],
            errorHandler: nil
        ) else {
            throw ScanError.io("unable to enumerate repo path")
        }

        var items: [InventoryItem] = []
        for case let url as URL in enumerator {
            let rel = relativePath(from: scanRoot, to: url)
            if rel == "." { continue }
            let segments = rel.split(separator: "/").map(String.init)
            if isDefaultExcluded(segments: segments) {
                if (try? url.resourceValues(forKeys: [.isDirectoryKey]).isDirectory) == true {
                    enumerator.skipDescendants()
                }
                continue
            }
            let values = try url.resourceValues(forKeys: Set(keys))
            let isDirectory = values.isDirectory == true
            if isDirectory, isSupportedBundle(rel) {
                items.append(InventoryItem(relativePath: rel, kind: kind(for: rel, isDirectory: true), sizeBytes: 0, startLine: 1, endLine: 1, skippedReason: nil))
                continue
            }
            guard values.isRegularFile == true else { continue }
            guard isSupportedFile(rel) else { continue }
            guard matchesProjectFilters(rel, options.projectFilters),
                  matchesIncludes(rel, options.includeGlobs),
                  !matchesAnyGlob(rel, options.excludeGlobs) else { continue }
            let size = values.fileSize ?? 0
            let reason = size > options.maxFileByteSize ? "file-too-large" : nil
            let lines = reason == nil ? lineCount(url) : 1
            items.append(InventoryItem(relativePath: rel, kind: kind(for: rel, isDirectory: false), sizeBytes: size, startLine: 1, endLine: max(1, lines), skippedReason: reason))
        }
        return items.sorted { left, right in
            if left.relativePath != right.relativePath { return left.relativePath < right.relativePath }
            return left.kind < right.kind
        }
    }

    static func isDefaultExcluded(segments: [String]) -> Bool {
        if segments.contains(where: { excludedSegmentNames.contains($0) }) { return true }
        for index in segments.indices {
            let pair = segments[index...min(index + 1, segments.count - 1)].joined(separator: "/")
            if pair == "Carthage/Build" || pair == "Carthage/Checkouts" || pair == "Pods/.build" || pair == "Pods/Build" {
                return true
            }
        }
        return false
    }

    static func isSupportedBundle(_ relativePath: String) -> Bool {
        relativePath.hasSuffix(".xcodeproj") || relativePath.hasSuffix(".xcworkspace") || relativePath.hasSuffix(".xcdatamodeld")
    }

    static func isSupportedFile(_ relativePath: String) -> Bool {
        let name = URL(fileURLWithPath: relativePath).lastPathComponent
        if ["Package.swift", "Package.resolved", "Podfile", "Podfile.lock", "Cartfile", "Cartfile.resolved", "Info.plist", "PrivacyInfo.xcprivacy", "project.pbxproj", "contents.xcworkspacedata"].contains(name) {
            return true
        }
        return [".swift", ".entitlements", ".xcdatamodel", ".storyboard", ".xib"].contains { relativePath.hasSuffix($0) }
    }

    static func kind(for relativePath: String, isDirectory: Bool) -> String {
        let name = URL(fileURLWithPath: relativePath).lastPathComponent
        if isDirectory && relativePath.hasSuffix(".xcodeproj") { return "xcode-project" }
        if isDirectory && relativePath.hasSuffix(".xcworkspace") { return "xcode-workspace" }
        if isDirectory && relativePath.hasSuffix(".xcdatamodeld") { return "coredata-model-bundle" }
        switch name {
        case "Package.swift": return "swiftpm-manifest"
        case "Package.resolved": return "swiftpm-resolved"
        case "Podfile", "Podfile.lock": return "cocoapods-metadata"
        case "Cartfile", "Cartfile.resolved": return "carthage-metadata"
        case "Info.plist": return "plist"
        case "PrivacyInfo.xcprivacy": return "privacy-manifest"
        case "project.pbxproj": return "xcode-project-metadata"
        case "contents.xcworkspacedata": return "xcode-workspace-metadata"
        default:
            if relativePath.hasSuffix(".swift") { return "swift-source" }
            if relativePath.hasSuffix(".storyboard") { return "storyboard" }
            if relativePath.hasSuffix(".xib") { return "xib" }
            if relativePath.hasSuffix(".entitlements") { return "entitlements" }
            if relativePath.hasSuffix(".xcdatamodel") { return "coredata-model" }
            return "metadata"
        }
    }

    static func matchesProjectFilters(_ relativePath: String, _ filters: [String]) -> Bool {
        guard !filters.isEmpty else { return true }
        return filters.map(normalizeRelativePath).contains { filter in
            relativePath == filter || relativePath.hasPrefix(filter + "/")
        }
    }

    static func matchesIncludes(_ relativePath: String, _ includes: [String]) -> Bool {
        includes.isEmpty || matchesAnyGlob(relativePath, includes)
    }

    static func matchesAnyGlob(_ relativePath: String, _ globs: [String]) -> Bool {
        globs.contains { globMatches(pattern: $0, value: relativePath) }
    }
}

extension Array where Element == InventoryItem {
    var hasOnlyTextualEvidence: Bool {
        allSatisfy { $0.kind == "swift-source" || $0.skippedReason != nil }
    }

    var projectIdentifiers: [String] {
        filter { ["swiftpm-manifest", "xcode-project", "xcode-workspace"].contains($0.kind) }
            .map(\.relativePath)
            .sorted()
    }
}

struct CoverageGap: Codable, Equatable {
    let kind: String
    let ruleId: String
    let message: String
    let filePath: String
    let startLine: Int
    let endLine: Int

    init(kind: String, ruleId: String, message: String, filePath: String = "scan-manifest.json", startLine: Int = 1, endLine: Int = 1) {
        self.kind = kind
        self.ruleId = ruleId
        self.message = message
        self.filePath = filePath
        self.startLine = startLine
        self.endLine = endLine
    }

    static func defaults(inventory: [InventoryItem]) -> [CoverageGap] {
        var gaps = inventory.compactMap { item -> CoverageGap? in
            guard let reason = item.skippedReason else { return nil }
            return CoverageGap(kind: reason, ruleId: RuleIds.projectLoadFailed, message: "Skipped \(item.relativePath): \(reason).", filePath: item.relativePath, startLine: 1, endLine: 1)
        }
        if inventory.isEmpty {
            gaps.append(CoverageGap(kind: "no-supported-swift-inputs", ruleId: RuleIds.projectLoadFailed, message: "No supported Swift files or project metadata were selected."))
        }
        return gaps
    }
}

enum RuleIds {
    static let repoManifest = "swift.repo.manifest.v1"
    static let fileInventory = "swift.file.inventory.v1"
    static let swiftPM = "swift.package.swiftpm.v1"
    static let cocoaPods = "swift.package.cocoapods.v1"
    static let carthage = "swift.package.carthage.v1"
    static let xcode = "swift.project.xcode.v1"
    static let toolchainUnavailable = "swift.toolchain.unavailable.v1"
    static let projectLoadFailed = "swift.project.load-failed.v1"
    static let dynamicBoundary = "swift.unsupported.dynamic-boundary.v1"
}

public struct ScanManifest: Codable, Equatable {
    public let scanId: String
    public let repoName: String
    public let remoteUrl: String?
    public let branch: String?
    public let commitSha: String
    public let scannerVersion: String
    public let scannedAt: String
    public let analysisLevel: String
    public let buildStatus: String
    public let solutions: [String]
    public let projects: [String]
    public let targetFrameworks: [String]
    public let knownGaps: [String]
    public let scanRootRelativePath: String?
    public let scanRootPathHash: String?
    public let gitRootHash: String?
    public let extractorVersions: [String: String]
}

public struct EvidenceSpan: Codable, Equatable {
    public let filePath: String
    public let startLine: Int
    public let endLine: Int
    public let snippetHash: String?
    public let extractorId: String
    public let extractorVersion: String
}

public struct CodeFact: Codable, Equatable {
    public let factId: String
    public let scanId: String
    public let repo: String
    public let commitSha: String
    public let projectPath: String?
    public let factType: String
    public let ruleId: String
    public let evidenceTier: String
    public let sourceSymbol: String?
    public let targetSymbol: String?
    public let contractElement: String?
    public let evidence: EvidenceSpan
    public let properties: [String: String]
}

enum FactFactory {
    static func facts(manifest: ScanManifest, inventory: [InventoryItem], gaps: [CoverageGap]) -> [CodeFact] {
        var facts: [CodeFact] = []
        facts.append(makeFact(
            manifest: manifest,
            factType: "FileInventoried",
            ruleId: RuleIds.repoManifest,
            evidenceTier: "Tier2Structural",
            filePath: "scan-manifest.json",
            startLine: 1,
            endLine: 1,
            targetSymbol: manifest.repoName,
            properties: [
                "language": "swift",
                "adapterKind": "scaffold",
                "repoNameHash": sha256Hex(manifest.repoName),
                "scanRootRelativePath": manifest.scanRootRelativePath ?? "."
            ]
        ))
        for item in inventory where item.selected {
            facts.append(makeFact(
                manifest: manifest,
                factType: "FileInventoried",
                ruleId: ruleId(for: item),
                evidenceTier: evidenceTier(for: item),
                filePath: item.relativePath,
                startLine: item.startLine,
                endLine: item.endLine,
                targetSymbol: item.relativePath,
                contractElement: item.relativePath,
                properties: [
                    "language": "swift",
                    "inventoryKind": item.kind,
                    "relativePathHash": sha256Hex(item.relativePath),
                    "sizeBytes": String(item.sizeBytes),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        for gap in gaps {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: "Tier4Unknown",
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                properties: [
                    "gapKind": gap.kind,
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        return facts.sorted { $0.factId < $1.factId }
    }

    private static func makeFact(
        manifest: ScanManifest,
        factType: String,
        ruleId: String,
        evidenceTier: String,
        filePath: String,
        startLine: Int,
        endLine: Int,
        targetSymbol: String? = nil,
        contractElement: String? = nil,
        properties: [String: String]
    ) -> CodeFact {
        let sortedProperties = Dictionary(uniqueKeysWithValues: properties.sorted { $0.key < $1.key })
        let identity = [
            manifest.scanId,
            factType,
            ruleId,
            evidenceTier,
            filePath,
            String(startLine),
            String(endLine),
            targetSymbol ?? "",
            contractElement ?? "",
            properties.sorted { $0.key < $1.key }.map { "\($0.key)=\($0.value)" }.joined(separator: "|")
        ].joined(separator: "\n")
        return CodeFact(
            factId: "swift-fact-" + sha256Hex(identity, length: 32),
            scanId: manifest.scanId,
            repo: manifest.repoName,
            commitSha: manifest.commitSha,
            projectPath: nil,
            factType: factType,
            ruleId: ruleId,
            evidenceTier: evidenceTier,
            sourceSymbol: nil,
            targetSymbol: targetSymbol,
            contractElement: contractElement,
            evidence: EvidenceSpan(
                filePath: filePath,
                startLine: max(1, startLine),
                endLine: max(max(1, startLine), endLine),
                snippetHash: nil,
                extractorId: TraceMapSwiftVersion.extractorId,
                extractorVersion: TraceMapSwiftVersion.extractorVersion
            ),
            properties: sortedProperties
        )
    }

    private static func ruleId(for item: InventoryItem) -> String {
        switch item.kind {
        case "swiftpm-manifest", "swiftpm-resolved": return RuleIds.swiftPM
        case "cocoapods-metadata": return RuleIds.cocoaPods
        case "carthage-metadata": return RuleIds.carthage
        case "xcode-project", "xcode-workspace", "xcode-project-metadata", "xcode-workspace-metadata": return RuleIds.xcode
        default: return RuleIds.fileInventory
        }
    }

    private static func evidenceTier(for item: InventoryItem) -> String {
        switch item.kind {
        case "swift-source": return "Tier3SyntaxOrTextual"
        default: return "Tier2Structural"
        }
    }
}

enum OutputWriter {
    static func write(outputPath: URL, manifest: ScanManifest, facts: [CodeFact], inventory: [InventoryItem]) throws {
        let fm = FileManager.default
        if fm.fileExists(atPath: outputPath.path) {
            try fm.removeItem(at: outputPath)
        }
        try fm.createDirectory(at: outputPath, withIntermediateDirectories: true)
        try fm.createDirectory(at: outputPath.appendingPathComponent("logs"), withIntermediateDirectories: true)
        try writeJSON(manifest, to: outputPath.appendingPathComponent("scan-manifest.json"), pretty: true)
        try writeFacts(facts, to: outputPath.appendingPathComponent("facts.ndjson"))
        try SQLiteWriter.write(path: outputPath.appendingPathComponent("index.sqlite"), manifest: manifest, facts: facts)
        try report(manifest: manifest, facts: facts, inventory: inventory).write(to: outputPath.appendingPathComponent("report.md"), atomically: true, encoding: .utf8)
        try analyzerLog(manifest: manifest, facts: facts).write(to: outputPath.appendingPathComponent("logs/analyzer.log"), atomically: true, encoding: .utf8)
    }

    private static func writeFacts(_ facts: [CodeFact], to url: URL) throws {
        let encoder = stableEncoder(pretty: false)
        var output = ""
        for fact in facts.sorted(by: { $0.factId < $1.factId }) {
            output += String(decoding: try encoder.encode(fact), as: UTF8.self) + "\n"
        }
        try output.write(to: url, atomically: true, encoding: .utf8)
    }

    private static func report(manifest: ScanManifest, facts: [CodeFact], inventory: [InventoryItem]) -> String {
        let byType = count(facts.map(\.factType))
        let byRule = count(facts.map(\.ruleId))
        let byTier = count(facts.map(\.evidenceTier))
        var lines: [String] = [
            "# TraceMap Swift Scan Report",
            "",
            "- Repo: \(manifest.repoName)",
            "- Commit: \(manifest.commitSha)",
            "- Branch: \(manifest.branch ?? "unknown")",
            "- Analysis level: \(manifest.analysisLevel)",
            "- Build status: \(manifest.buildStatus)",
            "- Scanner: \(manifest.scannerVersion)",
            "- Extractor: \(TraceMapSwiftVersion.extractorId) \(TraceMapSwiftVersion.extractorVersion)",
            "",
            "## Artifacts",
            "",
            "- scan-manifest.json",
            "- facts.ndjson",
            "- index.sqlite",
            "- report.md",
            "- logs/analyzer.log",
            "",
            "## Coverage",
            "",
            "This Swift v0 scaffold is reduced coverage. Absence of evidence is not evidence of absence.",
            "",
            "## Fact Counts By Type",
            ""
        ]
        lines += markdownTable(byType)
        lines += ["", "## Fact Counts By Rule", ""]
        lines += markdownTable(byRule)
        lines += ["", "## Fact Counts By Evidence Tier", ""]
        lines += markdownTable(byTier)
        lines += ["", "## Known Gaps", ""]
        lines += manifest.knownGaps.isEmpty ? ["- None"] : manifest.knownGaps.map { "- \($0)" }
        lines += [
            "",
            "## Selected Inventory",
            ""
        ]
        lines += inventory.filter(\.selected).prefix(50).map { "- \($0.kind): \($0.relativePath)" }
        lines += [
            "",
            "## Swift Limitations",
            "",
            "- Static scaffold evidence only; no build, package resolution, simulator, device, runtime, UI navigation, network reachability, storage access, deployment, or production-use proof.",
            "- SwiftSyntax, SourceKit, SwiftPM semantic loading, Xcode semantic loading, Objective-C bridging, macros, result builders, protocol dispatch, property wrappers, and generated-code semantics are future slices.",
            "- Raw source snippets, local absolute paths, raw remotes, secrets, provisioning details, and unsafe values are omitted or hashed.",
            "",
            "## Downstream Commands",
            "",
            "```bash",
            "dotnet run --project src/dotnet/TraceMap.Cli -- export --index <swift-scan-output>/index.sqlite --out <tmp>/swift-export --format json",
            "dotnet run --project src/dotnet/TraceMap.Cli -- combine --index <swift-scan-output>/index.sqlite --label swift --out <tmp>/swift-combined.sqlite",
            "dotnet run --project src/dotnet/TraceMap.Cli -- report --index <tmp>/swift-combined.sqlite --out <tmp>/swift-report",
            "```",
            ""
        ]
        return lines.joined(separator: "\n")
    }

    private static func analyzerLog(manifest: ScanManifest, facts: [CodeFact]) -> String {
        [
            "TraceMap Swift scaffold scan",
            "scanId=\(manifest.scanId)",
            "commitSha=\(manifest.commitSha)",
            "analysisLevel=\(manifest.analysisLevel)",
            "buildStatus=\(manifest.buildStatus)",
            "factCount=\(facts.count)",
            "rawSourceSnippets=false",
            "runtimeExecution=false",
            ""
        ].joined(separator: "\n")
    }
}

enum SQLiteWriter {
    static func write(path: URL, manifest: ScanManifest, facts: [CodeFact]) throws {
        let tmp = URL(fileURLWithPath: path.path + ".tmp")
        try? FileManager.default.removeItem(at: tmp)
        var sql = schemaSQL()
        sql += insertManifestSQL(manifest)
        for fact in facts.sorted(by: { $0.factId < $1.factId }) {
            sql += insertFactSQL(fact)
        }
        _ = try runProcess(executable: "/usr/bin/sqlite3", arguments: [tmp.path], input: sql)
        try? FileManager.default.removeItem(at: path)
        try FileManager.default.moveItem(at: tmp, to: path)
    }

    private static func insertManifestSQL(_ manifest: ScanManifest) -> String {
        let manifestJson = jsonString(manifest, pretty: false)
        return """
        insert into scan_manifest (scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
        values (\(q(manifest.scanId)), \(q(manifest.repoName)), \(q(manifest.commitSha)), \(q(manifest.scannerVersion)), \(q(manifest.scannedAt)), \(q(manifest.analysisLevel)), \(q(manifest.buildStatus)), \(q(manifestJson)));

        """
    }

    private static func insertFactSQL(_ fact: CodeFact) -> String {
        let propsJson = jsonString(fact.properties, pretty: false)
        return """
        insert into facts (fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier, source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, properties_json)
        values (\(q(fact.factId)), \(q(fact.scanId)), \(q(fact.repo)), \(q(fact.commitSha)), null, \(q(fact.factType)), \(q(fact.ruleId)), \(q(fact.evidenceTier)), null, \(q(fact.targetSymbol)), \(q(fact.contractElement)), \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine), null, \(q(propsJson)));

        """
    }

    private static func schemaSQL() -> String {
        """
        create table scan_manifest (
          scan_id text primary key,
          repo text not null,
          commit_sha text not null,
          scanner_version text not null,
          scanned_at text not null,
          analysis_level text not null,
          build_status text not null,
          manifest_json text not null
        );
        create table facts (
          fact_id text primary key,
          scan_id text not null,
          repo text not null,
          commit_sha text not null,
          project_path text,
          fact_type text not null,
          rule_id text not null,
          evidence_tier text not null,
          source_symbol text,
          target_symbol text,
          contract_element text,
          file_path text not null,
          start_line integer not null,
          end_line integer not null,
          snippet_hash text,
          properties_json text not null
        );
        create table symbols (scan_id text not null, symbol_id text not null, language text not null, symbol_kind text not null, display_name text not null, assembly_name text, assembly_version text, containing_symbol_id text, primary key (scan_id, symbol_id));
        create table symbol_occurrences (occurrence_id text primary key, scan_id text not null, symbol_id text not null, fact_id text not null, role text not null, occurrence_kind text not null, evidence_tier text not null, rule_id text not null, file_path text not null, start_line integer not null, end_line integer not null);
        create table fact_symbols (fact_id text not null, scan_id text not null, symbol_id text not null, role text not null, primary key (fact_id, symbol_id, role));
        create table symbol_relationships (relationship_id text primary key, scan_id text not null, source_symbol_id text not null, target_symbol_id text not null, relationship_kind text not null, rule_id text not null, evidence_tier text not null, file_path text not null, start_line integer not null, end_line integer not null);
        create table call_edges (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, caller_symbol text, caller_assembly_name text, caller_assembly_version text, callee_symbol text not null, callee_assembly_name text, callee_assembly_version text, callee_containing_type text, call_kind text, file_path text not null, start_line integer not null, end_line integer not null);
        create table object_creations (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, caller_symbol text, caller_assembly_name text, caller_assembly_version text, created_type text not null, created_type_assembly_name text, created_type_assembly_version text, constructor_symbol text, assigned_to text, file_path text not null, start_line integer not null, end_line integer not null);
        create table argument_flows (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, caller_symbol text, caller_assembly_name text, caller_assembly_version text, callee_symbol text not null, callee_assembly_name text, callee_assembly_version text, call_kind text, parameter_ordinal integer not null, parameter_name text not null, parameter_type text, argument_ordinal integer not null, argument_expression_kind text, argument_expression_hash text, argument_symbol text, argument_symbol_kind text, argument_type text, argument_assembly_name text, argument_assembly_version text, argument_source_file text, argument_source_start_line integer, argument_source_end_line integer, file_path text not null, start_line integer not null, end_line integer not null);
        create table local_aliases (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, containing_symbol text, alias_symbol text not null, alias_symbol_kind text, alias_type text, origin_symbol text not null, origin_symbol_kind text, origin_type text, file_path text not null, start_line integer not null, end_line integer not null);
        create table field_aliases (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, containing_symbol text, field_symbol text not null, field_symbol_kind text, field_type text, origin_symbol text not null, origin_symbol_kind text, origin_type text, file_path text not null, start_line integer not null, end_line integer not null);
        create table parameter_forward_edges (fact_id text primary key, scan_id text not null, repo text not null, commit_sha text not null, evidence_tier text not null, rule_id text not null, source_method_symbol text not null, source_parameter_symbol text not null, source_node_key text not null, target_method_symbol text not null, target_parameter_name text not null, target_parameter_type text, target_parameter_symbol text not null, target_node_key text not null, target_assembly_name text, target_assembly_version text, file_path text not null, start_line integer not null, end_line integer not null);
        create index ix_facts_type on facts(fact_type);
        create index ix_facts_rule on facts(rule_id);
        create index ix_facts_target_symbol on facts(target_symbol);
        create index ix_facts_contract_element on facts(contract_element);
        create index ix_facts_file on facts(file_path);

        """
    }
}

enum Toolchain {
    static func swiftAvailable() -> Bool {
        if ProcessInfo.processInfo.environment["TRACEMAP_SWIFT_DISABLE_TOOLCHAIN"] == "1" {
            return false
        }
        return (try? runProcess(executable: "/usr/bin/swift", arguments: ["--version"])) != nil
    }
}

func runProcess(executable: String, arguments: [String], input: String? = nil) throws -> String {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    let stdout = Pipe()
    let stderr = Pipe()
    process.standardOutput = stdout
    process.standardError = stderr
    if let input {
        let stdin = Pipe()
        process.standardInput = stdin
        try process.run()
        stdin.fileHandleForWriting.write(Data(input.utf8))
        try stdin.fileHandleForWriting.close()
    } else {
        try process.run()
    }
    process.waitUntilExit()
    let out = String(decoding: stdout.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
    let err = String(decoding: stderr.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
    guard process.terminationStatus == 0 else {
        throw ScanError.io(err.trimmed().isEmpty ? "process failed: \(executable) \(arguments.joined(separator: " "))" : err.trimmed())
    }
    return out
}

func writeJSON<T: Encodable>(_ value: T, to url: URL, pretty: Bool) throws {
    try stableEncoder(pretty: pretty).encode(value).write(to: url)
    try "\n".data(using: .utf8)!.append(to: url)
}

func stableEncoder(pretty: Bool) -> JSONEncoder {
    let encoder = JSONEncoder()
    encoder.outputFormatting = pretty ? [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes] : [.sortedKeys, .withoutEscapingSlashes]
    return encoder
}

func jsonString<T: Encodable>(_ value: T, pretty: Bool) -> String {
    String(decoding: try! stableEncoder(pretty: pretty).encode(value), as: UTF8.self)
}

func q(_ value: String?) -> String {
    guard let value else { return "null" }
    return "'" + value.replacingOccurrences(of: "'", with: "''") + "'"
}

func sha256Hex(_ input: String, length: Int? = nil) -> String {
    let digest = SHA256.hash(data: Data(input.utf8))
    let hex = digest.map { String(format: "%02x", $0) }.joined()
    if let length { return String(hex.prefix(length)) }
    return hex
}

func isoNow() -> String {
    let formatter = ISO8601DateFormatter()
    formatter.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
    return formatter.string(from: Date())
}

func relativePath(from base: URL, to url: URL) -> String {
    let basePath = base.standardizedFileURL.path
    let path = url.standardizedFileURL.path
    guard path != basePath else { return "." }
    guard path.hasPrefix(basePath + "/") else { return URL(fileURLWithPath: path).lastPathComponent }
    return String(path.dropFirst(basePath.count + 1))
}

func normalizeRelativePath(_ value: String) -> String {
    value.replacingOccurrences(of: "\\", with: "/").trimmingCharacters(in: CharacterSet(charactersIn: "/"))
}

func lineCount(_ url: URL) -> Int {
    guard let text = try? String(contentsOf: url, encoding: .utf8), !text.isEmpty else { return 1 }
    return max(1, text.split(separator: "\n", omittingEmptySubsequences: false).count)
}

func globMatches(pattern: String, value: String) -> Bool {
    let escaped = NSRegularExpression.escapedPattern(for: normalizeRelativePath(pattern))
        .replacingOccurrences(of: "\\*", with: ".*")
        .replacingOccurrences(of: "\\?", with: ".")
    return value.range(of: "^" + escaped + "$", options: .regularExpression) != nil
        || value.range(of: "^" + escaped + "(/.*)?$", options: .regularExpression) != nil
}

func count(_ values: [String]) -> [(String, Int)] {
    Dictionary(grouping: values, by: { $0 }).map { ($0.key, $0.value.count) }.sorted { $0.0 < $1.0 }
}

func markdownTable(_ rows: [(String, Int)]) -> [String] {
    ["| Name | Count |", "| --- | ---: |"] + rows.map { "| \($0.0) | \($0.1) |" }
}

extension String {
    func trimmed() -> String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }

    var nilIfEmpty: String? {
        isEmpty ? nil : self
    }
}

extension Data {
    func append(to url: URL) throws {
        let handle = try FileHandle(forWritingTo: url)
        defer { try? handle.close() }
        try handle.seekToEnd()
        try handle.write(contentsOf: self)
    }
}
