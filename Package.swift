// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "WordFloat",
    platforms: [.macOS(.v13)],
    products: [.executable(name: "WordFloat", targets: ["WordFloat"])],
    targets: [
        .target(name: "WordCore", path: "App/Core", resources: [.copy("Resources")]),
        .executableTarget(name: "WordFloat", dependencies: ["WordCore"], path: "App/Desktop"),
        .testTarget(name: "WordCoreTests", dependencies: ["WordCore"], path: "Tests")
    ]
)
