// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MacCompare",
    defaultLocalization: "en",
    platforms: [
        .macOS(.v14)
    ],
    products: [
        .executable(
            name: "MacCompare",
            targets: ["MacCompare"]
        ),
        .executable(
            name: "mcdiff",
            targets: ["mcdiff"]
        ),
        .library(
            name: "MacCompareKit",
            targets: ["MacCompareKit"]
        )
    ],
    dependencies: [],
    targets: [
        .target(
            name: "MacCompareKit",
            dependencies: [],
            path: "Sources/MacCompareKit"
        ),
        .executableTarget(
            name: "MacCompare",
            dependencies: ["MacCompareKit"],
            path: "Sources/MacCompare"
        ),
        .executableTarget(
            name: "mcdiff",
            dependencies: ["MacCompareKit"],
            path: "Sources/mcdiff"
        ),
        .testTarget(
            name: "MacCompareTests",
            dependencies: ["MacCompareKit"],
            path: "Tests/MacCompareTests"
        )
    ]
)
