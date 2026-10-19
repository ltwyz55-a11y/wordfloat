import AppKit
import SwiftUI
import WordCore

struct CardLearningContent: View {
    let entry: BookEntry
    let details: LearningDetails
    @ObservedObject var audio: Pronunciation
    let ink: Color
    let secondaryInk: Color

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            if !details.dictionaryMeaning.isEmpty && details.dictionaryMeaning != entry.meaning {
                section("词典完整释义") { paragraph(details.dictionaryMeaning) }
            }
            section("词根与词源") {
                if details.etymologies.isEmpty {
                    paragraph("暂无可靠词源资料")
                } else {
                    ForEach(Array(details.etymologies.enumerated()), id: \.offset) { _, note in
                        paragraph(note.text)
                    }
                    sourceButton("维基词典 · CC BY-SA 4.0", url: details.etymologies[0].sourceURL, id: "etymology.source")
                }
            }
            if !details.forms.isEmpty {
                section("词形变化") {
                    ForEach(Array(details.forms.enumerated()), id: \.offset) { _, form in
                        paragraph("\(form.label)  \(form.text)")
                    }
                }
            }
            section("中英例句 · 通用用法") {
                if details.examples.isEmpty {
                    paragraph("暂无双语例句")
                } else {
                    ForEach(details.examples) { example in
                        VStack(alignment: .leading, spacing: 7) {
                            Text(example.english).foregroundStyle(ink)
                            paragraph(example.chinese)
                            HStack(spacing: 10) {
                                play("英文", id: "example.english.\(example.id)") { audio.speak(example.english) }
                                play("中文", id: "example.chinese.\(example.id)") { audio.speak(example.chinese, language: .chinese) }
                                play("中英连读", id: "example.pair.\(example.id)") {
                                    audio.speakExample(english: example.english, chinese: example.chinese)
                                }
                            }
                            Text(example.attribution).font(.system(size: 9)).foregroundStyle(secondaryInk)
                            HStack(spacing: 12) {
                                sourceButton("查看来源", url: example.sourceURL, id: "example.source.\(example.id)")
                                sourceButton("许可", url: example.id.hasPrefix("wiki-")
                                    ? "https://creativecommons.org/licenses/by-sa/4.0/"
                                    : "https://creativecommons.org/licenses/by/2.0/fr/", id: "example.license.\(example.id)")
                            }
                        }.padding(.bottom, 8)
                    }
                    if let message = audio.message { paragraph(message) }
                }
            }
            if !details.senses.isEmpty {
                section("词典补充义项") {
                    ForEach(Array(details.senses.enumerated()), id: \.offset) { _, sense in
                        paragraph([sense.partOfSpeech, sense.text].filter { !$0.isEmpty }.joined(separator: " · "))
                    }
                    sourceButton("维基词典 · CC BY-SA 4.0", url: details.senses[0].sourceURL, id: "senses.source")
                }
            }
            if !details.englishDefinition.isEmpty {
                section("英英释义") { paragraph(details.englishDefinition) }
            }
            Text("词典释义及词形：ECDICT · MIT\n词源及补充义项：维基词典贡献者\n例句可能使用不同词义；来源语料未经逐句审校。")
                .font(.system(size: 9)).foregroundStyle(secondaryInk)
            sourceButton("词源与补充义项许可", url: "https://creativecommons.org/licenses/by-sa/4.0/", id: "dictionary.license")
        }
        .font(.system(size: 12)).lineSpacing(3)
        .frame(maxWidth: .infinity, alignment: .leading)
        .fixedSize(horizontal: false, vertical: true)
        .padding(.bottom, 8)
    }

    private func section<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 8) {
            Rectangle().fill(ink.opacity(0.12)).frame(height: 1)
            Text(title).font(.system(size: 11, weight: .semibold)).foregroundStyle(ink)
            content()
        }
    }
    private func paragraph(_ text: String) -> some View {
        Text(text).foregroundStyle(secondaryInk).fixedSize(horizontal: false, vertical: true)
    }
    private func play(_ title: String, id: String, action: @escaping () -> Void) -> some View {
        CardInlineButton(title: "▶ \(title)", identifier: id, color: ink, action: action).frame(height: 23)
    }
    private func sourceButton(_ title: String, url: String, id: String) -> some View {
        CardInlineButton(title: title, identifier: id, color: secondaryInk) {
            if let link = URL(string: url), link.scheme == "https" { NSWorkspace.shared.open(link) }
        }.frame(height: 20)
    }
}
