using System;
using OpenSage.Scripting;
using Xunit;

namespace OpenSage.Tests.Scripting;

public class ScriptExecutionTests : MockedGameTest
{
    [Theory]
    [InlineData(SageGame.CncGenerals, 0u)]
    [InlineData(SageGame.CncGenerals, 1u)]
    [InlineData(SageGame.CncGenerals, 3u)]
    [InlineData(SageGame.CncGeneralsZeroHour, 0u)]
    [InlineData(SageGame.CncGeneralsZeroHour, 1u)]
    [InlineData(SageGame.CncGeneralsZeroHour, 3u)]
    public void RepeatedEvaluationsUseTheConfiguredNumberOfScriptingTicks(SageGame gameType, uint seconds)
    {
        var game = gameType == SageGame.CncGenerals ? Generals : ZeroHour;
        var context = new ScriptExecutionContext(game);
        var script = CreateScript(true, false);
        SetProperty(script, nameof(Script.EvaluationInterval), seconds);
        var period = Math.Max(1u, seconds * game.Scripting.TickRate);

        // Observe three complete periods, including every intervening tick.
        for (uint tick = 0; tick <= period * 3; tick++)
        {
            script.Execute(context);
            Assert.Equal((int)(tick / period + 1), game.Scripting.GetCounterValue("executions"));
        }
    }

    [Fact]
    public void FalseBranchDoesNotDeactivateAScriptWaitingForSuccess()
    {
        var context = new ScriptExecutionContext(Generals);
        var script = CreateScript(false, true);
        SetProperty(script, nameof(Script.ActionsIfFalse), IncrementActions());

        script.Execute(context);
        script.Execute(context);

        Assert.True(script.IsActive);
        Assert.Equal(2, Generals.Scripting.GetCounterValue("executions"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SuccessfulOneShotDeactivatesEvenWithNoActions(bool hasActions)
    {
        var script = CreateScript(true, true);
        if (!hasActions)
        {
            SetProperty(script, nameof(Script.ActionsIfTrue), Array.Empty<ScriptAction>());
        }

        var context = new ScriptExecutionContext(Generals);
        script.Execute(context);
        script.Execute(context);

        Assert.False(script.IsActive);
        Assert.Equal(hasActions ? 1 : 0, Generals.Scripting.GetCounterValue("executions"));
    }

    [Fact]
    public void InactiveScriptsAndSubroutinesDoNotRunDuringNormalEvaluation()
    {
        var script = CreateScript(true, false);
        var context = new ScriptExecutionContext(Generals);
        script.IsActive = false;
        script.Execute(context);
        script.IsActive = true;
        SetProperty(script, nameof(Script.IsSubroutine), true);
        script.Execute(context);
        Assert.Equal(0, Generals.Scripting.GetCounterValue("executions"));

        script.ExecuteAsSubroutine(context);
        Assert.Equal(1, Generals.Scripting.GetCounterValue("executions"));
    }

    private static Script CreateScript(bool conditionValue, bool deactivate)
    {
        var condition = new ScriptCondition(conditionValue ? ScriptConditionType.True : ScriptConditionType.False);
        var orCondition = new ScriptOrCondition();
        SetProperty(orCondition, nameof(ScriptOrCondition.Conditions), new[] { condition });
        var script = new Script { IsActive = true };
        SetProperty(script, nameof(Script.OrConditions), new[] { orCondition });
        SetProperty(script, nameof(Script.ActionsIfTrue), IncrementActions());
        SetProperty(script, nameof(Script.ActionsIfFalse), Array.Empty<ScriptAction>());
        SetProperty(script, nameof(Script.DeactivateUponSuccess), deactivate);
        return script;
    }

    private static ScriptAction[] IncrementActions() => new[]
    {
        new ScriptAction
        {
            ContentType = ScriptActionType.IncrementCounter,
            Arguments = new[]
            {
                new ScriptArgument(ScriptArgumentType.CounterName, "executions"),
                new ScriptArgument(ScriptArgumentType.Integer, 1)
            }
        }
    };

    // Script definitions normally come from the binary parser. Keep synthetic
    // fixtures independent of proprietary maps without exposing mutable public APIs.
    private static void SetProperty<T>(T target, string name, object value) =>
        typeof(T).GetProperty(name).SetValue(target, value);
}
