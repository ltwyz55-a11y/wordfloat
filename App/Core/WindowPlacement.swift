import Foundation

public enum WindowPlacement {
    public static func clamp(_ frame: CGRect, to screens: [CGRect]) -> CGRect {
        guard let fallback = screens.first else { return frame }
        let usable = frame.origin.x.isFinite && frame.origin.y.isFinite ? frame : CGRect(origin: fallback.origin, size: frame.size)
        func overlap(_ screen: CGRect) -> CGFloat {
            let r = usable.intersection(screen)
            return r.isNull ? 0 : r.width * r.height
        }
        let best = screens.max { overlap($0) < overlap($1) } ?? fallback
        let screen = overlap(best) > 0 ? best : fallback
        return CGRect(x: min(max(usable.minX, screen.minX), max(screen.minX, screen.maxX - usable.width)),
            y: min(max(usable.minY, screen.minY), max(screen.minY, screen.maxY - usable.height)),
            width: usable.width, height: usable.height)
    }
}
