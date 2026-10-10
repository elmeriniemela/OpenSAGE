using System;
using System.Numerics;
using OpenSage.Mathematics;

namespace OpenSage.Input;

/// <summary>Distinguishes command clicks from right-button camera drags.</summary>
internal sealed class MouseClickGesture
{
    private Point2D _start;
    private TimeSpan _startTime;
    private Vector3 _cameraPosition;
    private bool _pressed;

    internal void Begin(Point2D position, TimeSpan time, Vector3 cameraPosition)
    {
        _start = position;
        _startTime = time;
        _cameraPosition = cameraPosition;
        _pressed = true;
    }

    internal bool End(Point2D position, TimeSpan time, Vector3 cameraPosition, MouseData mouse)
    {
        var wasPressed = _pressed;
        _pressed = false;
        return wasPressed && IsClick(_start, position, time - _startTime,
            Vector3.Distance(_cameraPosition, cameraPosition), mouse);
    }

    internal static bool IsClick(Point2D start, Point2D end, TimeSpan elapsed, float cameraDistance, MouseData mouse)
    {
        return elapsed >= TimeSpan.Zero && elapsed.TotalMilliseconds <= mouse.DragToleranceMS &&
            Math.Abs(end.X - start.X) <= mouse.DragTolerance &&
            Math.Abs(end.Y - start.Y) <= mouse.DragTolerance &&
            cameraDistance <= mouse.DragTolerance3D;
    }
}
