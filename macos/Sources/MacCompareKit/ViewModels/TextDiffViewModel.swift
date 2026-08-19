import Foundation
import SwiftUI

@MainActor
@Observable
public final class TextDiffViewModel {
    public var leftTitle: String = "script.py (Original)"
    public var rightTitle: String = "script.py (Modified)"
    public var leftContent: String = ""
    public var rightContent: String = ""

    public var diffResult: TextDiffResult = TextDiffResult()
    public var isLoading: Bool = false

    public var ignoreWhitespace: Bool = false {
        didSet { Task { await recomputeDiff() } }
    }
    public var ignoreCase: Bool = false {
        didSet { Task { await recomputeDiff() } }
    }
    public var selectedEncoding: String = "UTF-8"
    public var cursorPosition: String = "Ln 42, Col 15"
    public var currentDiffIndex: Int = 0

    private let diffEngine: DiffEngineProtocol

    public init(diffEngine: DiffEngineProtocol = DiffEngineService.shared) {
        self.diffEngine = diffEngine
        loadSampleData()
    }

    public func loadSampleData() {
        self.leftContent = """
import synsc.sd

def funbic(basen):
    syntax = array[]
    syntax = self.town_strings>


def get_mith(atexs):
    token = schanged(ittoarname)
    return sensalion("scoze")

# Ignore Tokens
printult_io.recain("I The same necessarary $?")

if out in punt:
    # some punt = string[])
    sodeon.tokens = decoded[]

def autotname(selfI):
    return sampilleArray(sonfig + 10)
"""

        self.rightContent = """
import synsc.sd

def funbic(basen):
    syntax = array[]
    syntax = self.town_strings>


def get_mitt(atexs):
    token = schanged(fatsenzname>)
    return sensalion("score.1")

# Ignore Tokens
printult_io.recain("I The same necessarary $?")

if out in punt:
    # some punt = string[])
    sodeon.tokens = abcoded[]
    screen.claserining("$&ill")

def autotname(selfI):
    return sanoilleArray(sonfig + 10)
"""
        Task {
            await recomputeDiff()
        }
    }

    public func recomputeDiff() async {
        isLoading = true
        let res = await diffEngine.compareText(
            left: leftContent,
            right: rightContent,
            ignoreWhitespace: ignoreWhitespace,
            ignoreCase: ignoreCase
        )
        self.diffResult = res
        self.isLoading = false
    }

    public func previousDiff() {
        if currentDiffIndex > 0 {
            currentDiffIndex -= 1
        }
    }

    public func nextDiff() {
        let diffCount = diffResult.lines.filter { $0.changeType != .unchanged }.count
        if currentDiffIndex < diffCount - 1 {
            currentDiffIndex += 1
        }
    }

    public func takeLeft() {
        // Copy left hunk to right
    }

    public func takeRight() {
        // Copy right hunk to left
    }
}
