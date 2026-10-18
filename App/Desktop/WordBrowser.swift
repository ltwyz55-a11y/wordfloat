import SwiftUI
import WordCore

struct WordBrowser: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    @Environment(\.dismiss) private var dismiss
    @State private var query = ""
    @State private var results: [BookEntry] = []
    @State private var selectedID: String?
    @State private var error: String?
    private var selected: BookEntry? { results.first { $0.id == selectedID } }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Text(study.currentBook?.name ?? "词条").font(.title2.weight(.semibold))
                Spacer()
                Button("完成") { dismiss() }.keyboardShortcut(.cancelAction)
            }.padding(22)
            HStack {
                Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                TextField("搜索英文或中文释义", text: $query).textFieldStyle(.plain)
                if !query.isEmpty { Button { query = "" } label: { Image(systemName: "xmark.circle.fill").foregroundStyle(.secondary) }.buttonStyle(.plain) }
            }.padding(11).background(Color.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 9)).padding(.horizontal, 22).padding(.bottom, 16)
            Divider()
            if let error {
                Text(error).foregroundStyle(.secondary).frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if results.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "magnifyingglass").font(.largeTitle).foregroundStyle(.tertiary)
                    Text("没有找到匹配的词条").foregroundStyle(.secondary)
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                HStack(spacing: 0) {
                    List(results, selection: $selectedID) { entry in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(entry.word.spelling).font(.system(size: 14, weight: .medium))
                            Text(entry.meaning.replacingOccurrences(of: "\n", with: " "))
                                .font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                        }.padding(.vertical, 5).tag(entry.id)
                    }.listStyle(.inset).frame(width: 285)
                    Divider()
                    if let entry = selected {
                        VStack(alignment: .leading, spacing: 18) {
                            Text(entry.word.spelling).font(.system(size: 28, weight: .medium, design: .rounded)).textSelection(.enabled)
                            if !entry.word.phonetic.isEmpty { Text(entry.word.phonetic).foregroundStyle(.secondary) }
                            Button { audio.speak(entry.word.spelling) } label: { Label("听发音", systemImage: "speaker.wave.2") }.buttonStyle(.borderless)
                            ScrollView { Text(entry.meaning).font(.system(size: 15)).lineSpacing(7).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                            Button("从这个词继续") { study.jump(to: entry); dismiss() }.buttonStyle(.borderedProminent)
                        }.padding(26).frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
                    } else { Spacer() }
                }
            }
        }
        .frame(width: 760, height: 530)
        .onAppear { refresh() }
        .onChange(of: query) { _ in refresh() }
    }
    private func refresh() {
        do {
            results = try study.store.entries(bookID: study.selectedBookID, search: query)
            if !results.contains(where: { $0.id == selectedID }) {
                selectedID = results.first { $0.id == study.current?.id }?.id ?? results.first?.id
            }
            error = nil
        } catch { self.error = "无法读取词条：\(error.localizedDescription)" }
    }
}
