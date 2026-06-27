// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SwiftPackageBasic",
    products: [
        .library(name: "SwiftPackageBasic", targets: ["SwiftPackageBasic"])
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser", from: "1.0.0")
    ],
    targets: [
        .target(name: "SwiftPackageBasic"),
        .testTarget(name: "SwiftPackageBasicTests", dependencies: ["SwiftPackageBasic"])
    ]
)
