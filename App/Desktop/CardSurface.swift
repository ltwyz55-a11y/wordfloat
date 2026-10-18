import SwiftUI
import AppKit
import WordCore

extension CardRGB {
    var color: Color { Color(.sRGB, red: red, green: green, blue: blue, opacity: 1) }
    init(color: Color) {
        let value = NSColor(color).usingColorSpace(.sRGB) ?? .black
        self.init(value.redComponent, value.greenComponent, value.blueComponent)
    }
}

struct CardSurface: View {
    let style: CardStyle
    var wallpaper: NSImage?
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency

    var body: some View {
        ZStack {
            if style.mode == .wallpaper, let wallpaper {
                GeometryReader { geometry in
                    Image(nsImage: wallpaper).resizable().scaledToFill()
                        .frame(width: geometry.size.width, height: geometry.size.height).clipped()
                }
                style.tint.color.opacity(style.tintOpacity)
            } else if style.mode == .solid {
                style.tint.color
            } else if reduceTransparency {
                if style.followsSystem { Color(nsColor: .windowBackgroundColor) }
                else { style.tint.color }
            } else {
                if style.mode == .frosted { FrostedBackdrop(followsSystem: style.followsSystem) }
                if !style.followsSystem { style.tint.color.opacity(style.tintOpacity) }
                LinearGradient(colors: [.white.opacity(0.10), .clear, .black.opacity(0.05)], startPoint: .topLeading, endPoint: .bottomTrailing)
            }
        }
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay {
            RoundedRectangle(cornerRadius: 18, style: .continuous).strokeBorder(
                LinearGradient(colors: [.white.opacity(0.42), .white.opacity(0.09), .white.opacity(0.24)],
                    startPoint: .topLeading, endPoint: .bottomTrailing), lineWidth: 0.8)
        }
        .allowsHitTesting(false)
    }
}

private struct FrostedBackdrop: NSViewRepresentable {
    let followsSystem: Bool
    func makeNSView(context: Context) -> NSVisualEffectView {
        let view = NSVisualEffectView()
        view.blendingMode = .behindWindow
        view.state = .active
        view.wantsLayer = true
        view.layer?.cornerRadius = 18
        view.layer?.masksToBounds = true
        return view
    }
    func updateNSView(_ view: NSVisualEffectView, context: Context) {
        view.material = followsSystem ? .popover : .hudWindow
        view.appearance = followsSystem ? nil : NSAppearance(named: .darkAqua)
    }
}
