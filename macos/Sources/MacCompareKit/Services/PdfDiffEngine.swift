import Foundation

public final class PdfDiffEngine: Sendable {
    public static let shared = PdfDiffEngine()

    public init() {}

    public func compareDocuments(
        left: PdfDocumentModel,
        right: PdfDocumentModel,
        ignoreWhitespace: Bool = false
    ) async -> PdfDiffResult {
        var blocks: [PdfPageDiffBlock] = []
        var totalDiffs = 0

        let maxPages = max(left.pageCount, right.pageCount)

        for index in 0..<maxPages {
            let leftPage = index < left.pages.count ? left.pages[index] : nil
            let rightPage = index < right.pages.count ? right.pages[index] : nil

            if let l = leftPage, let r = rightPage {
                let lText = ignoreWhitespace ? sanitize(l.text) : l.text
                let rText = ignoreWhitespace ? sanitize(r.text) : r.text

                if lText == rText {
                    blocks.append(PdfPageDiffBlock(
                        leftPageIndex: l.pageIndex,
                        rightPageIndex: r.pageIndex,
                        changeType: .unchanged,
                        leftText: l.text,
                        rightText: r.text
                    ))
                } else {
                    totalDiffs += 1
                    let tokens = computeTokenDiff(left: l.text, right: r.text)
                    blocks.append(PdfPageDiffBlock(
                        leftPageIndex: l.pageIndex,
                        rightPageIndex: r.pageIndex,
                        changeType: .modified,
                        leftText: l.text,
                        rightText: r.text,
                        tokens: tokens
                    ))
                }
            } else if let l = leftPage {
                totalDiffs += 1
                blocks.append(PdfPageDiffBlock(
                    leftPageIndex: l.pageIndex,
                    rightPageIndex: nil,
                    changeType: .deleted,
                    leftText: l.text,
                    rightText: nil
                ))
            } else if let r = rightPage {
                totalDiffs += 1
                blocks.append(PdfPageDiffBlock(
                    leftPageIndex: nil,
                    rightPageIndex: r.pageIndex,
                    changeType: .inserted,
                    leftText: nil,
                    rightText: r.text
                ))
            }
        }

        return PdfDiffResult(blocks: blocks, totalDiffCount: totalDiffs)
    }

    private func sanitize(_ text: String) -> String {
        text.components(separatedBy: .whitespacesAndNewlines).filter { !$0.isEmpty }.joined(separator: " ")
    }

    private func computeTokenDiff(left: String, right: String) -> [PdfDiffToken] {
        let lTokens = tokenize(left)
        let rTokens = tokenize(right)

        let m = lTokens.count
        let n = rTokens.count

        guard m > 0 || n > 0 else { return [] }
        if m == 0 {
            return mergeConsecutiveTokens(rTokens.map { PdfDiffToken(text: $0, changeType: .inserted) })
        }
        if n == 0 {
            return mergeConsecutiveTokens(lTokens.map { PdfDiffToken(text: $0, changeType: .deleted) })
        }

        // Standard LCS Dynamic Programming Table
        var dp = [[Int]](repeating: [Int](repeating: 0, count: n + 1), count: m + 1)
        for i in 0..<m {
            for j in 0..<n {
                if lTokens[i] == rTokens[j] {
                    dp[i + 1][j + 1] = dp[i][j] + 1
                } else {
                    dp[i + 1][j + 1] = max(dp[i + 1][j], dp[i][j + 1])
                }
            }
        }

        // Backtrack LCS matrix to reconstruct diff
        var reversedTokens: [PdfDiffToken] = []
        var i = m
        var j = n

        while i > 0 || j > 0 {
            if i > 0 && j > 0 && lTokens[i - 1] == rTokens[j - 1] {
                reversedTokens.append(PdfDiffToken(text: lTokens[i - 1], changeType: .unchanged))
                i -= 1
                j -= 1
            } else if j > 0 && (i == 0 || dp[i][j - 1] >= dp[i - 1][j]) {
                reversedTokens.append(PdfDiffToken(text: rTokens[j - 1], changeType: .inserted))
                j -= 1
            } else if i > 0 && (j == 0 || dp[i][j - 1] < dp[i - 1][j]) {
                reversedTokens.append(PdfDiffToken(text: lTokens[i - 1], changeType: .deleted))
                i -= 1
            }
        }

        let rawTokens = reversedTokens.reversed()
        return mergeConsecutiveTokens(Array(rawTokens))
    }

    private func tokenize(_ text: String) -> [String] {
        var tokens: [String] = []
        var currentAscii = ""

        for char in text {
            if char.isASCII && (char.isLetter || char.isNumber) {
                currentAscii.append(char)
            } else {
                if !currentAscii.isEmpty {
                    tokens.append(currentAscii)
                    currentAscii = ""
                }
                tokens.append(String(char))
            }
        }
        if !currentAscii.isEmpty {
            tokens.append(currentAscii)
        }
        return tokens
    }

    private func mergeConsecutiveTokens(_ tokens: [PdfDiffToken]) -> [PdfDiffToken] {
        guard !tokens.isEmpty else { return [] }
        var merged: [PdfDiffToken] = []
        var currentType = tokens[0].changeType
        var currentText = tokens[0].text

        for token in tokens.dropFirst() {
            if token.changeType == currentType {
                currentText.append(token.text)
            } else {
                merged.append(PdfDiffToken(text: currentText, changeType: currentType))
                currentType = token.changeType
                currentText = token.text
            }
        }
        merged.append(PdfDiffToken(text: currentText, changeType: currentType))
        return merged
    }
}
