import XCTest
@testable import MacCompareKit

final class MacCompareTests: XCTestCase {
    var tempDirectory: URL!

    override func setUp() {
        super.setUp()
        tempDirectory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try? FileManager.default.createDirectory(at: tempDirectory, withIntermediateDirectories: true)
    }

    override func tearDown() {
        try? FileManager.default.removeItem(at: tempDirectory)
        super.tearDown()
    }

    func testRustDiffBridgeDirect() {
        XCTAssertTrue(RustDiffBridge.isAvailable)
        XCTAssertTrue(RustDiffBridge.version.contains("MacCompare Core"))

        let left = "Alpha\nBeta\nGamma"
        let right = "Alpha\nBeta Modified\nGamma\nDelta"
        let diff = RustDiffBridge.compareText(left: left, right: right)
        XCTAssertNotNil(diff)
        XCTAssertEqual(diff?.totalModifications, 1)
        XCTAssertEqual(diff?.totalAdditions, 1)
        XCTAssertEqual(diff?.hunks.count, 2)
    }

    func testTwoWayTextDiff() async {
        let left = "line 1\nline 2\nline 3"
        let right = "line 1\nline 2 modified\nline 3"

        // Test with Rust engine
        DiffEngineService.shared.enginePreference = .rust
        let resultRust = await DiffEngineService.shared.compareText(left: left, right: right)
        XCTAssertEqual(resultRust.lines.count, 3)
        XCTAssertEqual(resultRust.lines[0].changeType, .unchanged)
        XCTAssertEqual(resultRust.lines[1].changeType, .modified)
        XCTAssertEqual(resultRust.lines[2].changeType, .unchanged)
        XCTAssertEqual(resultRust.totalModifications, 1)

        // Test with Swift fallback engine
        DiffEngineService.shared.enginePreference = .swift
        let resultSwift = await DiffEngineService.shared.compareText(left: left, right: right)
        XCTAssertEqual(resultSwift.lines.count, 3)
        XCTAssertEqual(resultSwift.lines[0].changeType, .unchanged)
        XCTAssertEqual(resultSwift.lines[1].changeType, .modified)
        XCTAssertEqual(resultSwift.lines[2].changeType, .unchanged)
        XCTAssertEqual(resultSwift.totalModifications, 1)

        // Restore Auto
        DiffEngineService.shared.enginePreference = .auto
    }

    func testThreeWayMergeClean() async {
        let base = "line 1\nline 2\nline 3"
        let local = "line 1 modified\nline 2\nline 3"
        let remote = "line 1\nline 2\nline 3"

        let result = await DiffEngineService.shared.mergeThreeWay(local: local, base: base, remote: remote)
        XCTAssertEqual(result.conflictCount, 0)
        XCTAssertEqual(result.autoResolvedCount, 1)
        XCTAssertEqual(result.lines[0].status, .cleanLocal)
    }

    func testThreeWayMergeConflict() async {
        let base = "line 1"
        let local = "line 1 local"
        let remote = "line 1 remote"

        let result = await DiffEngineService.shared.mergeThreeWay(local: local, base: base, remote: remote)
        XCTAssertEqual(result.conflictCount, 1)
        XCTAssertEqual(result.lines[0].status, .conflict)
    }

    func testFileSaveAndBackup() throws {
        let fileURL = tempDirectory.appendingPathComponent("test.txt")
        let content1 = "Hello World"
        let content2 = "Hello MacCompare"

        try DiffEngineService.shared.saveFile(to: fileURL, content: content1, encoding: .utf8, createBackup: true)
        let loaded1 = try DiffEngineService.shared.loadFile(from: fileURL, encoding: .utf8)
        XCTAssertEqual(loaded1, content1)

        try DiffEngineService.shared.saveFile(to: fileURL, content: content2, encoding: .utf8, createBackup: true)
        let loaded2 = try DiffEngineService.shared.loadFile(from: fileURL, encoding: .utf8)
        XCTAssertEqual(loaded2, content2)

        let backupURL = fileURL.appendingPathExtension("bak")
        XCTAssertTrue(FileManager.default.fileExists(atPath: backupURL.path))
        let backupContent = try DiffEngineService.shared.loadFile(from: backupURL, encoding: .utf8)
        XCTAssertEqual(backupContent, content1)
    }

    func testFolderComparisonAndSync() async throws {
        let dirA = tempDirectory.appendingPathComponent("dirA")
        let dirB = tempDirectory.appendingPathComponent("dirB")
        try FileManager.default.createDirectory(at: dirA, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: dirB, withIntermediateDirectories: true)

        let fileA = dirA.appendingPathComponent("shared.txt")
        let fileB = dirB.appendingPathComponent("shared.txt")
        try "Content A".write(to: fileA, atomically: true, encoding: .utf8)
        try "Content B".write(to: fileB, atomically: true, encoding: .utf8)

        let leftOnly = dirA.appendingPathComponent("left_only.txt")
        try "Left Only Content".write(to: leftOnly, atomically: true, encoding: .utf8)

        // Compare folders
        let entries = await DiffEngineService.shared.compareFolders(
            leftPath: dirA.path,
            rightPath: dirB.path,
            mode: 1 // Deep Hash
        )
        XCTAssertEqual(entries.count, 2)

        let sharedEntry = entries.first(where: { $0.relativePath == "shared.txt" })
        XCTAssertEqual(sharedEntry?.status, .contentDifferent)

        let leftOnlyEntry = entries.first(where: { $0.relativePath == "left_only.txt" })
        XCTAssertEqual(leftOnlyEntry?.status, .leftOnly)

        // Test Sync Plan execution
        let syncItem = SyncPlanItem(
            action: .copyLeftToRight,
            relativePath: "left_only.txt",
            sourceURL: leftOnly,
            targetURL: dirB.appendingPathComponent("left_only.txt"),
            size: nil
        )

        let syncResult = try await DiffEngineService.shared.executeSyncPlan(items: [syncItem])
        XCTAssertEqual(syncResult.successCount, 1)
        XCTAssertEqual(syncResult.errorCount, 0)
        XCTAssertTrue(FileManager.default.fileExists(atPath: dirB.appendingPathComponent("left_only.txt").path))
    }

    @MainActor
    func testVersionComparison() {
        let checker = UpdateCheckerService.shared
        XCTAssertTrue(checker.isNewerVersion(latest: "0.2.0", current: "0.1.0"))
        XCTAssertTrue(checker.isNewerVersion(latest: "0.1.1", current: "0.1.0"))
        XCTAssertTrue(checker.isNewerVersion(latest: "1.0.0", current: "0.9.9"))
        XCTAssertFalse(checker.isNewerVersion(latest: "0.1.0", current: "0.1.0"))
        XCTAssertFalse(checker.isNewerVersion(latest: "0.0.9", current: "0.1.0"))
    }
}
