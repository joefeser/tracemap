// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SwiftStorageDataSurfaces",
    products: [
        .library(name: "SwiftStorageDataSurfaces", targets: ["App"])
    ],
    targets: [
        .target(name: "App", resources: [.process("Resources")])
    ]
)
