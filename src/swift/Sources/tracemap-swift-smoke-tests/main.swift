import Foundation
import TraceMapSwift

@main
struct TraceMapSwiftSmokeTests {
    static func main() throws {
        try parsesHelpVersionAndRepeatableOptions()
        try missingRepoFailsBeforeArtifacts()
        try scanWritesRequiredArtifactsAndReducedCoverage()
        try factsAreStableWhenOnlyOutputPathChanges()
        try symbolIdsIgnoreDeclarationBodyEdits()
        try duplicateSymbolIdentitiesEmitGapsAndDistinctIds()
        try duplicateSymbolRelationshipsUseRewrittenIds()
        try malformedMultipleSuperclassCandidatesDoNotCrash()
        try dangerousOutputPathsAreRejected()
        try detachedHeadBranchIsUnknown()
        try defaultExcludesUsePathSegments()
        try bundleFactsHonorUserFilters()
        try projectAndPackageMetadataFactsAreEmittedSafely()
        try dependencySurfaceFactsAreEmittedSafely()
        try swiftHttpClientSurfaceFactsAreEmittedSafely()
        try swiftSyntaxDeclarationAndCallFactsAreStored()
        try swiftSyntaxSymbolRelationshipsAreStored()
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
        assert(result.manifest.analysisLevel == "Level3SyntaxAnalysis")
        assert(result.manifest.buildStatus == "NotRun")
        for artifact in ["scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log"] {
            assert(FileManager.default.fileExists(atPath: out.appendingPathComponent(artifact).path), "missing \(artifact)")
        }
        assert(result.facts.contains { $0.factType == "FileInventoried" && $0.ruleId == "swift.file.inventory.v1" })
        assert(result.facts.contains { $0.factType == "SwiftSourceFileDeclared" && $0.ruleId == "swift.inventory.source-file.v1" })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.unsupported.dynamic-boundary.v1" })
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("Level3SyntaxAnalysis"))
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

