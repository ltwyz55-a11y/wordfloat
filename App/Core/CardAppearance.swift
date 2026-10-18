import Foundation
import Combine
import AppKit
import ImageIO
import UniformTypeIdentifiers

public struct CardRGB: Codable, Equatable {
    public var red: Double
    public var green: Double
    public var blue: Double
    public init(_ red: Double, _ green: Double, _ blue: Double) {
        self.red = red; self.green = green; self.blue = blue
        self = normalized
    }
    public var normalized: CardRGB {
        var copy = self
        copy.red = Self.clamp(red); copy.green = Self.clamp(green); copy.blue = Self.clamp(blue)
        return copy
    }
    private static func clamp(_ value: Double) -> Double { value.isFinite ? min(1, max(0, value)) : 0 }
    public static let white = CardRGB(1, 1, 1)
    public static let charcoal = CardRGB(0.13, 0.15, 0.16)
}

public enum CardBackgroundMode: String, Codable, CaseIterable {
    case frosted, clear, solid, wallpaper
    public var title: String {
        switch self { case .frosted: return "磨砂"; case .clear: return "清透"; case .solid: return "纯色"; case .wallpaper: return "图片" }
    }
}

public enum CardPreset: String, Codable, CaseIterable, Identifiable {
    case system, frosted, clear, paper, sage, midnight
    public var id: String { rawValue }
    public var title: String {
        switch self {
        case .system: return "跟随系统"; case .frosted: return "雾面玻璃"; case .clear: return "清透玻璃"
        case .paper: return "暖白纸卡"; case .sage: return "鼠尾草"; case .midnight: return "午夜蓝"
        }
    }
    public var style: CardStyle {
        var result = CardStyle()
        result.preset = self
        result.followsSystem = self == .system
        switch self {
        case .system: break
        case .frosted: result.mode = .frosted; result.tint = CardRGB(0.19,0.24,0.22); result.tintOpacity = 0.32; result.text = .white
        case .clear: result.mode = .clear; result.tint = CardRGB(0.12,0.18,0.19); result.tintOpacity = 0.12; result.text = .white
        case .paper: result.mode = .solid; result.tint = CardRGB(0.97,0.95,0.89); result.text = CardRGB(0.25,0.23,0.20)
        case .sage: result.mode = .solid; result.tint = CardRGB(0.77,0.83,0.76); result.text = CardRGB(0.16,0.25,0.20)
        case .midnight: result.mode = .solid; result.tint = CardRGB(0.10,0.15,0.23); result.text = CardRGB(0.92,0.95,0.99)
        }
        return result
    }
}

public struct CardStyle: Codable, Equatable {
    public var preset: CardPreset? = .system
    public var mode: CardBackgroundMode = .frosted
    public var tint = CardRGB(0.92,0.94,0.93)
    public var text = CardRGB.charcoal
    public var tintOpacity = 0.18
    public var followsSystem = true
    public var wallpaperFile: String?
    public init() {}
}

public struct AppearanceError: LocalizedError {
    public let message: String
    public var errorDescription: String? { message }
}

@MainActor
public final class CardAppearance: ObservableObject {
    @Published public private(set) var style: CardStyle
    @Published public private(set) var wallpaper: NSImage?
    private let defaults: UserDefaults?
    public let wallpaperDirectory: URL
    private static let key = "card.appearance.v1"

