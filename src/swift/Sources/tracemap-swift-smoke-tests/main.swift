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
        assert(result.facts.contains { $0.factType == "AnalysisGap" && $0.ruleId == "swift.unsupported.dynamic-boundary.v1" })
        let report = try String(contentsOf: out.appendingPathComponent("report.md"), encoding: .utf8)
        assert(report.contains("Level1SemanticAnalysisReduced"))
        assert(report.contains("Absence of evidence is not evidence of absence"))
        assert(!report.contains(out.path))
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
    }

    static func emittedRuleIdsAreCataloged() throws {
        let fixture = try SwiftFixture()
        let result = try SwiftScanEngine.scan(options: SwiftScanOptions(repoPath: fixture.repo, outputPath: fixture.temp.url.appendingPathComponent("scan")))
        let catalogPath = URL(fileURLWithPath: FileManager.default.currentDirectoryPath).appendingPathComponent("rules/rule-catalog.yml")
        let catalog = try String(contentsOf: catalogPath, encoding: .utf8)
        for ruleId in Set(result.facts.map(\.ruleId)) {
            assert(catalog.contains("id: \(ruleId)"), "missing rule \(ruleId)")
        }
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
