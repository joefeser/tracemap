// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SwiftHttpSurfaceSample",
    platforms: [.iOS(.v17)],
    targets: [
        .executableTarget(name: "App", path: "Sources/App")
    ]
)

