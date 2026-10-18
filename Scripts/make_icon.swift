import AppKit

let folder = URL(fileURLWithPath: CommandLine.arguments[1])
try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
for (size, suffix) in [(16,"16x16"),(32,"16x16@2x"),(32,"32x32"),(64,"32x32@2x"),(128,"128x128"),(256,"128x128@2x"),(256,"256x256"),(512,"256x256@2x"),(512,"512x512"),(1024,"512x512@2x")] {
    let bitmap = NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:size,pixelsHigh:size,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
    NSGraphicsContext.saveGraphicsState()
    let context = NSGraphicsContext(bitmapImageRep:bitmap)!
    NSGraphicsContext.current = context
    context.cgContext.scaleBy(x:CGFloat(size)/1024,y:CGFloat(size)/1024)
    NSColor(calibratedRed:0.94,green:0.95,blue:0.92,alpha:1).setFill()
    NSBezierPath(roundedRect:NSRect(x:42,y:42,width:940,height:940),xRadius:205,yRadius:205).fill()
    NSColor(calibratedRed:0.23,green:0.47,blue:0.40,alpha:0.24).setFill()
    NSBezierPath(roundedRect:NSRect(x:210,y:175,width:640,height:590),xRadius:80,yRadius:80).fill()
    NSColor(calibratedRed:0.23,green:0.47,blue:0.40,alpha:1).setFill()
    NSBezierPath(roundedRect:NSRect(x:165,y:250,width:640,height:580),xRadius:80,yRadius:80).fill()
    let text = "Aa" as NSString
    let attrs: [NSAttributedString.Key:Any] = [.font:NSFont.systemFont(ofSize:262,weight:.medium),.foregroundColor:NSColor.white]
    let bounds = text.size(withAttributes:attrs)
    text.draw(at:NSPoint(x:485-bounds.width/2,y:420),withAttributes:attrs)
    NSColor.white.withAlphaComponent(0.5).setFill()
    NSBezierPath(roundedRect:NSRect(x:414,y:332,width:142,height:12),xRadius:6,yRadius:6).fill()
    NSGraphicsContext.restoreGraphicsState()
    try bitmap.representation(using:.png,properties:[:])!.write(to:folder.appendingPathComponent("icon_\(suffix).png"))
}
