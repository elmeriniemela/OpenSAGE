using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using OpenSage.Diagnostics;
using OpenSage.Logic;
using OpenSage.Logic.Object;
using OpenSage.Logic.Orders;
using OpenSage.Scripting;
using Xunit;

namespace OpenSage.Tests.Diagnostics;

public class SimulationScenarioRunnerTests : MockedGameTest
{
    [Theory]
    [InlineData(SageGame.CncGenerals)]
    [InlineData(SageGame.CncGeneralsZeroHour)]
    public void FixedSeedScenariosUseProductionOrdersScriptsObjectsAndTicks(SageGame gameType)
    {
        var first = RunTrace(gameType, 123);
        var second = RunTrace(gameType, 123);
        var otherSeed = RunTrace(gameType, 456);

        Assert.Equal(first, second);
        Assert.NotEqual(first, otherSeed);
    }

    private static int[] RunTrace(SageGame gameType, uint seed)
    {
        using var game = new TestGame(gameType, initializeSimulation: true);
        var trace = new List<int>();
        var scenario = new SimulationScenario
        {
            Name = "selection-and-scripting",
            Map = "synthetic/selection",
            Seed = seed,
            MaxTicks = 61,
            Setup = host =>
            {
                var obj = host.GameLogic.CreateObject(new ObjectDefinition(), host.PlayerManager.NeutralPlayer);
                SetProperty(obj, nameof(GameObject.Name), "scenario-object");
                AttachScript(host, CreateScript());
            }
        };
        // IDs start at 1 in a fresh host. These are the real player-order messages.
        scenario.Orders.Add(new ScenarioOrder(0, Order.CreateSetSelection(0, new ObjectId(1))));
        scenario.Orders.Add(new ScenarioOrder(1, new Order(0, OrderType.CreateGroup0)));
        scenario.Orders.Add(new ScenarioOrder(2, Order.CreateClearSelection(0)));
        scenario.Orders.Add(new ScenarioOrder(3, new Order(0, OrderType.SelectGroup0)));
        for (var tick = 0; tick < scenario.MaxTicks; tick++)
        {
            var expectedTick = tick;
            scenario.Assertions.Add(new ScenarioAssertion(tick, $"state at tick {tick}", host =>
            {
                trace.Add(host.GameLogic.Random.Next(0, 1000000));
                return host.GameLogic.CurrentFrame.Value == expectedTick + 1
                    && host.MapTime.TotalTime.Ticks == (long)expectedTick * TimeSpan.TicksPerSecond / 30
                    && host.Scripting.GetCounterValue("heartbeat") == expectedTick + 1
                    && host.PlayerManager.NeutralPlayer.SelectedUnits.Count == (expectedTick == 2 ? 0 : 1);
            }));
        }

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        Assert.True(report.Status == "Passed", JsonSerializer.Serialize(report));
        Assert.Equal(61, report.CompletedTicks);
        Assert.Equal(61, report.AssertionsEvaluated);
        Assert.Equal(4, report.OrdersQueued);
        Assert.False(report.CompletionGatePassed);
        Assert.False(game.Scripting.StrictValidation);
        Assert.False(game.NetworkMessageBuffer.StrictValidation);
        return trace.ToArray();
    }

