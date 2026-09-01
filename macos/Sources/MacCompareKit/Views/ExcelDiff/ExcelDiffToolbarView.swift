import SwiftUI
import AppKit
import UniformTypeIdentifiers

public struct ExcelDiffToolbarView: View {
    @Bindable var viewModel: ExcelDiffViewModel
    @State private var languageManager = LanguageManager.shared
    @State private var showClearConfirmation: Bool = false

    public init(viewModel: ExcelDiffViewModel) {
        self.viewModel = viewModel
    }

    public var body: some View {
        VStack(spacing: 0) {
            // Row 1: Main Actions & Quick Switches Bar
            HStack(spacing: 10) {
                // Navigation (Prev Diff / Next Diff)
                HStack(spacing: 4) {
                    Button {
                        viewModel.prevDiff()
                    } label: {
                        Label(languageManager.text(.prevDiff), systemImage: "arrow.up")
                            .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(!viewModel.hasBothFiles)
                    .help("Previous Difference (⌘↑)")
                    .keyboardShortcut(.upArrow, modifiers: .command)

                    Button {
                        viewModel.nextDiff()
                    } label: {
                        Label(languageManager.text(.nextDiff), systemImage: "arrow.down")
                            .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(!viewModel.hasBothFiles)
                    .help("Next Difference (⌘↓)")
                    .keyboardShortcut(.downArrow, modifiers: .command)
                }

                Divider().frame(height: 16)

                // Inline Toggles: Ignore Whitespace & Ignore Case
                Toggle(isOn: Binding(
                    get: { viewModel.rules.ignoreWhitespace },
                    set: { val in
                        viewModel.rules.ignoreWhitespace = val
                        Task { await viewModel.recompare() }
                    }
                )) {
                    Text(languageManager.text(.ignoreWhitespace))
                        .font(.system(size: 11))
                }
                .toggleStyle(.switch)
                .controlSize(.mini)

                Toggle(isOn: Binding(
                    get: { viewModel.rules.ignoreCase },
                    set: { val in
                        viewModel.rules.ignoreCase = val
                        Task { await viewModel.recompare() }
                    }
                )) {
                    Text(languageManager.text(.ignoreCase))
                        .font(.system(size: 11))
                }
                .toggleStyle(.switch)
                .controlSize(.mini)

                Spacer()

                // Rules Button
                Button {
                    viewModel.isRulesSheetPresented = true
                } label: {
                    Label(languageManager.text(.excelRules), systemImage: "slider.horizontal.3")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .help("Comparison Rules & Key Columns")

                // Swap Button
                Button {
                    viewModel.swapFiles()
                } label: {
                    Label(languageManager.text(.swapSides), systemImage: "arrow.left.arrow.right")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(!viewModel.hasBothFiles)
                .help("Swap Left and Right Files")

                // Reload Button
                Button {
                    Task { await viewModel.loadAndCompare() }
                } label: {
                    Label(languageManager.text(.refresh), systemImage: "arrow.clockwise")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(!viewModel.hasLeftFile && !viewModel.hasRightFile)
                .help("Reload & Re-compare")

                Divider().frame(height: 16)

                // Merge Actions: Take Left & Take Right
                HStack(spacing: 4) {
                    Button {
                        viewModel.takeLeft()
                    } label: {
                        Label(languageManager.text(.takeLeft), systemImage: "arrow.right.to.line")
                            .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(!viewModel.hasBothFiles)
                    .help("Copy left spreadsheet to right")

                    Button {
                        viewModel.takeRight()
                    } label: {
                        Label(languageManager.text(.takeRight), systemImage: "arrow.left.to.line")
                            .font(.system(size: 11))
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.small)
                    .disabled(!viewModel.hasBothFiles)
                    .help("Copy right spreadsheet to left")
                }

                Divider().frame(height: 16)

                // Clear All Button
                Button {
                    showClearConfirmation = true
                } label: {
                    Label(languageManager.text(.clearAll), systemImage: "trash")
                        .font(.system(size: 11))
                }
                .buttonStyle(.bordered)
                .controlSize(.small)
                .disabled(!viewModel.hasLeftFile && !viewModel.hasRightFile)
                .help(languageManager.text(.clearAll))
                .alert(languageManager.text(.confirmClearTitle), isPresented: $showClearConfirmation) {
                    Button(languageManager.text(.clear), role: .destructive) {
                        viewModel.clearAll()
                    }
                    Button(languageManager.text(.cancel), role: .cancel) {}
                } message: {
                    Text(languageManager.text(.confirmClearMessage))
                }
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 5)
            .background(Color(nsColor: .windowBackgroundColor))

            Divider()

            // Row 2: Filter Segmentation & Search Bar
            HStack(spacing: 12) {
                // Filter Buttons (All / Diffs / Same)
                Picker("", selection: $viewModel.filterMode) {
                    Text(languageManager.text(.filterAll)).tag(ExcelDiffFilter.all)
                    Text(languageManager.text(.filterDiffs)).tag(ExcelDiffFilter.diffs)
                    Text(languageManager.text(.filterSame)).tag(ExcelDiffFilter.same)
                }
                .pickerStyle(.segmented)
                .frame(width: 220)
                .controlSize(.small)
                .offset(x: -2)
                .disabled(!viewModel.hasBothFiles)

                Spacer()

                // Search Bar
                HStack(spacing: 6) {
                    Image(systemName: "magnifyingglass")
                        .foregroundColor(.secondary)
                        .font(.system(size: 11))
                    TextField(languageManager.text(.searchOrFilter), text: $viewModel.searchQuery)
                        .textFieldStyle(.plain)
                        .font(.system(size: 11))
                    if !viewModel.searchQuery.isEmpty {
                        Button {
                            viewModel.searchQuery = ""
                        } label: {
                            Image(systemName: "xmark.circle.fill")
                                .foregroundColor(.secondary)
                                .font(.system(size: 11))
                        }
                        .buttonStyle(.plain)
                    }
                }
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(Color(nsColor: .controlBackgroundColor))
                .cornerRadius(6)
                .overlay(
                    RoundedRectangle(cornerRadius: 6)
                        .stroke(Color(nsColor: .separatorColor).opacity(0.6), lineWidth: 0.8)
                )
                .frame(width: 240)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, 4)
            .background(Color(nsColor: .windowBackgroundColor).opacity(0.85))

            Divider()

            // Row 3: File Headers Bar (Left & Right)
            HStack(spacing: 0) {
                fileHeaderView(
                    isLeft: true,
                    title: viewModel.hasLeftFile ? viewModel.leftTitle : languageManager.text(.sourceFile),
                    url: viewModel.leftURL,
                    workbook: viewModel.leftWorkbook,
                    onOpen: { viewModel.openLeftFile() }
                )

                Divider()

                fileHeaderView(
                    isLeft: false,
                    title: viewModel.hasRightFile ? viewModel.rightTitle : languageManager.text(.targetFile),
                    url: viewModel.rightURL,
                    workbook: viewModel.rightWorkbook,
                    onOpen: { viewModel.openRightFile() }
                )
            }
            .frame(height: 28)
            .background(Color(nsColor: .controlBackgroundColor).opacity(0.6))

            Divider()
        }
    }

    @ViewBuilder
    private func fileHeaderView(
        isLeft: Bool,
        title: String,
        url: URL?,
        workbook: ExcelWorkbookModel?,
        onOpen: @escaping () -> Void
    ) -> some View {
        HStack(spacing: 8) {
            Image(systemName: "tablecells")
                .foregroundColor(.green)
                .font(.system(size: 12))

            if let url = url, let wb = workbook {
                Text(url.lastPathComponent)
                    .font(.system(size: 11, weight: .medium))
                    .lineLimit(1)
                    .truncationMode(.middle)
                Text("(\(wb.metadata.fileSizeFormatted))")
                    .font(.system(size: 10))
                    .foregroundColor(.secondary)
            } else {
                Text(title)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }

            Spacer()

            Button(languageManager.text(.chooseButton)) {
                onOpen()
            }
            .buttonStyle(.bordered)
            .controlSize(.mini)
        }
        .padding(.horizontal, 12)
        .frame(maxWidth: .infinity)
    }
}
