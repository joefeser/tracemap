import Foundation
import TraceMapSwift

@main
struct TraceMapSwiftSmokeTests {
    static func main() throws {
        try parsesHelpVersionAndRepeatableOptions()
        try missingRepoFailsBeforeArtifacts()
        try scanWritesRequiredArtifactsAndReducedCoverage()
        try factsAreStableWhenOnlyOutputPathChanges()
        try dangerousOutputPathsAreRejected()
        try detachedHeadBranchIsUnknown()
        try defaultExcludesUsePathSegments()
        try bundleFactsHonorUserFilters()
        try projectAndPackageMetadataFactsAreEmittedSafely()
        try swiftSyntaxDeclarationAndCallFactsAreStored()
        try parserDiagnosticsEmitHashedGapWithoutRawText()
        try conditionalAndOptionalCallGapsUseSwiftSyntaxAnalysisGapRule()
        try exportedImportsRemainSyntaxOnlyAndDoNotClaimRuntimeReexport()
        try unsupportedMetadataEmitsGaps()
        try oversizedFilesBecomeGaps()
        try sqliteContainsSharedTablesAndFacts()
        try emittedRuleIdsAreCataloged()
        print("TraceMap Swift smoke tests passed")
    }

    static func parsesHelpVersionAndRepeatableOptions() throws {
        assert(TraceMapSwiftCLI.usage.contains("--max-file-byte-size <bytes>"))
        let options = try TraceMapSwiftCLI.parseScanOptions([
            "--repo", "/tmp/repo",
            "--out", "/tmp/out",
            "--project", "App",
            "--project", "Package.swift",
            "--include", "*.swift",
            "--exclude", "Generated/*",
            "--max-file-byte-size", "42"
        ])
        assert(options.projectFilters == ["App", "Package.swift"])
        assert(options.includeGlobs == ["*.swift"])
        assert(options.excludeGlobs == ["Generated/*"])
        assert(options.maxFileByteSize == 42)
    }

