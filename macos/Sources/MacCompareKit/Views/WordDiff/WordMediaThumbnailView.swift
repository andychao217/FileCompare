import SwiftUI
import AppKit

public struct WordMediaThumbnailView: View {
    public let mediaDiff: WordMediaDiffItem
    public let isLeft: Bool

    @State private var isShowingDetailPopover: Bool = false

    public init(mediaDiff: WordMediaDiffItem, isLeft: Bool) {
        self.mediaDiff = mediaDiff
        self.isLeft = isLeft
    }

    private var activeMedia: WordMediaItem? {
        isLeft ? mediaDiff.leftMedia : mediaDiff.rightMedia
    }

    public var body: some View {
        if let media = activeMedia {
            VStack(alignment: .leading, spacing: 4) {
                // Main Thumbnail or Placeholder
                Group {
                    if media.mediaType == .image, let data = media.data, let nsImage = NSImage(data: data) {
                        Image(nsImage: nsImage)
                            .resizable()
                            .aspectRatio(contentMode: .fit)
                            .frame(maxWidth: min(260, media.widthPoints ?? 240), maxHeight: min(180, media.heightPoints ?? 160))
                            .clipShape(RoundedRectangle(cornerRadius: 6))
                            .overlay(
                                RoundedRectangle(cornerRadius: 6)
                                    .stroke(borderColor, lineWidth: 1.5)
                            )
                            .shadow(color: Color.black.opacity(0.08), radius: 3, x: 0, y: 1)
                            .onTapGesture {
                                isShowingDetailPopover = true
                            }
                    } else {
                        // Video / Audio / Attachment Media Card
                        HStack(spacing: 8) {
                            ZStack {
                                RoundedRectangle(cornerRadius: 6)
                                    .fill(iconBackgroundColor)
                                    .frame(width: 36, height: 36)

                                Image(systemName: media.mediaType.iconName)
                                    .font(.system(size: 16))
                                    .foregroundColor(.white)
                            }

                            VStack(alignment: .leading, spacing: 2) {
                                Text(media.fileName)
                                    .font(.system(size: 11, weight: .semibold))
                                    .lineLimit(1)
                                    .foregroundColor(.primary)

                                HStack(spacing: 6) {
                                    Text(media.formattedSize)
                                        .font(.system(size: 10, design: .monospaced))
                                        .foregroundColor(.secondary)

                                    Text("• \(media.fileExtension.uppercased())")
                                        .font(.system(size: 10))
                                        .foregroundColor(.secondary)
                                }
                            }

                            Spacer()

                            if media.mediaType == .video || media.mediaType == .audio {
                                Image(systemName: "play.circle.fill")
                                    .font(.system(size: 20))
                                    .foregroundColor(.accentColor)
                            }
                        }
                        .padding(8)
                        .frame(maxWidth: 260)
                        .background(
                            RoundedRectangle(cornerRadius: 6)
                                .fill(Color(nsColor: .controlBackgroundColor))
                                .overlay(
                                    RoundedRectangle(cornerRadius: 6)
                                        .stroke(borderColor, lineWidth: 1.5)
                                )
                        )
                        .onTapGesture {
                            isShowingDetailPopover = true
                        }
                    }
                }

                // Diff Status Badge & Descriptions
                if mediaDiff.changeType != .unchanged {
                    HStack(spacing: 4) {
                        Circle()
                            .fill(statusColor)
                            .frame(width: 6, height: 6)

                        Text(statusBadgeText)
                            .font(.system(size: 10, weight: .medium))
                            .foregroundColor(statusColor)

                        if !mediaDiff.changeDescriptions.isEmpty {
                            Text("• \(mediaDiff.changeDescriptions.first ?? "")")
                                .font(.system(size: 10))
                                .foregroundColor(.secondary)
                                .lineLimit(1)
                        }
                    }
                    .padding(.horizontal, 4)
                }
            }
            .popover(isPresented: $isShowingDetailPopover) {
                mediaDetailPopover(media: media)
            }
        } else {
            // Phantom Media Placeholder for deletions/additions
            HStack(spacing: 6) {
                Image(systemName: isLeft ? "photo.badge.plus" : "photo.badge.minus")
                    .font(.system(size: 14))
                    .foregroundColor(.secondary.opacity(0.5))
                Text(isLeft ? "右侧新增了媒体资源" : "左侧已移除此媒体资源")
                    .font(.system(size: 11))
                    .foregroundColor(.secondary.opacity(0.7))
            }
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
            .frame(maxWidth: 240, alignment: .leading)
            .background(
                RoundedRectangle(cornerRadius: 6)
                    .stroke(style: StrokeStyle(lineWidth: 1, dash: [4, 4]))
                    .foregroundColor(Color.secondary.opacity(0.3))
            )
        }
    }

