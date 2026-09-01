import Foundation
import SwiftUI

public struct RecentCompareRecord: Identifiable, Codable, Equatable, Sendable {
    public let id: UUID
    public let title: String
    public let leftPath: String
    public let rightPath: String
    public let typeRawValue: String
    public let timestamp: Date

    public init(
        id: UUID = UUID(),
        title: String? = nil,
        leftPath: String,
        rightPath: String,
        type: TabContentType,
        timestamp: Date = Date()
    ) {
        self.id = id
        let leftName = URL(fileURLWithPath: leftPath).lastPathComponent
        let rightName = URL(fileURLWithPath: rightPath).lastPathComponent
        self.title = title ?? "\(leftName) ↔ \(rightName)"
        self.leftPath = leftPath
        self.rightPath = rightPath
        self.typeRawValue = type.rawValue
        self.timestamp = timestamp
    }

    public var type: TabContentType {
        TabContentType(rawValue: typeRawValue) ?? .textDiff
    }

    public var formattedDate: String {
        let formatter = DateFormatter()
        formatter.dateFormat = "MM-dd HH:mm"
        return formatter.string(from: timestamp)
    }
}

@MainActor
@Observable
public final class RecentHistoryManager {
    public static let shared = RecentHistoryManager()

    private let storageKey = "MacCompare_RecentCompareRecords"
    public var records: [RecentCompareRecord] = []

    public init() {
        loadRecords()
    }

    public func addRecord(left: URL, right: URL, type: TabContentType) {
        let leftPath = left.path
        let rightPath = right.path

        // Deduplicate
        records.removeAll(where: {
            ($0.leftPath == leftPath && $0.rightPath == rightPath) ||
            ($0.leftPath == rightPath && $0.rightPath == leftPath)
        })

        let newRecord = RecentCompareRecord(
            leftPath: leftPath,
            rightPath: rightPath,
            type: type,
            timestamp: Date()
        )

        records.insert(newRecord, at: 0)

        // Limit to 10 records
        if records.count > 10 {
            records = Array(records.prefix(10))
        }

        saveRecords()
    }

    public func removeRecord(id: UUID) {
        records.removeAll(where: { $0.id == id })
        saveRecords()
    }

    public func clearAll() {
        records.removeAll()
        UserDefaults.standard.removeObject(forKey: storageKey)
    }

    private func loadRecords() {
        guard let data = UserDefaults.standard.data(forKey: storageKey),
              let decoded = try? JSONDecoder().decode([RecentCompareRecord].self, from: data) else {
            self.records = []
            return
        }
        self.records = decoded
    }

    private func saveRecords() {
        if let encoded = try? JSONEncoder().encode(records) {
            UserDefaults.standard.set(encoded, forKey: storageKey)
        }
    }
}