    static func missingRepoFailsBeforeArtifacts() throws {
        let temp = try TempDir()
        let missing = temp.url.appendingPathComponent("missing")
        let out = temp.url.appendingPathComponent("out")
        do {
            _ = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: missing, outputPath: out))
            throw SmokeFailure("scan should have failed for a missing repo")
        } catch {
            assert(!FileManager.default.fileExists(atPath: out.path))
        }
    }

    static func scanWritesRequiredArtifactsAndReducedCoverage() throws {
        let fixture = try SwiftFixture()
        let out = fixture.temp.url.appendingPathComponent("scan")
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        assert(result.manifest.analysisLevel == "Level1SemanticAnalysisReduced")
        assert(result.manifest.buildStatus == "FailedOrPartial")
        for artifact in ["scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log"] {
            assert(FileManager.default.fileExists(atPath: out.appendingPathComponent(artifact).path), "missing \(artifact)")
        }
        assert(result.facts.contains { $0.factType == "FileInventoried" && $0.ruleId == "swift.file.inventory.v1" })
        assert(result.facts.contains { $0.factType == "SwiftSourceFileDeclared" && $0.ruleId == "swift.inventory.source-file.v1" })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.unsupported.dynamic-boundary.v1" })
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("Level1SemanticAnalysisReduced"))
        assert(report.contains("absence of evidence is not evidence of absence"))
        assert(!report.contains(out.path))
        let analyzerLog = try String(contentsOf: out.appendingPathComponent("logs/analyzer.log"), encoding: .utf8)
        assert(analyzerLog.contains("repoNameHash="))
        assert(analyzerLog.contains("gitRootHash="))
        assert(analyzerLog.contains("commitSha="))
        assert(!analyzerLog.contains(fixture.repo.path))
    }

    static func factsAreStableWhenOnlyOutputPathChanges() throws {
        let fixture = try SwiftFixture()
        let first = fixture.temp.url.appendingPathComponent("first")
        let second = fixture.temp.url.appendingPathComponent("second")
        let firstResult = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: first))
        let secondResult = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: second))
        assert(firstResult.manifest.scanId == secondResult.manifest.scanId)
        assert(firstResult.facts.map(\.factId) == secondResult.facts.map(\.factId))
        let firstFacts = try String(contentsOf: first.appendingPathComponent("facts.ndjson"), encoding: .utf8)
        let secondFacts = try String(contentsOf: second.appendingPathComponent("facts.ndjson"), encoding: .utf8)
        assert(firstFacts == secondFacts)
    }

    static func defaultExcludesUsePathSegments() throws {
        let fixture = try SwiftFixture(extraFiles: [
            ".build/checkouts/Dependency/Hidden.swift": "struct Hidden {}\n",
            "DerivedData/App/Generated.swift": "struct Generated {}\n",
            "Sources/App/Visible.swift": "struct Visible {}\n"
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let paths = Set(result.inventory.map(\.relativePath))
        assert(paths.contains("Sources/App/Visible.swift"))
        assert(!paths.contains(".build/checkouts/Dependency/Hidden.swift"))
        assert(!paths.contains("DerivedData/App/Generated.swift"))
    }

    static func dangerousOutputPathsAreRejected() throws {
        let fixture = try SwiftFixture()
        do {
            _ = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.repo))
            throw SmokeFailure("scan should reject output path equal to repo root")
        } catch {
            assert(String(describing: error).contains("scan root") || String(describing: error).contains("git root"))
        }

        do {
            _ = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url))
            throw SmokeFailure("scan should reject output path that is an ancestor of the repo root")
        } catch {
            assert(String(describing: error).contains("ancestor"))
        }
    }

    static func detachedHeadBranchIsUnknown() throws {
        let fixture = try SwiftFixture()
        try run("/usr/bin/git", ["-C", fixture.repo.path, "checkout", "--detach", "HEAD"])

        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))

        assert(result.manifest.branch == nil)
    }

    static func bundleFactsHonorUserFilters() throws {
        let fixture = try SwiftFixture(extraDirectories: [
            "App.xcodeproj",
            "App.xcworkspace",
            "Sources/App/Model.xcdatamodeld"
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(
            repoPath: fixture.repo,
            outputPath: fixture.temp.url.appendingPathComponent("scan"),
            projectFilters: ["Sources/App"],
            excludeGlobs: ["*.xcdatamodeld"]
        ))
        let paths = Set(result.inventory.map(\.relativePath))
        assert(!paths.contains("App.xcodeproj"))
        assert(!paths.contains("App.xcworkspace"))
        assert(!paths.contains("Sources/App/Model.xcdatamodeld"))
    }

    static func projectAndPackageMetadataFactsAreEmittedSafely() throws {
        let fixture = try SwiftFixture(
            extraFiles: [
                "Package.resolved": """
                {"version":2,"pins":[{"identity":"swift-argument-parser","location":"https://github.com/apple/swift-argument-parser","state":{"version":"1.0.0"}}]}
                """,
                "App.xcworkspace/contents.xcworkspacedata": """
                <?xml version="1.0" encoding="UTF-8"?><Workspace version="1.0"><FileRef location="group:App.xcodeproj"></FileRef></Workspace>
                """,
                "App.xcodeproj/project.pbxproj": """
                productType = com.apple.product-type.application;
                name = Debug;
                name = Release;
                """,
                "Sources/App/Info.plist": """
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0"><dict>
                  <key>CFBundleIdentifier</key><string>com.example.publicsafe</string>
                  <key>CFBundleURLTypes</key><array><dict><key>CFBundleURLSchemes</key><array><string>sample</string></array></dict></array>
                  <key>NSCameraUsageDescription</key><string>Needed by sample tests.</string>
                  <key>NSAppTransportSecurity</key><dict/>
                </dict></plist>
                """,
                "Podfile.lock": """
                PODS:
                  - Alamofire
                """,
                "Cartfile.resolved": """
                github "ReactiveX/RxSwift" "6.0.0"
                """
            ],
            extraDirectories: [
                "App.xcodeproj",
                "App.xcworkspace"
            ]
        )
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let factTypes = Set(result.facts.map(\.factType))
        for factType in [
            "SwiftSourceRootDeclared",
            "SwiftSourceFileDeclared",
            "SwiftPackageManifestDeclared",
            "SwiftPackageResolvedDeclared",
            "SwiftXcodeProjectDeclared",
            "SwiftXcodeWorkspaceDeclared",
            "SwiftInfoPlistDeclared",
            "SwiftEcosystemMetadataDeclared"
        ] {
            assert(factTypes.contains(factType), "missing \(factType)")
        }
        let packageResolved = try requireFact(result, "SwiftPackageResolvedDeclared")
        assert(packageResolved.properties["safeIdentityCount"] == "1")
        assert(packageResolved.properties["unsafeLocationCount"] == "1")
        let plist = try requireFact(result, "SwiftInfoPlistDeclared")
        assert(plist.properties["urlSchemeCount"] == "1")
        assert(plist.properties["permissionKeyCount"] == "1")
        let rendered = try String(contentsOf: fixture.temp.url.appendingPathComponent("scan/facts.ndjson"), encoding: .utf8)
        assert(!rendered.contains("https://github.com/apple/swift-argument-parser"))
        assert(!rendered.contains("com.example.publicsafe"))
        assert(!rendered.contains("Needed by sample tests"))
    }

    static func unsupportedMetadataEmitsGaps() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Package.swift": """
            // swift-tools-version: 6.0
            import PackageDescription
            let dynamicName = ProcessInfo.processInfo.environment["PACKAGE_NAME"] ?? "Dynamic"
            let package = Package(name: dynamicName)
            """,
            "Package.resolved": #"{"version":99,"pins":[]}"#,
            "Sources/App/Info.plist": "bplist00unsupported",
            "App.xcworkspace/contents.xcworkspacedata": #"<Workspace><FileRef location="https://example.invalid/App.xcodeproj"></FileRef></Workspace>"#
        ], extraDirectories: ["App.xcworkspace"])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let gapKinds = Set(result.facts.filter { $0.factType == "AnalysisGap" }.compactMap { $0.properties["gapKind"] })
        assert(gapKinds.contains("swiftpm-manifest-dynamic"))
        assert(gapKinds.contains("swiftpm-resolved-unknown-version"))
        assert(gapKinds.contains("plist-binary-unsupported"))
        assert(gapKinds.contains("xcode-workspace-external-reference"))
    }

    static func swiftSyntaxDeclarationAndCallFactsAreStored() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Feature.swift": """
            import Foundation
            @_exported import struct Foundation.URL

            protocol Sending {
              func send(_ value: Int)
            }

            actor Worker {
              func run() {}
            }

            class Service: Sending {
              func send(_ value: Int) {}
            }

            struct Handler {
              let service = Service()

              func run(value: Int) {
                let localToken = Service()
                service.send(value)
                Service(); Service()
                Logger.configure()
                rename(from: "a")
              }

              func rename(from old: String) {}
            }

            enum Logger {
              static func configure() {}
            }

            #if canImport(UIKit)
            import UIKit
            struct ConditionalView {
              func draw() {
                print("conditional")
              }
            }
            #endif
            """
        ])
        let out = fixture.temp.url.appendingPathComponent("scan")
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        let factTypes = Set(result.facts.map(\.factType))
        assert(factTypes.contains("SwiftDeclarationDeclared"))
        assert(factTypes.contains("SwiftImportDeclared"))
        assert(factTypes.contains("SwiftCallCandidate"))
        assert(factTypes.contains("SwiftConstructionCandidate"))
        assert(result.facts.contains { $0.factType == "SwiftDeclarationDeclared" && $0.properties["declarationKind"] == "actor" })
        assert(result.facts.contains { $0.factType == "SwiftImportDeclared" && $0.properties["exportedImport"] == "true" })
        assert(result.facts.contains { $0.factType == "SwiftImportDeclared" && $0.properties["importKind"] == "struct" && $0.properties["importedModule"] == "Foundation.URL" })
        assert(result.facts.contains { $0.properties["conditionalCompilation"] == "true" })
        assert(!result.facts.contains { $0.factType == "SwiftDeclarationDeclared" && $0.properties["declarationKind"] == "property" && $0.properties["name"] == "localToken" })
        assert(result.facts.contains { $0.factType == "SwiftDeclarationDeclared" && $0.properties["displaySignature"] == "function rename(from)" && $0.properties["parameterLabels"] == "from" })
        let declarations = result.facts.filter { $0.factType == "SwiftDeclarationDeclared" }
        assert(!declarations.isEmpty)
        assert(declarations.allSatisfy { $0.targetSymbol?.hasPrefix("swift-syntax:v0:") == true })
        assert(declarations.allSatisfy { ($0.targetSymbol?.dropFirst("swift-syntax:v0:".count).count ?? 0) == 64 })
        assert(result.facts.first { $0.factType == "SwiftSourceRootDeclared" }?.evidenceTier == .tier3SyntaxOrTextual)
        let gapKinds = Set(result.facts.filter { $0.factType == "AnalysisGap" }.compactMap { $0.properties["gapKind"] })
        assert(gapKinds.contains("ConditionalCompilationAmbiguous"))
        assert(gapKinds.contains("CanImportConditionalAmbiguous"))
        assert(result.facts.allSatisfy { $0.evidenceTier != .tier1Semantic })
        assert(result.facts.filter { ["SwiftDeclarationDeclared", "SwiftImportDeclared", "SwiftCallCandidate", "SwiftConstructionCandidate"].contains($0.factType) }.allSatisfy { $0.evidenceTier == .tier3SyntaxOrTextual })
        let serviceCallFacts = result.facts.filter { $0.factType == "SwiftCallCandidate" && $0.properties["calleeName"] == "Service" }
        assert(serviceCallFacts.count >= 3)
        assert(Set(serviceCallFacts.map(\.factId)).count == serviceCallFacts.count)
        assert(result.facts.contains { $0.factType == "SwiftCallCandidate" && $0.properties["calleeName"] == "rename" && $0.properties["argumentLabels"] == "from" })
        assert(!result.facts.contains { $0.factType == "SwiftConstructionCandidate" && $0.properties["createdTypeSyntax"] == "Logger.configure" })

        let symbols = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from symbols where language='swift';"]).trimmed()
        let callEdges = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from call_edges where rule_id='swift.syntax.call.v1';"]).trimmed()
        let creations = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from object_creations where rule_id='swift.syntax.construction.v1';"]).trimmed()
        let supportedCallFacts = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from call_edges c join facts f on f.fact_id = c.fact_id where f.fact_type='SwiftCallCandidate';"]).trimmed()
        let supportedCreationFacts = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from object_creations c join facts f on f.fact_id = c.fact_id where f.fact_type='SwiftConstructionCandidate';"]).trimmed()
        assert((Int(symbols) ?? 0) > 0)
        assert((Int(callEdges) ?? 0) > 0)
        assert((Int(creations) ?? 0) > 0)
        assert((Int(supportedCallFacts) ?? 0) == (Int(callEdges) ?? -1))
        assert((Int(supportedCreationFacts) ?? 0) == (Int(creations) ?? -1))
    }

    static func parserDiagnosticsEmitHashedGapWithoutRawText() throws {
        let sentinel = "DO_NOT_RENDER_PARSE_DIAGNOSTIC_SENTINEL"
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Broken.swift": """
            struct Broken {
              let value =
              let secret = "\(sentinel)"
            }
            """
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let gaps = result.facts.filter { $0.factType == "AnalysisGap" && $0.properties["gapKind"] == "SwiftParseDiagnostics" }
        assert(!gaps.isEmpty)
        assert(gaps.allSatisfy { $0.ruleId == "swift.syntax.analysis-gap.v1" })
        assert(result.manifest.knownGaps.contains { $0.contains("diagnosticMessageHash=") })
        assert(!result.manifest.knownGaps.contains { $0.contains(sentinel) })
        assert(!result.facts.contains { fact in
            fact.properties.values.contains { $0.contains(sentinel) }
        })
    }

    static func conditionalAndOptionalCallGapsUseSwiftSyntaxAnalysisGapRule() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Conditional.swift": """
            #if canImport(UIKit)
            import UIKit
            #endif

            struct OptionalCaller {
              let service: Service?
              func run() {
                service?.send()
              }
            }
            struct Service {
              func send() {}
            }
            """,
            "Loose.swift": """
            import Foundation
            struct Loose {}
            """
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let gapFacts = result.facts.filter { $0.factType == "AnalysisGap" }
        let gapKinds = Set(gapFacts.compactMap { $0.properties["gapKind"] })
        assert(gapKinds.contains("ConditionalCompilationAmbiguous"))
        assert(gapKinds.contains("CanImportConditionalAmbiguous"))
        assert(gapKinds.contains("swift-module-context-unavailable"))
        assert(gapKinds.contains("swift-call-optional-chaining-unresolved"))
        let reviewedKinds = ["ConditionalCompilationAmbiguous", "CanImportConditionalAmbiguous", "swift-module-context-unavailable", "swift-call-optional-chaining-unresolved"]
        assert(gapFacts.filter { reviewedKinds.contains($0.properties["gapKind"] ?? "") }.allSatisfy { $0.ruleId == "swift.syntax.analysis-gap.v1" })
    }

    static func exportedImportsRemainSyntaxOnlyAndDoNotClaimRuntimeReexport() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Exports.swift": """
            @_exported import Foundation
            struct UsesURL {
              let value: URL?
            }
            """
        ])
        let out = fixture.temp.url.appendingPathComponent("scan")
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        guard let exported = result.facts.first(where: { $0.factType == "SwiftImportDeclared" && $0.properties["exportedImport"] == "true" }) else {
            throw SmokeFailure("missing exported import fact")
        }
        assert(exported.evidenceTier == .tier3SyntaxOrTextual)
        assert(exported.ruleId == "swift.syntax.import.v1")
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8).lowercased()
        for forbidden in ["runtime re-export", "runtime target", "will call", "executed", "injected", "impacted"] {
            assert(!report.contains(forbidden), "report contained forbidden wording: \(forbidden)")
        }
    }

    static func oversizedFilesBecomeGaps() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Large.swift": String(repeating: "x", count: 128)
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan"), maxFileByteSize: 8))
        assert(result.inventory.contains { $0.relativePath == "Sources/App/Large.swift" && $0.skippedReason == "file-too-large" })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.evidence.filePath == "Sources/App/Large.swift" })
    }

    static func sqliteContainsSharedTablesAndFacts() throws {
        let fixture = try SwiftFixture()
        let out = fixture.temp.url.appendingPathComponent("scan")
        _ = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        let tables = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select name from sqlite_master where type='table' order by name;"])
            .split(separator: "\n")
            .map(String.init)
        for table in ["scan_manifest", "facts", "symbols", "symbol_occurrences", "fact_symbols", "symbol_relationships"] {
            assert(tables.contains(table), "missing table \(table)")
        }
        let factCount = try run("/usr/bin/sqlite3", [out.appendingPathComponent("index.sqlite").path, "select count(*) from facts;"]).trimmed()
        assert((Int(factCount) ?? 0) > 0)
        let db = out.appendingPathComponent("index.sqlite").path
        let orphanCalls = try run("/usr/bin/sqlite3", [db, "select count(*) from call_edges c left join facts f on f.fact_id = c.fact_id where f.fact_id is null;"]).trimmed()
        let orphanCreations = try run("/usr/bin/sqlite3", [db, "select count(*) from object_creations o left join facts f on f.fact_id = o.fact_id where f.fact_id is null;"]).trimmed()
        let orphanOccurrences = try run("/usr/bin/sqlite3", [db, "select count(*) from symbol_occurrences o left join facts f on f.fact_id = o.fact_id where f.fact_id is null;"]).trimmed()
        assert(orphanCalls == "0", "orphan call_edges without backing fact")
        assert(orphanCreations == "0", "orphan object_creations without backing fact")
        assert(orphanOccurrences == "0", "orphan symbol_occurrences without backing fact")
    }

    static func emittedRuleIdsAreCataloged() throws {
        let fixture = try SwiftFixture()
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let catalogPath = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent("rules/rule-catalog.yml")
        let catalog = try String(contentsOf: catalogPath, encoding: .utf8)
        for ruleId in Set(result.facts.map(\.ruleId)) {
            assert(catalog.contains("id: \(ruleId)"), "missing rule \(ruleId)")
        }
        assert(!catalog.contains("id: swift.syntax.not-cataloged.v1"))
    }

    static func requireFact(_ result: SwiftScanResult, _ factType: String) throws -> CodeFact {
        guard let fact = result.facts.first(where: { $0.factType == factType }) else {
            throw SmokeFailure("missing fact \(factType)")
        }
        return fact
    }
}

private struct SwiftFixture {
    let temp: TempDir
    let repo: URL

    init(extraFiles: [String: String] = [:], extraDirectories: [String] = []) throws {
        temp = try TempDir()
        repo = temp.url.appendingPathComponent("repo")
        try FileManager.default.createDirectory(at: repo.appendingPathComponent("Sources/App"), withIntermediateDirectories: true)
        try """
        // swift-tools-version: 6.0
        import PackageDescription
        let package = Package(name: "Sample", targets: [.executableTarget(name: "App")])
        """.write(to: repo.appendingPathComponent("Package.swift"), atomically: true, encoding: .utf8)
        try "print(\"hello\")\n".write(to: repo.appendingPathComponent("Sources/App/main.swift"), atomically: true, encoding: .utf8)
        for (path, text) in extraFiles {
            let url = repo.appendingPathComponent(path)
            try FileManager.default.createDirectory(at: url.deletingLastPathComponent(), withIntermediateDirectories: true)
            try text.write(to: url, atomically: true, encoding: .utf8)
        }
        for path in extraDirectories {
            try FileManager.default.createDirectory(at: repo.appendingPathComponent(path), withIntermediateDirectories: true)
        }
        try run("/usr/bin/git", ["-C", repo.path, "init"])
        try run("/usr/bin/git", ["-C", repo.path, "config", "user.email", "swift-test@example.invalid"])
        try run("/usr/bin/git", ["-C", repo.path, "config", "user.name", "TraceMap Swift Test"])
        try run("/usr/bin/git", ["-C", repo.path, "add", "."])
        try run("/usr/bin/git", ["-C", repo.path, "commit", "-m", "fixture"])
    }
}

private final class TempDir {
    let url: URL

    init() throws {
        url = URL(fileURLWithPath: NSTemporaryDirectory()).appendingPathComponent("tracemap-swift-tests-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
    }

    deinit {
        try? FileManager.default.removeItem(at: url)
    }
}

private struct SmokeFailure: Error, LocalizedError {
    let message: String
    init(_ message: String) { self.message = message }
    var errorDescription: String? { message }
}

@discardableResult
private func run(_ executable: String, _ arguments: [String]) throws -> String {
    let process = Process()
    process.executableURL = URL(fileURLWithPath: executable)
    process.arguments = arguments
    let stdout = Pipe()
    let stderr = Pipe()
    process.standardOutput = stdout
    process.standardError = stderr
    try process.run()
    process.waitUntilExit()
    let out = String(decoding: stdout.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
    let err = String(decoding: stderr.fileHandleForReading.readDataToEndOfFile(), as: UTF8.self)
    guard process.terminationStatus == 0 else {
        throw SmokeFailure(err)
    }
    return out
}

private extension String {
    func trimmed() -> String {
        trimmingCharacters(in: .whitespacesAndNewlines)
    }
}