    [Fact]
    public void AssertionFailureRetainsOriginAndStopsAtTheFailingTick()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario();
        scenario.Assertions.Clear();
        var origin = new ScenarioOrigin("synthetic/map", "objective-script", "truck", "supply-delivery");
        scenario.Assertions.Add(new ScenarioAssertion(2, "Truck did not deliver supplies.", _ => false, origin));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("Failed", report.Status);
        Assert.Equal(3, report.CompletedTicks);
        Assert.Equal(2, failure.Tick);
        Assert.Equal("Assertion", failure.Stage);
        Assert.Equal(origin, failure.Origin);
        Assert.Contains("Truck did not deliver supplies", failure.Message);
        Assert.Contains(nameof(InvalidOperationException), failure.Exception);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingAndEmptyActionsFailInsteadOfPassingSilently(bool emptyHandler)
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var type = emptyHandler ? ScriptActionType.TeamFollowWaypointsExact : (ScriptActionType)uint.MaxValue;
        var scenario = Scenario(host => AttachScript(host, CreateScript(type)));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("Failed", report.Status);
        Assert.Equal(0, report.CompletedTicks);
        Assert.Equal("Scripting", failure.Stage);
        Assert.Equal("heartbeat-script", failure.Origin.Script);
        Assert.Equal($"ScriptAction:{type}", failure.Origin.Feature);
        Assert.Equal(scenario.Map, failure.Origin.Map);
        Assert.Contains(emptyHandler ? "EmptyHandler" : "MissingHandler", failure.Message);
        Assert.False(game.Scripting.StrictValidation);
    }

    [Fact]
    public void MissingConditionsFailWithTheOriginatingScript()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var script = CreateScript();
        SetProperty(script.OrConditions[0], nameof(ScriptOrCondition.Conditions),
            new[] { new ScriptCondition((ScriptConditionType)uint.MaxValue) });
        var scenario = Scenario(host => AttachScript(host, script));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        var failure = Assert.Single(report.Failures);
        Assert.Equal("heartbeat-script", failure.Origin.Script);
        Assert.Equal($"ScriptCondition:{uint.MaxValue}", failure.Origin.Feature);
        Assert.Contains("MissingHandler", failure.Message);
    }

    [Fact]
    public void UnimplementedOrdersFailWithTheirScheduledOrigin()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario();
        scenario.Orders.Add(new ScenarioOrder(1, new Order(0, (OrderType)int.MaxValue),
            new ScenarioOrigin(Script: "reinforcements", Object: "truck", Feature: "order")));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(1, failure.Tick);
        Assert.Equal("Simulation", failure.Stage);
        Assert.Equal("reinforcements", failure.Origin.Script);
        Assert.Equal("truck", failure.Origin.Object);
        Assert.Equal($"Order:{int.MaxValue}", failure.Origin.Feature);
        Assert.False(game.NetworkMessageBuffer.StrictValidation);
    }

    [Fact]
    public void SchedulerExceptionsRetainTheObjectAndUpdateFeature()
    {
        using var game = new TestGame(SageGame.CncGeneralsZeroHour, true);
        var scenario = Scenario(host =>
        {
            var obj = host.GameLogic.CreateObject(new ObjectDefinition(), host.PlayerManager.NeutralPlayer);
            SetProperty(obj, nameof(GameObject.Name), "broken-truck");
            var queue = (SleepyUpdateList)typeof(GameLogic)
                .GetField("_sleepyUpdates", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host.GameLogic);
            queue.Add(new ThrowingUpdate(obj, host.GameEngine));
        });

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        var failure = Assert.Single(report.Failures);
        Assert.Equal(nameof(ThrowingUpdate), failure.Origin.Feature);
        Assert.Contains("broken-truck/1", failure.Origin.Object);
        Assert.Contains("scheduler regression", failure.Message);
        Assert.Equal("Simulation", failure.Stage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void OutOfBoundsAssertionsCannotBeOmittedFromAPassingReport(int assertionTick)
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario();
        scenario.Assertions.Add(new ScenarioAssertion(assertionTick, "out of bounds", _ => true));
        Assert.Throws<ArgumentException>(() => SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time));
        Assert.Equal(LogicFrame.Zero, game.GameLogic.CurrentFrame);
    }

    [Fact]
    public void ZeroAssertionsCannotPass()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario();
        scenario.Assertions.Clear();
        Assert.Throws<ArgumentException>(() => SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time));
    }

    [Fact]
    public void HostMustApplyTheClockAndMustBeFreshForEachRun()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario();
        var report = SimulationScenarioRunner.Run(game, scenario, _ => { });
        Assert.Equal("Clock", Assert.Single(report.Failures).Stage);
        Assert.Equal(0, report.CompletedTicks);

        Assert.Throws<ArgumentException>(() => SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time));
        using var freshGame = new TestGame(SageGame.CncGenerals, true);
        report = SimulationScenarioRunner.Run(freshGame, scenario, time => freshGame.MapTime = time);
        Assert.Equal("Passed", report.Status);
        Assert.Throws<ArgumentException>(() => SimulationScenarioRunner.Run(freshGame, scenario, time => freshGame.MapTime = time));
    }

    [Fact]
    public void CancellationIsRecordedAndRestoresStrictValidation()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        game.Scripting.StrictValidation = true;
        var report = SimulationScenarioRunner.Run(game, Scenario(), time => game.MapTime = time,
            new CancellationToken(canceled: true));
        Assert.Equal("Cancelled", report.Status);
        Assert.Equal(0, report.CompletedTicks);
        Assert.Single(report.Failures);
        Assert.True(game.Scripting.StrictValidation);
        Assert.False(game.NetworkMessageBuffer.StrictValidation);
    }

    [Fact]
    public void FailedSetupCannotLeaveStateForASecondScenario()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var report = SimulationScenarioRunner.Run(game, Scenario(host =>
        {
            host.Scripting.SetCounterValue("stale-state", 100);
            throw new InvalidOperationException("setup regression");
        }), time => game.MapTime = time);

        Assert.Equal("Setup", Assert.Single(report.Failures).Stage);
        Assert.Equal(LogicFrame.Zero, game.GameLogic.CurrentFrame);
        Assert.Throws<ArgumentException>(() => SimulationScenarioRunner.Run(game, Scenario(), time => game.MapTime = time));
    }

    [Fact]
    public void FailureReportsCanBeWrittenAndReadAsStructuredJson()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var scenario = Scenario(host => AttachScript(host, CreateScript((ScriptActionType)uint.MaxValue)));
        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);
        var directory = Path.Combine(Path.GetTempPath(), "opensage-scenario-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(directory, "report.json");
            report.WriteTo(path);
            report.WriteTo(path); // Replacing a previous report also works.
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(1, json.RootElement.GetProperty("FormatVersion").GetInt32());
            Assert.Equal("Failed", json.RootElement.GetProperty("Status").GetString());
            var origin = json.RootElement.GetProperty("Failures")[0].GetProperty("Origin");
            Assert.Equal(scenario.Map, origin.GetProperty("Map").GetString());
            Assert.Equal("heartbeat-script", origin.GetProperty("Script").GetString());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void DisabledMissingActionsAndConditionsDoNotCauseFalseFailures()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var script = CreateScript((ScriptActionType)uint.MaxValue);
        SetProperty(script.ActionsIfTrue[0], nameof(ScriptAction.Enabled), false);
        var condition = new ScriptCondition((ScriptConditionType)uint.MaxValue);
        SetProperty(condition, nameof(ScriptCondition.Enabled), false);
        SetProperty(script.OrConditions[0], nameof(ScriptOrCondition.Conditions), new[] { condition });
        var scenario = Scenario(host => AttachScript(host, script));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        Assert.True(report.Status == "Passed", JsonSerializer.Serialize(report));
        Assert.Equal(0, game.Scripting.GetCounterValue("heartbeat"));
    }

    [Theory]
    [InlineData(ScriptConditionType.False, true, 3)]
    [InlineData(ScriptConditionType.True, true, 0)]
    [InlineData(ScriptConditionType.False, false, 0)]
    [InlineData(ScriptConditionType.True, false, 3)]
    public void InvertedConditionsControlWhichBranchExecutes(ScriptConditionType conditionType, bool inverted, int executions)
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var script = CreateScript();
        var condition = new ScriptCondition(conditionType);
        SetProperty(condition, nameof(ScriptCondition.IsInverted), inverted);
        SetProperty(script.OrConditions[0], nameof(ScriptOrCondition.Conditions), new[] { condition });
        var scenario = Scenario(host => AttachScript(host, script));
        scenario.Assertions.Add(new ScenarioAssertion(2, "inversion controls branch",
            host => host.Scripting.GetCounterValue("heartbeat") == executions));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        Assert.True(report.Status == "Passed", JsonSerializer.Serialize(report));
    }

    [Theory]
    [InlineData(true, 6)]
    [InlineData(false, 1)]
    public void NestedGroupsExecuteBeforeTheirParentScriptsAndRespectActivation(bool childActive, int value)
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var childScript = CreateScript(ScriptActionType.SetCounter);
        childScript.ActionsIfTrue[0].Arguments[1].IntValue = 5;
        var child = Group("child", childActive, new[] { childScript });
        var parent = Group("parent", true, new[] { CreateScript() }, new[] { child });
        var scenario = Scenario(host =>
        {
            var list = new ScriptList();
            SetProperty(list, nameof(ScriptList.ScriptGroups), new[] { parent });
            host.Scene3D.PlayerScripts.ScriptLists = new[] { list };
            host.Scripting.Active = true;
        });
        scenario.Assertions.Add(new ScenarioAssertion(0, "nested group execution order",
            host => host.Scripting.GetCounterValue("heartbeat") == value));

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        Assert.True(report.Status == "Passed", JsonSerializer.Serialize(report));
    }

    [Fact]
    public void NestedScriptFailuresPreserveTheirGroupPath()
    {
        using var game = new TestGame(SageGame.CncGenerals, true);
        var child = Group("child", true, new[] { CreateScript((ScriptActionType)uint.MaxValue) });
        var parent = Group("parent", true, Array.Empty<Script>(), new[] { child });
        var scenario = Scenario(host =>
        {
            var list = new ScriptList();
            SetProperty(list, nameof(ScriptList.ScriptGroups), new[] { parent });
            host.Scene3D.PlayerScripts.ScriptLists = new[] { list };
            host.Scripting.Active = true;
        });

        var report = SimulationScenarioRunner.Run(game, scenario, time => game.MapTime = time);

        Assert.Equal("parent/child/heartbeat-script", Assert.Single(report.Failures).Origin.Script);
    }

    private static ScriptGroup Group(string name, bool active, Script[] scripts, ScriptGroup[] groups = null)
    {
        var result = new ScriptGroup();
        SetProperty(result, nameof(ScriptGroup.Name), name);
        SetProperty(result, nameof(ScriptGroup.Scripts), scripts);
        SetProperty(result, nameof(ScriptGroup.Groups), groups ?? Array.Empty<ScriptGroup>());
        typeof(ScriptGroup).GetField("_isActive", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(result, active);
        return result;
    }

    private static SimulationScenario Scenario(Action<IGame> setup = null)
    {
        var result = new SimulationScenario { Name = "regression", Map = "synthetic/test", Seed = 17, MaxTicks = 3, Setup = setup };
        result.Assertions.Add(new ScenarioAssertion(2, "runs to bound", _ => true));
        return result;
    }

    private static void AttachScript(IGame game, Script script)
    {
        var list = new ScriptList();
        SetProperty(list, nameof(ScriptList.Scripts), new[] { script });
        game.Scene3D.PlayerScripts.ScriptLists = new[] { list };
        game.Scripting.Active = true;
    }

    private static Script CreateScript(ScriptActionType actionType = ScriptActionType.IncrementCounter)
    {
        var script = new Script { IsActive = true };
        SetProperty(script, nameof(Script.Name), "heartbeat-script");
        var orCondition = new ScriptOrCondition();
        SetProperty(orCondition, nameof(ScriptOrCondition.Conditions), new[] { new ScriptCondition(ScriptConditionType.True) });
        SetProperty(script, nameof(Script.OrConditions), new[] { orCondition });
        SetProperty(script, nameof(Script.ActionsIfTrue), new[]
        {
            new ScriptAction
            {
                ContentType = actionType,
                Arguments = new[]
                {
                    new ScriptArgument(ScriptArgumentType.CounterName, "heartbeat"),
                    new ScriptArgument(ScriptArgumentType.Integer, 1)
                }
            }
        });
        SetProperty(script, nameof(Script.ActionsIfFalse), Array.Empty<ScriptAction>());
        return script;
    }

    private static void SetProperty<T>(T target, string name, object value) =>
        typeof(T).GetProperty(name).SetValue(target, value);

    private sealed class ThrowingUpdate : UpdateModule
    {
        internal ThrowingUpdate(GameObject gameObject, IGameEngine engine) : base(gameObject, engine) { }
        public override UpdateSleepTime Update() => throw new InvalidOperationException("scheduler regression");
    }
}
