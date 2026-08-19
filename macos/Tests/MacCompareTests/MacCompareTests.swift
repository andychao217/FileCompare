import XCTest
@testable import MacCompareKit

final class MacCompareTests: XCTestCase {
    func testTwoWayTextDiff() async {
        let left = "hello\nworld\nfoo"
        let right = "hello\nearth\nfoo"

        let result = await DiffEngineService.shared.compareText(left: left, right: right)
        XCTAssertEqual(result.lines.count, 3)
        XCTAssertEqual(result.lines[0].changeType, .unchanged)
        XCTAssertEqual(result.lines[1].changeType, .modified)
        XCTAssertEqual(result.lines[2].changeType, .unchanged)
    }

    func testThreeWayMergeClean() async {
        let base = "line 1\nline 2"
        let local = "line 1 modified\nline 2"
        let remote = "line 1\nline 2"

        let result = await DiffEngineService.shared.mergeThreeWay(local: local, base: base, remote: remote)
        XCTAssertEqual(result.conflictCount, 0)
        XCTAssertEqual(result.autoResolvedCount, 1)
    }

    func testThreeWayMergeConflict() async {
        let base = "line 1"
        let local = "line 1 local"
        let remote = "line 1 remote"

        let result = await DiffEngineService.shared.mergeThreeWay(local: local, base: base, remote: remote)
        XCTAssertEqual(result.conflictCount, 1)
    }
}
