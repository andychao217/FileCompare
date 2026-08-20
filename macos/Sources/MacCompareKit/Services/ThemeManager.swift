import Foundation
import SwiftUI
import AppKit

public enum AppTheme: String, CaseIterable, Identifiable, Sendable {
    case system = "system"
    case light = "light"
    case dark = "dark"

    public var id: String { rawValue }

    public var colorScheme: ColorScheme? {
        switch self {
        case .system: return nil
        case .light: return .light
        case .dark: return .dark
        }
    }

    public func localizedName(for language: AppLanguage) -> String {
        switch self {
        case .system:
            switch language {
            case .zhHans: return "跟随系统 (Auto)"
            case .ja: return "システム設定 (Auto)"
            default: return "Auto (System)"
            }
        case .light:
            switch language {
            case .zhHans: return "浅色模式 (Light)"
            case .ja: return "ライト (Light)"
            default: return "Light"
            }
        case .dark:
            switch language {
            case .zhHans: return "深色模式 (Dark)"
            case .ja: return "ダーク (Dark)"
            default: return "Dark"
            }
        }
    }
}

@MainActor
@Observable
public final class ThemeManager {
    public static let shared = ThemeManager()

    public var currentTheme: AppTheme {
        didSet {
            UserDefaults.standard.set(currentTheme.rawValue, forKey: "app_appearance_theme")
            applyAppearance()
        }
    }

    public var colorScheme: ColorScheme? {
        currentTheme.colorScheme
    }

    private init() {
        let saved = UserDefaults.standard.string(forKey: "app_appearance_theme") ?? AppTheme.system.rawValue
        self.currentTheme = AppTheme(rawValue: saved) ?? .system
        applyAppearance()
    }

    public func applyAppearance() {
        switch self.currentTheme {
        case .system:
            NSApp?.appearance = nil
        case .light:
            NSApp?.appearance = NSAppearance(named: .aqua)
        case .dark:
            NSApp?.appearance = NSAppearance(named: .darkAqua)
        }
    }
}
