using OpenSage.Logic.Object;
using Xunit;

namespace OpenSage.Tests.Logic.Object.Body;

public class HighlanderBodyTests : ModuleTest
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
        var body = new HighlanderBody(gameObject, ZeroHour.GameEngine, new HighlanderBodyModuleData
        {
            InitialHealth = 100.0f,
            MaxHealth = 100.0f,
        });

        body.AttemptDamage(new DamageInfoInput
        {
            DamageType = damageType,
            Amount = 150.0f,
        });

        Assert.Equal(100.0f, body.Health);
    }
}
