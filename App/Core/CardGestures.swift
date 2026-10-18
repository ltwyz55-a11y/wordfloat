import Foundation

public enum CardPointerAction: Equatable { case none, scheduleSingle, openLibrary, next, previous }

public struct CardPointerGesture {
    private var start: CGPoint?
    private var moved = false
    public init() {}
    public mutating func begin(at point: CGPoint) { start = point; moved = false }
    public mutating func move(to point: CGPoint) {
        guard let start else { return }
        if hypot(point.x-start.x, point.y-start.y) > 6 { moved = true }
    }
    public mutating func end(at point: CGPoint, clickCount: Int) -> CardPointerAction {
        guard let start else { return .none }
        move(to: point)
        let dx = point.x-start.x, dy = point.y-start.y
        self.start = nil
        if moved {
            guard abs(dx) >= 44, abs(dx) > abs(dy)*1.3 else { return .none }
            return dx < 0 ? .next : .previous
        }
        return clickCount == 2 ? .openLibrary : (clickCount == 1 ? .scheduleSingle : .none)
    }
    public mutating func cancel() { start = nil; moved = false }
}

public struct HorizontalScrollGesture {
    private var total: CGFloat = 0
    private var consumed = false
    private var lastTime: TimeInterval = -.infinity
    public init() {}
    public mutating func consume(dx: CGFloat, dy: CGFloat, time: TimeInterval, begins: Bool, momentum: Bool, precise: Bool) -> Int? {
        if begins || time-lastTime > 0.25 { total = 0; consumed = false }
        lastTime = time
        guard !momentum, !consumed, abs(dx) > abs(dy) else { return nil }
        total += dx
        guard abs(total) >= (precise ? 40 : 8) else { return nil }
        consumed = true
        return total < 0 ? 1 : -1
    }
}
