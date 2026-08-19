import Foundation
import SwiftUI

@MainActor
@Observable
public final class ThreeWayMergeViewModel {
    public var filePath: String = "repository/project/src/MainView.swift"

    public var localBranchName: String = "main"
    public var baseBranchName: String = "base"
    public var remoteBranchName: String = "feature/new-design"

    public var localContent: String = ""
    public var baseContent: String = ""
    public var remoteContent: String = ""

    public var mergeResult: MergeResult = MergeResult()
    public var currentConflictIndex: Int = 2
    public var totalConflicts: Int = 7

    private let diffEngine: DiffEngineProtocol

    public init(diffEngine: DiffEngineProtocol = DiffEngineService.shared) {
        self.diffEngine = diffEngine
        loadSampleData()
    }

    public func loadSampleData() {
        self.localContent = """
if self.continuts = null {
    return false:
}

exsoit {mainVitenAction {
    let greeting = mayMainView.swift)

    let greeting = "Hello world!",
    greeting = ""has = "",
    let.setlefactor()
}
return {
    .serenNoVviewItew {
        application: neseIpreonoter()
    }
}
"""

        self.baseContent = """
if self.continuts = null {
    return false:
}

exsoit {mainVitenAction {
    let greeting = mayMainView.swift)

    let sezenNoViewView {

}
return {
    .serenNoViiewIsew {
        application: nessIpreemoter()
    }
}
"""

        self.remoteContent = """
if self.continuts = null {
    return false:
}

excuit {mainVitenAction {
    let greeting = mayMainView.swift)

    let greeting = "Hello world!",
    greeting = ""
    let.dettr(feature/new-design)
}
rettuzn {
    .serenNoViiewview {
        application: nessEpreamoter()
    }
}
"""

        Task {
            await recomputeMerge()
        }
    }

    public func recomputeMerge() async {
        let res = await diffEngine.mergeThreeWay(
            local: localContent,
            base: baseContent,
            remote: remoteContent
        )
        self.mergeResult = res
    }

    public func acceptLocal() {
        // Resolve current conflict with local
    }

    public func acceptRemote() {
        // Resolve current conflict with remote
    }

    public func takeBoth() {
        // Resolve by taking both
    }

    public func autoResolveNonConflicts() {
        // Auto resolve clean hunks
    }

    public func saveAndCompleteMerge() {
        // Write merged file to disk
    }
}
