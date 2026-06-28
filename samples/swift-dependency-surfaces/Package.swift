// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "DependencySurfaceSample",
    dependencies: [
        .package(url: "https://example.invalid/Alamofire.git", from: "5.0.0"),
        .package(name: "ArgumentParser", url: "https://example.invalid/swift-argument-parser.git", branch: "main"),
        .package(path: "../LocalOnlyPackage")
    ],
    targets: [
        .executableTarget(name: "App")
    ]
)
