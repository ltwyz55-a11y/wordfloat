import XCTest
import AppKit
@testable import WordCore

@MainActor
final class AppearanceTests: XCTestCase {
    func testPresetsCustomColorsAndPersistence() throws {
        let name = "WordFloat.AppearanceTests.\(UUID())"
        let prefs = UserDefaults(suiteName: name)!
        defer { prefs.removePersistentDomain(forName: name) }
        let appearance = CardAppearance(defaults: prefs)
        XCTAssertTrue(appearance.style.followsSystem)
        appearance.apply(.frosted)
        XCTAssertEqual(appearance.style.text, .white)
        XCTAssertEqual(appearance.style.mode, .frosted)
        appearance.setTint(CardRGB(-1,0.4,2))
        appearance.setText(CardRGB(0.2,0.3,0.4))
        appearance.setOpacity(1.5)
        XCTAssertNil(appearance.style.preset)
        let restored = CardAppearance(defaults: prefs)
        XCTAssertEqual(restored.style, appearance.style)
        XCTAssertEqual(restored.style.tint, CardRGB(0,0.4,1))
        XCTAssertEqual(restored.style.tintOpacity, 1)
        appearance.apply(.system)
        XCTAssertTrue(appearance.style.followsSystem)
    }
    func testManagedWallpaperSurvivesSourceRemovalAndInvalidImport() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let name = "WordFloat.AppearanceTests.\(UUID())"
        let prefs = UserDefaults(suiteName: name)!
        defer { prefs.removePersistentDomain(forName: name) }
        let image = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:4,pixelsHigh:4,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
        memset(image.bitmapData!, 180, image.bytesPerRow * image.pixelsHigh)
        let source = root.appendingPathComponent("original.png")
        try image.representation(using: .png, properties: [:])!.write(to: source)
        let managed = root.appendingPathComponent("managed")
        let appearance = CardAppearance(defaults: prefs, wallpaperDirectory: managed)
        try appearance.importWallpaper(from: source)
        let chosenStyle = appearance.style
        try FileManager.default.removeItem(at: source)
        let restored = CardAppearance(defaults: prefs, wallpaperDirectory: managed)
        XCTAssertNotNil(restored.wallpaper)
        XCTAssertEqual(restored.style, chosenStyle)
        try Data("not an image".utf8).write(to: source)
        XCTAssertThrowsError(try restored.importWallpaper(from: source))
        XCTAssertEqual(restored.style, chosenStyle)
        restored.apply(.sage)
        XCTAssertNotNil(restored.wallpaper)
        restored.setMode(.wallpaper)
        restored.removeWallpaper()
        XCTAssertTrue(restored.style.followsSystem)
        XCTAssertTrue(try FileManager.default.contentsOfDirectory(atPath: managed.path).isEmpty)
        XCTAssertTrue(FileManager.default.fileExists(atPath: source.path))
    }
    func testMissingWallpaperAndCorruptedPreferencesRecover() throws {
        let name = "WordFloat.AppearanceTests.\(UUID())"
        let prefs = UserDefaults(suiteName: name)!
        defer { prefs.removePersistentDomain(forName: name) }
        var missing = CardPreset.frosted.style
        missing.mode = .wallpaper; missing.wallpaperFile = "../outside.png"
        prefs.set(try JSONEncoder().encode(missing), forKey: "card.appearance.v1")
        XCTAssertTrue(CardAppearance(defaults: prefs).style.followsSystem)
        prefs.set(Data("invalid".utf8), forKey: "card.appearance.v1")
        XCTAssertEqual(CardAppearance(defaults: prefs).style, CardStyle())
    }
}
