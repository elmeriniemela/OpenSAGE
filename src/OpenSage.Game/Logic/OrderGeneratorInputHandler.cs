using OpenSage.Input;
using OpenSage.Mathematics;

namespace OpenSage.Logic;

public class OrderGeneratorInputHandler : InputMessageHandler
{
    private readonly OrderGeneratorSystem _orderGeneratorSystem;

    private readonly MouseClickGesture _rightClick = new();
    private bool _isDragging;
    private Point2D _mousePosition;
    private Point2D _dragEndPosition;

    private KeyModifiers _keyModifiers;

    public override HandlingPriority Priority => _priority;
    private HandlingPriority _priority = HandlingPriority.Disabled;

    internal KeyModifiers KeyModifiers => _keyModifiers;

    public OrderGeneratorInputHandler(OrderGeneratorSystem orderGeneratorSystem)
    {
        _orderGeneratorSystem = orderGeneratorSystem;
    }

    private void UpdateMousePosition(Point2D position)
    {
        _mousePosition = position;
        _orderGeneratorSystem.UpdatePosition(position.ToVector2());
    }

    public void Update()
    {
        _priority = HandlingPriority.OrderGeneratorPriority;

        _orderGeneratorSystem.UpdatePosition(_mousePosition.ToVector2());
    }

    public override InputMessageResult HandleMessage(InputMessage message, in TimeInterval gameTime)
    {
        switch (message.MessageType)
        {
            case InputMessageType.MouseMove:
                if (_isDragging)
                {
                    _dragEndPosition = message.Value.MousePosition;
                    _orderGeneratorSystem.UpdateDrag(_dragEndPosition.ToVector2());
                }
                else
                {
                    _mousePosition = message.Value.MousePosition;
                }
                break;

            case InputMessageType.MouseLeftButtonDown:
                UpdateMousePosition(message.Value.MousePosition);
                if (_orderGeneratorSystem.IsContextual)
                {
                    // Selection handles contextual left clicks after deciding whether this is a box drag.
                    break;
                }
                if (!_orderGeneratorSystem.ActiveGenerator.CanDrag)
                {
                    if (_orderGeneratorSystem.TryActivate(_keyModifiers))
                    {
                        return InputMessageResult.Handled;
                    }
                    break;
                }
                _isDragging = true;
                // Copy initial position to drag end position so that delta is 0 if the drag ends immediately
                _dragEndPosition = _mousePosition;
                return InputMessageResult.Handled;

            case InputMessageType.MouseLeftButtonUp:
                UpdateMousePosition(message.Value.MousePosition);
                if (_isDragging)
                {
                    _orderGeneratorSystem.TryActivate(_keyModifiers);
                    _isDragging = false;
                    return InputMessageResult.Handled;
                }
                break;

            case InputMessageType.MouseRightButtonDown:
                UpdateMousePosition(message.Value.MousePosition);
                _orderGeneratorSystem.RightClickHandled = false;
                _rightClick.Begin(_mousePosition, gameTime.TotalTime, _orderGeneratorSystem.CameraPosition);
                break;

            case InputMessageType.MouseRightButtonUp:
                UpdateMousePosition(message.Value.MousePosition);
                if (_rightClick.End(_mousePosition, gameTime.TotalTime,
                    _orderGeneratorSystem.CameraPosition, _orderGeneratorSystem.MouseData))
                {
                    if (!_orderGeneratorSystem.IsContextual)
                    {
                        _orderGeneratorSystem.CancelOrderGenerator();
                        _isDragging = false;
                        _orderGeneratorSystem.RightClickHandled = true;
                    }
                    else if (_orderGeneratorSystem.UseAlternateMouse)
                    {
                        _orderGeneratorSystem.TryActivate(_keyModifiers, allowSelection: false);
                    }
                }
                // Selection and the camera must receive button-up to finish panning.
                break;

            case InputMessageType.KeyDown:
                if (message.Value.Key == Veldrid.Key.ControlLeft ||
                    message.Value.Key == Veldrid.Key.ControlRight)
                {
                    _keyModifiers |= KeyModifiers.Ctrl;
                    _orderGeneratorSystem.InputKeyModifiers = _keyModifiers;
                    return InputMessageResult.Handled;
                }
                break;

            case InputMessageType.KeyUp:
                if (message.Value.Key == Veldrid.Key.ControlLeft ||
                    message.Value.Key == Veldrid.Key.ControlRight)
                {
                    _keyModifiers &= ~KeyModifiers.Ctrl;
                    _orderGeneratorSystem.InputKeyModifiers = _keyModifiers;
                    return InputMessageResult.Handled;
                }
                break;
        }

        return InputMessageResult.NotHandled;
    }
}