    // MARK: - Detail Popover

    private func mediaDetailPopover(media: WordMediaItem) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: media.mediaType.iconName)
                    .foregroundColor(.accentColor)
                Text(media.fileName)
                    .font(.system(size: 12, weight: .bold))
                Spacer()
                Text(media.mediaType.rawValue)
                    .font(.caption2)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 2)
                    .background(Capsule().fill(Color.secondary.opacity(0.15)))
            }

            Divider()

            if media.mediaType == .image, let data = media.data, let nsImage = NSImage(data: data) {
                Image(nsImage: nsImage)
                    .resizable()
                    .aspectRatio(contentMode: .fit)
                    .frame(maxWidth: 380, maxHeight: 280)
                    .clipShape(RoundedRectangle(cornerRadius: 6))
            }

            VStack(alignment: .leading, spacing: 4) {
                infoRow(label: "文件大小", value: media.formattedSize)
                if let w = media.widthPoints, let h = media.heightPoints {
                    infoRow(label: "显示尺寸", value: "\(Int(w)) × \(Int(h)) pt")
                }
                infoRow(label: "SHA-256", value: String(media.hashSHA256.prefix(16)) + "...")
                if !mediaDiff.changeDescriptions.isEmpty {
                    Divider().padding(.vertical, 2)
                    Text("变更详情:")
                        .font(.system(size: 10, weight: .semibold))
                        .foregroundColor(.primary)
                    ForEach(mediaDiff.changeDescriptions, id: \.self) { desc in
                        Text("• \(desc)")
                            .font(.system(size: 10))
                            .foregroundColor(.secondary)
                    }
                }
            }
        }
        .padding(14)
        .frame(width: 320)
    }

    private func infoRow(label: String, value: String) -> some View {
        HStack {
            Text("\(label):")
                .font(.system(size: 10))
                .foregroundColor(.secondary)
            Spacer()
            Text(value)
                .font(.system(size: 10, weight: .medium, design: .monospaced))
                .foregroundColor(.primary)
        }
    }

    // MARK: - Styling Helpers

    private var borderColor: Color {
        switch mediaDiff.changeType {
        case .added: return Color.green.opacity(0.7)
        case .deleted: return Color.red.opacity(0.7)
        case .modified: return Color.orange.opacity(0.7)
        case .unchanged: return Color.secondary.opacity(0.2)
        }
    }

    private var statusColor: Color {
        switch mediaDiff.changeType {
        case .added: return .green
        case .deleted: return .red
        case .modified: return .orange
        case .unchanged: return .secondary
        }
    }

    private var statusBadgeText: String {
        switch mediaDiff.changeType {
        case .added: return "新增"
        case .deleted: return "已删除"
        case .modified: return "已修改"
        case .unchanged: return "未修改"
        }
    }

    private var iconBackgroundColor: Color {
        switch mediaDiff.mediaType {
        case .image: return .blue
        case .video: return .purple
        case .audio: return .orange
        case .attachment: return .teal
        }
    }
}
