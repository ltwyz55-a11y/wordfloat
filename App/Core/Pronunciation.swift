import Foundation
import AVFoundation
import Combine

public struct SpeechVoice: Equatable {
    public let id: String
    public let language: String
    public init(id: String, language: String) { self.id = id; self.language = language }
}

public enum SpeechLanguage { case english, chinese }

@MainActor
public protocol SpeechEngine: AnyObject {
    var voices: [SpeechVoice] { get }
    func stop()
    func speak(_ text: String, voiceID: String)
}

@MainActor
public final class SystemSpeechEngine: SpeechEngine {
    private let synthesizer = AVSpeechSynthesizer()
    public init() {}
    public var voices: [SpeechVoice] {
        AVSpeechSynthesisVoice.speechVoices().map { SpeechVoice(id: $0.identifier, language: $0.language) }
    }
    public func stop() { synthesizer.stopSpeaking(at: .immediate) }
    public func speak(_ text: String, voiceID: String) {
        let utterance = AVSpeechUtterance(string: text)
        utterance.voice = AVSpeechSynthesisVoice(identifier: voiceID)
        utterance.rate = AVSpeechUtteranceDefaultSpeechRate * 0.84
        synthesizer.speak(utterance)
    }
}

@MainActor
public final class Pronunciation: ObservableObject {
    @Published public var accent: String {
        didSet { stop(); defaults?.set(accent, forKey: "speech.accent") }
    }
    @Published public private(set) var message: String?
    private let engine: SpeechEngine
    private let defaults: UserDefaults?
    public init(engine: SpeechEngine? = nil, defaults: UserDefaults? = nil) {
        self.engine = engine ?? SystemSpeechEngine()
        self.defaults = defaults
        let saved = defaults?.string(forKey: "speech.accent") ?? "en-US"
        accent = ["en-US", "en-GB"].contains(saved) ? saved : "en-US"
    }
    public func stop() { engine.stop() }
    public func speak(_ text: String, language: SpeechLanguage = .english) {
        stop()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        guard let voice = voice(for: language) else {
            message = language == .english ? "未找到英语语音，请在系统设置的语音选项中下载英语声音。" : "未找到中文语音，请在系统设置的语音选项中下载中文声音。"
            return
        }
        message = language == .english && voice.language != accent ? "所选口音不可用，正在使用已安装的英语声音。" : nil
        engine.speak(text, voiceID: voice.id)
    }
    public func speakExample(english: String, chinese: String) {
        stop()
        let englishVoice = voice(for: .english), chineseVoice = voice(for: .chinese)
        var missing: [String] = []
        for (text, selectedVoice, name) in [(english, englishVoice, "英语"), (chinese, chineseVoice, "中文")] {
            guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { continue }
            if let selectedVoice { engine.speak(text, voiceID: selectedVoice.id) }
            else { missing.append(name) }
        }
        // AVSpeechSynthesizer queues these utterances; changing word or replaying stops the queue.
        message = missing.isEmpty ? nil : "未找到\(missing.joined(separator: "、"))语音，已跳过；可在系统设置中下载。"
    }
    private func voice(for language: SpeechLanguage) -> SpeechVoice? {
        let prefix = language == .english ? "en" : "zh"
        let preferred = language == .english ? accent : "zh-CN"
        let voices = engine.voices.filter { $0.language == prefix || $0.language.hasPrefix(prefix + "-") }
        return voices.first { $0.language == preferred } ?? voices.first
    }
}
