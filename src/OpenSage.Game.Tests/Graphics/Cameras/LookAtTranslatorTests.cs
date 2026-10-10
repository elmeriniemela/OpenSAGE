using System.Numerics;
using OpenSage.Graphics.Cameras;
using OpenSage.Mathematics;
using Xunit;

namespace OpenSage.Tests.Graphics.Cameras;

public class LookAtTranslatorTests
{
    [Theory]
    [InlineData(50, 50)]
    [InlineData(512, 384)]
    [InlineData(1000, 700)]
    public void StationaryRightClickDoesNotScrollOrProduceNaN(int x, int y)
    {
        var pointer = new Point2D(x, y);
        var anchor = pointer;

        var offset = LookAtTranslator.CalculateRightMouseScroll(pointer, ref anchor,
            new Vector2(1024, 768), Vector2.One, 0.6f);

        Assert.Equal(Vector2.Zero, offset);
        Assert.Equal(pointer, anchor);
    }

    [Fact]
    public void DragScrollsRelativeToClickPosition()
    {
        var anchor = new Point2D(100, 200);
        var offset = LookAtTranslator.CalculateRightMouseScroll(new Point2D(110, 200), ref anchor,
            new Vector2(1024, 768), Vector2.One, 0.6f);

        Assert.Equal(new Point2D(100, 200), anchor);
        Assert.Equal(10.36f, offset.X, 4);
        Assert.Equal(0, offset.Y);
    }

    [Fact]
    public void LongDragLimitsAnchorDistanceFromPointer()
    {
        var anchor = Point2D.Zero;
        var offset = LookAtTranslator.CalculateRightMouseScroll(new Point2D(1000, 700), ref anchor,
            new Vector2(1024, 768), Vector2.One, 0.6f);

        Assert.Equal(new Point2D(488, 316), anchor);
        Assert.True(float.IsFinite(offset.X));
        Assert.True(float.IsFinite(offset.Y));
        Assert.True(offset.X > 0 && offset.Y > 0);
    }
}
