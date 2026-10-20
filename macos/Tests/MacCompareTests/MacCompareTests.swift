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

    @MainActor
    func testUpdateStagesAndSkipVersion() {
        let checker = UpdateCheckerService.shared
        checker.stage = .prompt
        XCTAssertEqual(checker.stage, .prompt)

        checker.stage = .downloading(progress: 0.5, downloadedBytes: 50, totalBytes: 100)
        if case .downloading(let p, let d, let t) = checker.stage {
            XCTAssertEqual(p, 0.5)
            XCTAssertEqual(d, 50)
            XCTAssertEqual(t, 100)
        } else {
            XCTFail("Expected downloading stage")
        }

        checker.stage = .extracting
        XCTAssertEqual(checker.stage, .extracting)

        let mockApp = URL(fileURLWithPath: "/tmp/MacCompare.app")
        checker.stage = .readyToInstall(extractedAppPath: mockApp)
        if case .readyToInstall(let url) = checker.stage {
            XCTAssertEqual(url, mockApp)
        } else {
            XCTFail("Expected readyToInstall stage")
        }

        checker.latestReleaseVersion = "9.9.9"
        checker.skipVersion()
        XCTAssertEqual(checker.skippedVersion, "9.9.9")
        XCTAssertEqual(checker.stage, .prompt)
        checker.skippedVersion = nil
    }

    @MainActor
    func testLocalizedReleaseNotesParsing() {
        let checker = UpdateCheckerService.shared
        let sampleNotes = """
        <!-- zh-Hans -->
        - 中文新特性 1
        - 中文新特性 2

        <!-- en -->
        - English feature 1
        - English feature 2

        <!-- ja -->
        - 日本語機能 1
        - 日本語機能 2
        """

        checker.latestReleaseNotes = sampleNotes

        let zh = checker.localizedReleaseNotes(for: .zhHans)
        XCTAssertTrue(zh.contains("中文新特性 1"))
        XCTAssertFalse(zh.contains("English feature 1"))

        let en = checker.localizedReleaseNotes(for: .en)
        XCTAssertTrue(en.contains("English feature 1"))
        XCTAssertFalse(en.contains("日本語機能 1"))

        let ja = checker.localizedReleaseNotes(for: .ja)
        XCTAssertTrue(ja.contains("日本語機能 1"))
        XCTAssertFalse(ja.contains("中文新特性 1"))

        // Untagged plain text fallback test
        checker.latestReleaseNotes = "Plain notes without language tags."
        XCTAssertEqual(checker.localizedReleaseNotes(for: .zhHans), "Plain notes without language tags.")
        XCTAssertEqual(checker.localizedReleaseNotes(for: .en), "Plain notes without language tags.")
    }

    @MainActor
    func testFolderRowDepthAndParentPath() {
        let rootFile = AlignedFolderRow(
            relativePath: ".dockerignore",
            name: ".dockerignore",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "10 B",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "10 B",
            rightModifiedFormatted: "-",
            leftHash: nil,
            rightHash: nil,
            leftURL: nil,
            rightURL: nil
        )
        XCTAssertEqual(rootFile.depth, 0)
        XCTAssertNil(rootFile.parentPath)

        let subFolder = AlignedFolderRow(
            relativePath: "issue_templates",
            name: "issue_templates",
            isDirectory: true,
            status: .equal,
            leftSizeFormatted: "-",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "-",
            rightModifiedFormatted: "-",
            leftHash: nil,
            rightHash: nil,
            leftURL: nil,
            rightURL: nil
        )
        XCTAssertEqual(subFolder.depth, 0)
        XCTAssertNil(subFolder.parentPath)

        let childFile = AlignedFolderRow(
            relativePath: "issue_templates/Bug.md",
            name: "Bug.md",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "973 B",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "973 B",
            rightModifiedFormatted: "-",
            leftHash: nil,
            rightHash: nil,
            leftURL: nil,
            rightURL: nil
        )
        XCTAssertEqual(childFile.depth, 1)
        XCTAssertEqual(childFile.parentPath, "issue_templates")

        let nestedFile = AlignedFolderRow(
            relativePath: "a/b/c/nested.txt",
            name: "nested.txt",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "100 B",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "100 B",
            rightModifiedFormatted: "-",
            leftHash: nil,
            rightHash: nil,
            leftURL: nil,
            rightURL: nil
        )
        XCTAssertEqual(nestedFile.depth, 3)
        XCTAssertEqual(nestedFile.parentPath, "a/b/c")
    }

    @MainActor
    func testFolderTreeSorting() {
        let r1 = AlignedFolderRow(
            relativePath: "issue_templates/Bug.md",
            name: "Bug.md",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "-",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "-",
            rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let r2 = AlignedFolderRow(
            relativePath: "issue_templates",
            name: "issue_templates",
            isDirectory: true,
            status: .equal,
            leftSizeFormatted: "-",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "-",
            rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let r3 = AlignedFolderRow(
            relativePath: ".dockerignore",
            name: ".dockerignore",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "-",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "-",
            rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let r4 = AlignedFolderRow(
            relativePath: "issue_templates/sub_dir",
            name: "sub_dir",
            isDirectory: true,
            status: .equal,
            leftSizeFormatted: "-",
            leftModifiedFormatted: "-",
            rightSizeFormatted: "-",
            rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )

        let sorted = FolderDiffViewModel.sortFolderRows([r1, r2, r3, r4])
        let sortedPaths = sorted.map { $0.relativePath }

        // .dockerignore is file in root; issue_templates is folder in root (folder first, or sorted relative)
        // Parent folder issue_templates must precede its child issue_templates/Bug.md and sub_dir
        XCTAssertTrue(sortedPaths.firstIndex(of: "issue_templates")! < sortedPaths.firstIndex(of: "issue_templates/Bug.md")!)
        XCTAssertTrue(sortedPaths.firstIndex(of: "issue_templates")! < sortedPaths.firstIndex(of: "issue_templates/sub_dir")!)
        // In issue_templates: sub_dir is directory, should precede Bug.md file
        XCTAssertTrue(sortedPaths.firstIndex(of: "issue_templates/sub_dir")! < sortedPaths.firstIndex(of: "issue_templates/Bug.md")!)
    }

    @MainActor
    func testFolderCollapseAndExpand() {
        let vm = FolderDiffViewModel()

        let folder = AlignedFolderRow(
            relativePath: "issue_templates",
            name: "issue_templates",
            isDirectory: true,
            status: .equal,
            leftSizeFormatted: "-", leftModifiedFormatted: "-", rightSizeFormatted: "-", rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let child1 = AlignedFolderRow(
            relativePath: "issue_templates/Bug.md",
            name: "Bug.md",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "-", leftModifiedFormatted: "-", rightSizeFormatted: "-", rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let child2 = AlignedFolderRow(
            relativePath: "issue_templates/sub/Deep.md",
            name: "Deep.md",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "-", leftModifiedFormatted: "-", rightSizeFormatted: "-", rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )
        let otherFile = AlignedFolderRow(
            relativePath: ".gitignore",
            name: ".gitignore",
            isDirectory: false,
            status: .equal,
            leftSizeFormatted: "-", leftModifiedFormatted: "-", rightSizeFormatted: "-", rightModifiedFormatted: "-",
            leftHash: nil, rightHash: nil, leftURL: nil, rightURL: nil
        )

        vm.entries = [folder, child1, child2, otherFile]

        // Default: all expanded
        XCTAssertTrue(vm.isFolderExpanded(path: "issue_templates"))
        XCTAssertEqual(vm.visibleEntries.count, 4)

        // Toggle to collapse
        vm.toggleFolderExpansion(path: "issue_templates")
        XCTAssertFalse(vm.isFolderExpanded(path: "issue_templates"))
        // Both child1 and deep child2 must be hidden; folder itself and otherFile remain visible
        XCTAssertEqual(vm.visibleEntries.count, 2)
        XCTAssertEqual(vm.visibleEntries.map { $0.relativePath }, ["issue_templates", ".gitignore"])

        // Expand all
        vm.expandAllFolders()
        XCTAssertTrue(vm.isFolderExpanded(path: "issue_templates"))
        XCTAssertEqual(vm.visibleEntries.count, 4)

        // Collapse all
        vm.collapseAllFolders()
        XCTAssertFalse(vm.isFolderExpanded(path: "issue_templates"))
        XCTAssertEqual(vm.visibleEntries.count, 2)

        // Test double click folder toggles expansion
        vm.handleRowDoubleClick(entry: folder)
        XCTAssertTrue(vm.isFolderExpanded(path: "issue_templates"))
        XCTAssertEqual(vm.visibleEntries.count, 4)
    }
}
