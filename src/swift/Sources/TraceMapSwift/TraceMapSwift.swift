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
        try OutputWriter.validateOutputPath(scanRoot: repo, gitRoot: git.gitRoot, outputPath: options.outputPath)
        let inventory = try InventoryBuilder.build(scanRoot: repo, gitRoot: git.gitRoot, options: options)
        let scanId = stableScanId(git: git, options: options, inventory: inventory, scanRoot: repo)
        let syntax = SwiftSyntaxEvidenceExtractor.extract(scanRoot: repo, inventory: inventory)
        let toolchain = Toolchain.diagnostics(inventory: inventory)
        var gaps = CoverageGap.defaults(inventory: inventory)
        gaps += toolchain.gaps
        gaps += MetadataGapFactory.gaps(scanRoot: repo, inventory: inventory)
        gaps += UnsupportedSwiftFeatureGapFactory.gaps(scanRoot: repo, inventory: inventory)
        gaps += syntax.gaps
        let dependencies = DependencyExtractor.extract(scanRoot: repo, inventory: inventory)
        let http = SwiftHttpExtractor.extract(scanRoot: repo, inventory: inventory)
        let ui = SwiftUiExtractor.extract(scanRoot: repo, inventory: inventory)
        let storage = SwiftStorageExtractor.extract(scanRoot: repo, inventory: inventory, maxFileByteSize: options.maxFileByteSize)
        gaps += dependencies.gaps
        gaps += http.gaps
        gaps += ui.gaps
        gaps += storage.gaps
        if inventory.contains(where: { $0.kind == "swiftpm-manifest" }) {
            gaps.append(CoverageGap(kind: "swiftpm-load-deferred", ruleId: RuleIds.analysisGap, message: "SwiftPM semantic package loading is deferred; checked-in Package.swift metadata is inventory-only."))
        }
        if inventory.contains(where: { $0.kind == "xcode-project" || $0.kind == "xcode-workspace" }) {
            gaps.append(CoverageGap(kind: "xcode-load-deferred", ruleId: RuleIds.analysisGap, message: "Xcode project/workspace semantic loading is deferred; checked-in metadata is inventory-only."))
        }
        gaps.append(CoverageGap(kind: "swift-semantic-extractor-deferred", ruleId: RuleIds.dynamicBoundary, message: "SourceKit, Swift semantic resolution, protocol dispatch, Objective-C bridging, UI, storage, and runtime analysis are out of scope for this syntax slice."))

        let manifest = ScanManifest(
            scanId: scanId,
            repoName: git.repoName,
            remoteUrl: git.remoteUrl.map { "sha256:\(sha256Hex($0))" },
            branch: git.branch,
            commitSha: git.commitSha,
            scannerVersion: TraceMapSwiftVersion.scanner,
            scannedAt: isoNow(),
            analysisLevel: "Level3SyntaxAnalysis",
            buildStatus: "NotRun",
            solutions: [],
            projects: inventory.projectIdentifiers,
            targetFrameworks: [],
            knownGaps: gaps.map(\.message).sorted(),
            scanRootRelativePath: relativePath(from: git.gitRoot, to: repo),
            scanRootPathHash: sha256Hex(repo.path),
            gitRootHash: sha256Hex(git.gitRoot.path),
            extractorVersions: [TraceMapSwiftVersion.extractorId: TraceMapSwiftVersion.extractorVersion]
        )

        let facts = FactFactory.facts(manifest: manifest, inventory: inventory, gaps: gaps, toolchainDiagnostics: toolchain.diagnostics, scanRoot: repo, syntax: syntax, dependencies: dependencies, http: http, ui: ui, storage: storage)
        try OutputWriter.write(outputPath: options.outputPath, manifest: manifest, facts: facts, inventory: inventory)
        return SwiftScanResult(manifest: manifest, facts: facts, inventory: inventory)
    }

    private static func stableScanId(git: GitMetadata, options: SwiftScanOptions, inventory: [InventoryItem], scanRoot: URL) -> String {
        let optionSignature = [
            "project=\(options.projectFilters.sorted().joined(separator: ","))",
            "include=\(options.includeGlobs.sorted().joined(separator: ","))",
            "exclude=\(options.excludeGlobs.sorted().joined(separator: ","))",
            "max=\(options.maxFileByteSize)"
        ].joined(separator: ";")
        let inventorySignature = inventory
            .map { item in
                let hash = item.selected ? fileHash(scanRoot.appendingPathComponent(item.relativePath)) : ""
                return "\(item.relativePath)|\(item.kind)|\(item.sizeBytes)|\(item.skippedReason ?? "selected")|\(hash)"
            }
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
        let branch = (try? runGit(scanRoot, ["rev-parse", "--abbrev-ref", "HEAD"]).trimmed())
            .flatMap { $0 == "HEAD" ? nil : $0.nilIfEmpty }
        let remote = try? runGit(scanRoot, ["config", "--get", "remote.origin.url"]).trimmed()
        let gitRoot = URL(fileURLWithPath: root).standardizedFileURL
        let repoName = gitRoot.lastPathComponent.replacingOccurrences(of: ".git", with: "")
        return GitMetadata(repoName: repoName, remoteUrl: remote?.nilIfEmpty, branch: branch, commitSha: commit.lowercased(), gitRoot: gitRoot)
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
        ".git", ".build", ".swiftpm", ".tracemap-demo", ".tmp", "DerivedData", "SourcePackages"
    ]

    static func build(scanRoot: URL, gitRoot: URL, options: SwiftScanOptions) throws -> [InventoryItem] {
        let keys: [URLResourceKey] = [.isDirectoryKey, .fileSizeKey, .isRegularFileKey]
        let includeMatchers = try options.includeGlobs.map(GlobMatcher.init(pattern:))
        let excludeMatchers = try options.excludeGlobs.map(GlobMatcher.init(pattern:))
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
                guard matchesProjectFilters(rel, options.projectFilters),
                      matchesIncludes(rel, includeMatchers),
                      !matchesAnyGlob(rel, excludeMatchers) else { continue }
                items.append(InventoryItem(relativePath: rel, kind: kind(for: rel, isDirectory: true), sizeBytes: 0, startLine: 1, endLine: 1, skippedReason: nil))
                continue
            }
            guard values.isRegularFile == true else { continue }
            guard isSupportedFile(rel) else { continue }
            guard matchesProjectFilters(rel, options.projectFilters),
                  matchesIncludes(rel, includeMatchers),
                  !matchesAnyGlob(rel, excludeMatchers) else { continue }
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
        for index in 0..<segments.count {
            let current = segments[index]
            if current == "Carthage", index + 1 < segments.count {
                let next = segments[index + 1]
                if next == "Build" || next == "Checkouts" { return true }
            }
            if current == "Pods", index + 1 < segments.count {
                let next = segments[index + 1]
                if next == ".build" || next == "Build" { return true }
            }
            if current == "vendor" && index + 1 < segments.count { return true }
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
        return [".swift", ".entitlements", ".xcdatamodel", ".storyboard", ".xib", ".plist", ".sql"].contains { relativePath.hasSuffix($0) }
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
            if relativePath.hasSuffix(".sql") { return "sql-resource" }
            return "metadata"
        }
    }

    static func matchesProjectFilters(_ relativePath: String, _ filters: [String]) -> Bool {
        guard !filters.isEmpty else { return true }
        return filters.map(normalizeRelativePath).contains { filter in
            relativePath == filter || relativePath.hasPrefix(filter + "/")
        }
    }

    static func matchesIncludes(_ relativePath: String, _ includes: [GlobMatcher]) -> Bool {
        includes.isEmpty || matchesAnyGlob(relativePath, includes)
    }

    static func matchesAnyGlob(_ relativePath: String, _ globs: [GlobMatcher]) -> Bool {
        globs.contains { $0.matches(relativePath) }
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

struct ToolchainExtraction {
    let diagnostics: [ToolchainDiagnostic]
    let gaps: [CoverageGap]
}

struct ToolchainDiagnostic {
    let toolName: String
    let category: String
    let status: String
    let requiredFor: String
    let gapKind: String?
    let message: String
}

enum MetadataGapFactory {
    static func gaps(scanRoot: URL, inventory: [InventoryItem]) -> [CoverageGap] {
        var gaps: [CoverageGap] = []
        for item in inventory where item.selected {
            switch item.kind {
            case "swiftpm-manifest":
                gaps += swiftPackageManifestGaps(scanRoot.appendingPathComponent(item.relativePath), item)
            case "swiftpm-resolved":
                gaps += packageResolvedGaps(scanRoot.appendingPathComponent(item.relativePath), item)
            case "xcode-project":
                gaps.append(CoverageGap(kind: "xcode-project-graph-deferred", ruleId: RuleIds.analysisGap, message: "Xcode project object graph parsing is deferred in v0 inventory.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine))
            case "xcode-workspace-metadata":
                gaps += workspaceMetadataGaps(scanRoot.appendingPathComponent(item.relativePath), item)
            case "plist":
                gaps += plistGaps(scanRoot.appendingPathComponent(item.relativePath), item)
            default:
                break
            }
        }
        return gaps
    }

    private static func swiftPackageManifestGaps(_ url: URL, _ item: InventoryItem) -> [CoverageGap] {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else {
            return [CoverageGap(kind: "swiftpm-manifest-unreadable", ruleId: RuleIds.analysisGap, message: "Package.swift could not be read as UTF-8.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        let scanText = stripSwiftCommentsAndStringLiterals(text)
        let dynamicMarkers = ["ProcessInfo.", "FileManager.", "getenv(", "#if ", " if ", "\nif ", " for ", "\nfor ", " while ", "\nwhile ", " func ", "\nfunc ", " var ", "\nvar "]
        guard dynamicMarkers.contains(where: { scanText.contains($0) }) else { return [] }
        return [CoverageGap(kind: "swiftpm-manifest-dynamic", ruleId: RuleIds.analysisGap, message: "Package.swift contains dynamic or unsupported constructs; token-scanned inventory is partial.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
    }

    private static func packageResolvedGaps(_ url: URL, _ item: InventoryItem) -> [CoverageGap] {
        guard let data = try? Data(contentsOf: url),
              let object = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return [CoverageGap(kind: "swiftpm-resolved-malformed", ruleId: RuleIds.analysisGap, message: "Package.resolved is malformed or unsupported; lockfile inventory is partial.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        let version = object["version"] as? Int
        return version == 1 || version == 2 ? [] : [CoverageGap(kind: "swiftpm-resolved-unknown-version", ruleId: RuleIds.analysisGap, message: "Package.resolved schema version is unsupported; lockfile inventory is partial.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
    }

    private static func workspaceMetadataGaps(_ url: URL, _ item: InventoryItem) -> [CoverageGap] {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else {
            return [CoverageGap(kind: "xcode-workspace-unreadable", ruleId: RuleIds.analysisGap, message: "Workspace metadata could not be read as UTF-8.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        let locations = regexCaptures(#"location\s*=\s*"([^"]+)""#, in: text)
        if locations.contains(where: { $0.hasPrefix("http:") || $0.hasPrefix("https:") || $0.hasPrefix("absolute:") || $0.hasPrefix("container:/") }) {
            return [CoverageGap(kind: "xcode-workspace-external-reference", ruleId: RuleIds.analysisGap, message: "Workspace contains an external or absolute reference; only safe counts and hashes are emitted.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        return []
    }

    private static func plistGaps(_ url: URL, _ item: InventoryItem) -> [CoverageGap] {
        guard let data = try? Data(contentsOf: url) else {
            return [CoverageGap(kind: "plist-unreadable", ruleId: RuleIds.analysisGap, message: "Info.plist could not be read.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        guard !isBinaryPlist(data) else {
            return [CoverageGap(kind: "plist-binary-unsupported", ruleId: RuleIds.analysisGap, message: "Binary plist parsing is deferred in v0 inventory.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        guard (try? PropertyListSerialization.propertyList(from: data, options: [], format: nil)) != nil else {
            return [CoverageGap(kind: "plist-malformed", ruleId: RuleIds.analysisGap, message: "Info.plist is malformed or unsupported; plist inventory is partial.", filePath: item.relativePath, startLine: item.startLine, endLine: item.endLine)]
        }
        return []
    }
}

enum UnsupportedSwiftFeatureGapFactory {
    static func gaps(scanRoot: URL, inventory: [InventoryItem]) -> [CoverageGap] {
        var gaps: [CoverageGap] = []
        for item in inventory where item.selected {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            switch item.kind {
            case "swift-source":
                guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
                gaps += swiftSourceGaps(text: text, item: item)
                if isGeneratedPath(item.relativePath) {
                    gaps.append(gap("swift-generated-code-reduced", item, item.startLine, item.endLine, "Generated Swift source is static evidence only; generated-code semantics are reduced coverage."))
                }
            case "storyboard":
                gaps.append(gap("swift-storyboard-wiring-unresolved", item, item.startLine, item.endLine, "Storyboard wiring is checked-in metadata only; runtime UI navigation and outlet/action binding are not proven."))
            case "xib":
                gaps.append(gap("swift-nib-wiring-unresolved", item, item.startLine, item.endLine, "XIB wiring is checked-in metadata only; runtime UI navigation and outlet/action binding are not proven."))
            default:
                break
            }
        }
        return gaps.sorted { [$0.filePath, String($0.startLine), $0.kind].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.kind].joined(separator: "|") }
    }

    private static func swiftSourceGaps(text: String, item: InventoryItem) -> [CoverageGap] {
        let searchable = maskSwiftCommentsAndStringLiterals(text)
        var gaps: [CoverageGap] = []
        gaps += patternGaps(#"(?m)^[ \t]*#(?!if\b|elseif\b|else\b|endif\b|available\b|unavailable\b|selector\b|keyPath\b|file\b|fileID\b|filePath\b|line\b|column\b|function\b|dsohandle\b|sourceLocation\b|warning\b|error\b)[A-Za-z_][A-Za-z0-9_]*\b|@(?:attached|freestanding)\b"#, "swift-macro-expansion-unsupported", "Swift macro or macro declaration evidence is not expanded in v0; macro-generated declarations and calls are reduced coverage.", searchable, text, item)
        gaps += patternGaps(#"(?m)^[ \t]*#(?:if|elseif|if\s+canImport)\b"#, "swift-conditional-compilation-reduced", "Conditional compilation changes selected declarations or calls; inactive branch behavior is reduced coverage.", searchable, text, item)
        gaps += patternGaps(#"@objc(?:Members)?\b|@IBAction\b|@IBOutlet\b"#, "swift-objective-c-bridging-reduced", "Objective-C bridging, outlets, and actions are static markers only; runtime selector binding is not proven.", searchable, text, item)
        gaps += patternGaps(#"#selector\s*\("#, "swift-selector-dynamic", "Swift selector expression is static syntax only; Objective-C runtime dispatch target is not proven.", searchable, text, item)
        gaps += patternGaps(#"\b(?:NSClassFromString|NSSelectorFromString|Selector)\s*\(|\bMirror\s*\(\s*reflecting\s*:"#, "swift-reflection-dynamic", "Reflection-style Swift or Objective-C lookup is dynamic; static extraction cannot prove target behavior.", searchable, text, item)
        gaps += patternGaps(#"(?ms)\bprotocol\s+[A-Za-z_][A-Za-z0-9_]*\b[^{]*\{.*?\}\s*extension\s+[A-Za-z_][A-Za-z0-9_]*\b"#, "swift-protocol-dispatch-reduced", "Protocol/default-implementation dispatch is syntax evidence only; runtime witness selection is not proven.", searchable, text, item)
        return collapseDuplicateGaps(gaps)
    }

    private static func patternGaps(_ pattern: String, _ kind: String, _ message: String, _ searchable: String, _ original: String, _ item: InventoryItem) -> [CoverageGap] {
        regexMatches(pattern, in: searchable, dotMatchesLineSeparators: true).compactMap { match in
            guard let range = Range(match.range, in: searchable),
                  searchable[range].contains(where: { !$0.isWhitespace }) else {
                return nil
            }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
            return gap(kind, item, start, end, message)
        }
    }

    private static func collapseDuplicateGaps(_ gaps: [CoverageGap]) -> [CoverageGap] {
        var seen: Set<String> = []
        var collapsed: [CoverageGap] = []
        for gap in gaps.sorted(by: { [$0.filePath, String($0.startLine), $0.kind].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.kind].joined(separator: "|") }) {
            let key = [gap.filePath, String(gap.startLine), gap.kind].joined(separator: "|")
            guard seen.insert(key).inserted else { continue }
            collapsed.append(gap)
        }
        return collapsed
    }

    private static func isGeneratedPath(_ relativePath: String) -> Bool {
        let segments = relativePath.split(separator: "/").map(String.init)
        return segments.contains("Generated") || relativePath.hasSuffix(".generated.swift")
    }

    private static func gap(_ kind: String, _ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ message: String) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.reducedCoverageGap, message: message, filePath: item.relativePath, startLine: max(1, startLine), endLine: max(max(1, startLine), endLine))
    }
}

enum RuleIds {
    static let repoManifest = "swift.repo.manifest.v1"
    static let fileInventory = "swift.file.inventory.v1"
    static let sourceFile = "swift.inventory.source-file.v1"
    static let exclusion = "swift.inventory.exclusion.v1"
    static let swiftPM = "swift.package.swiftpm.v1"
    static let swiftPMManifest = "swift.swiftpm.manifest.v1"
    static let swiftPMResolved = "swift.swiftpm.resolved.v1"
    static let cocoaPods = "swift.package.cocoapods.v1"
    static let carthage = "swift.package.carthage.v1"
    static let xcode = "swift.project.xcode.v1"
    static let xcodeProject = "swift.xcode.project.v1"
    static let xcodeWorkspace = "swift.xcode.workspace.v1"
    static let infoPlist = "swift.plist.info.v1"
    static let ecosystemMetadata = "swift.ecosystem.metadata.v1"
    static let dependencyManifest = "swift.dependency.manifest.v1"
    static let dependencyLockfileSwiftPM = "swift.dependency.lockfile.swiftpm.v1"
    static let dependencyLockfileText = "swift.dependency.lockfile.text.v1"
    static let dependencySurface = "swift.dependency.surface.v1"
    static let dependencyAnalysisGap = "swift.dependency.analysis-gap.v1"
    static let swiftHttpURLSession = "swift.http.urlsession.v1"
    static let swiftHttpClientLibrary = "swift.http.client-library.v1"
    static let swiftHttpAnalysisGap = "swift.http.analysis-gap.v1"
    static let swiftUiView = "swift.ui.swiftui.view.v1"
    static let swiftUiNavigation = "swift.ui.swiftui.navigation.v1"
    static let swiftUiAction = "swift.ui.swiftui.action.v1"
    static let swiftUIKitController = "swift.ui.uikit.controller.v1"
    static let swiftUIKitAction = "swift.ui.uikit.action.v1"
    static let swiftUIKitBinding = "swift.ui.uikit.binding.v1"
    static let swiftUiAnalysisGap = "swift.ui.analysis-gap.v1"
    static let swiftStorageCoreData = "swift.storage.coredata.metadata.v1"
    static let swiftStorageUserDefaults = "swift.storage.userdefaults.key.v1"
    static let swiftStorageKeychain = "swift.storage.keychain.access.v1"
    static let swiftStorageSQLiteSQL = "swift.storage.sqlite.sql.v1"
    static let swiftStorageSQLiteTable = "swift.storage.sqlite.table.v1"
    static let swiftStorageRealmModel = "swift.storage.realm.model.v1"
    static let swiftStorageAnalysisGap = "swift.storage.analysis-gap.v1"
    static let toolchainDiagnostic = "swift.toolchain.diagnostic.v1"
    static let reducedCoverageGap = "swift.reduced-coverage.gap.v1"
    static let analysisGap = "swift.analysis-gap.v1"
    static let toolchainUnavailable = "swift.toolchain.unavailable.v1"
    static let projectLoadFailed = "swift.project.load-failed.v1"
    static let dynamicBoundary = "swift.unsupported.dynamic-boundary.v1"
    static let swiftSyntaxDeclaration = "swift.syntax.declaration.v1"
    static let swiftSyntaxImport = "swift.syntax.import.v1"
    static let swiftSyntaxCall = "swift.syntax.call.v1"
    static let swiftSyntaxConstruction = "swift.syntax.construction.v1"
    static let swiftSyntaxSymbolRelationship = "swift.syntax.symbol-relationship.v1"
    static let swiftSyntaxOverrideCandidate = "swift.syntax.override-candidate.v1"
    static let swiftSyntaxIdentityGap = "swift.syntax.identity-gap.v1"
    static let swiftSyntaxAnalysisGap = "swift.syntax.analysis-gap.v1"
}

public enum EvidenceTier: String, Codable, Equatable {
    case tier1Semantic = "Tier1Semantic"
    case tier2Structural = "Tier2Structural"
    case tier3SyntaxOrTextual = "Tier3SyntaxOrTextual"
    case tier4Unknown = "Tier4Unknown"
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
    public let ruleId: String
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
    public let evidenceTier: EvidenceTier
    public let sourceSymbol: String?
    public let targetSymbol: String?
    public let contractElement: String?
    public let evidence: EvidenceSpan
    public let properties: [String: String]
}

struct DependencyExtraction {
    let records: [DependencyRecord]
    let gaps: [CoverageGap]
}

struct DependencyRecord {
    let factType: String
    let ruleId: String
    let evidenceTier: EvidenceTier
    let filePath: String
    let startLine: Int
    let endLine: Int
    let safeIdentity: String?
    let identityDiscriminator: String
    let properties: [String: String]
}

struct SwiftHttpExtraction {
    let records: [SwiftHttpRecord]
    let gaps: [CoverageGap]
}

struct SwiftHttpRecord {
    let ruleId: String
    let filePath: String
    let startLine: Int
    let endLine: Int
    let method: String
    let normalizedPathKey: String
    let identityDiscriminator: String
    let properties: [String: String]
}

enum SwiftHttpExtractor {
    static func extract(scanRoot: URL, inventory: [InventoryItem]) -> SwiftHttpExtraction {
        var records: [SwiftHttpRecord] = []
        var gaps: [CoverageGap] = []
        for item in inventory where item.selected && item.relativePath.hasSuffix(".swift") {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
            let extracted = swiftFile(text: text, item: item)
            records += extracted.records
            gaps += extracted.gaps
        }
        return SwiftHttpExtraction(
            records: records.sorted { [$0.filePath, String(format: "%08d", $0.startLine), $0.method, $0.normalizedPathKey, $0.identityDiscriminator].joined(separator: "|") < [$1.filePath, String(format: "%08d", $1.startLine), $1.method, $1.normalizedPathKey, $1.identityDiscriminator].joined(separator: "|") },
            gaps: gaps.sorted { [$0.filePath, String($0.startLine), $0.kind].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.kind].joined(separator: "|") }
        )
    }

    private static func swiftFile(text: String, item: InventoryItem) -> SwiftHttpExtraction {
        let searchable = maskSwiftCommentsAndStringLiterals(text)
        var records: [SwiftHttpRecord] = []
        var gaps: [CoverageGap] = []
        let requestVarMatches = regexMatches(#"\b(?:let|var)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*URLRequest\s*\(\s*url\s*:\s*URL\s*\(\s*string\s*:\s*"[^"]+"\s*\)\s*!?\s*\)"#, in: text, dotMatchesLineSeparators: true)
        for (ordinal, match) in requestVarMatches.enumerated() {
            guard isCodeRange(match.range, in: searchable) else { continue }
            let variable = capture(match, 1, in: text) ?? ""
            guard let sourceRange = Range(match.range, in: text) else { continue }
            let source = String(text[sourceRange])
            guard let rawUrl = firstCapture(#"URL\s*\(\s*string\s*:\s*"([^"]+)""#, in: source) else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: text)
            guard let use = firstURLSessionUse(of: variable, after: match.range.location + match.range.length, in: text) else {
                gaps.append(gap("swift-http-method-unknown-projection-omitted", item, start, end, "URLRequest is not passed to a recognized URLSession call in this slice; shared HTTP projection omitted."))
                continue
            }
            let assignments = httpMethodAssignments(to: variable, after: match.range.location + match.range.length, before: use.range.location, in: text, originalText: text)
            guard assignments.count == 1, let method = standardMethod(assignments[0].method) else {
                gaps.append(gap(assignments.isEmpty ? "swift-http-method-unknown-projection-omitted" : "swift-http-method-dynamic", item, start, end, "URLRequest method is missing, ambiguous, or unsupported; shared HTTP projection omitted."))
                continue
            }
            if let record = record(rawUrl: rawUrl, method: method, item: item, start: start, end: end, ruleId: RuleIds.swiftHttpURLSession, framework: "foundation", clientKind: "urlrequest", apiName: "URLSession.\(use.apiName)", ordinal: ordinal) {
                records.append(record)
            } else {
                gaps.append(gap("swift-http-path-unsafe-omitted", item, start, end, "URLRequest path could not be safely normalized; shared HTTP projection omitted."))
            }
        }
        let dynamicRequestMatches = regexMatches(#"\b(?:let|var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*URLRequest\s*\(\s*url\s*:\s*(?!URL\s*\(\s*string\s*:\s*"[^"]+")[^)]+\)"#, in: searchable, dotMatchesLineSeparators: true)
        for match in dynamicRequestMatches where isCodeRange(match.range, in: searchable) {
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: text)
            gaps.append(gap("swift-http-url-dynamic", item, start, end, "URLRequest URL argument is dynamic or indirect; shared HTTP projection omitted."))
        }

        let alamofireMatches = regexMatches(#"\b(AF|Alamofire|Session\.default)\.request\s*\(\s*"([^"]+)"\s*,\s*method\s*:\s*\.([A-Za-z]+)"#, in: text)
        for (ordinal, match) in alamofireMatches.enumerated() {
            guard isCodeRange(match.range, in: searchable) else { continue }
            guard let rawUrl = capture(match, 2, in: text),
                  let method = standardMethod(capture(match, 3, in: text) ?? "") else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: text)
            if let record = record(rawUrl: rawUrl, method: method, item: item, start: start, end: end, ruleId: RuleIds.swiftHttpClientLibrary, framework: "alamofire", clientKind: "alamofire", apiName: "request", ordinal: ordinal) {
                records.append(record)
            } else {
                gaps.append(gap("swift-http-path-unsafe-omitted", item, start, end, "Alamofire request path could not be safely normalized; shared HTTP projection omitted."))
            }
        }
        let dynamicAlamofire = regexMatches(#"\b(AF|Alamofire|Session\.default)\.request\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)\s*,\s*method\s*:\s*\.([A-Za-z]+)"#, in: searchable)
        for match in dynamicAlamofire where isCodeRange(match.range, in: searchable) {
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: text)
            gaps.append(gap("swift-http-url-dynamic", item, start, end, "Alamofire request URL argument is dynamic; shared HTTP projection omitted."))
        }

        for target in targetTypeBodies(in: text, searchable: searchable) {
            let body = target.body
            let pathMatch = firstMatch(#"(?m)^\s*var\s+path\s*:\s*String\s*\{\s*"([^"]+)""#, in: body)
            let methodMatch = firstMatch(#"(?m)^\s*var\s+method\s*:[^{]+\{\s*\.([A-Za-z]+)"#, in: body)
            let baseMatch = firstMatch(#"(?m)^\s*var\s+baseURL\s*:[^{]+\{\s*URL\s*\(\s*string\s*:\s*"([^"]+)""#, in: body)
            let boundaryLine = lineNumber(atUTF16Offset: target.startOffset + (pathMatch?.range.location ?? methodMatch?.range.location ?? 0), in: text)
            if let pathMatch, let rawPath = capture(pathMatch, 1, in: body), let methodMatch, let method = standardMethod(capture(methodMatch, 1, in: body) ?? "") {
                let start = lineNumber(atUTF16Offset: target.startOffset + pathMatch.range.location, in: text)
                let end = lineNumber(atUTF16Offset: target.startOffset + pathMatch.range.location + pathMatch.range.length, in: text)
                if let record = record(rawUrl: rawPath, method: method, item: item, start: start, end: end, ruleId: RuleIds.swiftHttpClientLibrary, framework: "moya", clientKind: "moya", apiName: "TargetType.path", ordinal: records.count) {
                    records.append(record)
                }
                gaps.append(gap("swift-http-moya-target-partial", item, boundaryLine, boundaryLine, baseMatch == nil ? "Moya target baseURL is missing or dynamic; full route join is not proven." : "Moya target baseURL/path join is static metadata only; runtime route reachability is not proven."))
            } else if pathMatch != nil {
                gaps.append(gap("swift-http-method-unknown-projection-omitted", item, boundaryLine, boundaryLine, "Moya target path is static but method is missing or dynamic; shared HTTP projection omitted."))
            } else {
                gaps.append(gap("swift-http-moya-target-partial", item, boundaryLine, boundaryLine, "Moya target path is missing or dynamic; shared HTTP projection omitted."))
            }
        }
        return SwiftHttpExtraction(records: records, gaps: gaps)
    }

    private static func record(rawUrl: String, method: String, item: InventoryItem, start: Int, end: Int, ruleId: String, framework: String, clientKind: String, apiName: String, ordinal: Int) -> SwiftHttpRecord? {
        guard let parsed = parseURLSurface(rawUrl) else { return nil }
        var properties: [String: String] = [
            "coverageCeiling": "syntax-only",
            "framework": framework,
            "httpMethod": method,
            "language": "swift",
            "methodStatus": "present",
            "normalizedPathKey": parsed.normalizedPathKey,
            "pathStatus": "present",
            "queryStatus": parsed.queryStatus,
            "sourceLocationStatus": "literal",
            "staticEvidenceOnly": "true",
            "swiftApiName": apiName,
            "swiftClientKind": clientKind,
            "urlHash": roleHash("url", rawUrl)
        ]
        if let host = parsed.host {
            properties["hostHash"] = roleHash("host", host)
        }
        return SwiftHttpRecord(
            ruleId: ruleId,
            filePath: item.relativePath,
            startLine: start,
            endLine: end,
            method: method,
            normalizedPathKey: parsed.normalizedPathKey,
            identityDiscriminator: ["swift-http/v1", item.relativePath, String(start), String(end), method, parsed.normalizedPathKey, String(ordinal + 1), roleHash("url", rawUrl)].joined(separator: "\u{1f}"),
            properties: properties
        )
    }

    private static func firstURLSessionUse(of variable: String, after offset: Int, in text: String) -> (range: NSRange, apiName: String)? {
        let rest = String(text[String.Index(utf16Offset: min(offset, text.utf16.count), in: text)...])
        let patterns = [
            (#"URLSession(?:\.shared)?\.dataTask\s*\(\s*with\s*:\s*\#(variable)\b"#, "dataTask(with:)"),
            (#"URLSession(?:\.shared)?\.data\s*\(\s*for\s*:\s*\#(variable)\b"#, "data(for:)"),
            (#"URLSession(?:\.shared)?\.data\s*\(\s*from\s*:\s*\#(variable)\b"#, "data(from:)")
        ]
        return patterns.compactMap { pattern, apiName -> (NSRange, String)? in
            guard let match = firstMatch(pattern, in: rest) else { return nil }
            return (NSRange(location: offset + match.range.location, length: match.range.length), apiName)
        }.sorted { $0.0.location < $1.0.location }.first
    }

    private static func httpMethodAssignments(to variable: String, after start: Int, before end: Int, in text: String, originalText: String) -> [(range: NSRange, method: String)] {
        guard end > start else { return [] }
        let startIndex = String.Index(utf16Offset: min(start, text.utf16.count), in: text)
        let endIndex = String.Index(utf16Offset: min(end, text.utf16.count), in: text)
        let originalSlice = String(originalText[startIndex..<endIndex])
        let searchableSlice = maskSwiftCommentsAndStringLiterals(originalSlice)
        return regexMatches(#"\b\#(variable)\.httpMethod\s*=\s*"([^"]+)""#, in: originalSlice).compactMap { match in
            guard isCodeRange(match.range, in: searchableSlice) else { return nil }
            let range = NSRange(location: start + match.range.location, length: match.range.length)
            return (range, capture(match, 1, in: originalSlice) ?? "")
        }
    }

    private static func isCodeRange(_ range: NSRange, in maskedText: String) -> Bool {
        guard let swiftRange = Range(range, in: maskedText) else { return false }
        return maskedText[swiftRange].contains { !$0.isWhitespace }
    }

    private static func targetTypeBodies(in text: String, searchable: String) -> [(body: String, startOffset: Int)] {
        regexMatches(#"\b(?:enum|struct|class)\s+[A-Za-z_][A-Za-z0-9_]*\s*:\s*TargetType\s*\{"#, in: searchable).compactMap { match in
            let open = match.range.location + match.range.length - 1
            guard let close = matchingBraceOffset(in: searchable, openOffset: open), close > open else { return nil }
            let start = open + 1
            guard start <= text.utf16.count, close <= text.utf16.count else { return nil }
            let startIndex = String.Index(utf16Offset: start, in: text)
            let endIndex = String.Index(utf16Offset: close, in: text)
            return (String(text[startIndex..<endIndex]), start)
        }
    }

    private static func matchingBraceOffset(in text: String, openOffset: Int) -> Int? {
        let units = Array(text.utf16)
        guard openOffset >= 0, openOffset < units.count else { return nil }
        let openBrace = Character("{").utf16.first!
        let closeBrace = Character("}").utf16.first!
        var depth = 0
        for index in openOffset..<units.count {
            if units[index] == openBrace {
                depth += 1
            } else if units[index] == closeBrace {
                depth -= 1
                if depth == 0 { return index }
            }
        }
        return nil
    }

    private static func standardMethod(_ value: String) -> String? {
        let method = value.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        return ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"].contains(method) ? method : nil
    }

    private static func gap(_ kind: String, _ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ message: String) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.swiftHttpAnalysisGap, message: message, filePath: item.relativePath, startLine: max(1, startLine), endLine: max(max(1, startLine), endLine))
    }

    private static func roleHash(_ role: String, _ value: String) -> String {
        sha256Hex("swift.http|\(role)|\(value)")
    }
}

struct SwiftUiExtraction {
    let records: [SwiftUiRecord]
    let gaps: [CoverageGap]
}

struct SwiftUiRecord {
    let factType: String
    let ruleId: String
    let filePath: String
    let startLine: Int
    let endLine: Int
    let safeIdentity: String
    let identityDiscriminator: String
    let properties: [String: String]
}

enum SwiftUiExtractor {
    static func extract(scanRoot: URL, inventory: [InventoryItem]) -> SwiftUiExtraction {
        var records: [SwiftUiRecord] = []
        var gaps: [CoverageGap] = []
        var swiftTexts: [String: String] = [:]
        var viewNameCounts: [String: Int] = [:]
        for item in inventory where item.selected && item.kind == "swift-source" {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
            swiftTexts[item.relativePath] = text
            let searchable = maskSwiftCommentsAndStringLiterals(text)
            for declaration in swiftUIViewDeclarations(searchable: searchable) {
                viewNameCounts[declaration.name, default: 0] += 1
            }
        }
        for item in inventory where item.selected {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            switch item.kind {
            case "swift-source":
                guard let text = swiftTexts[item.relativePath] ?? (try? String(contentsOf: url, encoding: .utf8)) else { continue }
                let extracted = swiftFile(text: text, item: item, viewNameCounts: viewNameCounts)
                records += extracted.records
                gaps += extracted.gaps
            case "storyboard":
                gaps.append(gap("swift-ui-storyboard-wiring-unresolved", item, item.startLine, item.endLine, "Storyboard is checked-in UI metadata only; controller, outlet, action, segue, and runtime wiring are not proven."))
            case "xib":
                gaps.append(gap("swift-ui-xib-wiring-unresolved", item, item.startLine, item.endLine, "XIB is checked-in UI metadata only; controller, outlet, action, and runtime wiring are not proven."))
            default:
                break
            }
        }
        return SwiftUiExtraction(
            records: records.sorted(by: recordSortPrecedes),
            gaps: gaps.sorted(by: gapSortPrecedes)
        )
    }

    private struct SwiftUIViewDeclaration {
        let name: String
        let match: NSTextCheckingResult
        let bodyStartOffset: Int
        let bodyEndOffset: Int
    }

    private struct RecordDuplicateKey: Hashable {
        let factType: String
        let ruleId: String
        let filePath: String
        let startLine: Int
        let safeIdentity: String
        let surfaceKind: String
        let uiRole: String
    }

    private struct GapDuplicateKey: Hashable {
        let filePath: String
        let startLine: Int
        let kind: String
    }

    private static func swiftFile(text: String, item: InventoryItem, viewNameCounts: [String: Int]) -> SwiftUiExtraction {
        let searchable = maskSwiftCommentsAndStringLiterals(text)
        var records: [SwiftUiRecord] = []
        var gaps: [CoverageGap] = []
        records += swiftUIViewRecords(text: text, searchable: searchable, item: item)
        records += swiftUISceneRootRecords(text: text, searchable: searchable, item: item)
        records += swiftUINavigationRecords(text: text, searchable: searchable, item: item, viewNameCounts: viewNameCounts)
        records += swiftUIContainerRecords(text: text, searchable: searchable, item: item)
        records += swiftUIActionRecords(text: text, searchable: searchable, item: item)
        records += uiKitControllerRecords(text: text, searchable: searchable, item: item)
        records += uiKitActionRecords(text: text, searchable: searchable, item: item)
        records += uiKitBindingRecords(text: text, searchable: searchable, item: item)
        gaps += dynamicSwiftUIGaps(text: text, searchable: searchable, item: item)
        if item.relativePath.split(separator: "/").contains("Generated") || item.relativePath.hasSuffix(".generated.swift") {
            gaps.append(gap("swift-ui-generated-source-reduced", item, item.startLine, item.endLine, "Generated Swift UI source is static evidence only; generated-code semantics are reduced coverage."))
        }
        return SwiftUiExtraction(records: collapseDuplicateRecords(records), gaps: collapseDuplicateGaps(gaps))
    }

    private static func swiftUIViewRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        swiftUIViewDeclarations(searchable: searchable).map { declaration in
            let propertyWrappers = safePropertyWrappers(in: searchable, declaration: declaration)
            let start = lineNumber(atUTF16Offset: declaration.match.range.location, in: searchable)
            var properties = [
                "surfaceKind": "view",
                "uiFramework": "swiftui",
                "uiRole": "view",
                "viewIdentityStatus": "syntax-local"
            ]
            if !propertyWrappers.isEmpty {
                properties["propertyWrappers"] = propertyWrappers.joined(separator: ",")
            }
            return record(
                factType: "SwiftUiSurfaceDeclared",
                ruleId: RuleIds.swiftUiView,
                item: item,
                start: start,
                end: start,
                safeIdentity: safeLabel(declaration.name),
                role: "swiftui-view",
                ordinal: declaration.match.range.location,
                properties: properties
            )
        }
    }

    private static func swiftUISceneRootRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        let pattern = #"\b(WindowGroup|DocumentGroup)\b[^{]*\{[\s\S]{0,500}?\b([A-Z][A-Za-z0-9_]*)\s*\("#
        return regexMatches(pattern, in: searchable, dotMatchesLineSeparators: true).compactMap { match in
            guard let sceneKind = capture(match, 1, in: searchable),
                  let destination = capture(match, 2, in: searchable) else { return nil }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
            return record(
                factType: "SwiftUiSurfaceDeclared",
                ruleId: RuleIds.swiftUiView,
                item: item,
                start: start,
                end: end,
                safeIdentity: safeLabel(destination),
                role: "swiftui-scene-root",
                ordinal: match.range.location,
                properties: [
                    "sceneKind": safeLabel(sceneKind),
                    "surfaceKind": "scene-root",
                    "uiFramework": "swiftui",
                    "uiRole": "root-view",
                    "viewIdentityStatus": "syntax-local"
                ]
            )
        }
    }

    private static func swiftUINavigationRecords(text: String, searchable: String, item: InventoryItem, viewNameCounts: [String: Int]) -> [SwiftUiRecord] {
        let patterns: [(String, String)] = [
            (#"\bNavigationLink\s*\(\s*destination\s*:\s*([A-Z][A-Za-z0-9_]*)\s*\("#, "navigationlink"),
            (#"\bNavigationLink\b[^{]*\{[\s\S]{0,500}?\b([A-Z][A-Za-z0-9_]*)\s*\("#, "navigationlink"),
            (#"\.(navigationDestination|sheet|fullScreenCover|popover)\b[^{]*\{[\s\S]{0,500}?\b([A-Z][A-Za-z0-9_]*)\s*\("#, "modifier")
        ]
        var records: [SwiftUiRecord] = []
        for (pattern, kind) in patterns {
            for match in regexMatches(pattern, in: searchable, dotMatchesLineSeparators: true) {
                let destinationIndex = kind == "modifier" ? 2 : 1
                guard let destination = capture(match, destinationIndex, in: searchable) else { continue }
                let navigationKind = kind == "modifier" ? safeLabel(capture(match, 1, in: searchable) ?? "modifier") : kind
                let destinationStatus = destinationIdentityStatus(destination, viewNameCounts: viewNameCounts)
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
                records.append(record(
                    factType: "SwiftUiNavigationCandidate",
                    ruleId: RuleIds.swiftUiNavigation,
                    item: item,
                    start: start,
                    end: end,
                    safeIdentity: safeLabel(destination),
                    role: "swiftui-navigation",
                    ordinal: match.range.location,
                    properties: [
                        "destinationBacked": "true",
                        "destinationIdentityStatus": destinationStatus,
                        "navigationKind": navigationKind,
                        "surfaceKind": "navigation-or-presentation",
                        "uiFramework": "swiftui",
                        "uiRole": "navigation-candidate"
                    ]
                ))
            }
        }
        return records
    }

    private static func swiftUIViewDeclarations(searchable: String) -> [SwiftUIViewDeclaration] {
        regexMatches(#"\b(?:struct|class|enum)\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*[^{}\n]*\bView\b[^{}]*\{"#, in: searchable).compactMap { match in
            guard let name = capture(match, 1, in: searchable) else {
                return nil
            }
            let open = match.range.location + match.range.length - 1
            guard let close = matchingBraceOffset(in: searchable, openOffset: open), close > open else {
                return nil
            }
            let startIndex = String.Index(utf16Offset: open, in: searchable)
            let endIndex = String.Index(utf16Offset: close, in: searchable)
            guard firstMatch(#"\bvar\s+body\s*:\s*some\s+View\b"#, in: String(searchable[startIndex..<endIndex])) != nil else {
                return nil
            }
            return SwiftUIViewDeclaration(name: name, match: match, bodyStartOffset: open, bodyEndOffset: close)
        }
    }

    private static func destinationIdentityStatus(_ destination: String, viewNameCounts: [String: Int]) -> String {
        switch viewNameCounts[destination] ?? 0 {
        case 1: return "resolved"
        case 2...: return "ambiguous"
        default: return "unresolved"
        }
    }

    private static func safePropertyWrappers(in searchable: String) -> [String] {
        let allowed = Set(["State", "Binding", "ObservedObject", "StateObject", "Environment", "EnvironmentObject"])
        let names = regexCaptures(#"@(State|Binding|ObservedObject|StateObject|Environment|EnvironmentObject)\b"#, in: searchable)
        return Array(Set(names.filter { allowed.contains($0) }.map(safeLabel))).sorted()
    }

    private static func safePropertyWrappers(in searchable: String, declaration: SwiftUIViewDeclaration) -> [String] {
        guard declaration.bodyEndOffset > declaration.bodyStartOffset else {
            return []
        }
        let startIndex = String.Index(utf16Offset: declaration.bodyStartOffset, in: searchable)
        let endIndex = String.Index(utf16Offset: declaration.bodyEndOffset, in: searchable)
        return safePropertyWrappers(in: String(searchable[startIndex..<endIndex]))
    }

    private static func recordSortPrecedes(_ lhs: SwiftUiRecord, _ rhs: SwiftUiRecord) -> Bool {
        if lhs.filePath != rhs.filePath { return lhs.filePath < rhs.filePath }
        if lhs.startLine != rhs.startLine { return lhs.startLine < rhs.startLine }
        if lhs.factType != rhs.factType { return lhs.factType < rhs.factType }
        if lhs.safeIdentity != rhs.safeIdentity { return lhs.safeIdentity < rhs.safeIdentity }
        return lhs.identityDiscriminator < rhs.identityDiscriminator
    }

    private static func gapSortPrecedes(_ lhs: CoverageGap, _ rhs: CoverageGap) -> Bool {
        if lhs.filePath != rhs.filePath { return lhs.filePath < rhs.filePath }
        if lhs.startLine != rhs.startLine { return lhs.startLine < rhs.startLine }
        return lhs.kind < rhs.kind
    }

    private static func swiftUIContainerRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        regexMatches(#"\b(NavigationStack|NavigationSplitView|TabView|List)\b\s*\{"#, in: searchable).map { match in
            let container = capture(match, 1, in: searchable) ?? "Container"
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            return record(
                factType: "SwiftUiNavigationCandidate",
                ruleId: RuleIds.swiftUiNavigation,
                item: item,
                start: start,
                end: start,
                safeIdentity: safeLabel(container),
                role: "swiftui-container",
                ordinal: match.range.location,
                properties: [
                    "destinationBacked": "false",
                    "destinationIdentityStatus": "not-applicable",
                    "navigationKind": "container",
                    "surfaceKind": "container",
                    "uiContainerKind": safeLabel(container),
                    "uiFramework": "swiftui",
                    "uiRole": "container-context"
                ]
            )
        }
    }

    private static func swiftUIActionRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        let patterns: [(String, String, Int)] = [
            (#"\bButton\s*\("#, "button", 0),
            (#"\.(onTapGesture|onSubmit|onAppear|task|refreshable|swipeActions)\b"#, "modifier", 1),
            (#"\bToolbarItem\s*\("#, "toolbar-item", 0),
            (#"\.alert\b"#, "alert-presentation", 0)
        ]
        var records: [SwiftUiRecord] = []
        for (pattern, fallbackKind, captureIndex) in patterns {
            for match in regexMatches(pattern, in: searchable) {
                let actionKind = captureIndex > 0 ? safeLabel(capture(match, captureIndex, in: searchable) ?? fallbackKind) : fallbackKind
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                records.append(record(
                    factType: "SwiftUiActionCandidate",
                    ruleId: RuleIds.swiftUiAction,
                    item: item,
                    start: start,
                    end: start,
                    safeIdentity: actionKind,
                    role: "swiftui-action",
                    ordinal: match.range.location,
                    properties: [
                        "actionKind": actionKind,
                        "destinationBacked": "false",
                        "surfaceKind": "action",
                        "uiFramework": "swiftui",
                        "uiRole": "action-candidate"
                    ]
                ))
            }
        }
        return records
    }

    private static func uiKitControllerRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        let bases = "UIViewController|UITableViewController|UICollectionViewController|UITabBarController|UINavigationController"
        return regexMatches(#"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*[^{}\n]*\b(\#(bases))\b"#, in: searchable).compactMap { match in
            guard let name = capture(match, 1, in: searchable),
                  let base = capture(match, 2, in: searchable) else { return nil }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            return record(
                factType: "UIKitControllerDeclared",
                ruleId: RuleIds.swiftUIKitController,
                item: item,
                start: start,
                end: start,
                safeIdentity: safeLabel(name),
                role: "uikit-controller",
                ordinal: match.range.location,
                properties: [
                    "controllerBase": safeLabel(base),
                    "surfaceKind": "controller",
                    "uiFramework": "uikit",
                    "uiRole": "controller"
                ]
            )
        }
    }

    private static func uiKitActionRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        var records: [SwiftUiRecord] = []
        for match in regexMatches(#"@IBAction\b[\s\S]{0,160}?\bfunc\s+([A-Za-z_][A-Za-z0-9_]*)\s*\("#, in: searchable, dotMatchesLineSeparators: true) {
            guard let name = capture(match, 1, in: searchable) else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
            records.append(record(
                factType: "UIKitActionDeclared",
                ruleId: RuleIds.swiftUIKitAction,
                item: item,
                start: start,
                end: end,
                safeIdentity: safeLabel(name),
                role: "uikit-action",
                ordinal: match.range.location,
                properties: [
                    "actionKind": "ibaction",
                    "surfaceKind": "action",
                    "uiFramework": "uikit",
                    "uiRole": "action"
                ]
            ))
        }
        if searchable.range(of: #"\bclass\s+[A-Za-z_][A-Za-z0-9_]*\s*:\s*[^{}\n]*\bUI(?:View|TableView|CollectionView|TabBar|Navigation)Controller\b"#, options: .regularExpression) != nil {
            for match in regexMatches(#"\boverride\s+func\s+(viewDidLoad|viewWillAppear|viewDidAppear|prepare)\s*\("#, in: searchable) {
                guard let name = capture(match, 1, in: searchable) else { continue }
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                records.append(record(
                    factType: "UIKitActionDeclared",
                    ruleId: RuleIds.swiftUIKitAction,
                    item: item,
                    start: start,
                    end: start,
                    safeIdentity: safeLabel(name),
                    role: "uikit-lifecycle",
                    ordinal: match.range.location,
                    properties: [
                        "actionKind": "lifecycle",
                        "surfaceKind": "lifecycle",
                        "uiFramework": "uikit",
                        "uiRole": "lifecycle-candidate"
                    ]
                ))
            }
        }
        return records
    }

    private static func uiKitBindingRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftUiRecord] {
        var records: [SwiftUiRecord] = []
        for match in regexMatches(#"@IBOutlet\b[\s\S]{0,180}?\b(?:weak\s+)?var\s+([A-Za-z_][A-Za-z0-9_]*)\b"#, in: searchable, dotMatchesLineSeparators: true) {
            guard let name = capture(match, 1, in: searchable) else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
            records.append(record(
                factType: "UIKitActionBindingCandidate",
                ruleId: RuleIds.swiftUIKitBinding,
                item: item,
                start: start,
                end: end,
                safeIdentity: safeLabel(name),
                role: "uikit-outlet",
                ordinal: match.range.location,
                properties: [
                    "bindingKind": "outlet",
                    "surfaceKind": "binding",
                    "uiFramework": "uikit",
                    "uiRole": "outlet-context",
                    "wiringProven": "false"
                ]
            ))
        }
        for match in regexMatches(#"\.addTarget\s*\([^)]*#selector\s*\(\s*([A-Za-z_][A-Za-z0-9_\.]*)"#, in: searchable, dotMatchesLineSeparators: true) {
            let selector = capture(match, 1, in: searchable) ?? "selector"
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
            records.append(record(
                factType: "UIKitActionBindingCandidate",
                ruleId: RuleIds.swiftUIKitBinding,
                item: item,
                start: start,
                end: end,
                safeIdentity: safeLabel(selector.replacingOccurrences(of: ".", with: "_")),
                role: "uikit-addtarget",
                ordinal: match.range.location,
                properties: [
                    "bindingKind": "add-target-selector",
                    "selectorStatus": "syntax-local",
                    "surfaceKind": "binding",
                    "uiFramework": "uikit",
                    "uiRole": "action-binding-candidate",
                    "wiringProven": "false"
                ]
            ))
        }
        return records
    }

    private static func dynamicSwiftUIGaps(text: String, searchable: String, item: InventoryItem) -> [CoverageGap] {
        var gaps: [CoverageGap] = []
        let dynamicPatterns = [
            (#"\.(navigationDestination|sheet|fullScreenCover|popover)\b[^{\n]*(?:item|isPresented|for)\s*:"#, "swift-ui-dynamic-presentation"),
            (#"\bNavigationLink\s*\(\s*value\s*:"#, "swift-ui-dynamic-navigation-value"),
            (#"#selector\s*\("#, "swift-ui-objective-c-selector-reduced")
        ]
        for (pattern, kind) in dynamicPatterns {
            for match in regexMatches(pattern, in: searchable, dotMatchesLineSeparators: true) {
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: searchable)
                gaps.append(gap(kind, item, start, end, "Swift UI evidence uses dynamic or runtime-mediated syntax; static extraction records reduced coverage only."))
            }
        }
        return gaps
    }

    private static func declarationBodyContains(_ pattern: String, match: NSTextCheckingResult, searchable: String) -> Bool {
        let open = match.range.location + match.range.length - 1
        guard let close = matchingBraceOffset(in: searchable, openOffset: open), close > open else { return false }
        let startIndex = String.Index(utf16Offset: open, in: searchable)
        let endIndex = String.Index(utf16Offset: close, in: searchable)
        return firstMatch(pattern, in: String(searchable[startIndex..<endIndex])) != nil
    }

    private static func record(factType: String, ruleId: String, item: InventoryItem, start: Int, end: Int, safeIdentity: String, role: String, ordinal: Int, properties: [String: String]) -> SwiftUiRecord {
        var safeProperties = properties
        safeProperties["coverageCeiling"] = "syntax-only"
        safeProperties["language"] = "swift"
        safeProperties["runtimeProof"] = "false"
        safeProperties["staticEvidenceOnly"] = "true"
        return SwiftUiRecord(
            factType: factType,
            ruleId: ruleId,
            filePath: item.relativePath,
            startLine: max(1, start),
            endLine: max(max(1, start), end),
            safeIdentity: safeIdentity,
            identityDiscriminator: ["swift-ui/v1", role, item.relativePath, String(start), String(end), safeIdentity, String(ordinal)].joined(separator: "\u{1f}"),
            properties: safeProperties
        )
    }

    private static func collapseDuplicateRecords(_ records: [SwiftUiRecord]) -> [SwiftUiRecord] {
        var seen: Set<RecordDuplicateKey> = []
        var collapsed: [SwiftUiRecord] = []
        for record in records {
            let key = RecordDuplicateKey(
                factType: record.factType,
                ruleId: record.ruleId,
                filePath: record.filePath,
                startLine: record.startLine,
                safeIdentity: record.safeIdentity,
                surfaceKind: record.properties["surfaceKind"] ?? "",
                uiRole: record.properties["uiRole"] ?? ""
            )
            guard seen.insert(key).inserted else { continue }
            collapsed.append(record)
        }
        return collapsed
    }

    private static func collapseDuplicateGaps(_ gaps: [CoverageGap]) -> [CoverageGap] {
        var seen: Set<GapDuplicateKey> = []
        var collapsed: [CoverageGap] = []
        for gap in gaps {
            let key = GapDuplicateKey(filePath: gap.filePath, startLine: gap.startLine, kind: gap.kind)
            guard seen.insert(key).inserted else { continue }
            collapsed.append(gap)
        }
        return collapsed
    }

    private static func gap(_ kind: String, _ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ message: String) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.swiftUiAnalysisGap, message: message, filePath: item.relativePath, startLine: max(1, startLine), endLine: max(max(1, startLine), endLine))
    }

    private static func matchingBraceOffset(in text: String, openOffset: Int) -> Int? {
        let utf16 = text.utf16
        guard openOffset >= 0, openOffset < utf16.count else { return nil }
        let openBrace = Character("{").utf16.first!
        let closeBrace = Character("}").utf16.first!
        var depth = 0
        var index = utf16.index(utf16.startIndex, offsetBy: openOffset)
        while index < utf16.endIndex {
            if utf16[index] == openBrace {
                depth += 1
            } else if utf16[index] == closeBrace {
                depth -= 1
                if depth == 0 {
                    return utf16.distance(from: utf16.startIndex, to: index)
                }
            }
            index = utf16.index(after: index)
        }
        return nil
    }
}

struct SwiftStorageExtraction {
    let records: [SwiftStorageRecord]
    let gaps: [CoverageGap]
}

struct SwiftStorageRecord {
    let factType: String
    let ruleId: String
    let evidenceTier: EvidenceTier
    let filePath: String
    let startLine: Int
    let endLine: Int
    let targetSymbol: String?
    let contractElement: String?
    let identityDiscriminator: String
    let properties: [String: String]
}

enum SwiftStorageExtractor {
    static func extract(scanRoot: URL, inventory: [InventoryItem], maxFileByteSize: Int = TraceMapSwiftVersion.defaultMaxFileByteSize) -> SwiftStorageExtraction {
        var records: [SwiftStorageRecord] = []
        var gaps: [CoverageGap] = []
        for item in inventory where item.selected {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            switch item.kind {
            case "coredata-model":
                let extracted = coreDataModel(url: url, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "coredata-model-bundle":
                records.append(record(
                    factType: "SwiftCoreDataModelDeclared",
                    ruleId: RuleIds.swiftStorageCoreData,
                    tier: .tier2Structural,
                    item: item,
                    start: item.startLine,
                    end: item.endLine,
                    safeIdentity: safeLabel(URL(fileURLWithPath: item.relativePath).deletingPathExtension().lastPathComponent),
                    role: "coredata-model-bundle",
                    ordinal: 0,
                    properties: [
                        "frameworkFamily": "coredata",
                        "modelDescriptorKind": "model-bundle",
                        "modelHash": roleHash("coredata-model-bundle", item.relativePath),
                        "modelPathHash": roleHash("coredata-path", item.relativePath),
                        "runtimeModelChoiceProven": "false"
                    ]
                ))
                let extracted = coreDataModelBundle(url: url, item: item, maxFileByteSize: maxFileByteSize)
                records += extracted.records
                gaps += extracted.gaps
            case "sql-resource":
                let extracted = sqlResource(url: url, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "swift-source":
                guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
                let extracted = swiftSource(text: text, item: item)
                records += extracted.records
                gaps += extracted.gaps
            default:
                break
            }
        }
        return SwiftStorageExtraction(
            records: records.sorted(by: recordSortPrecedes),
            gaps: collapseDuplicateGaps(gaps).sorted(by: gapSortPrecedes)
        )
    }

    private static func coreDataModelBundle(url: URL, item: InventoryItem, maxFileByteSize: Int) -> SwiftStorageExtraction {
        guard let enumerator = FileManager.default.enumerator(
            at: url,
            includingPropertiesForKeys: [.isRegularFileKey, .fileSizeKey],
            options: [.skipsHiddenFiles],
            errorHandler: nil
        ) else {
            return SwiftStorageExtraction(records: [], gaps: [gap("swift-storage-coredata-unreadable", item, item.startLine, item.endLine, "CoreData model bundle could not be enumerated; storage extraction is partial.")])
        }

        var records: [SwiftStorageRecord] = []
        var gaps: [CoverageGap] = []
        for case let child as URL in enumerator {
            guard child.lastPathComponent == "contents",
                  child.deletingLastPathComponent().pathExtension == "xcdatamodel",
                  (try? child.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile) == true else { continue }
            let childRelativePath = item.relativePath + "/" + relativePath(from: url, to: child)
            let size = (try? child.resourceValues(forKeys: [.fileSizeKey]).fileSize) ?? 0
            guard size <= maxFileByteSize else {
                gaps.append(gap("swift-storage-coredata-too-large", item, item.startLine, item.endLine, "CoreData model bundle content exceeded the configured file-size limit; storage extraction is partial."))
                continue
            }
            let modelItem = InventoryItem(
                relativePath: childRelativePath,
                kind: "coredata-model",
                sizeBytes: size,
                startLine: 1,
                endLine: lineCount(child),
                skippedReason: nil
            )
            let extracted = coreDataModel(url: child, item: modelItem)
            records += extracted.records
            gaps += extracted.gaps
        }
        if records.isEmpty && gaps.isEmpty {
            gaps.append(gap("swift-storage-coredata-unsupported", item, item.startLine, item.endLine, "CoreData model bundle did not contain parseable checked-in model contents."))
        }
        return SwiftStorageExtraction(records: records, gaps: gaps)
    }

    private static func coreDataModel(url: URL, item: InventoryItem) -> SwiftStorageExtraction {
        guard let text = try? String(contentsOf: url, encoding: .utf8) else {
            return SwiftStorageExtraction(records: [], gaps: [gap("swift-storage-coredata-unreadable", item, item.startLine, item.endLine, "CoreData model metadata could not be read; storage extraction is partial.")])
        }
        guard text.range(of: #"<(?:model|entity)\b"#, options: .regularExpression) != nil else {
            return SwiftStorageExtraction(records: [], gaps: [gap("swift-storage-coredata-unsupported", item, item.startLine, item.endLine, "CoreData model metadata is unsupported or malformed; storage extraction is partial.")])
        }
        let modelName = coreDataModelName(for: item.relativePath)
        let modelHash = roleHash("coredata-model", text)
        var records: [SwiftStorageRecord] = [
            record(
                factType: "SwiftCoreDataModelDeclared",
                ruleId: RuleIds.swiftStorageCoreData,
                tier: .tier2Structural,
                item: item,
                start: item.startLine,
                end: item.endLine,
                safeIdentity: modelName,
                role: "coredata-model",
                ordinal: 0,
                properties: [
                    "frameworkFamily": "coredata",
                    "modelDescriptorKind": "model",
                    "modelHash": modelHash,
                    "modelName": modelName,
                    "runtimeStoreProven": "false"
                ]
            )
        ]
        var entityRanges: [(range: NSRange, entity: SafeIdentity)] = []
        for (ordinal, match) in regexMatches(#"<entity\b[^>]*>"#, in: text).enumerated() {
            guard let tag = matchText(match, in: text) else { continue }
            let attributes = xmlAttributes(tag)
            let entity = safeOrHash(attributes["name"], role: "coredata-entity")
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            entityRanges.append((match.range, entity))
            var properties = coreDataProperties(kind: "entity", modelHash: modelHash, entity: entity)
            properties["abstract"] = boolString(attributes["isAbstract"])
            properties["managedClassName"] = safeOrHash(attributes["representedClassName"], role: "coredata-class").display
            if let parent = attributes["parentEntity"] {
                properties["parentEntityName"] = safeOrHash(parent, role: "coredata-parent").display
            }
            records.append(record(
                factType: "SwiftCoreDataEntityDeclared",
                ruleId: RuleIds.swiftStorageCoreData,
                tier: .tier2Structural,
                item: item,
                start: start,
                end: start,
                safeIdentity: entity.display,
                role: "coredata-entity",
                ordinal: ordinal,
                properties: properties
            ))
        }
        records += coreDataPropertyRecords(text: text, item: item, modelHash: modelHash, pattern: #"<attribute\b[^>]*>"#, propertyKind: "attribute", entityRanges: entityRanges)
        records += coreDataPropertyRecords(text: text, item: item, modelHash: modelHash, pattern: #"<relationship\b[^>]*>"#, propertyKind: "relationship", entityRanges: entityRanges)
        records += coreDataPropertyRecords(text: text, item: item, modelHash: modelHash, pattern: #"<fetchedProperty\b[^>]*>"#, propertyKind: "fetched-property", entityRanges: entityRanges)
        records += coreDataPropertyRecords(text: text, item: item, modelHash: modelHash, pattern: #"<fetchRequest\b[^>]*>"#, propertyKind: "fetch-request", entityRanges: entityRanges)
        return SwiftStorageExtraction(records: records, gaps: [])
    }

    private static func coreDataModelName(for relativePath: String) -> String {
        let url = URL(fileURLWithPath: relativePath)
        if url.lastPathComponent == "contents", url.deletingLastPathComponent().pathExtension == "xcdatamodel" {
            return safeLabel(url.deletingLastPathComponent().deletingPathExtension().lastPathComponent)
        }
        return safeLabel(url.deletingPathExtension().lastPathComponent)
    }

    private static func coreDataPropertyRecords(text: String, item: InventoryItem, modelHash: String, pattern: String, propertyKind: String, entityRanges: [(range: NSRange, entity: SafeIdentity)]) -> [SwiftStorageRecord] {
        regexMatches(pattern, in: text).enumerated().compactMap { ordinal, match in
            guard let tag = matchText(match, in: text) else { return nil }
            let attributes = xmlAttributes(tag)
            let property = safeOrHash(attributes["name"], role: "coredata-property")
            let destination = safeOrHash(attributes["destinationEntity"], role: "coredata-destination")
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let containingEntity = coreDataContainingEntity(for: match.range.location, entityRanges: entityRanges)
            var properties = coreDataProperties(kind: propertyKind, modelHash: modelHash, entity: containingEntity)
            properties["propertyName"] = property.display
            properties["propertyNameStatus"] = property.status
            properties["propertyKind"] = propertyKind
            properties["attributeType"] = safeLabel(attributes["attributeType"])
            properties["relationshipDestinationName"] = destination.display
            properties["relationshipDestinationStatus"] = destination.status
            properties["optional"] = boolString(attributes["optional"])
            properties["toMany"] = boolString(attributes["toMany"])
            return record(
                factType: "SwiftCoreDataPropertyDeclared",
                ruleId: RuleIds.swiftStorageCoreData,
                tier: .tier2Structural,
                item: item,
                start: start,
                end: start,
                safeIdentity: property.display,
                role: "coredata-\(propertyKind)",
                ordinal: ordinal,
                properties: properties
            )
        }
    }

    private static func coreDataContainingEntity(for offset: Int, entityRanges: [(range: NSRange, entity: SafeIdentity)]) -> SafeIdentity {
        let candidates = entityRanges.filter { range, _ in range.location <= offset }
        return candidates.max { left, right in left.range.location < right.range.location }?.entity ?? safeOrHash(nil, role: "coredata-entity")
    }

    private static func swiftSource(text: String, item: InventoryItem) -> SwiftStorageExtraction {
        let searchable = maskSwiftCommentsAndStringLiterals(text)
        let stringConstants = literalStringConstants(text: text, searchable: searchable)
        var records: [SwiftStorageRecord] = []
        var gaps: [CoverageGap] = []
        records += userDefaultsRecords(text: text, searchable: searchable, item: item, constants: stringConstants)
        records += keychainRecords(text: text, searchable: searchable, item: item)
        let sql = swiftSQLRecords(text: text, searchable: searchable, item: item)
        records += sql.records
        gaps += sql.gaps
        let realm = realmRecords(text: text, searchable: searchable, item: item)
        records += realm.records
        gaps += realm.gaps
        gaps += storageDynamicGaps(text: text, searchable: searchable, item: item, constants: stringConstants)
        return SwiftStorageExtraction(records: records, gaps: gaps)
    }

    private static func userDefaultsRecords(text: String, searchable: String, item: InventoryItem, constants: [String: String]) -> [SwiftStorageRecord] {
        let patterns: [(String, String, Int, String)] = [
            (#"\bUserDefaults(?:\.standard)?\.(string|bool|integer|double|data|object|array|dictionary)\s*\(\s*forKey\s*:\s*"([^"]+)""#, "read", 2, "typed-getter"),
            (#"\bUserDefaults(?:\.standard)?\.set\s*\([^,\n]+,\s*forKey\s*:\s*"([^"]+)""#, "write", 1, "set"),
            (#"\bUserDefaults(?:\.standard)?\.removeObject\s*\(\s*forKey\s*:\s*"([^"]+)""#, "remove", 1, "removeObject")
        ]
        var records: [SwiftStorageRecord] = []
        for (pattern, direction, keyCapture, apiName) in patterns {
            for (ordinal, match) in regexMatches(pattern, in: text, dotMatchesLineSeparators: true).enumerated() {
                guard isCodeRange(match.range, in: searchable),
                      let key = capture(match, keyCapture, in: text) else { continue }
                let start = lineNumber(atUTF16Offset: match.range.location, in: text)
                records.append(userDefaultsRecord(key: key, item: item, start: start, end: start, apiName: apiName, direction: direction, ordinal: match.range.location + ordinal))
            }
        }
        for match in regexMatches(#"\bUserDefaults(?:\.standard)?\.register\s*\(\s*defaults\s*:\s*\[([\s\S]*?)\]\s*\)"#, in: text, dotMatchesLineSeparators: true) {
            guard isCodeRange(match.range, in: searchable),
                  let dictionary = capture(match, 1, in: text) else { continue }
            for (ordinal, keyMatch) in regexMatches(#""([^"]+)"\s*:"#, in: dictionary).enumerated() {
                guard let key = capture(keyMatch, 1, in: dictionary) else { continue }
                let keyOffset = match.range(at: 1).location + keyMatch.range.location
                let start = lineNumber(atUTF16Offset: keyOffset, in: text)
                records.append(userDefaultsRecord(key: key, item: item, start: start, end: start, apiName: "register", direction: "registration-defaults", ordinal: keyOffset + ordinal))
            }
        }
        let aliasPatterns: [(String, String)] = [
            (#"\bUserDefaults(?:\.standard)?\.(?:string|bool|integer|double|data|object|array|dictionary)\s*\(\s*forKey\s*:\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)"#, "read"),
            (#"\bUserDefaults(?:\.standard)?\.set\s*\([^,\n]+,\s*forKey\s*:\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)"#, "write"),
            (#"\bUserDefaults(?:\.standard)?\.removeObject\s*\(\s*forKey\s*:\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)"#, "remove")
        ]
        for (pattern, direction) in aliasPatterns {
            for (ordinal, match) in regexMatches(pattern, in: searchable).enumerated() {
                guard let name = capture(match, 1, in: searchable),
                      let key = literalConstantValue(for: name, constants: constants) else { continue }
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                records.append(userDefaultsRecord(key: key, item: item, start: start, end: start, apiName: "literal-alias", direction: direction, ordinal: match.range.location + ordinal))
            }
        }
        return records
    }

    private static func userDefaultsRecord(key: String, item: InventoryItem, start: Int, end: Int, apiName: String, direction: String, ordinal: Int) -> SwiftStorageRecord {
        let identity = safeOrHash(key, role: "userdefaults-key")
        var properties = baseProperties(framework: "userdefaults")
        properties["apiName"] = apiName
        properties["operationDirection"] = direction
        properties["keyIdentityStatus"] = identity.status
        properties["keyLength"] = String(key.count)
        if identity.status == "normalized" {
            properties["normalizedKey"] = identity.display
        } else {
            properties["keyHash"] = identity.hash
        }
        return record(
            factType: "SwiftUserDefaultsKeyAccessed",
            ruleId: RuleIds.swiftStorageUserDefaults,
            tier: .tier3SyntaxOrTextual,
            item: item,
            start: start,
            end: end,
            safeIdentity: identity.display,
            role: "userdefaults-key",
            ordinal: ordinal,
            properties: properties
        )
    }

    private static func keychainRecords(text: String, searchable: String, item: InventoryItem) -> [SwiftStorageRecord] {
        let operations: [(String, String)] = [
            ("SecItemAdd", "write"),
            ("SecItemCopyMatching", "read"),
            ("SecItemUpdate", "write"),
            ("SecItemDelete", "remove")
        ]
        var records: [SwiftStorageRecord] = []
        for (apiName, direction) in operations {
            for (ordinal, match) in regexMatches(#"\b\#(apiName)\s*\(\s*([A-Za-z_][A-Za-z0-9_]*)?"#, in: searchable).enumerated() {
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                let contextStart = max(0, match.range.location - 1500)
                let startIndex = String.Index(utf16Offset: contextStart, in: text)
                let endIndex = String.Index(utf16Offset: min(match.range.location + 300, text.utf16.count), in: text)
                let context = String(text[startIndex..<endIndex])
                if let argument = capture(match, 1, in: searchable), !argument.isEmpty {
                    let declarationPattern = #"\b(?:let|var)\s+\#(argument)\b[\s\S]{0,1200}?\["#
                    guard firstMatch(declarationPattern, in: context) != nil else { continue }
                }
                var properties = baseProperties(framework: "keychain")
                properties["apiName"] = apiName
                properties["operationDirection"] = direction
                properties["keychainClass"] = firstCapture(#"kSecClass\s+as\s+String\s*:\s*(kSecClass[A-Za-z0-9_]+)"#, in: context).map(safeLabel) ?? "unknown"
                addHashedDescriptor("service", pattern: #"kSecAttrService\s+as\s+String\s*:\s*"([^"]+)""#, context: context, properties: &properties)
                addHashedDescriptor("account", pattern: #"kSecAttrAccount\s+as\s+String\s*:\s*"([^"]+)""#, context: context, properties: &properties)
                addHashedDescriptor("accessGroup", pattern: #"kSecAttrAccessGroup\s+as\s+String\s*:\s*"([^"]+)""#, context: context, properties: &properties)
                properties["queryIdentityStatus"] = properties.keys.contains { $0.hasSuffix("Hash") } ? "hashed-descriptors" : "constants-only"
                records.append(record(
                    factType: "SwiftKeychainAccessPattern",
                    ruleId: RuleIds.swiftStorageKeychain,
                    tier: .tier3SyntaxOrTextual,
                    item: item,
                    start: start,
                    end: start,
                    safeIdentity: apiName,
                    role: "keychain-access",
                    ordinal: match.range.location + ordinal,
                    properties: properties
                ))
            }
        }
        return records
    }

    private static func swiftSQLRecords(text: String, searchable: String, item: InventoryItem) -> SwiftStorageExtraction {
        let patterns: [(String, String)] = [
            (#"\bexecute\s*\(\s*sql\s*:\s*"([^"]+)""#, "literal-string"),
            (#"\b(?:fetchAll|fetchOne|fetchCursor)\s*\([^)]*sql\s*:\s*"([^"]+)""#, "literal-string"),
            (#"\bexecute(?:Query|Update)\s*\(\s*"([^"]+)""#, "literal-string"),
            (#"\bsqlite3_prepare_v2\s*\([^,]+,\s*"([^"]+)""#, "literal-string")
        ]
        var records: [SwiftStorageRecord] = []
        var gaps: [CoverageGap] = []
        for (pattern, sourceKind) in patterns {
            for match in regexMatches(pattern, in: text, dotMatchesLineSeparators: true) {
                guard isCodeRange(match.range, in: searchable),
                      let sql = capture(match, 1, in: text) else { continue }
                let start = lineNumber(atUTF16Offset: match.range.location, in: text)
                if sql.contains("\\(") || isFollowedBySwiftConcatenation(match.range(at: 1), in: text) {
                    gaps.append(gap("swift-storage-dynamic-sql", item, start, start, "Swift SQL text is dynamic or interpolated; complete SqlTextUsed evidence omitted."))
                    continue
                }
                records += sqlRecords(sql: sql, sourceKind: sourceKind, item: item, start: start, end: start, tier: .tier3SyntaxOrTextual, ordinal: match.range.location)
            }
        }
        for match in regexMatches(#"\bsql\s*:\s*[A-Za-z_][A-Za-z0-9_]*|\bexecute(?:Query|Update)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*"#, in: searchable) {
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            gaps.append(gap("swift-storage-dynamic-sql", item, start, start, "Swift SQL argument is indirect; complete SqlTextUsed evidence omitted."))
        }
        return SwiftStorageExtraction(records: records, gaps: gaps)
    }

    private static func isFollowedBySwiftConcatenation(_ range: NSRange, in text: String) -> Bool {
        let end = range.location + range.length
        guard end <= text.utf16.count else { return false }
        let tailStart = String.Index(utf16Offset: end, in: text)
        var tail = text[tailStart...].drop { $0.isWhitespace }
        if tail.first == "\"" {
            tail = tail.dropFirst().drop { $0.isWhitespace }
        }
        return tail.first == "+"
    }

    private static func sqlResource(url: URL, item: InventoryItem) -> SwiftStorageExtraction {
        guard let sql = try? String(contentsOf: url, encoding: .utf8) else {
            return SwiftStorageExtraction(records: [], gaps: [gap("swift-storage-sql-resource-unreadable", item, item.startLine, item.endLine, "SQL resource could not be read; SQL evidence omitted.")])
        }
        return SwiftStorageExtraction(records: sqlRecords(sql: sql, sourceKind: "sql-file", item: item, start: item.startLine, end: item.endLine, tier: .tier2Structural, ordinal: 0), gaps: [])
    }

    private static func sqlRecords(sql: String, sourceKind: String, item: InventoryItem, start: Int, end: Int, tier: EvidenceTier, ordinal: Int) -> [SwiftStorageRecord] {
        let textHash = sha256Hex(sql, length: 32)
        let shape = sqlShape(sql)
        var base = baseProperties(framework: "sqlite")
        base["sqlSourceKind"] = sourceKind
        base["textHash"] = textHash
        base["textLength"] = String(sql.count)
        if let operation = shape.operationName {
            base["operationName"] = operation
        }
        let textFact = record(
            factType: "SqlTextUsed",
            ruleId: RuleIds.swiftStorageSQLiteSQL,
            tier: tier,
            item: item,
            start: start,
            end: end,
            safeIdentity: textHash,
            role: "sql-text",
            ordinal: ordinal,
            properties: base
        )
        var records = [textFact]
        if let queryShapeHash = shape.queryShapeHash {
            var properties = base
            properties["queryShapeHash"] = queryShapeHash
            if let tableName = shape.tableName {
                let table = safeOrHash(tableName, role: "sql-table")
                if table.status == "normalized" {
                    properties["tableName"] = table.display
                } else {
                    properties["tableNameHash"] = table.hash
                }
            }
            records.append(record(
                factType: "QueryPatternDetected",
                ruleId: RuleIds.swiftStorageSQLiteSQL,
                tier: tier,
                item: item,
                start: start,
                end: end,
                safeIdentity: queryShapeHash,
                role: "sql-shape",
                ordinal: ordinal,
                properties: properties
            ))
        }
        return records
    }

    private static func realmRecords(text: String, searchable: String, item: InventoryItem) -> SwiftStorageExtraction {
        var records: [SwiftStorageRecord] = []
        var gaps: [CoverageGap] = []
        let modelPattern = #"\b(?:final\s+)?class\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*[^{}\n]*(Object|EmbeddedObject|RealmSwiftObject)\b[^{]*\{"#
        for (ordinal, match) in regexMatches(modelPattern, in: searchable).enumerated() {
            guard let typeName = capture(match, 1, in: searchable) else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            let open = match.range.location + match.range.length - 1
            let close = matchingBraceOffset(in: searchable, openOffset: open) ?? min(searchable.utf16.count, open + 1500)
            let body = textSlice(text, start: open, end: close)
            let primaryKey = firstCapture(#"primaryKey\s*\(\s*\)\s*->\s*String\??\s*\{[^\"]*\"([^\"]+)\""#, in: body)
            let modelIdentity = safeOrHash(typeName, role: "realm-model")
            var properties = baseProperties(framework: "realm")
            properties["realmModelKind"] = safeLabel(capture(match, 2, in: searchable))
            properties["typeName"] = modelIdentity.display
            properties["typeNameStatus"] = modelIdentity.status
            properties["runtimeSchemaProven"] = "false"
            if let primaryKey {
                properties["primaryKeyName"] = safeOrHash(primaryKey, role: "realm-primary-key").display
            }
            records.append(record(
                factType: "SwiftRealmModelDeclared",
                ruleId: RuleIds.swiftStorageRealmModel,
                tier: .tier3SyntaxOrTextual,
                item: item,
                start: start,
                end: start,
                safeIdentity: modelIdentity.display,
                role: "realm-model",
                ordinal: ordinal,
                properties: properties
            ))
            for propertyMatch in regexMatches(#"@Persisted(?:\([^)]*\))?\s+var\s+([A-Za-z_][A-Za-z0-9_]*)\s*:\s*([A-Za-z_][A-Za-z0-9_<>\[\]?\.]*)"#, in: body) {
                guard let propertyName = capture(propertyMatch, 1, in: body) else { continue }
                let propertyStart = lineNumber(atUTF16Offset: open + propertyMatch.range.location, in: searchable)
                let column = safeOrHash(propertyName, role: "realm-property")
                var propertyInfo = baseProperties(framework: "realm")
                propertyInfo["mappingKind"] = "RealmPersistedProperty"
                propertyInfo["propertyKind"] = "persisted"
                propertyInfo["propertyName"] = column.display
                propertyInfo["propertyNameStatus"] = column.status
                propertyInfo["tableName"] = modelIdentity.display
                propertyInfo["tableNameStatus"] = modelIdentity.status
                propertyInfo["columnName"] = column.display
                propertyInfo["runtimeSchemaProven"] = "false"
                if let propertyType = capture(propertyMatch, 2, in: body) {
                    propertyInfo["propertyType"] = safeLabel(propertyType.replacingOccurrences(of: "?", with: ""))
                }
                records.append(record(
                    factType: "DatabaseColumnMapping",
                    ruleId: RuleIds.swiftStorageRealmModel,
                    tier: .tier3SyntaxOrTextual,
                    item: item,
                    start: propertyStart,
                    end: propertyStart,
                    safeIdentity: "\(modelIdentity.display).\(column.display)",
                    role: "realm-property",
                    ordinal: open + propertyMatch.range.location,
                    properties: propertyInfo
                ))
            }
        }
        for match in regexMatches(#"\.filter\s*\(\s*"#, in: searchable) {
            let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
            gaps.append(gap("swift-storage-realm-dynamic-query", item, start, start, "Realm predicate strings are static syntax only; runtime query execution and object graph behavior are not proven."))
        }
        return SwiftStorageExtraction(records: records, gaps: gaps)
    }

    private static func storageDynamicGaps(text: String, searchable: String, item: InventoryItem, constants: [String: String]) -> [CoverageGap] {
        var gaps: [CoverageGap] = []
        let patterns: [(String, String, String)] = [
            (#"\bSecItem(?:Add|CopyMatching|Update|Delete)\s*\([^)]*(?:merged|config|payload|request)"#, "swift-storage-dynamic-keychain-query", "Keychain query is dynamic or config-derived; access-pattern evidence is partial."),
            (#"\bNSPersistentContainer\s*\("#, "swift-storage-coredata-runtime-container", "CoreData runtime container setup is detected but runtime store loading is not proven.")
        ]
        for match in regexMatches(#"\bUserDefaults(?:\.standard)?\.[A-Za-z0-9_]+\s*\([^)]*forKey\s*:\s*([A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?|[^\"),\s][^),\s]*)"#, in: text) {
            guard isCodeRange(match.range, in: searchable),
                  let argument = capture(match, 1, in: text),
                  !resolvesToLiteralConstant(argument, constants: constants) else { continue }
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            gaps.append(gap("swift-storage-dynamic-userdefaults-key", item, start, start, "UserDefaults key is dynamic or unsupported; key surface evidence omitted."))
        }
        for (pattern, kind, message) in patterns {
            for match in regexMatches(pattern, in: searchable, dotMatchesLineSeparators: true) {
                let start = lineNumber(atUTF16Offset: match.range.location, in: searchable)
                gaps.append(gap(kind, item, start, start, message))
            }
        }
        return gaps
    }

    private static func resolvesToLiteralConstant(_ argument: String, constants: [String: String]) -> Bool {
        literalConstantValue(for: argument, constants: constants) != nil
    }

    private static func literalStringConstants(text: String, searchable: String) -> [String: String] {
        var constants: [String: String] = [:]
        for match in regexMatches(#"\b(?:static\s+)?let\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"([^"]+)""#, in: text) {
            guard isCodeRange(match.range, in: searchable),
                  let name = capture(match, 1, in: text),
                  let value = capture(match, 2, in: text) else { continue }
            constants[name] = value
        }
        for containerMatch in regexMatches(#"\b(?:enum|struct|class)\s+([A-Za-z_][A-Za-z0-9_]*)\b[^{]*\{"#, in: searchable) {
            guard let containerName = capture(containerMatch, 1, in: searchable) else { continue }
            let open = containerMatch.range.location + containerMatch.range.length - 1
            guard let close = matchingBraceOffset(in: searchable, openOffset: open), close > open else { continue }
            let body = textSlice(text, start: open, end: close)
            for constantMatch in regexMatches(#"\bstatic\s+let\s+([A-Za-z_][A-Za-z0-9_]*)\s*=\s*"([^"]+)""#, in: body) {
                guard let name = capture(constantMatch, 1, in: body),
                      let value = capture(constantMatch, 2, in: body) else { continue }
                constants["\(containerName).\(name)"] = value
            }
        }
        return constants
    }

    private static func literalConstantValue(for name: String, constants: [String: String]) -> String? {
        if let value = constants[name] { return value }
        guard !name.contains(".") else { return nil }
        return constants[name]
    }

    private static func record(factType: String, ruleId: String, tier: EvidenceTier, item: InventoryItem, start: Int, end: Int, safeIdentity: String, role: String, ordinal: Int, properties: [String: String]) -> SwiftStorageRecord {
        var safeProperties = properties
        safeProperties["coverageCeiling"] = tier == .tier2Structural ? "structural" : "syntax-or-textual"
        safeProperties["language"] = "swift"
        safeProperties["runtimeProof"] = "false"
        safeProperties["staticEvidenceOnly"] = "true"
        return SwiftStorageRecord(
            factType: factType,
            ruleId: ruleId,
            evidenceTier: tier,
            filePath: item.relativePath,
            startLine: max(1, start),
            endLine: max(max(1, start), end),
            targetSymbol: safeIdentity,
            contractElement: safeIdentity,
            identityDiscriminator: ["swift-storage/v1", role, item.relativePath, String(start), String(end), safeIdentity, String(ordinal)].joined(separator: "\u{1f}"),
            properties: safeProperties
        )
    }

    private static func coreDataProperties(kind: String, modelHash: String, entity: SafeIdentity) -> [String: String] {
        var properties = baseProperties(framework: "coredata")
        properties["modelDescriptorKind"] = kind
        properties["modelHash"] = modelHash
        properties["entityName"] = entity.display
        properties["entityNameStatus"] = entity.status
        properties["runtimeStoreProven"] = "false"
        return properties
    }

    private static func baseProperties(framework: String) -> [String: String] {
        [
            "frameworkFamily": framework,
            "storageRuntimeProof": "false"
        ]
    }

    private struct SafeIdentity {
        let display: String
        let status: String
        let hash: String
    }

    private static func safeOrHash(_ value: String?, role: String) -> SafeIdentity {
        let trimmed = value?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let hash = roleHash(role, trimmed)
        guard !trimmed.isEmpty else {
            return SafeIdentity(display: "", status: "absent", hash: "")
        }
        guard isStorageSafeLabel(trimmed) else {
            return SafeIdentity(display: "sha256:\(hash.prefix(24))", status: "hashed", hash: hash)
        }
        return SafeIdentity(display: trimmed, status: "normalized", hash: hash)
    }

    private static func isStorageSafeLabel(_ value: String) -> Bool {
        guard isSafeLabel(value), value.range(of: #"(secret|token|password|passwd|credential|bearer|apikey|api-key|private|keychain)"#, options: [.regularExpression, .caseInsensitive]) == nil else {
            return false
        }
        return true
    }

    private static func addHashedDescriptor(_ name: String, pattern: String, context: String, properties: inout [String: String]) {
        guard let value = firstCapture(pattern, in: context) else { return }
        properties["\(name)Hash"] = roleHash("keychain-\(name)", value)
        properties["\(name)IdentityStatus"] = "hashed"
    }

    private struct SqlShape {
        let queryShapeHash: String?
        let operationName: String?
        let tableName: String?
    }

    private static func sqlShape(_ sql: String) -> SqlShape {
        let masked = normalizeSQLForShape(sql)
        guard !masked.isEmpty else {
            return SqlShape(queryShapeHash: nil, operationName: nil, tableName: nil)
        }
        let operation = firstCapture(#"(?i)^\s*(SELECT|INSERT|UPDATE|DELETE|MERGE|CREATE|ALTER|DROP|TRUNCATE|CALL|EXEC|EXECUTE)\b"#, in: masked)?.uppercased()
        let tablePatterns = [
            #"(?i)\bFROM\s+([A-Za-z_][A-Za-z0-9_.$]*)"#,
            #"(?i)\bJOIN\s+([A-Za-z_][A-Za-z0-9_.$]*)"#,
            #"(?i)\bINTO\s+([A-Za-z_][A-Za-z0-9_.$]*)"#,
            #"(?i)^\s*UPDATE\s+([A-Za-z_][A-Za-z0-9_.$]*)"#,
            #"(?i)^\s*CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z_][A-Za-z0-9_.$]*)"#
        ]
        let table = tablePatterns.compactMap { firstCapture($0, in: masked) }.first
        return SqlShape(queryShapeHash: sha256Hex(masked, length: 32), operationName: operation, tableName: table)
    }

    private static func normalizeSQLForShape(_ sql: String) -> String {
        var value = sql.trimmingCharacters(in: .whitespacesAndNewlines)
        value = value.replacingOccurrences(of: #"--[^\n\r]*"#, with: " ", options: .regularExpression)
        value = value.replacingOccurrences(of: #"(?s)/\*.*?\*/"#, with: " ", options: .regularExpression)
        value = value.replacingOccurrences(of: #"'(?:''|\\['"]|[^'])*'"#, with: "' '", options: .regularExpression)
        value = value.replacingOccurrences(of: #""(?:""|\\["']|[^"])*""#, with: #"" ""#, options: .regularExpression)
        value = value.replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
        value = value.trimmingCharacters(in: .whitespacesAndNewlines)
        while value.hasSuffix(";") {
            value = String(value.dropLast()).trimmingCharacters(in: .whitespacesAndNewlines)
        }
        return value
    }

    private static func xmlAttributes(_ tag: String) -> [String: String] {
        var attributes: [String: String] = [:]
        for match in regexMatches(#"([A-Za-z_:][A-Za-z0-9_.:-]*)\s*=\s*"([^"]*)""#, in: tag) {
            guard let key = capture(match, 1, in: tag),
                  let value = capture(match, 2, in: tag) else { continue }
            attributes[key] = value
        }
        return attributes
    }

    private static func matchText(_ match: NSTextCheckingResult, in text: String) -> String? {
        guard let range = Range(match.range, in: text) else { return nil }
        return String(text[range])
    }

    private static func textSlice(_ text: String, start: Int, end: Int) -> String {
        let startIndex = String.Index(utf16Offset: max(0, min(start, text.utf16.count)), in: text)
        let endIndex = String.Index(utf16Offset: max(0, min(end, text.utf16.count)), in: text)
        return String(text[startIndex..<endIndex])
    }

    private static func isCodeRange(_ range: NSRange, in maskedText: String) -> Bool {
        guard let swiftRange = Range(range, in: maskedText) else { return false }
        return maskedText[swiftRange].contains { !$0.isWhitespace }
    }

    private static func boolString(_ value: String?) -> String {
        guard let value else { return "unknown" }
        return ["YES", "true", "1"].contains(value) ? "true" : "false"
    }

    private static func roleHash(_ role: String, _ value: String) -> String {
        sha256Hex("swift.storage|\(role)|\(value)")
    }

    private static func matchingBraceOffset(in text: String, openOffset: Int) -> Int? {
        let utf16 = text.utf16
        guard openOffset >= 0, openOffset < utf16.count else { return nil }
        let openBrace = Character("{").utf16.first!
        let closeBrace = Character("}").utf16.first!
        var depth = 0
        var index = utf16.index(utf16.startIndex, offsetBy: openOffset)
        while index < utf16.endIndex {
            if utf16[index] == openBrace {
                depth += 1
            } else if utf16[index] == closeBrace {
                depth -= 1
                if depth == 0 { return utf16.distance(from: utf16.startIndex, to: index) }
            }
            index = utf16.index(after: index)
        }
        return nil
    }

    private static func recordSortPrecedes(_ lhs: SwiftStorageRecord, _ rhs: SwiftStorageRecord) -> Bool {
        if lhs.filePath != rhs.filePath { return lhs.filePath < rhs.filePath }
        if lhs.startLine != rhs.startLine { return lhs.startLine < rhs.startLine }
        if lhs.factType != rhs.factType { return lhs.factType < rhs.factType }
        return lhs.identityDiscriminator < rhs.identityDiscriminator
    }

    private static func gapSortPrecedes(_ lhs: CoverageGap, _ rhs: CoverageGap) -> Bool {
        if lhs.filePath != rhs.filePath { return lhs.filePath < rhs.filePath }
        if lhs.startLine != rhs.startLine { return lhs.startLine < rhs.startLine }
        return lhs.kind < rhs.kind
    }

    private static func collapseDuplicateGaps(_ gaps: [CoverageGap]) -> [CoverageGap] {
        var seen: Set<String> = []
        var collapsed: [CoverageGap] = []
        for gap in gaps {
            let key = [gap.filePath, String(gap.startLine), gap.kind].joined(separator: "|")
            guard seen.insert(key).inserted else { continue }
            collapsed.append(gap)
        }
        return collapsed
    }

    private static func gap(_ kind: String, _ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ message: String) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.swiftStorageAnalysisGap, message: message, filePath: item.relativePath, startLine: max(1, startLine), endLine: max(max(1, startLine), endLine))
    }
}

enum DependencyExtractor {
    static func extract(scanRoot: URL, inventory: [InventoryItem]) -> DependencyExtraction {
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        for item in inventory where item.selected {
            let url = scanRoot.appendingPathComponent(item.relativePath)
            let fileName = URL(fileURLWithPath: item.relativePath).lastPathComponent
            guard let text = try? String(contentsOf: url, encoding: .utf8) else {
                if knownDependencyMetadataFile(fileName) {
                    gaps.append(gap("swift-dependency-metadata-unreadable", item, 1, item.endLine, "\(fileName) could not be read as UTF-8; dependency surface extraction is partial."))
                }
                continue
            }
            switch fileName {
            case "Package.swift":
                let extracted = swiftPMManifest(text: text, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "Package.resolved":
                let extracted = packageResolved(text: text, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "Podfile":
                let extracted = podfile(text: text, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "Podfile.lock":
                let extracted = podfileLock(text: text, item: item)
                records += extracted.records
                gaps += extracted.gaps
            case "Cartfile":
                let extracted = cartfile(text: text, item: item, resolved: false)
                records += extracted.records
                gaps += extracted.gaps
            case "Cartfile.resolved":
                let extracted = cartfile(text: text, item: item, resolved: true)
                records += extracted.records
                gaps += extracted.gaps
            default:
                break
            }
        }
        return DependencyExtraction(
            records: records.sorted {
                [$0.filePath, String(format: "%08d", $0.startLine), String(format: "%08d", $0.endLine), $0.identityDiscriminator].joined(separator: "|")
                    < [$1.filePath, String(format: "%08d", $1.startLine), String(format: "%08d", $1.endLine), $1.identityDiscriminator].joined(separator: "|")
            },
            gaps: gaps.sorted { [$0.filePath, String($0.startLine), $0.kind].joined(separator: "|") < [$1.filePath, String($1.startLine), $1.kind].joined(separator: "|") }
        )
    }

    private static func swiftPMManifest(text: String, item: InventoryItem) -> DependencyExtraction {
        let searchableText = maskSwiftCommentsAndStringLiterals(text)
        let matches = regexMatches(#"\.package\s*\((.*?)\)"#, in: searchableText, dotMatchesLineSeparators: true)
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        for (ordinal, match) in matches.enumerated() {
            let body = capture(match, 1, in: text) ?? ""
            let start = lineNumber(atUTF16Offset: match.range.location, in: text)
            let end = lineNumber(atUTF16Offset: match.range.location + match.range.length, in: text)
            let offset = utf8Offset(atUTF16Offset: match.range.location, in: text)
            if body.contains("\\(") || body.contains("ProcessInfo") || body.contains("FileManager") {
                gaps.append(gap("swift-dependency-manifest-dynamic", item, start, end, "Package.swift dependency declaration contains dynamic syntax; dependency surface extraction is partial."))
                continue
            }
            if body.contains("path:") {
                let hash = sha256Hex(body)
                gaps.append(gap("swift-dependency-local-path-omitted", item, start, end, "Package.swift local path dependency omitted; pathHash=\(hash)."))
                continue
            }
            let explicitName = firstCapture(#"name\s*:\s*"([^"]+)""#, in: body)
            let url = firstCapture(#"url\s*:\s*"([^"]+)""#, in: body)
            let candidate = explicitName ?? url.flatMap(urlIdentityCandidate)
            let safe = candidate.flatMap { isSafeLabel($0) ? $0 : nil }
            let identityHash = sha256Hex([candidate ?? "", url ?? body].joined(separator: "\n"))
            var properties = baseProperties(
                packageManager: "swiftpm",
                sourceMetadataKind: "Package.swift",
                declarationKind: "swiftpm-manifest-dependency",
                identity: safe,
                identityHash: identityHash,
                identityStatus: safe == nil ? "hashed" : "safe",
                versionStatus: body.contains("from:") || body.contains("exact:") || body.contains("branch:") || body.contains("revision:") ? "present" : "absent",
                revisionStatus: body.contains("revision:") || body.contains("branch:") ? "present" : "absent",
                sourceLocationStatus: url == nil ? "unknown" : "hashed"
            )
            properties["occurrenceIndex"] = String(ordinal + 1)
            if let url {
                properties["sourceLocationHash"] = sha256Hex(url)
            }
            records.append(DependencyRecord(
                factType: "SwiftDependencyDeclared",
                ruleId: RuleIds.dependencyManifest,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: item.relativePath,
                startLine: start,
                endLine: end,
                safeIdentity: safe,
                identityDiscriminator: discriminator(item, start, end, offset, "swiftpm-manifest", ordinal),
                properties: properties
            ))
        }
        return DependencyExtraction(records: records, gaps: gaps)
    }

    private static func packageResolved(text: String, item: InventoryItem) -> DependencyExtraction {
        guard let data = text.data(using: .utf8),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return DependencyExtraction(records: [], gaps: [gap("swift-dependency-lockfile-malformed", item, 1, item.endLine, "Package.resolved is malformed JSON; dependency lockfile evidence is partial.")])
        }
        let version = root["version"] as? Int ?? -1
        guard version == 1 || version == 2 else {
            return DependencyExtraction(records: [], gaps: [gap("swift-dependency-lockfile-unsupported-schema", item, 1, item.endLine, "Package.resolved schemaVersion=\(version) is unsupported by dependency surface extraction.")])
        }
        let pins = root["pins"] as? [[String: Any]] ?? ((root["object"] as? [String: Any])?["pins"] as? [[String: Any]] ?? [])
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        var seen: Set<String> = []
        var duplicateSeen = false
        let keyOffsets = jsonKeyOffsets(["identity", "package"], in: text)
        for (index, pin) in pins.enumerated() {
            guard let identity = pin["identity"] as? String ?? pin["package"] as? String else {
                gaps.append(gap("swift-dependency-lockfile-malformed", item, 1, item.endLine, "Package.resolved pin is missing identity/package; dependency lockfile evidence is partial."))
                continue
            }
            if !seen.insert(identity).inserted {
                duplicateSeen = true
            }
            let state = pin["state"] as? [String: Any] ?? [:]
            let location = pin["location"] as? String ?? pin["repositoryURL"] as? String
            let safe = isSafeLabel(identity) ? identity : nil
            let offset = keyOffsets.indices.contains(index) ? keyOffsets[index] : 0
            let line = lineNumber(atUTF16Offset: offset, in: text)
            var properties = baseProperties(
                packageManager: "swiftpm",
                sourceMetadataKind: "Package.resolved",
                declarationKind: "swiftpm-lockfile-pin",
                identity: safe,
                identityHash: sha256Hex(identity),
                identityStatus: safe == nil ? "hashed" : "safe",
                versionStatus: state["version"] == nil ? "absent" : "present",
                revisionStatus: state["revision"] == nil ? "absent" : "hashed",
                sourceLocationStatus: location == nil ? "unknown" : "hashed"
            )
            properties["schemaVersion"] = String(version)
            properties["stateKind"] = state.keys.sorted().joined(separator: ",")
            properties["occurrenceIndex"] = String(index + 1)
            if let location {
                properties["sourceLocationHash"] = sha256Hex(location)
            }
            if let revision = state["revision"] as? String {
                properties["revisionHash"] = sha256Hex(revision)
            }
            records.append(DependencyRecord(
                factType: "SwiftDependencyLockfileEntryDeclared",
                ruleId: RuleIds.dependencyLockfileSwiftPM,
                evidenceTier: .tier2Structural,
                filePath: item.relativePath,
                startLine: line,
                endLine: line,
                safeIdentity: safe,
                identityDiscriminator: discriminator(item, line, line, offset, "swiftpm-lockfile", index),
                properties: properties
            ))
        }
        if duplicateSeen {
            gaps.append(gap("swift-dependency-lockfile-malformed", item, 1, item.endLine, "Package.resolved contains duplicate pin identities; distinct rows were preserved with occurrence discriminators."))
        }
        return DependencyExtraction(records: records, gaps: gaps)
    }

    private static func podfile(text: String, item: InventoryItem) -> DependencyExtraction {
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        let lines = text.components(separatedBy: "\n")
        var lineOffsets: [Int] = []
        var utf16Offset = 0
        for line in lines {
            lineOffsets.append(utf16Offset)
            utf16Offset += line.utf16.count + 1
        }
        var lineIndex = 0
        while lineIndex < lines.count {
            let line = lines[lineIndex]
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            defer { lineIndex += 1 }
            guard trimmed.hasPrefix("pod ") else { continue }
            let match = firstMatch(#"\bpod\s+['"]([^'"]+)['"]"#, in: line)
            guard let match, let name = capture(match, 1, in: line) else {
                gaps.append(gap("swift-dependency-manifest-dynamic", item, lineIndex + 1, lineIndex + 1, "Podfile pod declaration is dynamic or unsupported; dependency evidence is partial."))
                continue
            }
            let safe = isSafeLabel(name) ? name : nil
            let declaration = podDeclarationBody(lines: lines, startIndex: lineIndex)
            let body = declaration.body
            let endLine = declaration.endIndex + 1
            var properties = baseProperties(
                packageManager: "cocoapods",
                sourceMetadataKind: "Podfile",
                declarationKind: "podfile-declaration",
                identity: safe,
                identityHash: sha256Hex(name),
                identityStatus: safe == nil ? "hashed" : "safe",
                versionStatus: body.contains("~>") || body.contains(">=") || body.contains("=") ? "present" : "absent",
                revisionStatus: "absent",
                sourceLocationStatus: body.contains(":git") || body.contains(":path") || body.contains(":source") ? "hashed" : "absent"
            )
            properties["occurrenceIndex"] = String(records.count + 1)
            if body.contains(":path") {
                gaps.append(gap("swift-dependency-local-path-omitted", item, lineIndex + 1, endLine, "Podfile local path dependency option omitted; dependency evidence is partial."))
            }
            let anchor = lineOffsets[lineIndex] + match.range.location
            records.append(DependencyRecord(
                factType: "SwiftDependencyDeclared",
                ruleId: RuleIds.dependencyManifest,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: item.relativePath,
                startLine: lineIndex + 1,
                endLine: endLine,
                safeIdentity: safe,
                identityDiscriminator: discriminator(item, lineIndex + 1, endLine, anchor, "podfile", records.count),
                properties: properties
            ))
        }
        return DependencyExtraction(records: records, gaps: gaps)
    }

    private static func podfileLock(text: String, item: InventoryItem) -> DependencyExtraction {
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        var section = ""
        var checksumNames: Set<String> = []
        var duplicateChecksum = false
        let lines = text.components(separatedBy: "\n")
        var utf16Offset = 0
        for (lineIndex, line) in lines.enumerated() {
            defer { utf16Offset += line.utf16.count + 1 }
            if let heading = firstCapture(#"^([A-Z][A-Z0-9 _-]+):\s*$"#, in: line) {
                section = heading
                continue
            }
            guard section == "PODS" || section == "DEPENDENCIES" || section == "SPEC CHECKSUMS" else { continue }
            if section == "SPEC CHECKSUMS" {
                if let name = firstCapture(#"^\s{2}([A-Za-z0-9_.+-]+):"#, in: line), !checksumNames.insert(name).inserted {
                    duplicateChecksum = true
                }
                continue
            }
            guard let name = firstCapture(#"^\s{2}-\s+([A-Za-z0-9_.+-]+)"#, in: line) else { continue }
            let safe = isSafeLabel(name) ? name : nil
            var properties = baseProperties(
                packageManager: "cocoapods",
                sourceMetadataKind: "Podfile.lock",
                declarationKind: "podfile-lock-entry",
                identity: safe,
                identityHash: sha256Hex(name),
                identityStatus: safe == nil ? "hashed" : "safe",
                versionStatus: line.contains("(") ? "present" : "absent",
                revisionStatus: "absent",
                sourceLocationStatus: "absent"
            )
            properties["sourceSection"] = safeSection(section) ?? sha256Hex(section)
            properties["occurrenceIndex"] = String(records.count + 1)
            records.append(DependencyRecord(
                factType: "SwiftDependencyLockfileEntryDeclared",
                ruleId: RuleIds.dependencyLockfileText,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: item.relativePath,
                startLine: lineIndex + 1,
                endLine: lineIndex + 1,
                safeIdentity: safe,
                identityDiscriminator: discriminator(item, lineIndex + 1, lineIndex + 1, utf16Offset, "podfile-lock-\(section)", records.count),
                properties: properties
            ))
        }
        if duplicateChecksum {
            gaps.append(gap("swift-dependency-lockfile-malformed", item, 1, item.endLine, "Podfile.lock SPEC CHECKSUMS contains duplicate pod names; checksum hash input was deduplicated."))
        }
        return DependencyExtraction(records: records, gaps: gaps)
    }

    private static func cartfile(text: String, item: InventoryItem, resolved: Bool) -> DependencyExtraction {
        var records: [DependencyRecord] = []
        var gaps: [CoverageGap] = []
        let lines = text.components(separatedBy: "\n")
        var utf16Offset = 0
        for (lineIndex, line) in lines.enumerated() {
            defer { utf16Offset += line.utf16.count + 1 }
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty || trimmed.hasPrefix("#") { continue }
            guard let match = firstMatch(#"^\s*(github|git|binary)\s+["']([^"']+)["'](?:\s+(.+))?"#, in: line),
                  let sourceKind = capture(match, 1, in: line),
                  let location = capture(match, 2, in: line) else {
                gaps.append(gap("swift-dependency-manifest-unsupported-shape", item, lineIndex + 1, lineIndex + 1, "Cartfile entry is unsupported; dependency evidence is partial."))
                continue
            }
            let versionPart = capture(match, 3, in: line)?.trimmingCharacters(in: .whitespacesAndNewlines).trimmingCharacters(in: CharacterSet(charactersIn: "\"'"))
            let identityCandidate: String? = sourceKind == "github" ? nil : urlIdentityCandidate(location)
            let safe = identityCandidate.flatMap { isSafeLabel($0) ? $0 : nil }
            var properties = baseProperties(
                packageManager: "carthage",
                sourceMetadataKind: resolved ? "Cartfile.resolved" : "Cartfile",
                declarationKind: resolved ? "cartfile-resolved-entry" : "cartfile-declaration",
                identity: safe,
                identityHash: sha256Hex(location),
                identityStatus: safe == nil ? "hashed" : "safe",
                versionStatus: versionPart == nil ? "absent" : (isSemVer(versionPart!) && resolved ? "present" : "hashed"),
                revisionStatus: resolved ? (versionPart == nil ? "absent" : (isSemVer(versionPart!) ? "absent" : "hashed")) : "absent",
                sourceLocationStatus: sourceKind == "binary" || location.contains("://") || location.contains("/") ? "hashed" : "unknown"
            )
            properties["sourceKind"] = ["github", "git", "binary"].contains(sourceKind) ? sourceKind : "unknown"
            properties["occurrenceIndex"] = String(records.count + 1)
            properties["sourceLocationHash"] = sha256Hex(location)
            if let versionPart, isSemVer(versionPart), resolved {
                properties["version"] = versionPart
            } else if let versionPart {
                properties["versionHash"] = sha256Hex(versionPart)
            }
            records.append(DependencyRecord(
                factType: resolved ? "SwiftDependencyLockfileEntryDeclared" : "SwiftDependencyDeclared",
                ruleId: resolved ? RuleIds.dependencyLockfileText : RuleIds.dependencyManifest,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: item.relativePath,
                startLine: lineIndex + 1,
                endLine: lineIndex + 1,
                safeIdentity: safe,
                identityDiscriminator: discriminator(item, lineIndex + 1, lineIndex + 1, utf16Offset + match.range.location, "cartfile-\(sourceKind)-\(resolved ? "resolved" : "manifest")", records.count),
                properties: properties
            ))
        }
        return DependencyExtraction(records: records, gaps: gaps)
    }

    private static func baseProperties(packageManager: String, sourceMetadataKind: String, declarationKind: String, identity: String?, identityHash: String, identityStatus: String, versionStatus: String, revisionStatus: String, sourceLocationStatus: String) -> [String: String] {
        var properties: [String: String] = [
            "packageManager": packageManager,
            "sourceMetadataKind": sourceMetadataKind,
            "declarationKind": declarationKind,
            "dependencyIdentityStatus": identityStatus,
            "dependencyIdentityHash": identityHash,
            "versionStatus": versionStatus,
            "revisionStatus": revisionStatus,
            "sourceLocationStatus": sourceLocationStatus
        ]
        if let identity, identityStatus == "safe" {
            properties["normalizedDependencyIdentity"] = identity
        }
        return properties
    }

    private static func gap(_ kind: String, _ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ message: String) -> CoverageGap {
        CoverageGap(kind: kind, ruleId: RuleIds.dependencyAnalysisGap, message: message, filePath: item.relativePath, startLine: max(1, startLine), endLine: max(max(1, startLine), endLine))
    }

    private static func knownDependencyMetadataFile(_ fileName: String) -> Bool {
        switch fileName {
        case "Package.swift", "Package.resolved", "Podfile", "Podfile.lock", "Cartfile", "Cartfile.resolved":
            return true
        default:
            return false
        }
    }

    private static func podDeclarationBody(lines: [String], startIndex: Int) -> (body: String, endIndex: Int) {
        var endIndex = startIndex
        var parts = [lines[startIndex]]
        var index = startIndex + 1
        while index < lines.count {
            let trimmed = lines[index].trimmingCharacters(in: .whitespaces)
            if trimmed.isEmpty || trimmed == "end" || trimmed.hasPrefix("pod ") || trimmed.hasPrefix("target ") || trimmed.hasPrefix("abstract_target ") {
                break
            }
            parts.append(lines[index])
            endIndex = index
            index += 1
        }
        return (parts.joined(separator: "\n"), endIndex)
    }

    private static func discriminator(_ item: InventoryItem, _ startLine: Int, _ endLine: Int, _ offset: Int, _ kind: String, _ ordinal: Int) -> String {
        [
            "swift-dependency/v1",
            item.relativePath,
            String(startLine),
            String(endLine),
            kind,
            String(offset),
            String(ordinal + 1)
        ].joined(separator: "\u{1f}")
    }
}

enum FactFactory {
    static func facts(
        manifest: ScanManifest,
        inventory: [InventoryItem],
        gaps: [CoverageGap],
        toolchainDiagnostics: [ToolchainDiagnostic] = [],
        scanRoot: URL,
        syntax: SwiftSyntaxExtraction = SwiftSyntaxExtraction(declarations: [], imports: [], calls: [], constructions: [], relationships: [], gaps: []),
        dependencies: DependencyExtraction? = nil,
        http: SwiftHttpExtraction? = nil,
        ui: SwiftUiExtraction? = nil,
        storage: SwiftStorageExtraction? = nil
    ) -> [CodeFact] {
        var facts: [CodeFact] = []
        facts.append(makeFact(
            manifest: manifest,
            factType: "FileInventoried",
            ruleId: RuleIds.repoManifest,
            evidenceTier: .tier2Structural,
            filePath: "scan-manifest.json",
            startLine: 1,
            endLine: 1,
            targetSymbol: manifest.repoName,
            properties: [
                "language": "swift",
                "adapterKind": "inventory-project-discovery",
                "coverageCeiling": "syntax-or-structural",
                "coverageLabel": coverageLabel(inventory: inventory, gaps: gaps),
                "repoNameHash": sha256Hex(manifest.repoName),
                "scanRootRelativePath": manifest.scanRootRelativePath ?? "."
            ]
        ))
        facts += toolchainFacts(manifest: manifest, diagnostics: toolchainDiagnostics)
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
        facts += sourceRootFacts(manifest: manifest, inventory: inventory)
        var aggregateFactIdsByPath: [String: [String]] = [:]
        for item in inventory where item.selected {
            let metadata = metadataFacts(manifest: manifest, item: item, scanRoot: scanRoot)
            for fact in metadata where [
                "SwiftPackageManifestDeclared",
                "SwiftPackageResolvedDeclared",
                "SwiftEcosystemMetadataDeclared"
            ].contains(fact.factType) {
                aggregateFactIdsByPath[fact.evidence.filePath, default: []].append(fact.factId)
            }
            facts += metadata
        }
        let dependencies = dependencies ?? DependencyExtractor.extract(scanRoot: scanRoot, inventory: inventory)
        facts += dependencyFacts(manifest: manifest, dependencies: dependencies.records, aggregateFactIdsByPath: aggregateFactIdsByPath)
        for gap in dependencies.gaps {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: .tier4Unknown,
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                identityDiscriminator: sha256Hex(gap.message),
                properties: [
                    "gapKind": gap.kind,
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        let http = http ?? SwiftHttpExtractor.extract(scanRoot: scanRoot, inventory: inventory)
        facts += httpFacts(manifest: manifest, records: http.records)
        for (gapOrdinal, gap) in http.gaps.enumerated() {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: .tier4Unknown,
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                identityDiscriminator: sha256Hex("\(gap.filePath)|\(gap.startLine)|\(gap.endLine)|\(gap.kind)|\(gapOrdinal)|\(gap.message)"),
                properties: [
                    "gapOrdinal": String(gapOrdinal),
                    "gapKind": gap.kind,
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        let ui = ui ?? SwiftUiExtractor.extract(scanRoot: scanRoot, inventory: inventory)
        facts += uiFacts(manifest: manifest, records: ui.records)
        for gap in ui.gaps {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: .tier4Unknown,
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                identityDiscriminator: sha256Hex(gap.message),
                properties: [
                    "gapKind": gap.kind,
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        let storage = storage ?? SwiftStorageExtractor.extract(scanRoot: scanRoot, inventory: inventory)
        facts += storageFacts(manifest: manifest, records: storage.records)
        for (gapOrdinal, gap) in storage.gaps.enumerated() {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: .tier4Unknown,
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                identityDiscriminator: sha256Hex("\(gap.filePath)|\(gap.startLine)|\(gap.endLine)|\(gap.kind)|\(gapOrdinal)|\(gap.message)"),
                properties: [
                    "frameworkFamily": storageGapFramework(gap.kind),
                    "gapKind": gap.kind,
                    "gapOrdinal": String(gapOrdinal),
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        facts += syntaxFacts(manifest: manifest, syntax: syntax)
        for gap in gaps where !isSpecializedExtractorGap(gap.kind) {
            facts.append(makeFact(
                manifest: manifest,
                factType: "AnalysisGap",
                ruleId: gap.ruleId,
                evidenceTier: .tier4Unknown,
                filePath: gap.filePath,
                startLine: gap.startLine,
                endLine: gap.endLine,
                targetSymbol: gap.kind,
                contractElement: gap.kind,
                identityDiscriminator: sha256Hex(gap.message),
                properties: [
                    "gapKind": gap.kind,
                    "messageHash": sha256Hex(gap.message),
                    "staticEvidenceOnly": "true"
                ]
            ))
        }
        return facts
    }

    private static func isSpecializedExtractorGap(_ kind: String) -> Bool {
        kind.hasPrefix("swift-dependency-")
            || kind.hasPrefix("swift-http-")
            || kind.hasPrefix("swift-ui-")
            || kind.hasPrefix("swift-storage-")
    }

    private static func toolchainFacts(manifest: ScanManifest, diagnostics: [ToolchainDiagnostic]) -> [CodeFact] {
        diagnostics.sorted { [$0.category, $0.toolName, $0.status].joined(separator: "|") < [$1.category, $1.toolName, $1.status].joined(separator: "|") }.map { diagnostic in
            var properties = [
                "coverageCeiling": "diagnostic",
                "diagnosticKind": "toolchain",
                "language": "swift",
                "requiredFor": diagnostic.requiredFor,
                "staticEvidenceOnly": "true",
                "toolCategory": diagnostic.category,
                "toolName": diagnostic.toolName,
                "toolStatus": diagnostic.status
            ]
            if let gapKind = diagnostic.gapKind {
                properties["gapKind"] = gapKind
            }
            return makeFact(
                manifest: manifest,
                factType: "SwiftToolchainDiagnostic",
                ruleId: RuleIds.toolchainDiagnostic,
                evidenceTier: .tier4Unknown,
                filePath: "scan-manifest.json",
                startLine: 1,
                endLine: 1,
                targetSymbol: diagnostic.toolName,
                contractElement: diagnostic.status,
                identityDiscriminator: [diagnostic.category, diagnostic.toolName, diagnostic.status, diagnostic.requiredFor].joined(separator: "\u{1f}"),
                properties: properties
            )
        }
    }

    private static func dependencyFacts(manifest: ScanManifest, dependencies: [DependencyRecord], aggregateFactIdsByPath: [String: [String]]) -> [CodeFact] {
        dependencies.map { dependency in
            var properties = dependency.properties
            let supporting = (aggregateFactIdsByPath[dependency.filePath] ?? []).sorted()
            if !supporting.isEmpty {
                properties["supportingFactIds"] = supporting.joined(separator: ",")
            }
            properties["coverageCeiling"] = dependency.evidenceTier == .tier2Structural ? "structural" : "syntax-or-textual"
            properties["language"] = "swift"
            properties["staticEvidenceOnly"] = "true"
            return makeFact(
                manifest: manifest,
                factType: dependency.factType,
                ruleId: dependency.ruleId,
                evidenceTier: dependency.evidenceTier,
                filePath: dependency.filePath,
                startLine: dependency.startLine,
                endLine: dependency.endLine,
                targetSymbol: dependency.safeIdentity,
                contractElement: dependency.safeIdentity,
                identityDiscriminator: dependency.identityDiscriminator,
                properties: properties
            )
        }
    }

    private static func httpFacts(manifest: ScanManifest, records: [SwiftHttpRecord]) -> [CodeFact] {
        records.map { record in
            makeFact(
                manifest: manifest,
                factType: "HttpCallDetected",
                ruleId: record.ruleId,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: record.filePath,
                startLine: record.startLine,
                endLine: record.endLine,
                targetSymbol: "\(record.method) \(record.normalizedPathKey)",
                contractElement: record.normalizedPathKey,
                identityDiscriminator: record.identityDiscriminator,
                properties: record.properties
            )
        }
    }

    private static func uiFacts(manifest: ScanManifest, records: [SwiftUiRecord]) -> [CodeFact] {
        records.map { record in
            makeFact(
                manifest: manifest,
                factType: record.factType,
                ruleId: record.ruleId,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: record.filePath,
                startLine: record.startLine,
                endLine: record.endLine,
                targetSymbol: record.safeIdentity,
                contractElement: record.safeIdentity,
                identityDiscriminator: record.identityDiscriminator,
                properties: record.properties
            )
        }
    }

    private static func storageFacts(manifest: ScanManifest, records: [SwiftStorageRecord]) -> [CodeFact] {
        records.map { record in
            makeFact(
                manifest: manifest,
                factType: record.factType,
                ruleId: record.ruleId,
                evidenceTier: record.evidenceTier,
                filePath: record.filePath,
                startLine: record.startLine,
                endLine: record.endLine,
                targetSymbol: record.targetSymbol,
                contractElement: record.contractElement,
                identityDiscriminator: record.identityDiscriminator,
                properties: record.properties
            )
        }
    }

    private static func storageGapFramework(_ kind: String) -> String {
        if kind.contains("coredata") { return "coredata" }
        if kind.contains("userdefaults") { return "userdefaults" }
        if kind.contains("keychain") { return "keychain" }
        if kind.contains("sql") { return "sqlite" }
        if kind.contains("realm") { return "realm" }
        return "storage"
    }

    private static func syntaxFacts(manifest: ScanManifest, syntax: SwiftSyntaxExtraction) -> [CodeFact] {
        var facts: [CodeFact] = []
        for declaration in syntax.declarations {
            facts.append(makeFact(
                manifest: manifest,
                factType: "SwiftDeclarationDeclared",
                ruleId: RuleIds.swiftSyntaxDeclaration,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: declaration.filePath,
                startLine: declaration.startLine,
                endLine: declaration.endLine,
                sourceSymbol: declaration.containingSymbolId,
                targetSymbol: declaration.symbolId,
                contractElement: declaration.displaySignature,
                properties: [
                    "coverageCeiling": "syntax-or-structural",
                    "declarationKind": declaration.kind,
                    "displaySignature": declaration.displaySignature,
                    "genericArity": String(declaration.genericArity),
                    "isAsync": declaration.isAsync ? "true" : "false",
                    "isThrows": declaration.isThrows ? "true" : "false",
                    "language": "swift",
                    "moduleName": declaration.moduleName,
                    "name": declaration.name,
                    "parameterLabels": declaration.parameterLabels.joined(separator: ","),
                    "staticEvidenceOnly": "true",
                    "symbolId": declaration.symbolId,
                    "syntaxHash": declaration.syntaxHash,
                    "conditionalCompilation": declaration.conditionalCompilation ? "true" : "false"
                ]
            ))
        }
        for imported in syntax.imports {
            facts.append(makeFact(
                manifest: manifest,
                factType: "SwiftImportDeclared",
                ruleId: RuleIds.swiftSyntaxImport,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: imported.filePath,
                startLine: imported.startLine,
                endLine: imported.endLine,
                targetSymbol: imported.importedModule,
                contractElement: imported.importedModule,
                properties: [
                    "coverageCeiling": "syntax-or-structural",
                    "conditionalCompilation": imported.conditionalCompilation ? "true" : "false",
                    "exportedImport": imported.exportedImport ? "true" : "false",
                    "importKind": imported.importKind,
                    "importedModule": imported.importedModule,
                    "language": "swift",
                    "staticEvidenceOnly": "true",
                    "syntaxHash": imported.syntaxHash
                ]
            ))
        }
        for call in syntax.calls {
            facts.append(makeFact(
                manifest: manifest,
                factType: "SwiftCallCandidate",
                ruleId: RuleIds.swiftSyntaxCall,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: call.filePath,
                startLine: call.startLine,
                endLine: call.endLine,
                sourceSymbol: call.callerSymbolId,
                targetSymbol: call.calleeName,
                contractElement: call.calleeName,
                identityDiscriminator: call.identityDiscriminator,
                properties: [
                    "argumentLabels": call.argumentLabels.joined(separator: ","),
                    "arity": String(call.arity),
                    "callKind": call.callKind,
                    "calleeName": call.calleeName,
                    "calleeSyntaxKind": call.calleeSyntaxKind,
                    "callerDisplayName": call.callerDisplayName,
                    "callerSymbolId": call.callerSymbolId ?? "",
                    "conditionalCompilation": call.conditionalCompilation ? "true" : "false",
                    "coverageCeiling": "syntax-only",
                    "language": "swift",
                    "staticEvidenceOnly": "true",
                    "syntaxOffset": call.identityDiscriminator,
                    "syntaxHash": call.syntaxHash,
                    "unsupportedReason": call.unsupportedReason ?? ""
                ]
            ))
        }
        for construction in syntax.constructions {
            facts.append(makeFact(
                manifest: manifest,
                factType: "SwiftConstructionCandidate",
                ruleId: RuleIds.swiftSyntaxConstruction,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: construction.filePath,
                startLine: construction.startLine,
                endLine: construction.endLine,
                sourceSymbol: construction.callerSymbolId,
                targetSymbol: construction.createdTypeSyntax,
                contractElement: construction.createdTypeSyntax,
                identityDiscriminator: construction.identityDiscriminator,
                properties: [
                    "argumentLabels": construction.argumentLabels.joined(separator: ","),
                    "callerDisplayName": construction.callerDisplayName,
                    "callerSymbolId": construction.callerSymbolId ?? "",
                    "conditionalCompilation": construction.conditionalCompilation ? "true" : "false",
                    "coverageCeiling": "syntax-only",
                    "createdTypeSyntax": construction.createdTypeSyntax,
                    "language": "swift",
                    "runtimeAllocationProven": "false",
                    "staticEvidenceOnly": "true",
                    "syntaxOffset": construction.identityDiscriminator,
                    "syntaxHash": construction.syntaxHash
                ]
            ))
        }
        for relationship in syntax.relationships {
            let ruleId = relationship.relationshipKind == "Overrides" ? RuleIds.swiftSyntaxOverrideCandidate : RuleIds.swiftSyntaxSymbolRelationship
            facts.append(makeFact(
                manifest: manifest,
                factType: "SymbolRelationship",
                ruleId: ruleId,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: relationship.filePath,
                startLine: relationship.startLine,
                endLine: relationship.endLine,
                sourceSymbol: relationship.sourceSymbolId,
                targetSymbol: relationship.targetSymbolId,
                contractElement: relationship.relationshipKind,
                properties: [
                    "conditionalCompilation": relationship.conditionalCompilation ? "true" : "false",
                    "coverageCeiling": "syntax-only",
                    "identityStatus": "source-local",
                    "language": "swift",
                    "relationshipKind": relationship.relationshipKind,
                    "runtimeDispatchProven": "false",
                    "sourceSymbolDisplayName": relationship.sourceSymbolDisplayName,
                    "sourceSymbolId": relationship.sourceSymbolId,
                    "sourceSymbolKind": relationship.sourceSymbolKind,
                    "sourceSymbolLanguage": "swift",
                    "staticEvidenceOnly": "true",
                    "swiftRelationshipDisplayKind": relationship.swiftRelationshipDisplayKind,
                    "syntaxHash": relationship.syntaxHash,
                    "targetSymbolDisplayName": relationship.targetSymbolDisplayName,
                    "targetSymbolId": relationship.targetSymbolId,
                    "targetSymbolKind": relationship.targetSymbolKind,
                    "targetSymbolLanguage": "swift"
                ]
            ))
        }
        return facts
    }

    private static func makeFact(
        manifest: ScanManifest,
        factType: String,
        ruleId: String,
        evidenceTier: EvidenceTier,
        filePath: String,
        startLine: Int,
        endLine: Int,
        sourceSymbol: String? = nil,
        targetSymbol: String? = nil,
        contractElement: String? = nil,
        identityDiscriminator: String? = nil,
        properties: [String: String]
    ) -> CodeFact {
        let sortedProperties = Dictionary(uniqueKeysWithValues: properties.sorted { $0.key < $1.key })
        let identity = [
            manifest.scanId,
            factType,
            ruleId,
            evidenceTier.rawValue,
            filePath,
            String(startLine),
            String(endLine),
            sourceSymbol ?? "",
            targetSymbol ?? "",
            contractElement ?? "",
            identityDiscriminator ?? ""
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
            sourceSymbol: sourceSymbol,
            targetSymbol: targetSymbol,
            contractElement: contractElement,
            evidence: EvidenceSpan(
                filePath: filePath,
                startLine: max(1, startLine),
                endLine: max(max(1, startLine), endLine),
                ruleId: ruleId,
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
        case "coredata-model", "coredata-model-bundle": return RuleIds.swiftStorageCoreData
        case "sql-resource": return RuleIds.swiftStorageSQLiteSQL
        default: return RuleIds.fileInventory
        }
    }

    private static func evidenceTier(for item: InventoryItem) -> EvidenceTier {
        switch item.kind {
        case "swift-source": return .tier3SyntaxOrTextual
        case "coredata-model", "coredata-model-bundle", "sql-resource": return .tier2Structural
        default: return .tier2Structural
        }
    }

    private static func coverageLabel(inventory: [InventoryItem], gaps: [CoverageGap]) -> String {
        if inventory.isEmpty { return "SwiftInventoryNotDetected" }
        let degradingKinds: Set<String> = [
            "CanImportConditionalAmbiguous",
            "ConditionalCompilationAmbiguous",
            "SwiftParseDiagnostics",
            "carthage-toolchain-unavailable",
            "carthage-toolchain-timeout",
            "cocoapods-toolchain-unavailable",
            "cocoapods-toolchain-timeout",
            "file-too-large",
            "no-supported-swift-inputs",
            "plist-binary-unsupported",
            "plist-malformed",
            "plist-unreadable",
            "swift-conditional-compilation-reduced",
            "swift-generated-code-reduced",
            "swift-macro-expansion-unsupported",
            "swift-nib-wiring-unresolved",
            "swift-objective-c-bridging-reduced",
            "swift-protocol-dispatch-reduced",
            "swift-reflection-dynamic",
            "swift-selector-dynamic",
            "swift-sourcekit-timeout",
            "swift-sourcekit-unavailable",
            "swift-storyboard-wiring-unresolved",
            "swift-call-optional-chaining-unresolved",
            "swift-call-unsupported-shape",
            "swift-ambiguous-symbol-identity",
            "swift-extension-membership-syntax-only",
            "swift-module-context-unavailable",
            "swift-module-identity-unknown",
            "swift-override-target-unresolved",
            "swift-relationship-kind-unsupported",
            "swift-source-unreadable",
            "swift-unresolved-external-symbol",
            "swift-toolchain-unavailable",
            "swift-toolchain-timeout",
            "swiftpm-manifest-dynamic",
            "swiftpm-manifest-unreadable",
            "swiftpm-resolved-malformed",
            "swiftpm-resolved-unknown-version",
            "xcode-project-graph-deferred",
            "xcode-workspace-external-reference",
            "xcode-workspace-unreadable",
            "xcodebuild-toolchain-unavailable",
            "xcodebuild-toolchain-timeout"
        ]
        if gaps.contains(where: { gap in
            degradingKinds.contains(gap.kind)
                || gap.kind.hasPrefix("swift-dependency-")
                || gap.kind.hasPrefix("swift-http-")
                || gap.kind.hasPrefix("swift-ui-")
                || gap.kind.hasPrefix("swift-storage-")
        }) {
            return "SwiftInventoryReduced"
        }
        return "SwiftInventoryFileBasedSucceeded"
    }

    private static func sourceRootFacts(manifest: ScanManifest, inventory: [InventoryItem]) -> [CodeFact] {
        let grouped = Dictionary(grouping: inventory.filter { $0.selected && ($0.kind == "swift-source" || $0.kind == "swift-generated-source") }) { sourceRoot(for: $0.relativePath) }
        return grouped.keys.sorted().map { root in
            let items = grouped[root] ?? []
            let rootKind = rootKind(for: root)
            return makeFact(
                manifest: manifest,
                factType: "SwiftSourceRootDeclared",
                ruleId: RuleIds.sourceFile,
                evidenceTier: .tier3SyntaxOrTextual,
                filePath: root,
                startLine: 1,
                endLine: 1,
                targetSymbol: root,
                contractElement: root,
                properties: [
                    "classificationEvidence": "repo-relative-path",
                    "coverageCeiling": "syntax-or-structural",
                    "fileCount": String(items.count),
                    "language": "swift",
                    "rootKind": rootKind,
                    "rootPathHash": sha256Hex(root)
                ]
            )
        }
    }

    private static func metadataFacts(manifest: ScanManifest, item: InventoryItem, scanRoot: URL) -> [CodeFact] {
        let url = scanRoot.appendingPathComponent(item.relativePath)
        switch item.kind {
        case "swift-source":
            return [makeFact(
                manifest: manifest,
                factType: "SwiftSourceFileDeclared",
                ruleId: RuleIds.sourceFile,
                evidenceTier: .tier2Structural,
                filePath: item.relativePath,
                startLine: item.startLine,
                endLine: item.endLine,
                targetSymbol: item.relativePath,
                contractElement: item.relativePath,
                properties: [
                    "coverageCeiling": "syntax-or-structural",
                    "fileKind": item.relativePath.hasSuffix(".generated.swift") ? "generated-source" : "source",
                    "language": "swift",
                    "lineCount": String(item.endLine),
                    "pathHash": sha256Hex(item.relativePath),
                    "rootKind": rootKind(for: item.relativePath)
                ]
            )]
        case "swiftpm-manifest":
            return [swiftPMManifestFact(manifest: manifest, item: item, url: url)]
        case "swiftpm-resolved":
            return [swiftPMResolvedFact(manifest: manifest, item: item, url: url)]
        case "xcode-project", "xcode-project-metadata":
            return [xcodeProjectFact(manifest: manifest, item: item, url: url)]
        case "xcode-workspace", "xcode-workspace-metadata":
            return [xcodeWorkspaceFact(manifest: manifest, item: item, url: url)]
        case "plist":
            return [infoPlistFact(manifest: manifest, item: item, url: url)]
        case "cocoapods-metadata", "carthage-metadata":
            return [ecosystemFact(manifest: manifest, item: item, url: url)]
        default:
            return []
        }
    }

    private static func swiftPMManifestFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let text = (try? String(contentsOf: url, encoding: .utf8)) ?? ""
        let packageNames = regexCaptures(#"Package\s*\(\s*name\s*:\s*"([^"]+)""#, in: text)
        let targetNames = regexCaptures(#"(?:target|executableTarget|testTarget)\s*\(\s*name\s*:\s*"([^"]+)""#, in: text)
        let productNames = regexCaptures(#"(?:library|executable)\s*\(\s*name\s*:\s*"([^"]+)""#, in: text)
        let dependencyNames = regexCaptures(#"\.package\s*\("#, in: text)
        return makeFact(
            manifest: manifest,
            factType: "SwiftPackageManifestDeclared",
            ruleId: RuleIds.swiftPMManifest,
            evidenceTier: .tier3SyntaxOrTextual,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: safeLabel(packageNames.first ?? "Package.swift"),
            contractElement: item.relativePath,
            properties: [
                "coverageCeiling": "syntax-or-structural",
                "dependencyDeclarationCount": String(dependencyNames.count),
                "language": "swift",
                "manifestHash": fileHash(url),
                "packageName": safeLabel(packageNames.first),
                "parserMode": "token-line-scan",
                "productCount": String(productNames.count),
                "safeProductLabels": safeLabels(productNames),
                "safeTargetLabels": safeLabels(targetNames),
                "targetCount": String(targetNames.count)
            ]
        )
    }

    private static func swiftPMResolvedFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let metadata = PackageResolvedMetadata.read(url)
        return makeFact(
            manifest: manifest,
            factType: "SwiftPackageResolvedDeclared",
            ruleId: RuleIds.swiftPMResolved,
            evidenceTier: .tier2Structural,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: item.relativePath,
            contractElement: item.relativePath,
            properties: [
                "coverageCeiling": "syntax-or-structural",
                "identityHashSample": metadata.identityHashSample,
                "language": "swift",
                "parserStatus": metadata.parserStatus,
                "safeIdentityCount": String(metadata.safeIdentityCount),
                "schemaVersion": metadata.schemaVersion,
                "stateCount": String(metadata.stateCount),
                "unsafeLocationCount": String(metadata.unsafeLocationCount)
            ]
        )
    }

    private static func xcodeProjectFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let text = (try? String(contentsOf: url, encoding: .utf8)) ?? ""
        let productTypes = Set(regexCaptures(#"productType\s*=\s*([^;]+);"#, in: text).map(safeLabel)).sorted()
        let configurations = Set(regexCaptures(#"name\s*=\s*(Debug|Release|[A-Za-z0-9_.-]+);"#, in: text).map(safeLabel)).sorted()
        return makeFact(
            manifest: manifest,
            factType: "SwiftXcodeProjectDeclared",
            ruleId: RuleIds.xcodeProject,
            evidenceTier: .tier2Structural,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: item.relativePath,
            contractElement: item.relativePath,
            properties: [
                "buildConfigurationLabels": configurations.prefix(10).joined(separator: ","),
                "coverageCeiling": "syntax-or-structural",
                "language": "swift",
                "parseStatus": item.kind == "xcode-project-metadata" ? "narrow-line-scan" : "bundle-inventory",
                "productTypeLabels": productTypes.prefix(10).joined(separator: ","),
                "projectPathHash": sha256Hex(item.relativePath),
                "runtimeReachabilityProven": "false"
            ]
        )
    }

    private static func xcodeWorkspaceFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let text = (try? String(contentsOf: url, encoding: .utf8)) ?? ""
        let references = regexCaptures(#"location\s*=\s*"([^"]+)""#, in: text)
        let safeReferences = references.filter { $0.hasPrefix("group:") }.map { String($0.dropFirst("group:".count)) }
        let externalCount = references.count - safeReferences.count
        return makeFact(
            manifest: manifest,
            factType: "SwiftXcodeWorkspaceDeclared",
            ruleId: RuleIds.xcodeWorkspace,
            evidenceTier: .tier2Structural,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: item.relativePath,
            contractElement: item.relativePath,
            properties: [
                "coverageCeiling": "syntax-or-structural",
                "externalReferenceCount": String(max(0, externalCount)),
                "language": "swift",
                "parseStatus": item.kind == "xcode-workspace-metadata" ? "xml-line-scan" : "bundle-inventory",
                "referencedProjectCount": String(safeReferences.filter { $0.hasSuffix(".xcodeproj") }.count),
                "referenceHashSample": safeReferences.map { sha256Hex($0) }.sorted().prefix(3).joined(separator: ",")
            ]
        )
    }

    private static func infoPlistFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let metadata = PlistMetadata.read(url)
        return makeFact(
            manifest: manifest,
            factType: "SwiftInfoPlistDeclared",
            ruleId: RuleIds.infoPlist,
            evidenceTier: .tier2Structural,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: item.relativePath,
            contractElement: item.relativePath,
            properties: [
                "atsKeyPresent": metadata.atsKeyPresent ? "true" : "false",
                "bundleIdentifierHash": metadata.bundleIdentifierHash,
                "coverageCeiling": "syntax-or-structural",
                "language": "swift",
                "parseStatus": metadata.parseStatus,
                "permissionKeyCount": String(metadata.permissionKeyCount),
                "platformLabels": metadata.platformLabels,
                "urlSchemeCount": String(metadata.urlSchemeCount)
            ]
        )
    }

    private static func ecosystemFact(manifest: ScanManifest, item: InventoryItem, url: URL) -> CodeFact {
        let text = (try? String(contentsOf: url, encoding: .utf8)) ?? ""
        let ecosystem = item.kind == "cocoapods-metadata" ? "cocoapods" : "carthage"
        let identities = ecosystem == "cocoapods" ? parsePodIdentities(text) : parseCartfileIdentities(text)
        let unsafeCount = text.components(separatedBy: .whitespacesAndNewlines).filter { token in
            token.contains("://") || token.hasPrefix("/") || token.contains("@")
        }.count
        var properties = [
            "coverageCeiling": "syntax-or-structural",
            "dependencyIdentityCount": String(identities.count),
            "ecosystem": ecosystem,
            "identityHashSample": identities.map { sha256Hex($0) }.sorted().prefix(3).joined(separator: ","),
            "language": "swift",
            "metadataKind": URL(fileURLWithPath: item.relativePath).lastPathComponent,
            "parserStatus": "inventory-only",
            "unsafeValueCount": String(unsafeCount)
        ]
        if URL(fileURLWithPath: item.relativePath).lastPathComponent == "Podfile.lock",
           let checksumHash = podChecksumSectionHash(text) {
            properties["podChecksumSectionHash"] = checksumHash
        }
        return makeFact(
            manifest: manifest,
            factType: "SwiftEcosystemMetadataDeclared",
            ruleId: RuleIds.ecosystemMetadata,
            evidenceTier: .tier2Structural,
            filePath: item.relativePath,
            startLine: item.startLine,
            endLine: item.endLine,
            targetSymbol: item.relativePath,
            contractElement: item.relativePath,
            properties: properties
        )
    }
}

struct PackageResolvedMetadata {
    let schemaVersion: String
    let stateCount: Int
    let safeIdentityCount: Int
    let identityHashSample: String
    let unsafeLocationCount: Int
    let parserStatus: String

    static func read(_ url: URL) -> PackageResolvedMetadata {
        guard let data = try? Data(contentsOf: url),
              let root = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            return PackageResolvedMetadata(schemaVersion: "unknown", stateCount: 0, safeIdentityCount: 0, identityHashSample: "", unsafeLocationCount: 0, parserStatus: "malformed")
        }
        let version = (root["version"] as? Int).map(String.init) ?? "unknown"
        let pins = root["pins"] as? [[String: Any]] ?? ((root["object"] as? [String: Any])?["pins"] as? [[String: Any]] ?? [])
        var identities: [String] = []
        var unsafeLocations = 0
        for pin in pins {
            let identity = (pin["identity"] as? String) ?? (pin["package"] as? String)
            if let identity, isSafeLabel(identity) {
                identities.append(identity)
            }
            let location = pin["location"] as? String ?? ((pin["repositoryURL"] as? String) ?? "")
            if location.contains("://") || location.contains("@") || location.hasPrefix("/") {
                unsafeLocations += 1
            }
        }
        let supported = version == "1" || version == "2"
        return PackageResolvedMetadata(
            schemaVersion: version,
            stateCount: pins.count,
            safeIdentityCount: identities.count,
            identityHashSample: identities.map { sha256Hex($0) }.sorted().prefix(3).joined(separator: ","),
            unsafeLocationCount: unsafeLocations,
            parserStatus: supported ? "parsed" : "unsupported-schema"
        )
    }
}

struct PlistMetadata {
    let bundleIdentifierHash: String
    let permissionKeyCount: Int
    let urlSchemeCount: Int
    let atsKeyPresent: Bool
    let platformLabels: String
    let parseStatus: String

    static func read(_ url: URL) -> PlistMetadata {
        guard let data = try? Data(contentsOf: url),
              !isBinaryPlist(data),
              let plist = try? PropertyListSerialization.propertyList(from: data, options: [], format: nil),
              let dictionary = plist as? [String: Any] else {
            return PlistMetadata(bundleIdentifierHash: "", permissionKeyCount: 0, urlSchemeCount: 0, atsKeyPresent: false, platformLabels: "", parseStatus: "unsupported-or-malformed")
        }
        let bundleIdentifier = dictionary["CFBundleIdentifier"] as? String
        let permissions = dictionary.keys.filter { $0.hasPrefix("NS") && ($0.hasSuffix("UsageDescription") || $0.contains("Usage")) }
        let urlTypes = dictionary["CFBundleURLTypes"] as? [[String: Any]] ?? []
        let urlSchemeCount = urlTypes.compactMap { $0["CFBundleURLSchemes"] as? [String] }.reduce(0) { $0 + $1.count }
        var platformValues: [String] = []
        if let platformName = dictionary["DTPlatformName"] as? String {
            platformValues.append(platformName)
        }
        if let supportedPlatforms = dictionary["CFBundleSupportedPlatforms"] as? [String] {
            platformValues += supportedPlatforms
        }
        let platformLabels = platformValues.map(safeLabel).sorted().joined(separator: ",")
        return PlistMetadata(
            bundleIdentifierHash: bundleIdentifier.map { sha256Hex($0) } ?? "",
            permissionKeyCount: permissions.count,
            urlSchemeCount: urlSchemeCount,
            atsKeyPresent: dictionary.keys.contains("NSAppTransportSecurity"),
            platformLabels: platformLabels,
            parseStatus: "parsed-xml"
        )
    }
}

enum OutputWriter {
    static func validateOutputPath(scanRoot: URL, gitRoot: URL, outputPath: URL) throws {
        let output = normalizedDirectoryPath(outputPath)
        let scanRootPath = normalizedDirectoryPath(scanRoot)
        let gitRootPath = normalizedDirectoryPath(gitRoot)
        guard output != "/" else {
            throw ScanError.invalidArguments("--out must not be the filesystem root")
        }
        guard output != scanRootPath, output != gitRootPath else {
            throw ScanError.invalidArguments("--out must not be the scan root or git root")
        }
        guard !isAncestor(output, of: scanRootPath), !isAncestor(output, of: gitRootPath) else {
            throw ScanError.invalidArguments("--out must not be an ancestor of the scan root or git root")
        }
    }

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
        for fact in facts {
            output += String(decoding: try encoder.encode(fact), as: UTF8.self) + "\n"
        }
        try output.write(to: url, atomically: true, encoding: .utf8)
    }

    private static func report(manifest: ScanManifest, facts: [CodeFact], inventory: [InventoryItem]) -> String {
        let byType = count(facts.map(\.factType))
        let byRule = count(facts.map(\.ruleId))
        let byTier = count(facts.map(\.evidenceTier.rawValue))
        let byRelationshipKind = count(facts.filter { $0.factType == "SymbolRelationship" }.compactMap { $0.properties["relationshipKind"] })
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
            "This Swift v0 adapter emits deterministic static inventory, metadata, SwiftSyntax declaration, call candidate, construction candidate, and direct source-local symbol relationship evidence. Coverage is reduced; absence of evidence is not evidence of absence.",
            "",
            "## Fact Counts By Type",
            ""
        ]
        lines += markdownTable(byType)
        lines += ["", "## Fact Counts By Rule", ""]
        lines += markdownTable(byRule)
        lines += ["", "## Fact Counts By Evidence Tier", ""]
        lines += markdownTable(byTier)
        if !byRelationshipKind.isEmpty {
            lines += ["", "## Symbol Relationships By Kind", ""]
            lines += markdownTable(byRelationshipKind)
        }
        let dependencyFacts = facts.filter { $0.factType == "SwiftDependencyDeclared" || $0.factType == "SwiftDependencyLockfileEntryDeclared" }
        if !dependencyFacts.isEmpty {
            lines += ["", "## Swift Dependency Metadata", ""]
            lines += [
                "### By Package Manager",
                ""
            ]
            lines += markdownTable(count(dependencyFacts.compactMap { $0.properties["packageManager"] }))
            lines += ["", "### By Metadata Kind", ""]
            lines += markdownTable(count(dependencyFacts.compactMap { $0.properties["sourceMetadataKind"] }))
            lines += ["", "### By Identity Status", ""]
            lines += markdownTable(count(dependencyFacts.compactMap { $0.properties["dependencyIdentityStatus"] }))
        }
        let httpFacts = facts.filter { $0.factType == "HttpCallDetected" && $0.ruleId.hasPrefix("swift.http.") }
        if !httpFacts.isEmpty {
            lines += ["", "## Swift HTTP/API Client Surfaces", ""]
            lines += ["### By Client Kind", ""]
            lines += markdownTable(count(httpFacts.compactMap { $0.properties["swiftClientKind"] }))
            lines += ["", "### By Framework", ""]
            lines += markdownTable(count(httpFacts.compactMap { $0.properties["framework"] }))
            lines += ["", "### By Method Status", ""]
            lines += markdownTable(count(httpFacts.compactMap { $0.properties["methodStatus"] }))
            lines += ["", "### By Path Status", ""]
            lines += markdownTable(count(httpFacts.compactMap { $0.properties["pathStatus"] }))
        }
        let uiFacts = facts.filter { $0.ruleId.hasPrefix("swift.ui.") && $0.factType != "AnalysisGap" }
        let uiGaps = facts.filter { $0.factType == "AnalysisGap" && $0.ruleId == RuleIds.swiftUiAnalysisGap }
        if !uiFacts.isEmpty || !uiGaps.isEmpty {
            lines += ["", "## Swift UI Static Surfaces", ""]
            lines += ["Static source evidence only; these rows do not prove rendered screens, runtime navigation, user actions, storyboard/nib wiring, or impact.", ""]
            if !uiFacts.isEmpty {
                lines += ["### By UI Framework", ""]
                lines += markdownTable(count(uiFacts.compactMap { $0.properties["uiFramework"] }))
                lines += ["", "### By Surface Kind", ""]
                lines += markdownTable(count(uiFacts.compactMap { $0.properties["surfaceKind"] }))
                lines += ["", "### By UI Role", ""]
                lines += markdownTable(count(uiFacts.compactMap { $0.properties["uiRole"] }))
                lines += ["", "### By Rule", ""]
                lines += markdownTable(count(uiFacts.map(\.ruleId)))
            }
            if !uiGaps.isEmpty {
                lines += ["", "### UI Gap Kinds", ""]
                lines += markdownTable(count(uiGaps.compactMap { $0.properties["gapKind"] }))
            }
        }
        let storageFacts = facts.filter { fact in
            fact.ruleId.hasPrefix("swift.storage.") && fact.factType != "AnalysisGap" && fact.factType != "FileInventoried"
        }
        let storageGaps = facts.filter { $0.factType == "AnalysisGap" && $0.ruleId == RuleIds.swiftStorageAnalysisGap }
        if !storageFacts.isEmpty || !storageGaps.isEmpty {
            lines += ["", "## Swift Storage/Data Static Surfaces", ""]
            lines += ["Static source and checked-in metadata evidence only; these rows do not prove runtime persistence, database existence, query execution, stored values, migrations, Keychain item presence, Realm live schema, or impact.", ""]
            if !storageFacts.isEmpty {
                lines += ["### By Framework Family", ""]
                lines += markdownTable(count(storageFacts.compactMap { $0.properties["frameworkFamily"] }))
                lines += ["", "### By Fact Type", ""]
                lines += markdownTable(count(storageFacts.map(\.factType)))
                lines += ["", "### By Rule", ""]
                lines += markdownTable(count(storageFacts.map(\.ruleId)))
            }
            if !storageGaps.isEmpty {
                lines += ["", "### Storage/Data Gap Kinds", ""]
                lines += markdownTable(count(storageGaps.compactMap { $0.properties["gapKind"] }))
            }
        }
        let diagnosticFacts = facts.filter { $0.factType == "SwiftToolchainDiagnostic" || ($0.factType == "AnalysisGap" && ($0.ruleId == RuleIds.reducedCoverageGap || $0.ruleId == RuleIds.toolchainDiagnostic)) }
        if !diagnosticFacts.isEmpty {
            lines += ["", "## Swift Diagnostics And Coverage", ""]
            let toolchainFacts = diagnosticFacts.filter { $0.factType == "SwiftToolchainDiagnostic" }
            if !toolchainFacts.isEmpty {
                lines += ["### Toolchain Status", ""]
                lines += markdownTable(count(toolchainFacts.compactMap { fact in
                    guard let name = fact.properties["toolName"], let status = fact.properties["toolStatus"] else { return nil }
                    return "\(name):\(status)"
                }))
            }
            let unsupportedGaps = diagnosticFacts.filter { $0.factType == "AnalysisGap" }
            if !unsupportedGaps.isEmpty {
                lines += ["", "### Reduced-Coverage Gap Kinds", ""]
                lines += markdownTable(count(unsupportedGaps.compactMap { $0.properties["gapKind"] }))
            }
        }
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
            "- Static syntax and metadata evidence only; no build, package resolution, simulator, device, runtime, UI navigation, network reachability, storage access, deployment, or production-use proof.",
            "- SourceKit, SwiftPM semantic loading, Xcode semantic loading, Objective-C bridging, macros, result builders, protocol dispatch, property wrappers, generated-code semantics, and compiler-proven Swift relationship semantics are future slices.",
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
            "TraceMap Swift adapter scan",
            "scanId=\(manifest.scanId)",
            "repoNameHash=\(sha256Hex(manifest.repoName))",
            "commitSha=\(manifest.commitSha)",
            "gitRootHash=\(manifest.gitRootHash ?? "")",
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
        sql += try insertManifestSQL(manifest)
        for fact in facts.sorted(by: { $0.factId < $1.factId }) {
            sql += try insertFactSQL(fact)
            sql += insertDerivedRowsSQL(fact)
        }
        _ = try runProcess(executable: "/usr/bin/sqlite3", arguments: [tmp.path], input: sql, timeoutSeconds: 120)
        try? FileManager.default.removeItem(at: path)
        try FileManager.default.moveItem(at: tmp, to: path)
    }

    private static func insertManifestSQL(_ manifest: ScanManifest) throws -> String {
        let manifestJson = try jsonString(manifest, pretty: false)
        return """
        insert into scan_manifest (scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
        values (\(q(manifest.scanId)), \(q(manifest.repoName)), \(q(manifest.commitSha)), \(q(manifest.scannerVersion)), \(q(manifest.scannedAt)), \(q(manifest.analysisLevel)), \(q(manifest.buildStatus)), \(q(manifestJson)));

        """
    }

    private static func insertFactSQL(_ fact: CodeFact) throws -> String {
        let propsJson = try jsonString(fact.properties, pretty: false)
        return """
        insert into facts (fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier, source_symbol, target_symbol, contract_element, file_path, start_line, end_line, snippet_hash, properties_json)
        values (\(q(fact.factId)), \(q(fact.scanId)), \(q(fact.repo)), \(q(fact.commitSha)), null, \(q(fact.factType)), \(q(fact.ruleId)), \(q(fact.evidenceTier.rawValue)), \(q(fact.sourceSymbol)), \(q(fact.targetSymbol)), \(q(fact.contractElement)), \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine), null, \(q(propsJson)));

        """
    }

    private static func insertDerivedRowsSQL(_ fact: CodeFact) -> String {
        switch fact.factType {
        case "SwiftDeclarationDeclared":
            guard let symbolId = fact.targetSymbol else { return "" }
            let occurrenceId = "swift-occurrence-" + sha256Hex([fact.scanId, fact.factId, symbolId, "declaration"].joined(separator: "|"), length: 32)
            return """
            insert into symbols (scan_id, symbol_id, language, symbol_kind, display_name, assembly_name, assembly_version, containing_symbol_id)
            values (\(q(fact.scanId)), \(q(symbolId)), 'swift', \(q(fact.properties["declarationKind"])), \(q(fact.properties["displaySignature"])), \(q(fact.properties["moduleName"])), null, \(q(fact.sourceSymbol)));
            insert into symbol_occurrences (occurrence_id, scan_id, symbol_id, fact_id, role, occurrence_kind, evidence_tier, rule_id, file_path, start_line, end_line)
            values (\(q(occurrenceId)), \(q(fact.scanId)), \(q(symbolId)), \(q(fact.factId)), 'declared', 'declaration', \(q(fact.evidenceTier.rawValue)), \(q(fact.ruleId)), \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine));
            insert into fact_symbols (fact_id, scan_id, symbol_id, role)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(symbolId)), 'declared');

            """
        case "SwiftCallCandidate":
            guard let callee = fact.targetSymbol else { return "" }
            var sql = """
            insert into call_edges (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, callee_symbol, callee_assembly_name, callee_assembly_version, callee_containing_type, call_kind, file_path, start_line, end_line)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(fact.repo)), \(q(fact.commitSha)), \(q(fact.evidenceTier.rawValue)), \(q(fact.ruleId)), \(q(fact.sourceSymbol)), null, null, \(q(callee)), null, null, null, \(q(fact.properties["callKind"])), \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine));

            """
            if let caller = fact.sourceSymbol, !caller.isEmpty {
                sql += """
                insert into fact_symbols (fact_id, scan_id, symbol_id, role)
                values (\(q(fact.factId)), \(q(fact.scanId)), \(q(caller)), 'caller');

                """
            }
            return sql
        case "SwiftConstructionCandidate":
            guard let created = fact.targetSymbol else { return "" }
            return """
            insert into object_creations (fact_id, scan_id, repo, commit_sha, evidence_tier, rule_id, caller_symbol, caller_assembly_name, caller_assembly_version, created_type, created_type_assembly_name, created_type_assembly_version, constructor_symbol, assigned_to, file_path, start_line, end_line)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(fact.repo)), \(q(fact.commitSha)), \(q(fact.evidenceTier.rawValue)), \(q(fact.ruleId)), \(q(fact.sourceSymbol)), null, null, \(q(created)), null, null, null, null, \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine));

            """
        case "SymbolRelationship":
            guard let source = fact.properties["sourceSymbolId"],
                  let target = fact.properties["targetSymbolId"],
                  let relationshipKind = fact.properties["relationshipKind"] else {
                return ""
            }
            return """
            insert or ignore into symbol_relationships (relationship_id, scan_id, source_symbol_id, target_symbol_id, relationship_kind, rule_id, evidence_tier, file_path, start_line, end_line)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(source)), \(q(target)), \(q(relationshipKind)), \(q(fact.ruleId)), \(q(fact.evidenceTier.rawValue)), \(q(fact.evidence.filePath)), \(fact.evidence.startLine), \(fact.evidence.endLine));
            insert into fact_symbols (fact_id, scan_id, symbol_id, role)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(source)), 'source');
            insert into fact_symbols (fact_id, scan_id, symbol_id, role)
            values (\(q(fact.factId)), \(q(fact.scanId)), \(q(target)), 'target');

            """
        default:
            return ""
        }
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
        create index ix_symbols_display on symbols(language, display_name);
        create index ix_symbol_occurrences_symbol on symbol_occurrences(scan_id, symbol_id);
        create index ix_fact_symbols_symbol on fact_symbols(scan_id, symbol_id);
        create index ix_symbol_relationships_source on symbol_relationships(scan_id, source_symbol_id);
        create index ix_symbol_relationships_target on symbol_relationships(scan_id, target_symbol_id);
        create index ix_symbol_relationships_kind on symbol_relationships(relationship_kind);

        """
    }
}

enum Toolchain {
    static func diagnostics(inventory: [InventoryItem]) -> ToolchainExtraction {
        let probes = requiredProbes(inventory: inventory)
        let diagnostics = probes.map { probe -> ToolchainDiagnostic in
            let status = toolStatus(probe: probe)
            return ToolchainDiagnostic(
                toolName: probe.name,
                category: probe.category,
                status: status,
                requiredFor: probe.requiredFor,
                gapKind: status == "available" ? nil : probe.gapKind(status: status),
                message: probe.message(status: status)
            )
        }
        let gaps = diagnostics.compactMap { diagnostic -> CoverageGap? in
            guard let gapKind = diagnostic.gapKind else { return nil }
            return CoverageGap(kind: gapKind, ruleId: RuleIds.toolchainDiagnostic, message: diagnostic.message)
        }
        return ToolchainExtraction(diagnostics: diagnostics, gaps: gaps.sorted { $0.kind < $1.kind })
    }

    static func swiftAvailable() -> Bool {
        if env("TRACEMAP_SWIFT_DISABLE_TOOLCHAIN") == "1" {
            return false
        }
        return toolStatus(probe: ToolProbe(name: "swift", category: "swift", requiredFor: "swift-source", executable: "/usr/bin/env", arguments: ["swift", "--version"], unavailableGapKind: "swift-toolchain-unavailable", timeoutGapKind: "swift-toolchain-timeout", unavailableMessage: "Swift toolchain probe unavailable; file-based inventory continued.", timeoutMessage: "Swift toolchain probe timed out; file-based inventory continued.")) == "available"
    }

    private static func requiredProbes(inventory: [InventoryItem]) -> [ToolProbe] {
        var probes: [ToolProbe] = []
        if inventory.contains(where: { $0.kind == "swift-source" || $0.kind == "swiftpm-manifest" || $0.kind == "swiftpm-resolved" }) {
            probes.append(ToolProbe(name: "swift", category: "swift", requiredFor: "swift-source", executable: "/usr/bin/env", arguments: ["swift", "--version"], unavailableGapKind: "swift-toolchain-unavailable", timeoutGapKind: "swift-toolchain-timeout", unavailableMessage: "Swift toolchain probe unavailable; file-based inventory continued.", timeoutMessage: "Swift toolchain probe timed out; file-based inventory continued."))
            probes.append(ToolProbe(name: "sourcekit-lsp", category: "sourcekit", requiredFor: "semantic-enrichment", executable: "/usr/bin/env", arguments: ["sourcekit-lsp", "--help"], unavailableGapKind: "swift-sourcekit-unavailable", timeoutGapKind: "swift-sourcekit-timeout", unavailableMessage: "SourceKit/sourcekit-lsp probe unavailable; semantic enrichment remains unavailable.", timeoutMessage: "SourceKit/sourcekit-lsp probe timed out; semantic enrichment remains unavailable."))
        }
        if inventory.contains(where: { $0.kind.hasPrefix("xcode-") || ["plist", "storyboard", "xib"].contains($0.kind) }) {
            probes.append(ToolProbe(name: "xcodebuild", category: "xcode", requiredFor: "xcode-metadata", executable: "/usr/bin/env", arguments: ["xcodebuild", "-version"], unavailableGapKind: "xcodebuild-toolchain-unavailable", timeoutGapKind: "xcodebuild-toolchain-timeout", unavailableMessage: "Xcode command-line probe unavailable; Xcode metadata remains checked-in inventory only.", timeoutMessage: "Xcode command-line probe timed out; Xcode metadata remains checked-in inventory only."))
        }
        if inventory.contains(where: { $0.kind == "cocoapods-metadata" }) {
            probes.append(ToolProbe(name: "pod", category: "cocoapods", requiredFor: "cocoapods-metadata", executable: "/usr/bin/env", arguments: ["pod", "--version"], unavailableGapKind: "cocoapods-toolchain-unavailable", timeoutGapKind: "cocoapods-toolchain-timeout", unavailableMessage: "CocoaPods probe unavailable; CocoaPods metadata remains checked-in inventory only.", timeoutMessage: "CocoaPods probe timed out; CocoaPods metadata remains checked-in inventory only."))
        }
        if inventory.contains(where: { $0.kind == "carthage-metadata" }) {
            probes.append(ToolProbe(name: "carthage", category: "carthage", requiredFor: "carthage-metadata", executable: "/usr/bin/env", arguments: ["carthage", "version"], unavailableGapKind: "carthage-toolchain-unavailable", timeoutGapKind: "carthage-toolchain-timeout", unavailableMessage: "Carthage probe unavailable; Carthage metadata remains checked-in inventory only.", timeoutMessage: "Carthage probe timed out; Carthage metadata remains checked-in inventory only."))
        }
        return probes.sorted { [$0.category, $0.name].joined(separator: "|") < [$1.category, $1.name].joined(separator: "|") }
    }

    private static func toolStatus(probe: ToolProbe) -> String {
        if let override = statusOverride(for: probe.name) {
            return override
        }
        if env("TRACEMAP_SWIFT_DISABLE_TOOLCHAIN") == "1" {
            return "not-found"
        }
        do {
            _ = try runProcess(executable: probe.executable, arguments: probe.arguments, timeoutSeconds: 5)
            return "available"
        } catch {
            if String(describing: error).contains("timed out") {
                return "timeout"
            }
            let description = String(describing: error)
            return description.contains("No such file") || description.contains("not found") ? "not-found" : "error-redacted"
        }
    }

    private static func statusOverride(for tool: String) -> String? {
        guard let raw = env("TRACEMAP_SWIFT_TOOL_STATUS_OVERRIDES") else { return nil }
        let allowed = Set(["available", "not-found", "timeout", "unsupported", "not-probed", "error-redacted"])
        for entry in raw.split(separator: ",") {
            let parts = entry.split(separator: "=", maxSplits: 1).map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            guard parts.count == 2, parts[0] == tool, allowed.contains(parts[1]) else { continue }
            return parts[1]
        }
        return nil
    }
}

private struct ToolProbe {
    let name: String
    let category: String
    let requiredFor: String
    let executable: String
    let arguments: [String]
    let unavailableGapKind: String
    let timeoutGapKind: String
    let unavailableMessage: String
    let timeoutMessage: String

    func gapKind(status: String) -> String {
        status == "timeout" ? timeoutGapKind : unavailableGapKind
    }

    func message(status: String) -> String {
        status == "timeout" ? timeoutMessage : unavailableMessage
    }
}

func env(_ name: String) -> String? {
    guard let value = getenv(name) else { return nil }
    return String(cString: value)
}

func runProcess(executable: String, arguments: [String], input: String? = nil, timeoutSeconds: TimeInterval = 30) throws -> String {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    let stdout = Pipe()
    let stderr = Pipe()
    process.standardOutput = stdout
    process.standardError = stderr
    let outputGroup = DispatchGroup()
    let outputQueue = DispatchQueue.global(qos: .utility)
    let outBuffer = ProcessOutputBuffer()
    let errBuffer = ProcessOutputBuffer()
    outputGroup.enter()
    outputQueue.async {
        outBuffer.set(stdout.fileHandleForReading.readDataToEndOfFile())
        outputGroup.leave()
    }
    outputGroup.enter()
    outputQueue.async {
        errBuffer.set(stderr.fileHandleForReading.readDataToEndOfFile())
        outputGroup.leave()
    }
    if let input {
        let stdin = Pipe()
        process.standardInput = stdin
        try process.run()
        stdin.fileHandleForWriting.write(Data(input.utf8))
        try stdin.fileHandleForWriting.close()
    } else {
        try process.run()
    }
    let deadline = Date().addingTimeInterval(timeoutSeconds)
    while process.isRunning && Date() < deadline {
        Thread.sleep(forTimeInterval: 0.05)
    }
    if process.isRunning {
        process.terminate()
        process.waitUntilExit()
        _ = outputGroup.wait(timeout: .now() + 5)
        throw ScanError.io("process timed out after \(Int(timeoutSeconds))s: \(executable) \(arguments.joined(separator: " "))")
    }
    _ = outputGroup.wait(timeout: .now() + 5)
    let out = String(decoding: outBuffer.data, as: UTF8.self)
    let err = String(decoding: errBuffer.data, as: UTF8.self)
    guard process.terminationStatus == 0 else {
        throw ScanError.io(err.trimmed().isEmpty ? "process failed: \(executable) \(arguments.joined(separator: " "))" : err.trimmed())
    }
    return out
}

final class ProcessOutputBuffer: @unchecked Sendable {
    private let lock = NSLock()
    private var value = Data()

    var data: Data {
        lock.lock()
        defer { lock.unlock() }
        return value
    }

    func set(_ data: Data) {
        lock.lock()
        value = data
        lock.unlock()
    }
}

func writeJSON<T: Encodable>(_ value: T, to url: URL, pretty: Bool) throws {
    var data = try stableEncoder(pretty: pretty).encode(value)
    data.append(0x0a)
    try data.write(to: url)
}

func stableEncoder(pretty: Bool) -> JSONEncoder {
    let encoder = JSONEncoder()
    encoder.outputFormatting = pretty ? [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes] : [.sortedKeys, .withoutEscapingSlashes]
    return encoder
}

func jsonString<T: Encodable>(_ value: T, pretty: Bool) throws -> String {
    String(decoding: try stableEncoder(pretty: pretty).encode(value), as: UTF8.self)
}

func q(_ value: String?) -> String {
    guard let value else { return "null" }
    return "'" + value.replacingOccurrences(of: "'", with: "''") + "'"
}

func sha256Hex(_ input: String, length: Int? = nil) -> String {
    let hex = PortableSHA256.hash(Data(input.utf8)).map { String(format: "%02x", $0) }.joined()
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
    if basePath == "/" {
        return String(path.dropFirst())
    }
    guard path.hasPrefix(basePath + "/") else { return URL(fileURLWithPath: path).lastPathComponent }
    return String(path.dropFirst(basePath.count + 1))
}

func normalizeRelativePath(_ value: String) -> String {
    value.replacingOccurrences(of: "\\", with: "/").trimmingCharacters(in: CharacterSet(charactersIn: "/"))
}

func sourceRoot(for path: String) -> String {
    let segments = path.split(separator: "/").map(String.init)
    guard !segments.isEmpty else { return "." }
    if segments.count >= 2, segments[0] == "Sources" || segments[0] == "Tests" {
        return "\(segments[0])/\(segments[1])"
    }
    return segments[0]
}

func rootKind(for path: String) -> String {
    let segments = path.split(separator: "/").map(String.init)
    if segments.first == "Sources" { return "source" }
    if segments.first == "Tests" { return "test" }
    if segments.contains("Generated") || path.hasSuffix(".generated.swift") { return "generated" }
    if segments.contains("vendor") || segments.contains("Pods") || segments.contains("Carthage") { return "vendor" }
    return "custom"
}

func regexCaptures(_ pattern: String, in text: String) -> [String] {
    guard let regex = try? NSRegularExpression(pattern: pattern) else { return [] }
    let range = NSRange(text.startIndex..<text.endIndex, in: text)
    return regex.matches(in: text, range: range).compactMap { match in
        let captureIndex = match.numberOfRanges > 1 ? 1 : 0
        guard let range = Range(match.range(at: captureIndex), in: text) else { return nil }
        return String(text[range])
    }
}

func regexMatches(_ pattern: String, in text: String, dotMatchesLineSeparators: Bool = false) -> [NSTextCheckingResult] {
    let options: NSRegularExpression.Options = dotMatchesLineSeparators ? [.dotMatchesLineSeparators] : []
    guard let regex = try? NSRegularExpression(pattern: pattern, options: options) else { return [] }
    let range = NSRange(text.startIndex..<text.endIndex, in: text)
    return regex.matches(in: text, range: range)
}

func firstMatch(_ pattern: String, in text: String) -> NSTextCheckingResult? {
    regexMatches(pattern, in: text).first
}

func firstCapture(_ pattern: String, in text: String) -> String? {
    guard let match = firstMatch(pattern, in: text) else { return nil }
    return capture(match, 1, in: text)
}

func capture(_ match: NSTextCheckingResult, _ index: Int, in text: String) -> String? {
    guard index < match.numberOfRanges,
          let range = Range(match.range(at: index), in: text) else {
        return nil
    }
    return String(text[range])
}

func maskSwiftCommentsAndStringLiterals(_ text: String) -> String {
    let scalars = Array(text.unicodeScalars)
    var output: [UnicodeScalar] = []
    output.reserveCapacity(scalars.count)
    var index = 0
    var inLineComment = false
    var blockCommentDepth = 0
    var inString = false
    var escapingString = false

    func isSlash(_ offset: Int = 0) -> Bool {
        index + offset < scalars.count && scalars[index + offset] == "/"
    }

    func isAsterisk(_ offset: Int = 0) -> Bool {
        index + offset < scalars.count && scalars[index + offset] == "*"
    }

    func appendMasked(_ scalar: UnicodeScalar) {
        if scalar == "\n" {
            output.append(scalar)
            return
        }
        for _ in scalar.utf16 {
            output.append(" ")
        }
    }

    while index < scalars.count {
        let scalar = scalars[index]
        if inLineComment {
            if scalar == "\n" {
                inLineComment = false
                output.append(scalar)
            } else {
                appendMasked(scalar)
            }
            index += 1
            continue
        }

        if blockCommentDepth > 0 {
            if isSlash() && isAsterisk(1) {
                output.append(" ")
                output.append(" ")
                blockCommentDepth += 1
                index += 2
                continue
            }
            if isAsterisk() && isSlash(1) {
                output.append(" ")
                output.append(" ")
                blockCommentDepth -= 1
                index += 2
                continue
            }
            appendMasked(scalar)
            index += 1
            continue
        }

        if inString {
            if scalar == "\n" {
                inString = false
                escapingString = false
                output.append(scalar)
            } else if escapingString {
                escapingString = false
                appendMasked(scalar)
            } else if scalar == "\\" {
                escapingString = true
                output.append(" ")
            } else if scalar == "\"" {
                inString = false
                output.append(" ")
            } else {
                appendMasked(scalar)
            }
            index += 1
            continue
        }

        if isSlash() && isSlash(1) {
            output.append(" ")
            output.append(" ")
            inLineComment = true
            index += 2
            continue
        }

        if isSlash() && isAsterisk(1) {
            output.append(" ")
            output.append(" ")
            blockCommentDepth = 1
            index += 2
            continue
        }

        if scalar == "\"" {
            inString = true
            output.append(" ")
            index += 1
            continue
        }

        output.append(scalar)
        index += 1
    }

    return String(String.UnicodeScalarView(output))
}

func lineNumber(atUTF16Offset offset: Int, in text: String) -> Int {
    let bounded = max(0, min(offset, text.utf16.count))
    let index = String.Index(utf16Offset: bounded, in: text)
    return text[..<index].reduce(1) { count, character in count + (character == "\n" ? 1 : 0) }
}

func utf8Offset(atUTF16Offset offset: Int, in text: String) -> Int {
    let bounded = max(0, min(offset, text.utf16.count))
    let index = String.Index(utf16Offset: bounded, in: text)
    guard let utf8Index = index.samePosition(in: text.utf8) else {
        return bounded
    }
    return text.utf8.distance(from: text.utf8.startIndex, to: utf8Index)
}

func urlIdentityCandidate(_ value: String) -> String? {
    let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty else { return nil }
    let withoutQuery = trimmed.split(separator: "?", maxSplits: 1).first.map(String.init) ?? trimmed
    var last = withoutQuery.split(separator: "/").last.map(String.init) ?? withoutQuery
    if last.hasSuffix(".git") {
        last = String(last.dropLast(4))
    }
    return last.nilIfEmpty
}

struct ParsedURLSurface {
    let normalizedPathKey: String
    let host: String?
    let queryStatus: String
}

func parseURLSurface(_ value: String) -> ParsedURLSurface? {
    let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !trimmed.isEmpty, !trimmed.contains("\\("), !trimmed.contains("+") else { return nil }
    var host: String?
    var path = trimmed
    var queryStatus = "unknown"
    if let components = URLComponents(string: trimmed), components.scheme != nil || components.host != nil {
        host = components.host
        path = components.path.isEmpty ? "/" : components.path
        queryStatus = components.query == nil ? "absent" : "present-omitted"
    } else if trimmed.hasPrefix("/") {
        let split = trimmed.split(separator: "?", maxSplits: 1, omittingEmptySubsequences: false)
        path = String(split.first ?? "/")
        queryStatus = split.count > 1 ? "present-omitted" : "absent"
    } else {
        return nil
    }
    guard let normalized = normalizeHTTPPath(path) else { return nil }
    return ParsedURLSurface(normalizedPathKey: normalized, host: host, queryStatus: queryStatus)
}

func normalizeHTTPPath(_ value: String) -> String? {
    let collapsed = value.replacingOccurrences(of: #"/+"#, with: "/", options: .regularExpression)
    let trimmedSlash = collapsed.count > 1 ? collapsed.trimmingCharacters(in: CharacterSet(charactersIn: "/")) : collapsed
    let rawSegments = trimmedSlash == "/" ? [] : trimmedSlash.split(separator: "/", omittingEmptySubsequences: true).map(String.init)
    var segments: [String] = []
    for raw in rawSegments {
        let lower = raw.lowercased()
        if lower.hasPrefix("{") && lower.hasSuffix("}") {
            segments.append("{}")
        } else if lower.hasPrefix(":") {
            segments.append("{}")
        } else if lower.range(of: #"^\d+$"#, options: .regularExpression) != nil || lower.range(of: #"^[0-9a-f]{8,}$"#, options: .regularExpression) != nil || lower.range(of: #"^[0-9a-f-]{32,}$"#, options: .regularExpression) != nil {
            segments.append("{}")
        } else {
            guard isSafePathSegment(lower) else { return nil }
            segments.append(lower)
        }
    }
    return "/" + segments.joined(separator: "/")
}

func isSafePathSegment(_ segment: String) -> Bool {
    guard !segment.isEmpty, segment.count <= 48 else { return false }
    if segment.contains(".") || segment.contains("@") || segment.contains("\\") { return false }
    if segment.range(of: #"^\d{1,3}(\.\d{1,3}){3}$"#, options: .regularExpression) != nil { return false }
    if segment.range(of: #"^[a-z0-9-]+\.[a-z]{2,}$"#, options: .regularExpression) != nil { return false }
    if segment.range(of: #"(secret|token|password|apikey|api-key|credential|bearer)"#, options: .regularExpression) != nil { return false }
    return segment.range(of: #"^[a-z0-9_-]+$"#, options: .regularExpression) != nil
}

func jsonKeyOffsets(_ keys: [String], in text: String) -> [Int] {
    let keyPattern = keys.map(NSRegularExpression.escapedPattern).joined(separator: "|")
    return regexMatches(#""\#(keyPattern)"\s*:"#, in: text).map(\.range.location)
}

func safeSection(_ value: String) -> String? {
    isSafeLabel(value.replacingOccurrences(of: " ", with: "-")) ? value : nil
}

func isSemVer(_ value: String) -> Bool {
    value.range(of: #"^[0-9]+(\.[0-9]+){1,2}([.-][A-Za-z0-9]+)?$"#, options: .regularExpression) != nil
}

func parsePodIdentities(_ text: String) -> [String] {
    let podNames = regexCaptures(#"\bpod\s+['"]([^'"]+)['"]"#, in: text)
    let lockNames = regexCaptures(#"(?m)^\s{2}-\s+([A-Za-z0-9_.+-]+)"#, in: text)
    return Array(Set((podNames + lockNames).filter(isSafeLabel))).sorted()
}

func podChecksumSectionHash(_ text: String) -> String? {
    var section = ""
    var names: Set<String> = []
    for line in text.components(separatedBy: "\n") {
        if let heading = firstCapture(#"^([A-Z][A-Z0-9 _-]+):\s*$"#, in: line) {
            section = heading
            continue
        }
        guard section == "SPEC CHECKSUMS",
              let name = firstCapture(#"^\s{2}([A-Za-z0-9_.+-]+):"#, in: line) else {
            continue
        }
        names.insert(name)
    }
    guard !names.isEmpty else { return nil }
    return sha256Hex(names.sorted().joined(separator: "\n"))
}

func parseCartfileIdentities(_ text: String) -> [String] {
    let repositories = regexCaptures(#"(?m)^\s*(?:github|git|binary)\s+["']([^"']+)["']"#, in: text)
    return Array(Set(repositories.map { value in
        value.split(separator: "/").last.map(String.init) ?? value
    }.filter(isSafeLabel))).sorted()
}

func isSafeLabel(_ value: String) -> Bool {
    value.range(of: #"^[A-Za-z0-9_.+-]{1,80}$"#, options: .regularExpression) != nil
}

func safeLabel(_ value: String) -> String {
    isSafeLabel(value) ? value : "sha256:\(sha256Hex(value, length: 24))"
}

func safeLabel(_ value: String?) -> String {
    guard let value, !value.isEmpty else { return "" }
    return safeLabel(value)
}

func safeLabels(_ values: [String]) -> String {
    values.map(safeLabel).sorted().prefix(20).joined(separator: ",")
}

func fileHash(_ url: URL) -> String {
    guard let data = try? Data(contentsOf: url) else { return "" }
    return PortableSHA256.hash(data).map { String(format: "%02x", $0) }.joined()
}

func isBinaryPlist(_ data: Data) -> Bool {
    data.starts(with: Data("bplist".utf8))
}

func stripSwiftCommentsAndStringLiterals(_ text: String) -> String {
    var output = ""
    var index = text.startIndex
    var inLineComment = false
    var inBlockComment = false
    var inString = false
    while index < text.endIndex {
        let current = text[index]
        let nextIndex = text.index(after: index)
        let next = nextIndex < text.endIndex ? text[nextIndex] : "\0"
        if inLineComment {
            if current == "\n" {
                inLineComment = false
                output.append("\n")
            } else {
                output.append(" ")
            }
        } else if inBlockComment {
            if current == "*" && next == "/" {
                inBlockComment = false
                output.append("  ")
                index = nextIndex
            } else {
                output.append(current == "\n" ? "\n" : " ")
            }
        } else if inString {
            if current == "\\" {
                output.append(" ")
                if nextIndex < text.endIndex {
                    output.append(" ")
                    index = nextIndex
                }
            } else if current == "\"" {
                inString = false
                output.append(" ")
            } else {
                output.append(current == "\n" ? "\n" : " ")
            }
        } else if current == "/" && next == "/" {
            inLineComment = true
            output.append("  ")
            index = nextIndex
        } else if current == "/" && next == "*" {
            inBlockComment = true
            output.append("  ")
            index = nextIndex
        } else if current == "\"" {
            inString = true
            output.append(" ")
        } else {
            output.append(current)
        }
        index = text.index(after: index)
    }
    return output
}

func lineCount(_ url: URL) -> Int {
    guard let text = try? String(contentsOf: url, encoding: .utf8), !text.isEmpty else { return 1 }
    return max(1, text.split(separator: "\n", omittingEmptySubsequences: false).count)
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

func normalizedDirectoryPath(_ url: URL) -> String {
    var path = url.standardizedFileURL.path
    while path.count > 1 && path.hasSuffix("/") {
        path.removeLast()
    }
    return path
}

func isAncestor(_ maybeAncestor: String, of path: String) -> Bool {
    guard maybeAncestor != path, maybeAncestor != "/" else { return maybeAncestor == "/" && path != "/" }
    return path.hasPrefix(maybeAncestor + "/")
}

struct GlobMatcher {
    private let exact: NSRegularExpression
    private let subtree: NSRegularExpression

    init(pattern: String) throws {
        let escaped = NSRegularExpression.escapedPattern(for: normalizeRelativePath(pattern))
            .replacingOccurrences(of: "\\*", with: ".*")
            .replacingOccurrences(of: "\\?", with: ".")
        exact = try NSRegularExpression(pattern: "^" + escaped + "$")
        subtree = try NSRegularExpression(pattern: "^" + escaped + "(/.*)?$")
    }

    func matches(_ value: String) -> Bool {
        let range = NSRange(value.startIndex..<value.endIndex, in: value)
        return exact.firstMatch(in: value, range: range) != nil
            || subtree.firstMatch(in: value, range: range) != nil
    }
}

enum PortableSHA256 {
    private static let initialHash: [UInt32] = [
        0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
        0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19
    ]

    private static let constants: [UInt32] = [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2
    ]

    static func hash(_ data: Data) -> [UInt8] {
        var bytes = [UInt8](data)
        let bitLength = UInt64(bytes.count) * 8
        bytes.append(0x80)
        while bytes.count % 64 != 56 {
            bytes.append(0)
        }
        bytes += stride(from: 56, through: 0, by: -8).map { UInt8((bitLength >> UInt64($0)) & 0xff) }

        var hash = initialHash
        for chunkStart in stride(from: 0, to: bytes.count, by: 64) {
            var words = Array(repeating: UInt32(0), count: 64)
            for index in 0..<16 {
                let offset = chunkStart + index * 4
                words[index] = (UInt32(bytes[offset]) << 24)
                    | (UInt32(bytes[offset + 1]) << 16)
                    | (UInt32(bytes[offset + 2]) << 8)
                    | UInt32(bytes[offset + 3])
            }
            for index in 16..<64 {
                let s0 = rotateRight(words[index - 15], by: 7) ^ rotateRight(words[index - 15], by: 18) ^ (words[index - 15] >> 3)
                let s1 = rotateRight(words[index - 2], by: 17) ^ rotateRight(words[index - 2], by: 19) ^ (words[index - 2] >> 10)
                words[index] = words[index - 16] &+ s0 &+ words[index - 7] &+ s1
            }

            var a = hash[0], b = hash[1], c = hash[2], d = hash[3]
            var e = hash[4], f = hash[5], g = hash[6], h = hash[7]
            for index in 0..<64 {
                let s1 = rotateRight(e, by: 6) ^ rotateRight(e, by: 11) ^ rotateRight(e, by: 25)
                let choice = (e & f) ^ (~e & g)
                let temp1 = h &+ s1 &+ choice &+ constants[index] &+ words[index]
                let s0 = rotateRight(a, by: 2) ^ rotateRight(a, by: 13) ^ rotateRight(a, by: 22)
                let majority = (a & b) ^ (a & c) ^ (b & c)
                let temp2 = s0 &+ majority
                h = g
                g = f
                f = e
                e = d &+ temp1
                d = c
                c = b
                b = a
                a = temp1 &+ temp2
            }

            hash[0] = hash[0] &+ a
            hash[1] = hash[1] &+ b
            hash[2] = hash[2] &+ c
            hash[3] = hash[3] &+ d
            hash[4] = hash[4] &+ e
            hash[5] = hash[5] &+ f
            hash[6] = hash[6] &+ g
            hash[7] = hash[7] &+ h
        }

        return hash.flatMap { word in
            [
                UInt8((word >> 24) & 0xff),
                UInt8((word >> 16) & 0xff),
                UInt8((word >> 8) & 0xff),
                UInt8(word & 0xff)
            ]
        }
    }

    private static func rotateRight(_ value: UInt32, by amount: UInt32) -> UInt32 {
        (value >> amount) | (value << (32 - amount))
    }
}
