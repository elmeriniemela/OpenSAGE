using OpenSage.Logic.AI;
using OpenSage.Logic.Object;
using Xunit;

namespace OpenSage.Tests.Logic.Object.Update;

public class AIUpdateTests : MockedGameTest
{
    [Fact]
    public void OverriddenFactoryInitializesDefaultStateOnce()
    {
        var gameObject = new GameObject(new ObjectDefinition(), Generals.GameEngine, Generals.PlayerManager.GetPlayerByIndex(0));
        var update = new DerivedAIUpdate(gameObject, Generals.GameEngine);

        Assert.Equal(AIStateIds.Idle, update.CurrentStateId);
        Assert.Equal(StateReturnTypeKind.Continue, update.UpdateStateMachine().Kind);
        Assert.Equal(StateReturnTypeKind.Continue, update.UpdateStateMachine().Kind);
        Assert.Equal(1, update.FactoryCalls);
    }

    private sealed class DerivedAIUpdate : AIUpdate
    {
        public int FactoryCalls { get; private set; }
        public StateId CurrentStateId => StateMachine.CurrentStateId;

        public DerivedAIUpdate(GameObject gameObject, IGameEngine gameEngine)
            : base(gameObject, gameEngine, new AIUpdateModuleData())
        {
        }

        public StateReturnType UpdateStateMachine() => StateMachine.Update();

        private protected override AIUpdateStateMachine CreateStateMachine()
        {
            FactoryCalls++;
            return new AIUpdateStateMachine(GameObject, GameEngine, this);
        }
    }
}
