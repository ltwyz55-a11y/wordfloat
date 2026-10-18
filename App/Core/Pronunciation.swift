import Foundation
import AVFoundation
import Combine

public struct SpeechVoice: Equatable {
    public let id: String
    public let language: String
    public init(id: String, language: String) { self.id = id; self.language = language }
}

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
    public func speak(_ text: String) {
        stop()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return }
        let english = engine.voices.filter { $0.language.hasPrefix("en-") || $0.language == "en" }
        guard let voice = english.first(where: { $0.language == accent }) ?? english.first else {
            message = "未找到英语语音，请在系统设置的语音选项中下载英语声音。"
            return
        }
        message = voice.language == accent ? nil : "所选口音不可用，正在使用已安装的英语声音。"
        engine.speak(text, voiceID: voice.id)
    }
}
