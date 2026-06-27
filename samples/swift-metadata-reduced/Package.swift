// swift-tools-version: 6.0
import PackageDescription

let dynamicName = ProcessInfo.processInfo.environment["PACKAGE_NAME"] ?? "SwiftMetadataReduced"
let package = Package(name: dynamicName)
