using System;
using System.Numerics;
using OpenSage.Input;
using OpenSage.Mathematics;
using Xunit;

namespace OpenSage.Tests.Input;

public class MouseClickGestureTests
{
    [Theory]
    [InlineData(0, 0, 100, 0, true)]
    [InlineData(25, -25, 250, 25, true)]
    [InlineData(26, 0, 100, 0, false)]
    [InlineData(0, -26, 100, 0, false)]
    [InlineData(0, 0, 251, 0, false)]
    [InlineData(0, 0, 1100, 0, false)]
    [InlineData(0, 0, 100, 26, false)]
    public void DistinguishesClicksFromCameraDrags(int dx, int dy, int milliseconds, float cameraDistance, bool expected)
    {
        var mouse = OriginalMouseSettings();
        var gesture = new MouseClickGesture();
        var start = new Point2D(100, 200);
        gesture.Begin(start, TimeSpan.FromSeconds(2), Vector3.Zero);

        Assert.Equal(expected, gesture.End(new Point2D(start.X + dx, start.Y + dy),
            TimeSpan.FromSeconds(2) + TimeSpan.FromMilliseconds(milliseconds),
            new Vector3(cameraDistance, 0, 0), mouse));
        Assert.False(gesture.End(start, TimeSpan.FromSeconds(2), Vector3.Zero, mouse));
    }

    private static MouseData OriginalMouseSettings()
    {
        // These values come from Zero Hour's Mouse.ini.
        var mouse = new MouseData();
        typeof(MouseData).GetProperty(nameof(MouseData.DragTolerance)).SetValue(mouse, 25);
        typeof(MouseData).GetProperty(nameof(MouseData.DragToleranceMS)).SetValue(mouse, 250);
        typeof(MouseData).GetProperty(nameof(MouseData.DragTolerance3D)).SetValue(mouse, 25);
        return mouse;
    }
}
