import SwiftUI
import WordCore

struct CardView: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    @ObservedObject var appearance: CardAppearance
    var onOpenLibrary: () -> Void = {}
    var previewRevealed: Bool? = nil
    @State private var hovering = false
    @State private var details = LearningDetails()
    @State private var detailError: String?
    private var revealed: Bool { previewRevealed ?? study.revealed }
    private var ink: Color { appearance.style.followsSystem ? .primary : appearance.style.text.color }
    private var secondaryInk: Color { appearance.style.followsSystem ? .secondary : ink.opacity(0.78) }

    var body: some View {
        VStack(spacing: 0) {
            Capsule().fill(ink.opacity(hovering ? 0.35 : 0.15))
                .frame(width: 28, height: 3).frame(maxWidth: .infinity).frame(height: 24)
                .overlay { if previewRevealed == nil { DragHandle().help("拖动这里移动卡片") } }
                .accessibilityLabel("拖动顶部可移动卡片")
            Group {
                if let entry = study.current {
                    if revealed {
                        ScrollView(.vertical) {
                            VStack(spacing: 10) {
                                wordHeader(entry)
                                Text(entry.meaning)
                                    .font(.system(size: 13)).lineSpacing(4)
                                    .multilineTextAlignment(.center)
                                    .frame(maxWidth: .infinity)
                                    .fixedSize(horizontal: false, vertical: true)
                                Text("↓ 下滑查看词源与例句")
                                    .font(.system(size: 10)).foregroundStyle(secondaryInk)
                                if let detailError {
                                    Text(detailError).font(.caption).foregroundStyle(secondaryInk)
                                } else {
                                    CardLearningContent(entry: entry, details: details, audio: audio,
                                                        ink: ink, secondaryInk: secondaryInk)
                                }
                            }
                            .padding(.horizontal, 18).padding(.vertical, 6)
                        }
                        .id(entry.id)
                    } else {
                        wordHeader(entry).padding(.horizontal, 20)
                            .frame(maxWidth: .infinity, maxHeight: .infinity)
                    }
                } else {
                    Text("选择一个词本").font(.title3).foregroundStyle(secondaryInk)
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            }
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
            .buttonStyle(.plain).foregroundStyle(secondaryInk)
            .frame(height: 36)
        }
        .foregroundStyle(ink)
        .frame(width: 260, height: revealed ? 220 : 150)
        .background { CardSurface(style: appearance.style, wallpaper: appearance.wallpaper) }
        .clipShape(RoundedRectangle(cornerRadius: 18, style: .continuous))
        .onHover { hovering = $0 }
        .onAppear(perform: loadDetails)
        .onChange(of: study.current?.word.id) { _ in loadDetails() }
    }

    private func wordHeader(_ entry: BookEntry) -> some View {
        VStack(spacing: 8) {
            Text(entry.word.spelling)
                .font(.system(size: entry.word.spelling.count > 23 ? 23 : 30, weight: .medium, design: .rounded))
                .tracking(-0.6).multilineTextAlignment(.center)
                .lineLimit(3).minimumScaleFactor(0.65)
                .frame(maxWidth: .infinity)
                .accessibilityLabel("单词：\(entry.word.spelling)")
            if revealed {
                HStack(spacing: 8) {
                    if !entry.word.partOfSpeech.isEmpty { Text(entry.word.partOfSpeech).italic() }
                    if !entry.word.phonetic.isEmpty { Text(entry.word.phonetic) }
                }.font(.system(size: 12)).foregroundStyle(secondaryInk)
                    .lineLimit(1).minimumScaleFactor(0.7)
            }
        }
    }

    private func loadDetails() {
        details = LearningDetails()
        detailError = nil
        guard let id = study.current?.word.id else { return }
        do { details = try study.store.learningDetails(wordID: id) }
        catch { detailError = "详情暂时无法读取，请重新打开应用。" }
    }

    private func toggle() {
        guard let current = study.current else { return }
        study.revealed.toggle()
        if study.revealed { audio.speak(current.word.spelling) }
    }
}
