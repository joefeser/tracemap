// swift-tools-version: 6.0

import PackageDescription

let package = Package(
    name: "TraceMapSwift",
    platforms: [
        .macOS(.v13)
    ],
    products: [
        .executable(name: "tracemap-swift", targets: ["tracemap-swift"]),
        .executable(name: "tracemap-swift-smoke-tests", targets: ["tracemap-swift-smoke-tests"]),
        .library(name: "TraceMapSwift", targets: ["TraceMapSwift"])
    ],
    dependencies: [
        .package(url: "https://github.com/swiftlang/swift-syntax.git", exact: "603.0.2")
    ],
    targets: [
        .target(
            name: "TraceMapSwift",
            dependencies: [
                .product(name: "SwiftParser", package: "swift-syntax"),
                .product(name: "SwiftParserDiagnostics", package: "swift-syntax"),
                .product(name: "SwiftSyntax", package: "swift-syntax")
            ]
        ),
        .executableTarget(
            name: "tracemap-swift",
            dependencies: ["TraceMapSwift"]
        ),
        .executableTarget(
            name: "tracemap-swift-smoke-tests",
            dependencies: ["TraceMapSwift"]
        ),
        .testTarget(
            name: "TraceMapSwiftTests",
            dependencies: ["TraceMapSwift"]
        )
    ]
)
