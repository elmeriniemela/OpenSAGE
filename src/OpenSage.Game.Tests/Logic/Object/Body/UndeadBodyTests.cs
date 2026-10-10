using OpenSage.Logic.Object;
using Xunit;

namespace OpenSage.Tests.Logic.Object.Body;

public class UndeadBodyTests : ModuleTest
{
    [Theory]
    [InlineData(DamageType.Explosion)]
    [InlineData(DamageType.Unresistable)]
    public void DamageToDeadObjectLeavesHealthUnchanged(DamageType damageType)
    {
        var gameObject = new GameObject(new ObjectDefinition(), ZeroHour.GameEngine, null)
        {
            IsEffectivelyDead = true,
        };
        var body = new UndeadBody(gameObject, ZeroHour.GameEngine, new UndeadBodyModuleData
        {
            InitialHealth = 100.0f,
            MaxHealth = 100.0f,
        });

        body.AttemptDamage(new DamageInfoInput
        {
            DamageType = damageType,
            Amount = 10.0f,
        });

        Assert.Equal(100.0f, body.Health);
    }
}
