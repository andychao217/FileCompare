import Foundation
import AppKit
import UniformTypeIdentifiers

extension NSItemProvider {
    public func loadFileURL(completion: @escaping @Sendable (URL?) -> Void) {
        nonisolated(unsafe) let provider = self
        if canLoadObject(ofClass: URL.self) {
            _ = loadObject(ofClass: URL.self) { reading, _ in
                if let url = reading {
                    completion(url)
                    return
                }
                Self.fallbackLoadItemURL(from: provider, completion: completion)
            }
        } else {
            Self.fallbackLoadItemURL(from: provider, completion: completion)
        }
    }

    private static func fallbackLoadItemURL(from provider: NSItemProvider, completion: @escaping @Sendable (URL?) -> Void) {
        let typeId = UTType.fileURL.identifier
        if provider.hasItemConformingToTypeIdentifier(typeId) {
            provider.loadItem(forTypeIdentifier: typeId, options: nil) { item, _ in
                if let url = item as? URL {
                    completion(url)
                } else if let data = item as? Data, let url = URL(dataRepresentation: data, relativeTo: nil) {
                    completion(url)
                } else if let str = item as? String, let url = URL(string: str) {
                    completion(url)
                } else {
                    completion(nil)
                }
            }
        } else {
            completion(nil)
        }
    }
}
