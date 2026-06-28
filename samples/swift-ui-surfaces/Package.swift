// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SwiftUiSurfaceSample",
    platforms: [.iOS(.v16)],
    products: [.library(name: "SwiftUiSurfaceSample", targets: ["App"])],
    targets: [.target(name: "App")]
)
