import SwiftUI
import AppKit
import UniformTypeIdentifiers
import WordCore

@MainActor
final class LibraryNavigation: ObservableObject {
    @Published var lookupQuery = ""
    @Published var showsAppearance = false
}

struct AppearanceSettingsView: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    @ObservedObject var appearance: CardAppearance
    var onReturn: () -> Void
    @State private var previewDefinition = false
    @State private var error: String?

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            HStack {
                VStack(alignment: .leading, spacing: 6) {
                    Text("卡片外观").font(.system(size: 27, weight: .semibold))
                    Text("选一种喜欢的样子。").font(.system(size: 13)).foregroundStyle(.secondary)
                }
                Spacer()
                Button(action: chooseWallpaper) {
                    Label(appearance.wallpaper == nil ? "图片壁纸…" : "更换图片…", systemImage: "photo")
                }.controlSize(.large)
                Button("悬浮学习", action: onReturn).buttonStyle(.borderedProminent).controlSize(.large)
            }
            ScrollView {
                VStack(alignment: .leading, spacing: 22) {
                    preview
                    VStack(alignment: .leading, spacing: 10) {
                        Text("预设").font(.system(size: 13, weight: .medium))
                        LazyVGrid(columns: Array(repeating: GridItem(.flexible(), spacing: 10), count: 3), spacing: 10) {
                            ForEach(CardPreset.allCases) { preset in
                                Button { appearance.apply(preset) } label: {
                                    HStack(spacing: 8) {
                                        RoundedRectangle(cornerRadius: 7)
                                            .fill(preset == .system ? Color(nsColor: .controlBackgroundColor) : preset.style.tint.color)
                                            .frame(width: 30, height: 30)
                                            .overlay(Text("Aa").font(.system(size: 11, weight: .medium))
                                                .foregroundStyle(preset == .system ? Color.primary : preset.style.text.color))
                                            .overlay(RoundedRectangle(cornerRadius: 7).strokeBorder(Color.primary.opacity(0.08)))
                                        Text(preset.title).font(.system(size: 12)).lineLimit(1)
                                        Spacer(minLength: 0)
                                        if appearance.style.preset == preset { Image(systemName: "checkmark").font(.system(size: 10, weight: .semibold)) }
                                    }.padding(9).frame(maxWidth: .infinity)
                                        .background(Color.primary.opacity(appearance.style.preset == preset ? 0.08 : 0.035), in: RoundedRectangle(cornerRadius: 10))
                                }.buttonStyle(.plain).accessibilityLabel("\(preset.title)\(appearance.style.preset == preset ? "，已选择" : "")")
                            }
                        }
                    }
                    customization
                    HStack(spacing: 12) {
                        if appearance.wallpaper != nil {
                            Button("移除图片") { appearance.removeWallpaper() }
                        }
                        Spacer()
                        Button("恢复默认") { appearance.apply(.system) }
                    }
                    Text("更改自动保存。图片居中裁切，玻璃效果会随卡片背后的桌面变化。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }.padding(2).padding(.bottom, 8)
            }
        }.padding(28).frame(maxWidth: .infinity, maxHeight: .infinity)
        .alert("无法使用这张图片", isPresented: Binding(get: { error != nil }, set: { if !$0 { error = nil } })) {
            Button("好") { error = nil }
        } message: { Text(error ?? "") }
    }

    private var preview: some View {
        HStack(spacing: 22) {
            CardView(study: study, audio: audio, appearance: appearance, previewRevealed: previewDefinition)
                .allowsHitTesting(false).accessibilityHidden(true)
                .shadow(color: .black.opacity(0.16), radius: 12, x: 0, y: 6)
            VStack(alignment: .leading, spacing: 10) {
                Text("实时预览").font(.system(size: 11)).foregroundStyle(.secondary)
                Text(appearance.style.preset?.title ?? (appearance.style.mode == .wallpaper ? "我的壁纸" : "自定义"))
                    .font(.system(size: 17, weight: .medium))
                Toggle("显示释义", isOn: $previewDefinition).toggleStyle(.switch).controlSize(.small)
                    .font(.system(size: 12))
            }.frame(maxWidth: .infinity, alignment: .leading)
        }.padding(20).frame(maxWidth: .infinity)
            .background {
                LinearGradient(colors: [Color(red:0.61,green:0.72,blue:0.66).opacity(0.38), Color(red:0.82,green:0.69,blue:0.48).opacity(0.28)], startPoint: .topLeading, endPoint: .bottomTrailing)
            }.clipShape(RoundedRectangle(cornerRadius: 18))
    }

    private var customization: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 24) {
                ColorPicker("卡片颜色", selection: Binding(get: { appearance.style.tint.color }, set: { appearance.setTint(CardRGB(color: $0)) }), supportsOpacity: false)
                ColorPicker("字体颜色", selection: Binding(get: { appearance.style.text.color }, set: { appearance.setText(CardRGB(color: $0)) }), supportsOpacity: false)
            }
            HStack {
                Text("背景效果").frame(width: 64, alignment: .leading)
                Picker("背景效果", selection: Binding(get: { appearance.style.mode }, set: { appearance.setMode($0) })) {
                    ForEach(CardBackgroundMode.allCases, id: \.self) { mode in
                        if mode != .wallpaper || appearance.wallpaper != nil { Text(mode.title).tag(mode) }
                    }
                }.pickerStyle(.segmented).labelsHidden()
            }
            if appearance.style.mode != .solid {
                HStack(spacing: 12) {
                    Text(appearance.style.mode == .wallpaper ? "蒙层浓度" : "染色浓度").frame(width: 64, alignment: .leading)
                    Slider(value: Binding(get: { appearance.style.tintOpacity }, set: { appearance.setOpacity($0) }), in: 0...1)
                        .accessibilityLabel("卡片颜色浓度")
                    Text("\(Int(appearance.style.tintOpacity * 100))%")
                        .monospacedDigit().foregroundStyle(.secondary).frame(width: 34, alignment: .trailing)
                }
            }
        }.font(.system(size: 12)).padding(16).background(Color.primary.opacity(0.035), in: RoundedRectangle(cornerRadius: 12))
    }

    private func chooseWallpaper() {
        let picker = NSOpenPanel()
        picker.title = "选择卡片壁纸"
        picker.prompt = "使用壁纸"
        picker.allowedContentTypes = [.png, .jpeg, .heic, .tiff]
        picker.allowsMultipleSelection = false
        picker.canChooseDirectories = false
        let completion: (NSApplication.ModalResponse) -> Void = { response in
            guard response == .OK, let url = picker.url else { return }
            do { try appearance.importWallpaper(from: url) }
            catch { self.error = error.localizedDescription }
        }
        if let window = NSApp.keyWindow { picker.beginSheetModal(for: window, completionHandler: completion) }
        else { picker.begin(completionHandler: completion) }
    }
}
