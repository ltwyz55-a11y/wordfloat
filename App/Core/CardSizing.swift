import Foundation

public struct CardSizing: Equatable {
    public private(set) var width: CGFloat
    public private(set) var compactHeight: CGFloat
    public private(set) var expandedHeight: CGFloat
    public init(saved: [Double]? = nil) {
        let values = saved?.count == 3 ? saved! : [260, 150, 220]
        width = Self.bounded(values[0], minimum: 220, maximum: 520, fallback: 260)
        compactHeight = Self.bounded(values[1], minimum: 130, maximum: 420, fallback: 150)
        expandedHeight = Self.bounded(values[2], minimum: 200, maximum: 420, fallback: 220)
    }
    public var saved: [Double] { [Double(width), Double(compactHeight), Double(expandedHeight)] }
    public func size(revealed: Bool) -> CGSize { CGSize(width: width, height: revealed ? expandedHeight : compactHeight) }
    public static func minimum(revealed: Bool) -> CGSize { CGSize(width: 220, height: revealed ? 200 : 130) }
    public static let maximum = CGSize(width: 520, height: 420)
    public static func clamped(_ size: CGSize, revealed: Bool) -> CGSize {
        CGSize(width: bounded(size.width, minimum: 220, maximum: 520, fallback: 260),
               height: bounded(size.height, minimum: revealed ? 200 : 130, maximum: 420, fallback: revealed ? 220 : 150))
    }
    public mutating func remember(_ size: CGSize, revealed: Bool) {
        let size = Self.clamped(size, revealed: revealed)
        width = size.width
        if revealed { expandedHeight = size.height } else { compactHeight = size.height }
    }
    private static func bounded(_ value: Double, minimum: Double, maximum: Double, fallback: Double) -> CGFloat {
        CGFloat(value.isFinite ? min(maximum, max(minimum, value)) : fallback)
    }
}

public struct CardResizeEdges: OptionSet {
    public let rawValue: Int
    public init(rawValue: Int) { self.rawValue = rawValue }
    public static let left = Self(rawValue: 1), right = Self(rawValue: 2)
    public static let bottom = Self(rawValue: 4), top = Self(rawValue: 8)
    public static func hitTest(_ point: CGPoint, size: CGSize) -> Self {
        guard point.x.isFinite, point.y.isFinite, point.x >= 0, point.y >= 0, point.x <= size.width, point.y <= size.height else { return [] }
        let horizontal: Self = point.x < 14 ? .left : point.x > size.width - 14 ? .right : []
        let vertical: Self = point.y < 14 ? .bottom : point.y > size.height - 14 ? .top : []
        if !horizontal.isEmpty && !vertical.isEmpty { return horizontal.union(vertical) }
        if point.x < 6 { return .left }; if point.x > size.width - 6 { return .right }
        if point.y < 6 { return .bottom }; if point.y > size.height - 6 { return .top }
        return []
    }
    // AppKit coordinates increase upward; keep the opposite edge fixed at either limit.
    public func frame(from start: CGRect, translation: CGPoint, revealed: Bool) -> CGRect {
        guard !isEmpty, translation.x.isFinite, translation.y.isFinite else { return start }
        let width = start.width + (contains(.left) ? -translation.x : contains(.right) ? translation.x : 0)
        let height = start.height + (contains(.bottom) ? -translation.y : contains(.top) ? translation.y : 0)
        let size = CardSizing.clamped(CGSize(width: width, height: height), revealed: revealed)
        return CGRect(x: contains(.left) ? start.maxX - size.width : start.minX,
                      y: contains(.bottom) ? start.maxY - size.height : start.minY, width: size.width, height: size.height)
    }
}
