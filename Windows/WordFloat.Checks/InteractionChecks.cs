using WordFloat.Core;

namespace WordFloat.Checks;

internal static partial class Program
{
    static partial void RunInteractionChecks()
    {
        Check("clicks swipes vertical drags and cancellation stay exclusive", () =>
        {
            var pointer = new CardPointer();
            pointer.Begin(130,70); Require(pointer.End(130,70,1) == CardAction.SingleClick,"Single");
            pointer.Begin(130,70); Require(pointer.End(130,70,2) == CardAction.OpenLibrary,"Double");
            pointer.Begin(180,70); Require(pointer.End(65,75,1) == CardAction.Next,"Swipe left");
            pointer.Begin(65,70); Require(pointer.End(180,75,1) == CardAction.Previous,"Swipe right");
            pointer.Begin(65,70); Require(pointer.End(66,125,1) == CardAction.None,"Vertical drag must not click or flip");
            pointer.Begin(65,70); Require(pointer.End(76,75,1) == CardAction.None,"Short drag must not click");
            pointer.Begin(65,70); pointer.Cancel(); Require(pointer.End(65,70,1)==CardAction.None,"Cancel");
        });
        Check("horizontal wheel changes one word per gesture", () =>
        {
            var wheel = new HorizontalWheel();
            Require(wheel.Consume(-40,0) is null && wheel.Consume(-50,0.1)==1,"Accumulation");
            Require(wheel.Consume(-120,0.2) is null,"Once per gesture");
            Require(wheel.Consume(120,0.6)==-1,"New gesture");
        });
        Check("offscreen restoration and negative-coordinate monitors", () =>
        {
            ScreenRect[] screens = [new(0,0,1920,1040),new(-1600,0,1600,900)];
            Require(WindowPlacement.Restore(-1500,100,260,220,screens)==(-1500,100),"Negative monitor preserved");
            var restored=WindowPlacement.Restore(3000,1500,260,220,screens);
            Require(restored==(1660,820),"Disconnected monitor clamped");
            Require(WindowPlacement.Restore(1850,950,520,440,screens)==(1400,600),"Expanded HiDPI bounds");
        });
    }
}
