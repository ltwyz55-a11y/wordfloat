import SwiftUI
import WordCore

private let inkAccent = Color(red: 0.23, green: 0.47, blue: 0.40)

struct LibraryView: View {
    @ObservedObject var study: StudySession
    @ObservedObject var audio: Pronunciation
    var onReturn: () -> Void
    @State private var category: BookCategory?
    @State private var error: String?
    @State private var bookQuery = ""
    @State private var browsingWords = false
    private var books: [VocabularyBook] { BookSearch.filter(study.books, category: category, query: bookQuery) }

    var body: some View {
        HStack(spacing: 0) {
            sidebar
            Divider()
            VStack(alignment: .leading, spacing: 20) {
                HStack(alignment: .center) {
                    VStack(alignment: .leading, spacing: 6) {
                        Text(category?.title ?? "我的词本").font(.system(size: 27, weight: .semibold))
                        Text("选一本，随手学一个。").font(.system(size: 13)).foregroundStyle(.secondary)
                    }
                    Spacer()
                    Button(action: onReturn) {
                        Label("悬浮学习", systemImage: "rectangle.inset.filled")
                    }.buttonStyle(.borderedProminent).tint(inkAccent).controlSize(.large)
                }
                TextField("搜索词本", text: $bookQuery).textFieldStyle(.roundedBorder).frame(maxWidth: 280)
                ScrollView {
                    if books.isEmpty {
                        Text("没有找到匹配的词本").foregroundStyle(.secondary).padding(30)
                    }
                    LazyVGrid(columns: [GridItem(.flexible(), spacing: 14), GridItem(.flexible(), spacing: 14)], spacing: 14) {
                        ForEach(books) { book in
                            BookTile(book: book, selected: study.selectedBookID == book.id) {
                                do { try study.selectBook(book.id) } catch { self.error = error.localizedDescription }
                            }
                        }
                    }.padding(2)
                }
                currentPreview
            }.padding(28).frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(minWidth: 800, minHeight: 580)
        .background(Color(nsColor: .windowBackgroundColor))
        .tint(inkAccent)
        .sheet(isPresented: $browsingWords) { WordBrowser(study: study, audio: audio) }
        .alert("无法打开词本", isPresented: Binding(get: { error != nil }, set: { if !$0 { error = nil } })) {
            Button("好") { error = nil }
        } message: { Text(error ?? "") }
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 10) {
                Image(systemName: "square.stack.3d.up.fill").font(.system(size: 22)).foregroundStyle(inkAccent)
                Text("浮词").font(.system(size: 22, weight: .semibold))
            }.padding(.bottom, 30).padding(.top, 6)
            categoryButton("全部词本", icon: "square.grid.2x2", value: nil)
            categoryButton("考试词汇", icon: "graduationcap", value: .exam)
            categoryButton("专业领域", icon: "cube.transparent", value: .professional)
            Spacer()
            VStack(alignment: .leading, spacing: 10) {
                Text("发音").font(.system(size: 11)).foregroundStyle(.secondary)
                Picker("发音口音", selection: $audio.accent) {
                    Text("美音").tag("en-US")
                    Text("英音").tag("en-GB")
                }.pickerStyle(.segmented).labelsHidden()
                if let message = audio.message { Text(message).font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true) }
            }.padding(.horizontal, 8).padding(.bottom, 18)
            Text("一点空闲，\n一个新词。")
                .font(.system(size: 13)).lineSpacing(6).foregroundStyle(.tertiary)
                .padding(.leading, 10).padding(.bottom, 12)
        }
        .padding(20).frame(width: 185).frame(maxHeight: .infinity)
        .background(Color.primary.opacity(0.025))
    }
    private func categoryButton(_ title: String, icon: String, value: BookCategory?) -> some View {
        Button { category = value } label: {
            HStack(spacing: 10) { Image(systemName: icon).frame(width: 18); Text(title); Spacer() }
                .font(.system(size: 13, weight: category == value ? .medium : .regular))
                .padding(.horizontal, 12).padding(.vertical, 11)
                .background(category == value ? inkAccent.opacity(0.12) : .clear, in: RoundedRectangle(cornerRadius: 9))
                .foregroundStyle(category == value ? inkAccent : Color.secondary)
        }.buttonStyle(.plain)
    }
    private var currentPreview: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("正在学习 · \(study.currentBook?.name ?? "")").font(.system(size: 11, weight: .medium)).foregroundStyle(.secondary)
                Spacer()
                Button("浏览词条") { browsingWords = true }.buttonStyle(.plain).font(.system(size: 11)).foregroundStyle(inkAccent)
                if let current = study.current {
                    Button { audio.speak(current.word.spelling) } label: { Image(systemName: "speaker.wave.2") }
                        .buttonStyle(.plain).help("播放发音")
                }
            }
            if let current = study.current {
                Text(current.word.spelling).font(.system(size: 23, weight: .medium, design: .rounded)).textSelection(.enabled)
                Text(current.meaning).font(.system(size: 13)).foregroundStyle(.secondary).lineLimit(2)
            }
        }.padding(18).frame(maxWidth: .infinity, alignment: .leading)
            .background(inkAccent.opacity(0.06), in: RoundedRectangle(cornerRadius: 14))
    }
}

private struct BookTile: View {
    let book: VocabularyBook
    let selected: Bool
    let action: () -> Void
    private var symbol: String {
        switch book.id {
        case "automotive": return "car.side"
        case "industrial": return "cube.transparent"
        case "materials": return "square.3.layers.3d"
        case "ielts", "toefl": return "globe"
        default: return "book.closed"
        }
    }
    var body: some View {
        Button(action: action) {
            HStack(alignment: .top, spacing: 14) {
                Image(systemName: symbol).font(.system(size: 20, weight: .light))
                    .frame(width: 40, height: 44).foregroundStyle(inkAccent)
                    .background(inkAccent.opacity(0.07), in: RoundedRectangle(cornerRadius: 9))
                VStack(alignment: .leading, spacing: 6) {
                    Text(book.name).font(.system(size: 16, weight: .medium)).foregroundStyle(.primary)
                    Text(book.subtitle).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                    Text("\(book.count.formatted()) 词").font(.system(size: 11)).foregroundStyle(.tertiary)
                }
                Spacer(minLength: 0)
                if selected { Image(systemName: "checkmark.circle.fill").font(.system(size: 14)).foregroundStyle(inkAccent) }
            }.padding(17).frame(maxWidth: .infinity, minHeight: 82, alignment: .leading)
                .background(Color(nsColor: .controlBackgroundColor), in: RoundedRectangle(cornerRadius: 13))
                .overlay(RoundedRectangle(cornerRadius: 13).strokeBorder(selected ? inkAccent.opacity(0.75) : Color.primary.opacity(0.07), lineWidth: selected ? 1.3 : 1))
                .contentShape(RoundedRectangle(cornerRadius: 13))
        }.buttonStyle(.plain).accessibilityLabel("\(book.name)，\(book.count)个词\(selected ? "，已选择" : "")")
    }
}
