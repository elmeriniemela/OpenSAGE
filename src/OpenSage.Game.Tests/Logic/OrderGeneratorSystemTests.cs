using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using OpenSage.Graphics.Cameras;
using OpenSage.Graphics.Rendering;
using OpenSage.Input;
using OpenSage.Logic;
using OpenSage.Logic.Object;
using OpenSage.Logic.OrderGenerators;
using OpenSage.Logic.Orders;
using OpenSage.Network;
using Xunit;

namespace OpenSage.Tests.Logic;

public class OrderGeneratorSystemTests : MockedGameTest
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ContextualClickCanSuppressSelection(bool allowSelection, bool expected)
    {
        var generator = new RecordingGenerator(Order.CreateSetSelection(0, new ObjectId(1)));
        var system = CreateSystem(generator);

        Assert.Equal(expected, system.TryActivate(KeyModifiers.None, allowSelection));
        Assert.Equal(expected ? 1 : 0, PendingOrders().Count);
        Assert.True(generator.CursorRefreshed);
    }

    [Fact]
    public void RightCommandClickQueuesMoveWithoutClearingSelection()
    {
        var order = Order.CreateMoveOrder(0, new Vector3(10, 20, 0));
        var generator = new RecordingGenerator(order);
        var system = CreateSystem(generator);

        Assert.True(system.TryActivate(KeyModifiers.Ctrl, allowSelection: false));
        Assert.Same(order, Assert.Single(PendingOrders()));
        Assert.Equal(KeyModifiers.Ctrl, generator.Modifiers);
    }

    [Fact]
    public void CancellingPendingCommandReturnsToContextualMode()
    {
        var system = CreateSystem(new RecordingGenerator(Order.CreateMoveOrder(0, Vector3.Zero)));
        Assert.False(system.IsContextual);

        system.CancelOrderGenerator();

        Assert.True(system.IsContextual);
    }

    private OrderGeneratorSystem CreateSystem(IOrderGenerator generator)
    {
        ZeroHour.NetworkMessageBuffer = new NetworkMessageBuffer(ZeroHour, null);
        var system = new OrderGeneratorSystem(ZeroHour) { ActiveGenerator = generator };
        // Supply a picked terrain position without constructing a GPU-backed terrain scene.
        typeof(OrderGeneratorSystem).GetField("_worldPosition", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(system, (Vector3?)Vector3.Zero);
        return system;
    }

    private List<Order> PendingOrders() => (List<Order>)typeof(NetworkMessageBuffer)
        .GetField("_localOrders", BindingFlags.Instance | BindingFlags.NonPublic)
        .GetValue(ZeroHour.NetworkMessageBuffer);

    private sealed class RecordingGenerator(Order order) : IOrderGenerator
    {
        public bool CanDrag => false;
        public bool CursorRefreshed { get; private set; }
        public KeyModifiers Modifiers { get; private set; }

        public string GetCursor(KeyModifiers keyModifiers)
        {
            CursorRefreshed = true;
            return "Move";
        }

        public OrderGeneratorResult TryActivate(IScene3D scene, KeyModifiers keyModifiers)
        {
            Assert.True(CursorRefreshed);
            Modifiers = keyModifiers;
            return OrderGeneratorResult.SuccessAndContinue([order]);
        }

        public void BuildRenderList(RenderList renderList, Camera camera, in TimeInterval gameTime) { }
        public void UpdatePosition(Vector2 mousePosition, Vector3 worldPosition) { }
        public void UpdateDrag(Vector3 position) { }
    }
}
