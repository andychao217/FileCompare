import Foundation
import MacCompareKit

func runCLI() async {
    let args = CommandLine.arguments

    if args.contains("-h") || args.contains("--help") || args.count < 2 {
        printUsage()
        return
    }

    if args.contains("-v") || args.contains("--version") {
        print("mcdiff version 0.2.0 (MacCompare CLI)")
        return
    }

    if let mergeIndex = args.firstIndex(of: "--merge") {
        let remaining = args.dropFirst(mergeIndex + 1)
        if remaining.count >= 3 {
            let localPath = Array(remaining)[0]
            let basePath = Array(remaining)[1]
            let remotePath = Array(remaining)[2]

            do {
                let localContent = try String(contentsOfFile: localPath)
                let baseContent = try String(contentsOfFile: basePath)
                let remoteContent = try String(contentsOfFile: remotePath)

                let result = await DiffEngineService.shared.mergeThreeWay(
                    local: localContent,
                    base: baseContent,
                    remote: remoteContent
                )

                if let outIndex = args.firstIndex(of: "-o"), outIndex + 1 < args.count {
                    let outPath = args[outIndex + 1]
                    try result.mergedText.write(toFile: outPath, atomically: true, encoding: .utf8)
                    print("Merged output written to: \(outPath)")
                } else {
                    print(result.mergedText)
                }

                if result.conflictCount > 0 {
                    print("[mcdiff] Warning: \(result.conflictCount) conflicts detected.")
                    exit(1)
                } else {
                    print("[mcdiff] Successfully merged without conflicts.")
                    exit(0)
                }
            } catch {
                print("[mcdiff] Error reading files: \(error.localizedDescription)")
                exit(1)
            }
        } else {
            print("Usage: mcdiff --merge <local> <base> <remote> [-o <output>]")
            exit(1)
        }
    }

    // 2-Way Diff
    let path1 = args[1]
    let path2 = args[2]

    var isDir1: ObjCBool = false
    var isDir2: ObjCBool = false

    let fm = FileManager.default
    if fm.fileExists(atPath: path1, isDirectory: &isDir1) && fm.fileExists(atPath: path2, isDirectory: &isDir2) {
        if isDir1.boolValue && isDir2.boolValue {
            print("[mcdiff] Comparing directories: \(path1) <-> \(path2)")
            let entries = await DiffEngineService.shared.compareFolders(leftPath: path1, rightPath: path2)
            for entry in entries {
                print("[\(entry.status.rawValue)] \(entry.relativePath)")
            }
        } else {
            do {
                let leftText = try String(contentsOfFile: path1)
                let rightText = try String(contentsOfFile: path2)
                let res = await DiffEngineService.shared.compareText(left: leftText, right: rightText)

                print("--- \(path1)")
                print("+++ \(path2)")
                print("@@ Additions: \(res.totalAdditions), Deletions: \(res.totalDeletions), Modifications: \(res.totalModifications) @@")

                for line in res.lines {
                    switch line.changeType {
                    case .unchanged:
                        print("  \(line.contentLeft)")
                    case .added:
                        print("+ \(line.contentRight)")
                    case .deleted:
                        print("- \(line.contentLeft)")
                    case .modified:
                        print("~ \(line.contentLeft) => \(line.contentRight)")
                    }
                }
            } catch {
                print("[mcdiff] Error reading files: \(error.localizedDescription)")
                exit(1)
            }
        }
    } else {
        print("[mcdiff] Error: One or both paths do not exist.")
        exit(1)
    }
}

func printUsage() {
    print("""
    MacCompare CLI (mcdiff) - Fast native diff & merge tool for macOS

    USAGE:
        mcdiff <file_a> <file_b>                      Compare two files
        mcdiff <dir_a> <dir_b>                        Compare two directories
        mcdiff --merge <local> <base> <remote> [-o <out>]   3-Way conflict merge

    OPTIONS:
        -h, --help        Show this help message
        -v, --version     Show version information
        -o <file>         Output merged file destination
    """)
}

await runCLI()