    public init(defaults: UserDefaults? = nil, wallpaperDirectory: URL? = nil) {
        self.defaults = defaults
        self.wallpaperDirectory = wallpaperDirectory ?? FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
            .appendingPathComponent("local.wordfloat.mac/Wallpapers", isDirectory: true)
        var saved = defaults?.data(forKey: Self.key).flatMap { try? JSONDecoder().decode(CardStyle.self, from: $0) } ?? CardStyle()
        saved.tint = saved.tint.normalized; saved.text = saved.text.normalized
        saved.tintOpacity = saved.tintOpacity.isFinite ? min(1,max(0,saved.tintOpacity)) : 0.18
        var loadedImage: NSImage?
        if let file = saved.wallpaperFile, file == URL(fileURLWithPath: file).lastPathComponent, file.hasSuffix(".png") {
            loadedImage = NSImage(contentsOf: self.wallpaperDirectory.appendingPathComponent(file))
        }
        if loadedImage == nil {
            saved.wallpaperFile = nil
            if saved.mode == .wallpaper { saved = CardPreset.system.style }
        }
        style = saved
        wallpaper = loadedImage
    }
    public func apply(_ preset: CardPreset) {
        var next = preset.style
        next.wallpaperFile = style.wallpaperFile
        commit(next)
    }
    public func setMode(_ mode: CardBackgroundMode) {
        guard mode != .wallpaper || wallpaper != nil else { return }
        var next = style; next.mode = mode; next.preset = nil; next.followsSystem = false
        commit(next)
    }
    public func setTint(_ color: CardRGB) {
        var next = style; next.tint = color.normalized; next.preset = nil; next.followsSystem = false
        commit(next)
    }
    public func setText(_ color: CardRGB) {
        var next = style; next.text = color.normalized; next.preset = nil; next.followsSystem = false
        commit(next)
    }
    public func setOpacity(_ opacity: Double) {
        guard opacity.isFinite else { return }
        var next = style; next.tintOpacity = min(1,max(0,opacity)); next.preset = nil; next.followsSystem = false
        commit(next)
    }
    public func importWallpaper(from url: URL) throws {
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        let size = try url.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
        guard size > 0, size <= 50 * 1024 * 1024 else { throw AppearanceError(message: "请选择 50 MB 以内的图片。") }
        guard let source = CGImageSourceCreateWithURL(url as CFURL, [kCGImageSourceShouldCache: false] as CFDictionary),
            let image = CGImageSourceCreateThumbnailAtIndex(source, 0, [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: 1600
            ] as CFDictionary) else { throw AppearanceError(message: "无法读取这张图片，请选择 PNG、JPEG、HEIC 或 TIFF 图片。") }
        let bytes = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(bytes, UTType.png.identifier as CFString, 1, nil) else {
            throw AppearanceError(message: "无法保存壁纸，请重试。")
        }
        CGImageDestinationAddImage(destination, image, nil)
        guard CGImageDestinationFinalize(destination) else { throw AppearanceError(message: "图片转换失败，请换一张图片。") }
        try FileManager.default.createDirectory(at: wallpaperDirectory, withIntermediateDirectories: true)
        let name = UUID().uuidString + ".png"
        try (bytes as Data).write(to: wallpaperDirectory.appendingPathComponent(name), options: .atomic)
        let previous = style.wallpaperFile
        wallpaper = NSImage(cgImage: image, size: NSSize(width: image.width, height: image.height))
        var next = style
        next.wallpaperFile = name; next.mode = .wallpaper; next.preset = nil; next.followsSystem = false
        next.tint = CardRGB(0,0,0); next.tintOpacity = 0.28; next.text = .white
        commit(next)
        removeManaged(previous)
    }
    public func removeWallpaper() {
        let previous = style.wallpaperFile
        wallpaper = nil
        var next = style.mode == .wallpaper ? CardPreset.system.style : style
        next.wallpaperFile = nil
        commit(next)
        removeManaged(previous)
    }
    private func removeManaged(_ name: String?) {
        guard let name, UUID(uuidString: String(name.dropLast(4))) != nil, name.hasSuffix(".png") else { return }
        try? FileManager.default.removeItem(at: wallpaperDirectory.appendingPathComponent(name))
    }
    private func commit(_ next: CardStyle) {
        style = next
        if let data = try? JSONEncoder().encode(next) { defaults?.set(data, forKey: Self.key) }
    }
}
