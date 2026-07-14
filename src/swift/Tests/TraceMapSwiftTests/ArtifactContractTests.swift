import Foundation
import XCTest
import TraceMapSwift

final class ArtifactContractTests: XCTestCase {
    func testSqlShapeHashesMatchSharedFixture() throws {
        for item in try sharedSqlShapeFixture() {
            let name = try XCTUnwrap(item["name"] as? String)
            let sql = try XCTUnwrap(item["sql"] as? String)
            let expected = try XCTUnwrap(item["queryShapeHash"] as? String)
            XCTAssertEqual(SwiftSqlShapeV1.queryShapeHash(sql), expected, "SQL shape mismatch for \(name)")
        }
    }

    func testSwiftSelfTestsRunUnderPackageTests() throws {
        try TraceMapSwiftSelfTests.run()
    }

    private func sharedSqlShapeFixture() throws -> [[String: Any]] {
        var root = URL(fileURLWithPath: #filePath)
        for _ in 0..<5 {
            root.deleteLastPathComponent()
        }
        let fixture = root.appendingPathComponent("samples/sql-shape-fixtures/sql-shape-v1.json")
        let object = try JSONSerialization.jsonObject(with: Data(contentsOf: fixture)) as? [String: Any]
        return try XCTUnwrap(object?["cases"] as? [[String: Any]])
    }
}
