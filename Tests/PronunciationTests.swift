import XCTest
@testable import WordCore

@MainActor
final class PronunciationTests: XCTestCase {
    final class Engine: SpeechEngine {
        var voices = [SpeechVoice(id: "us", language: "en-US"), SpeechVoice(id: "gb", language: "en-GB")]
        var events: [String] = []
        func stop() { events.append("stop") }
        func speak(_ text: String, voiceID: String) { events.append("\(voiceID):\(text)") }
    }
    func testRapidPlaybackStopsOldSpeechAndSwitchesAccent() {
        let engine = Engine(), player = Pronunciation(engine: Engine())
        let audio = Pronunciation(engine: engine)
        audio.speak("surface")
        audio.speak("material")
        audio.accent = "en-GB"
        audio.speak("design")
        XCTAssertEqual(engine.events, ["stop","us:surface","stop","us:material","stop","stop","gb:design"])
        XCTAssertEqual(player.accent, "en-US")
    }
    func testFallbackAndUnavailableVoice() {
        let engine = Engine()
        engine.voices = [SpeechVoice(id: "au", language: "en-AU")]
        let audio = Pronunciation(engine: engine)
        audio.speak("draft")
        XCTAssertEqual(engine.events.last, "au:draft")
        XCTAssertNotNil(audio.message)
        engine.voices = []
        audio.speak("draft")
        XCTAssertEqual(engine.events.last, "stop")
        XCTAssertTrue(audio.message?.contains("下载") == true)
    }
    func testBilingualQueueAndChineseNeverUsesEnglishVoice() {
        let engine = Engine()
        engine.voices.append(SpeechVoice(id: "zh", language: "zh-CN"))
        let audio = Pronunciation(engine: engine)
        audio.speakExample(english: "I like this design.", chinese: "我喜欢这个设计。")
        XCTAssertEqual(engine.events, ["stop", "us:I like this design.", "zh:我喜欢这个设计。"])
        audio.speak("下一句", language: .chinese)
        XCTAssertEqual(Array(engine.events.suffix(2)), ["stop", "zh:下一句"])
        engine.voices.removeAll { $0.language.hasPrefix("zh") }
        audio.speak("不能用英语读", language: .chinese)
        XCTAssertEqual(engine.events.last, "stop")
        XCTAssertTrue(audio.message?.contains("中文") == true)
        audio.speakExample(english: "Next sentence.", chinese: "下一句。")
        XCTAssertEqual(Array(engine.events.suffix(2)), ["stop", "us:Next sentence."])
        XCTAssertTrue(audio.message?.contains("已跳过") == true)
    }
}
