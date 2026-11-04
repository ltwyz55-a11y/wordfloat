import SwiftUI
import WordCore

@MainActor
struct DictionaryResultsView: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    let query: String
    @State private var results: [VocabularyWord] = []
    @State private var selectedID: String?
    @State private var memberships: [BookEntry] = []
    @State private var details = LearningDetails()
    @State private var loadedQuery = ""
    @State private var error: String?
    private var selected: VocabularyWord? { results.first { $0.id == selectedID } }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            HStack {
                Text("词典查询").font(.system(size: 23, weight: .semibold))
                Spacer()
                Text("全部内置词库 · 离线可用").font(.system(size: 11)).foregroundStyle(.secondary)
            }.padding(.top, 14)
            if loadedQuery != query {
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if let error {
                Text(error).foregroundStyle(.secondary).frame(maxWidth: .infinity, maxHeight: .infinity)
            } else if results.isEmpty {
                VStack(spacing: 12) {
                    Image(systemName: "magnifyingglass").font(.largeTitle).foregroundStyle(.tertiary)
                    Text("未在内置词库中找到匹配词条")
                    Text("试试其他拼写或中文关键词；当前查询仅覆盖随应用提供的词库。")
                        .font(.system(size: 12)).foregroundStyle(.secondary)
                }.frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                Text(results.count == 100 ? "显示前 100 条，请输入更完整的单词缩小范围" : "\(results.count) 个匹配词条")
                    .font(.system(size: 11)).foregroundStyle(.secondary)
                HStack(spacing: 0) {
                    List(results, selection: $selectedID) { word in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(word.spelling).font(.system(size: 14, weight: .medium))
                            Text(word.meaning.replacingOccurrences(of: "\n", with: " "))
                                .font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(2)
                        }.padding(.vertical, 5).tag(word.id)
                    }.listStyle(.inset).frame(width: 190)
                    Divider()
                    if let word = selected { definition(word).id(word.id) }
                    else { Text("选择一个单词查看释义").foregroundStyle(.secondary).frame(maxWidth: .infinity) }
                }
            }
        }.frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        .task(id: query) {
            do { try await Task.sleep(nanoseconds: 150_000_000) } catch { return }
            guard !Task.isCancelled else { return }
            refresh()
        }
        .onChange(of: query) { _ in audio.stop() }
        .onChange(of: selectedID) { _ in loadDefinition() }
        .onDisappear { audio.stop() }
    }
    private func definition(_ word: VocabularyWord) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 14) {
                Text(word.spelling).font(.system(size: 28, weight: .medium, design: .rounded)).textSelection(.enabled)
                if !word.phonetic.isEmpty || !word.partOfSpeech.isEmpty {
                    Text([word.partOfSpeech, word.phonetic].filter { !$0.isEmpty }.joined(separator: "   "))
                        .font(.system(size: 13)).foregroundStyle(.secondary).textSelection(.enabled)
                }
                CardInlineButton(title: "听发音", identifier: "dictionary.speak", color: .accentColor) { audio.speak(word.spelling) }
                    .frame(width: 92, height: 28)
                if let message = audio.message { Text(message).font(.system(size: 11)).foregroundStyle(.secondary) }
                Text(word.meaning).font(.system(size: 15)).lineSpacing(6).textSelection(.enabled)
                if !memberships.isEmpty {
                    Text("收录于：" + memberships.compactMap { entry in study.books.first { $0.id == entry.bookID }?.name }.joined(separator: "、"))
                        .font(.system(size: 11)).foregroundStyle(.secondary)
                    ForEach(memberships.filter { $0.domainMeaning?.isEmpty == false && $0.meaning != word.meaning }) { entry in
                        VStack(alignment: .leading, spacing: 5) {
                            Text(study.books.first { $0.id == entry.bookID }?.name ?? entry.bookID).font(.system(size: 11, weight: .semibold))
                            Text(entry.meaning).font(.system(size: 13)).textSelection(.enabled)
                        }
                    }
                }
                CardLearningContent(entry: BookEntry(bookID: "dictionary", word: word, position: 0), details: details,
                    audio: audio, ink: .primary, secondaryInk: .secondary)
            }.padding(.leading, 20).padding(.trailing, 8).padding(.bottom, 16).frame(maxWidth: .infinity, alignment: .leading)
        }
    }
    private func refresh() {
        do {
            results = try study.store.lookup(query)
            selectedID = results.first?.id
            error = nil
            loadDefinition()
        } catch { results = []; selectedID = nil; self.error = "无法查词：\(error.localizedDescription)" }
        loadedQuery = query
    }
    private func loadDefinition() {
        audio.stop(); memberships = []; details = LearningDetails()
        guard let word = selected else { return }
        do {
            memberships = try study.store.memberships(wordID: word.id)
            details = try study.store.learningDetails(wordID: word.id)
        } catch { self.error = "无法读取释义：\(error.localizedDescription)" }
    }
}
