import SwiftUI
import WordCore

struct CardView: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    var onOpenLibrary: () -> Void = {}
    @Environment(\.accessibilityReduceTransparency) private var reduceTransparency
    @State private var hovering = false

    var body: some View {
        VStack(spacing: 0) {
            Capsule().fill(Color.secondary.opacity(hovering ? 0.35 : 0.15))
                .frame(width: 28, height: 3).frame(height: 24)
                .overlay(DragHandle().help("拖动这里移动卡片"))
                .accessibilityLabel("拖动顶部可移动卡片")
            VStack(spacing: 9) {
                if let entry = study.current {
                    Text(entry.word.spelling)
                        .font(.system(size: entry.word.spelling.count > 23 ? 23 : 30, weight: .medium, design: .rounded))
                        .tracking(-0.6).multilineTextAlignment(.center)
                        .lineLimit(3).minimumScaleFactor(0.65)
                        .frame(maxWidth: .infinity)
                        .accessibilityLabel("单词：\(entry.word.spelling)")
                    if study.revealed {
                        if !entry.word.phonetic.isEmpty {
                            Text(entry.word.phonetic).font(.system(size: 12)).foregroundStyle(.secondary)
                                .lineLimit(1).minimumScaleFactor(0.7)
                        }
                        ScrollView(.vertical) {
                            Text(entry.meaning)
                                .font(.system(size: 13)).lineSpacing(4)
                                .foregroundStyle(.secondary).multilineTextAlignment(.center)
                                .frame(maxWidth: .infinity)
                        }.frame(maxHeight: 76)
                    }
                } else {
                    Text("选择一个词本").font(.title3).foregroundStyle(.secondary)
                }
            }
            .padding(.horizontal, 20)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .contentShape(Rectangle())
            .accessibilityElement(children: .contain)
            .accessibilityAction(named: "查看释义并发音", toggle)
            .accessibilityAction(named: "打开词本", onOpenLibrary)
            HStack(spacing: 44) {
                Button { study.advance(-1) } label: { Image(systemName: "chevron.left").font(.system(size: 10, weight: .medium)) }
                    .opacity(hovering ? 0.6 : 0).help("上一个单词")
                Button {
                    if let current = study.current { audio.speak(current.word.spelling) }
                } label: {
                    Image(systemName: "speaker.wave.2").font(.system(size: 13, weight: .regular))
                }
                .help(audio.message ?? "播放发音")
                .accessibilityLabel("播放发音")
                .disabled(study.current == nil)
                Button { study.advance(1) } label: { Image(systemName: "chevron.right").font(.system(size: 10, weight: .medium)) }
                    .opacity(hovering ? 0.6 : 0).help("下一个单词")
            }
            .buttonStyle(.plain).foregroundStyle(.secondary)
            .frame(height: 36)
        }
        .frame(width: 260, height: study.revealed ? 220 : 150)
        .background {
            if reduceTransparency { Color(nsColor: .windowBackgroundColor) }
            else { Rectangle().fill(.regularMaterial) }
        }
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 18).strokeBorder(Color.primary.opacity(0.07)))
        .onHover { hovering = $0 }
    }

    private func toggle() {
        guard let current = study.current else { return }
        study.revealed.toggle()
        if study.revealed { audio.speak(current.word.spelling) }
    }
}