    static func symbolIdsIgnoreDeclarationBodyEdits() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Stable.swift": """
            struct StableWorker {
              func run(value: Int) -> Int {
                value + 1
              }
              func parse(_ value: String) -> Int {
                value.count
              }
              func parse(_ value: Data) -> Int {
                value.count
              }
            }
            """
        ])
        let first = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("first")))
        try """
        struct StableWorker {
          func run(value: Int) -> Int {
            let adjusted = value + 2
            return adjusted
          }
          func parse(_ value: String) -> Int {
            let adjusted = value.count + 1
            return adjusted
          }
          func parse(_ value: Data) -> Int {
            value.count
          }
        }
        """.write(to: fixture.repo.appendingPathComponent("Sources/App/Stable.swift"), atomically: true, encoding: .utf8)
        let second = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("second")))
        let firstRun = first.facts.first { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "run" }
        let secondRun = second.facts.first { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "run" }
        assert(firstRun?.targetSymbol == secondRun?.targetSymbol)
        assert(firstRun?.properties["syntaxHash"] != secondRun?.properties["syntaxHash"])
        let firstParseSymbols = Set(first.facts.filter { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "parse" }.compactMap(\.targetSymbol))
        let secondParseSymbols = Set(second.facts.filter { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "parse" }.compactMap(\.targetSymbol))
        assert(firstParseSymbols.count == 2)
        assert(firstParseSymbols == secondParseSymbols)
    }

    static func duplicateSymbolIdentitiesEmitGapsAndDistinctIds() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/DuplicateA.swift": """
            struct DuplicateShape {
              func render() {}
            }
            """,
            "Sources/App/DuplicateB.swift": """
            struct DuplicateShape {
              func render() {
                print("other")
              }
            }
            """
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let duplicateTypes = result.facts.filter { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "DuplicateShape" }
        assert(duplicateTypes.count == 2)
        assert(Set(duplicateTypes.compactMap(\.targetSymbol)).count == 2)
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.syntax.identity-gap.v1" && $0.properties["gapKind"] == "swift-duplicate-symbol-identity" })
    }

    static func duplicateSymbolRelationshipsUseRewrittenIds() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/DuplicateRelationshipsA.swift": """
            protocol DuplicateProtocol {}
            struct RewrittenDuplicate: DuplicateProtocol {}
            """,
            "Sources/App/DuplicateRelationshipsB.swift": """
            struct RewrittenDuplicate: DuplicateProtocol {}
            """
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let duplicateSymbols = Set(result.facts.filter { $0.factType == "SwiftDeclarationDeclared" && $0.properties["name"] == "RewrittenDuplicate" }.compactMap(\.targetSymbol))
        let relationships = result.facts.filter { $0.factType == "SymbolRelationship" && $0.properties["relationshipKind"] == "ImplementsInterface" && $0.properties["targetSymbolDisplayName"] == "App.protocol DuplicateProtocol" }
        let relationshipSources = Set(relationships.compactMap { $0.properties["sourceSymbolId"] })
        assert(duplicateSymbols.count == 2)
        assert(relationshipSources == duplicateSymbols)
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.properties["gapKind"] == "swift-duplicate-symbol-identity" })
    }

    static func malformedMultipleSuperclassCandidatesDoNotCrash() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/MalformedInheritance.swift": """
            class BaseOne {
              func run() {}
            }
            class BaseTwo {
              func run() {}
            }
            class StrangeChild: BaseOne, BaseTwo {
              override func run() {}
            }
            """
        ])
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        assert(result.facts.contains { $0.factType == "SymbolRelationship" && $0.properties["relationshipKind"] == "InheritsFrom" })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.syntax.identity-gap.v1" && $0.properties["gapKind"] == "swift-ambiguous-symbol-identity" })
        assert(!result.facts.contains { $0.factType == "SymbolRelationship" && $0.properties["relationshipKind"] == "Overrides" && ($0.properties["sourceSymbolDisplayName"] ?? "").contains("run") })
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

    static func dependencySurfaceFactsAreEmittedSafely() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Package.swift": """
            // swift-tools-version: 6.0
            import PackageDescription
            let ignored = ".package(url: \"https://example.invalid/StringOnly.git\", from: \"1.0.0\")"
            let package = Package(
              name: "Deps",
              dependencies: [
                // .package(url: "https://example.invalid/CommentOnly.git", from: "1.0.0"),
                .package(url: "https://example.invalid/Alamofire.git", from: "5.0.0"),
                .package(url: "https://example.invalid/ExactKit.git", exact: "1.2.3"),
                .package(path: "../LocalOnly")
              ],
              targets: [.executableTarget(name: "App")]
            )
            """,
            "Package.resolved": """
            {"version":2,"pins":[
              {"identity":"alamofire","location":"https://example.invalid/Alamofire.git","state":{"version":"5.0.0","revision":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}},
              {"identity":"alamofire","location":"https://example.invalid/Alamofire.git","state":{"version":"5.0.1","revision":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"}},
              {"identity":"swift-argument-parser","location":"https://example.invalid/swift-argument-parser.git","state":{"branch":"main","revision":"cccccccccccccccccccccccccccccccccccccccc"}}
            ]}
            """,
            "Unsupported/Package.resolved": #"{"version":3,"pins":[]}"#,
            "Unreadable/Package.resolved": #"{"version":2,"pins":[]}"#,
            "Podfile": """
            target 'App' do
              pod 'Alamofire', '~> 5.0'
              pod 'RxSwift',
                  '~> 6.0'
              pod dynamic_pod_name
              pod 'LocalOnly', :path => '../LocalOnly'
            end
            """,
            "Podfile.lock": """
            PODS:
              - Alamofire (5.0.0)
              - RxSwift (6.0.0)

            DEPENDENCIES:
              - Alamofire
              - RxSwift

            SPEC CHECKSUMS:
              Alamofire: publicchecksum1
              Alamofire: duplicatechecksum
              RxSwift: publicchecksum2
            """,
            "Cartfile": """
            github "AcmeSecret/PaymentsSDK" ~> 1.0
            git "https://example.invalid/UtilityKit.git" "main"
            binary "https://example.invalid/BinaryKit.json" ~> 2.0
            """,
            "Cartfile.resolved": """
            github "ReactiveX/RxSwift" "6.0.0"
            binary "https://example.invalid/BinaryKit.json" "2.0.0"
            """
	        ])
	        let unreadable = fixture.repo.appendingPathComponent("Unreadable/Package.resolved")
	        try FileManager.default.setAttributes([.posixPermissions: 0o000], ofItemAtPath: unreadable.path)
	        defer {
	            try? FileManager.default.setAttributes([.posixPermissions: 0o644], ofItemAtPath: unreadable.path)
	        }
	        let out = fixture.temp.url.appendingPathComponent("scan")
	        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        let declarations = result.facts.filter { $0.factType == "SwiftDependencyDeclared" }
        let lockfileEntries = result.facts.filter { $0.factType == "SwiftDependencyLockfileEntryDeclared" }
        assert(!declarations.isEmpty)
        assert(!lockfileEntries.isEmpty)
        assert(declarations.allSatisfy { $0.ruleId == "swift.dependency.manifest.v1" })
        assert(lockfileEntries.contains { $0.ruleId == "swift.dependency.lockfile.swiftpm.v1" && $0.evidenceTier == .tier2Structural })
        assert(lockfileEntries.contains { $0.ruleId == "swift.dependency.lockfile.text.v1" && $0.evidenceTier == .tier3SyntaxOrTextual })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.dependency.analysis-gap.v1" && $0.properties["gapKind"] == "swift-dependency-local-path-omitted" })
	        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.dependency.analysis-gap.v1" && $0.properties["gapKind"] == "swift-dependency-lockfile-unsupported-schema" && $0.evidence.filePath == "Unsupported/Package.resolved" })
	        assert(!result.facts.contains { $0.factType == "SwiftDependencyLockfileEntryDeclared" && $0.evidence.filePath == "Unsupported/Package.resolved" })
	        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.dependency.analysis-gap.v1" && $0.properties["gapKind"] == "swift-dependency-metadata-unreadable" && $0.evidence.filePath == "Unreadable/Package.resolved" })
	        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.properties["gapKind"] == "swift-dependency-lockfile-malformed" })
	        assert(result.facts.contains { $0.factType == "SwiftEcosystemMetadataDeclared" && $0.properties["podChecksumSectionHash"]?.count == 64 })
	        assert(result.facts.allSatisfy { $0.properties["stableDependencySurfaceKey"] == nil })
	        assert(result.facts.filter { $0.properties["dependencyIdentityStatus"] == "hashed" || $0.properties["dependencyIdentityStatus"] == "unsafe-omitted" }.allSatisfy { $0.properties["normalizedDependencyIdentity"] == nil })
	        assert(declarations.contains { $0.properties["normalizedDependencyIdentity"] == "ExactKit" && $0.properties["versionStatus"] == "present" })
	        assert(declarations.contains { $0.evidence.filePath == "Podfile" && $0.properties["normalizedDependencyIdentity"] == "RxSwift" && $0.properties["versionStatus"] == "present" && $0.evidence.endLine > $0.evidence.startLine })
	        assert(!declarations.contains { $0.properties["normalizedDependencyIdentity"] == "CommentOnly" || $0.properties["normalizedDependencyIdentity"] == "StringOnly" })
	        let factsText = try String(contentsOf: out.appendingPathComponent("facts.ndjson"), encoding: .utf8)
        assert(!factsText.contains("https://example.invalid"))
        assert(!factsText.contains("../LocalOnly"))
        assert(!factsText.contains("AcmeSecret"))
        assert(!factsText.contains("PaymentsSDK"))
        assert(!factsText.contains("stableDependencySurfaceKey"))
        let aggregateLine = factsText.components(separatedBy: "\n").firstIndex { $0.contains("SwiftPackageManifestDeclared") } ?? -1
        let dependencyLine = factsText.components(separatedBy: "\n").firstIndex { $0.contains("SwiftDependencyDeclared") } ?? -1
        assert(aggregateLine >= 0 && dependencyLine > aggregateLine)
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("## Swift Dependency Metadata"))
        assert(report.contains("swiftpm"))
        assert(report.contains("cocoapods"))
        assert(report.contains("carthage"))
    }

    static func swiftHttpClientSurfaceFactsAreEmittedSafely() throws {
        let source = """
        import Foundation

        func requests(endpoint: String) {
          var postRequest = URLRequest(url: URL(string: "https://api.example.invalid/v1/users/123/roles?token=super-secret")!)
          postRequest.httpMethod = "POST"
          URLSession.shared.dataTask(with: postRequest)

          var deleteRequest = URLRequest(url: URL(string: "https://api.example.invalid/v1/sessions/abcdef123456")!)
          deleteRequest.httpMethod = "DELETE"
          URLSession.shared.dataTask(with: deleteRequest)

          var unknownMethod = URLRequest(url: URL(string: "https://api.example.invalid/v1/unknown")!)
          URLSession.shared.dataTask(with: unknownMethod)

          AF.request("https://api.example.invalid/v1/orders/42", method: .get)
          Alamofire.request("https://api.example.invalid/v1/orders/43?api_key=do-not-render", method: .post)
          AF.request(endpoint, method: .put)
        }

        protocol TargetType {}
        enum UserAPI: TargetType {
          var baseURL: URL { URL(string: "https://api.example.invalid")! }
          var path: String { "/v1/users/123/roles" }
          var method: Moya.Method { .get }
        }
        enum MissingMethodAPI: TargetType {
          var path: String { "/v1/missing-method" }
        }
        enum Moya { enum Method { case get } }
        enum AF { static func request(_ value: String, method: Method) {}; enum Method { case get; case put } }
        enum Alamofire { static func request(_ value: String, method: Method) {}; enum Method { case post } }
        """
        let fixture = try SwiftFixture(extraFiles: ["Sources/App/Http.swift": source])
        let out = fixture.temp.url.appendingPathComponent("scan-http")
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        let httpFacts = result.facts.filter { $0.factType == "HttpCallDetected" && $0.ruleId.hasPrefix("swift.http.") }
        assert(httpFacts.count >= 4)
        assert(httpFacts.allSatisfy { $0.evidenceTier == .tier3SyntaxOrTextual })
        assert(httpFacts.allSatisfy { $0.properties["httpMethod"] != nil && $0.properties["normalizedPathKey"] != nil })
        assert(httpFacts.contains { $0.ruleId == "swift.http.urlsession.v1" && $0.properties["httpMethod"] == "POST" && $0.properties["normalizedPathKey"] == "/v1/users/{}/roles" && $0.properties["queryStatus"] == "present-omitted" })
        assert(httpFacts.contains { $0.properties["httpMethod"] == "DELETE" && $0.properties["normalizedPathKey"] == "/v1/sessions/{}" && $0.properties["queryStatus"] == "absent" })
        assert(httpFacts.contains { $0.ruleId == "swift.http.client-library.v1" && $0.properties["framework"] == "alamofire" && $0.properties["httpMethod"] == "GET" })
        assert(httpFacts.contains { $0.properties["framework"] == "moya" && $0.properties["swiftClientKind"] == "moya" && $0.properties["httpMethod"] == "GET" })
        assert(httpFacts.allSatisfy { $0.properties["methodName"] == nil && $0.properties["surfaceKind"] == nil })
        let gapKinds = Set(result.facts.filter { $0.factType == "AnalysisGap" }.compactMap { $0.properties["gapKind"] })
        assert(gapKinds.contains("swift-http-method-unknown-projection-omitted"))
        assert(gapKinds.contains("swift-http-url-dynamic"))
        assert(gapKinds.contains("swift-http-moya-target-partial"))
        assert(!httpFacts.contains { $0.properties["normalizedPathKey"] == "/v1/unknown" })
        assert(!httpFacts.contains { $0.properties["normalizedPathKey"] == "/v1/missing-method" })
        let factsText = try String(contentsOf: out.appendingPathComponent("facts.ndjson"), encoding: .utf8)
        assert(!factsText.contains("https://api.example.invalid"))
        assert(!factsText.contains("super-secret"))
        assert(!factsText.contains("do-not-render"))
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("## Swift HTTP/API Client Surfaces"))
        assert(report.contains("urlrequest"))
        assert(report.contains("alamofire"))
        assert(report.contains("moya"))

        let out2 = fixture.temp.url.appendingPathComponent("scan-http-again")
        let result2 = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out2))
        let ids1 = result.facts.filter { $0.factType == "HttpCallDetected" }.map(\.factId).sorted()
        let ids2 = result2.facts.filter { $0.factType == "HttpCallDetected" }.map(\.factId).sorted()
        assert(ids1 == ids2)
    }

    static func swiftSyntaxDeclarationAndCallFactsAreStored() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Feature.swift": """
            import Foundation
            @_exported import struct Foundation.URL

            protocol Sending {
              func send(_ value: Int)
            }

            protocol AdvancedSending: Sending {}

            class BaseService {
              func refresh() {}
            }

            class ChildService: BaseService, AdvancedSending {
              override func refresh() {}
              func send(_ value: Int) {}
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

    static func swiftSyntaxSymbolRelationshipsAreStored() throws {
        let fixture = try SwiftFixture(extraFiles: [
            "Sources/App/Relationships.swift": """
            protocol BaseProtocol {
              func send(_ value: Int)
            }

            protocol DerivedProtocol: BaseProtocol {}

            class BaseController {
              func refresh() {}
            }

            class ChildController: BaseController, DerivedProtocol {
              override func refresh() {}
              func send(_ value: Int) {}
            }

            struct Worker: DerivedProtocol {
              func send(_ value: Int) {}
            }

            extension Worker: Codable {}
            extension MissingExternal: Codable {}
            """,
            "LooseExtension.swift": """
            extension Worker: Codable {}
            """
        ])
        let out = fixture.temp.url.appendingPathComponent("scan")
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: out))
        let relationships = result.facts.filter { $0.factType == "SymbolRelationship" }
        assert(!relationships.isEmpty)
        let relationshipKinds = Set(relationships.compactMap { $0.properties["relationshipKind"] })
        assert(relationshipKinds.contains("InheritsFrom"))
        assert(relationshipKinds.contains("ImplementsInterface"))
        assert(relationshipKinds.contains("ExtendsInterface"))
        assert(relationshipKinds.contains("Overrides"))
        assert(!relationshipKinds.contains("ExtensionOf"))
        assert(!relationshipKinds.contains("ImplementsInterfaceMember"))
        assert(relationships.allSatisfy { $0.evidenceTier == .tier3SyntaxOrTextual })
        assert(relationships.allSatisfy { $0.properties["sourceSymbolLanguage"] == "swift" && $0.properties["targetSymbolLanguage"] == "swift" })
        assert(relationships.allSatisfy { $0.properties["runtimeDispatchProven"] == "false" })
        assert(relationships.allSatisfy { $0.properties["sourceSymbolId"]?.hasPrefix("swift-syntax:v0:") == true })
        assert(relationships.allSatisfy { $0.properties["targetSymbolId"]?.hasPrefix("swift-syntax:v0:") == true })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.syntax.identity-gap.v1" && $0.properties["gapKind"] == "swift-unresolved-external-symbol" })
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.syntax.identity-gap.v1" && $0.properties["gapKind"] == "swift-module-identity-unknown" })
        assert(result.facts.contains { $0.factType == "SwiftDeclarationDeclared" && $0.properties["moduleName"] == "unknown-module" })
        assert(!result.facts.contains { $0.factType == "SymbolRelationship" && $0.properties["relationshipKind"] == "ImplementsInterfaceMember" })
        assert(!result.facts.contains { $0.factType == "SymbolRelationship" && $0.properties["relationshipKind"] == "Overrides" && ($0.properties["sourceSymbolDisplayName"] ?? "").contains("send") })

        let db = out.appendingPathComponent("index.sqlite").path
        let relationshipRows = try run("/usr/bin/sqlite3", [db, "select count(*) from symbol_relationships where rule_id in ('swift.syntax.symbol-relationship.v1','swift.syntax.override-candidate.v1');"]).trimmed()
        let sourceRoleRows = try run("/usr/bin/sqlite3", [db, "select count(*) from fact_symbols where role='source';"]).trimmed()
        let targetRoleRows = try run("/usr/bin/sqlite3", [db, "select count(*) from fact_symbols where role='target';"]).trimmed()
        let orphanRelationships = try run("/usr/bin/sqlite3", [db, "select count(*) from symbol_relationships r left join facts f on f.fact_id = r.relationship_id where f.fact_id is null;"]).trimmed()
        let relationshipColumns = try run("/usr/bin/sqlite3", [db, "pragma table_info(symbol_relationships);"])
            .split(separator: "\n")
            .map { String($0.split(separator: "|")[1]) }
        assert((Int(relationshipRows) ?? 0) == relationships.count)
        assert((Int(sourceRoleRows) ?? 0) >= relationships.count)
        assert((Int(targetRoleRows) ?? 0) >= relationships.count)
        assert(orphanRelationships == "0", "orphan symbol_relationships without backing fact")
        assert(relationshipColumns == ["relationship_id", "scan_id", "source_symbol_id", "target_symbol_id", "relationship_kind", "rule_id", "evidence_tier", "file_path", "start_line", "end_line"])
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("## Symbol Relationships By Kind"))
        assert(!report.lowercased().contains("runtime dispatch proven"))
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
        assert(gapKinds.contains("swift-module-identity-unknown"))
        assert(gapKinds.contains("swift-call-optional-chaining-unresolved"))
        let syntaxGapKinds = ["ConditionalCompilationAmbiguous", "CanImportConditionalAmbiguous", "swift-call-optional-chaining-unresolved"]
        assert(gapFacts.filter { syntaxGapKinds.contains($0.properties["gapKind"] ?? "") }.allSatisfy { $0.ruleId == "swift.syntax.analysis-gap.v1" })
        assert(gapFacts.filter { $0.properties["gapKind"] == "swift-module-identity-unknown" }.allSatisfy { $0.ruleId == "swift.syntax.identity-gap.v1" })
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
