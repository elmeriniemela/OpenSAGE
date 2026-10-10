using System;
using System.Reflection;
using OpenSage.Logic;
using OpenSage.Logic.Object;
using Xunit;

namespace OpenSage.Tests.Logic;

public class GameLogicTests : MockedGameTest
{
    [Fact]
    public void CreatingEarlierPhaseModuleDuringUpdatePreservesQueueOrder()
    {
        var gameObject = new GameObject(new ObjectDefinition(), ZeroHour.GameEngine, null);
        var queue = (SleepyUpdateList)typeof(GameLogic)
            .GetField("_sleepyUpdates", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(ZeroHour.GameLogic);
        var createdModule = new CallbackUpdate(gameObject, ZeroHour.GameEngine, UpdateOrder.Order1);
        var creatingModule = new CallbackUpdate(gameObject, ZeroHour.GameEngine, UpdateOrder.Order2,
            () => queue.Add(createdModule));
        queue.Add(creatingModule);
        for (uint frame = 5; frame <= 7; frame++)
        {
            queue.Add(new CallbackUpdate(gameObject, ZeroHour.GameEngine, UpdateOrder.Order2)
            {
                NextCallFrame = new LogicFrame(frame)
            });
        }

        ZeroHour.GameLogic.Update();

        Assert.Equal(1, creatingModule.Calls);
        Assert.Equal(1, createdModule.Calls);
        for (var i = 0; i < queue.Count; i++)
        {
            Assert.Equal(i, queue[i].IndexInLogic);
            if (i > 0)
            {
                Assert.True(queue[(i - 1) / 2].Priority <= queue[i].Priority);
            }
        }
    }

    private sealed class CallbackUpdate : UpdateModule
    {
        private readonly UpdateOrder _order;
        private readonly Action _onUpdate;
        protected override UpdateOrder UpdateOrder => _order;
        public int Calls { get; private set; }

        public CallbackUpdate(GameObject gameObject, IGameEngine gameEngine, UpdateOrder order, Action onUpdate = null)
            : base(gameObject, gameEngine)
        {
            _order = order;
            _onUpdate = onUpdate;
            NextCallFrame = LogicFrame.Zero;
        }

        public override UpdateSleepTime Update()
        {
            Calls++;
            _onUpdate?.Invoke();
            return UpdateSleepTime.Forever;
        }
    }
}
