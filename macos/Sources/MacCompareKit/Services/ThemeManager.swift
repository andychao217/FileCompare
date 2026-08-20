import Foundation
import SwiftUI
import AppKit

public enum AppTheme: String, CaseIterable, Identifiable, Sendable {
    case system = "system"
    case light = "light"
    case dark = "dark"

    public var id: String { rawValue }

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
            applyTheme()
        }
    }

    public private(set) var effectiveColorScheme: ColorScheme = .dark
    public private(set) var themeRevision: Int = 0

    public static var isSystemInDarkMode: Bool {
        if let style = CFPreferencesCopyAppValue("AppleInterfaceStyle" as CFString, kCFPreferencesAnyApplication) as? String {
            return style.caseInsensitiveCompare("Dark") == .orderedSame
        }
        return false
    }

    private init() {
        let saved = UserDefaults.standard.string(forKey: "app_appearance_theme") ?? AppTheme.system.rawValue
        self.currentTheme = AppTheme(rawValue: saved) ?? .system
        applyTheme()

        // Listen for macOS system-wide appearance changes (e.g. system dark/light toggle)
        DistributedNotificationCenter.default().addObserver(
            forName: NSNotification.Name("AppleInterfaceThemeChangedNotification"),
            object: nil,
            queue: .main
        ) { [weak self] _ in
            Task { @MainActor in
                self?.systemAppearanceDidChange()
            }
        }
    }

    public func systemAppearanceDidChange() {
        if currentTheme == .system {
            applyTheme()
        }
    }

    public func applyTheme() {
        // 1. Calculate the exact effective color scheme
        switch currentTheme {
        case .light:
            effectiveColorScheme = .light
        case .dark:
            effectiveColorScheme = .dark
        case .system:
            effectiveColorScheme = Self.isSystemInDarkMode ? .dark : .light
        }

        // 2. Apply the matching NSAppearance to App and all Windows
        let targetAppearance: NSAppearance?
        switch currentTheme {
        case .system:
            targetAppearance = nil
        case .light:
            targetAppearance = NSAppearance(named: .aqua)
        case .dark:
            targetAppearance = NSAppearance(named: .darkAqua)
        }

        NSApp?.appearance = targetAppearance
        for window in NSApp?.windows ?? [] {
            window.appearance = targetAppearance
            window.contentView?.needsDisplay = true
        }

        // 3. Increment revision to force re-evaluation of SwiftUI view hierarchy
        themeRevision += 1
    }
}
