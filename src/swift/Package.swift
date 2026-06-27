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
    targets: [
        .target(name: "TraceMapSwift"),
        .executableTarget(
            name: "tracemap-swift",
            dependencies: ["TraceMapSwift"]
        ),
        .executableTarget(
            name: "tracemap-swift-smoke-tests",
            dependencies: ["TraceMapSwift"]
        )
    ]
)
