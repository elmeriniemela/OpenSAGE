using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenSage.Logic;
using OpenSage.Logic.Object;

namespace OpenSage.Diagnostics;

/// <summary>
/// Drives a prepared, device-independent game host through the production tick pipeline.
/// The caller owns the host and updates its clock through setTime; no wall clock is read.
/// </summary>
public static class SimulationScenarioRunner
{
    private static readonly ConditionalWeakTable<IGame, HostState> s_hostStates = new();

    private sealed class HostState
    {
        internal int Started;
    }

    public static SimulationScenarioReport Run(IGame game, SimulationScenario scenario,
        Action<TimeInterval> setTime, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(setTime);
        if (string.IsNullOrWhiteSpace(scenario.Name) || scenario.MaxTicks <= 0 || scenario.Assertions.Count == 0)
        {
            throw new ArgumentException("A scenario needs a name, a positive tick bound and at least one assertion.", nameof(scenario));
        }
        if (game.GameLogic == null || game.GameLogic.CurrentFrame != LogicFrame.Zero || game.GameLogic.Objects.Any())
        {
            throw new ArgumentException("Use a fresh host; create scenario objects in Setup after the seed is initialized.", nameof(game));
        }
        if (game.Scene3D == null || game.PartitionCellManager == null || game.NetworkMessageBuffer == null || game.Scripting == null)
        {
            throw new ArgumentException("The host must provide a scene, partition manager, order queue and scripting system.", nameof(game));
        }
        if (game.GraphicsDevice != null || game.Panel != null)
        {
            throw new ArgumentException("Scenarios require a device-independent host without a graphics device or game panel.", nameof(game));
        }

        // Snapshot schedules so callbacks cannot change which assertions are required.
        var orders = scenario.Orders.OrderBy(x => x.Tick).ToArray();
        var assertions = scenario.Assertions.OrderBy(x => x.Tick).ToArray();
        if (orders.Any(x => x.Order == null || x.Tick < 0 || x.Tick >= scenario.MaxTicks)
            || assertions.Any(x => x.Check == null || x.Tick < 0 || x.Tick >= scenario.MaxTicks))
        {
            throw new ArgumentException("All orders and assertions must be valid and inside the tick bound.", nameof(scenario));
        }

        var logicRate = (uint)game.SageGame.LogicFramesPerSecond();
        var scriptingRate = game.Scripting.TickRate;
        if (logicRate == 0 || scriptingRate == 0)
        {
            throw new ArgumentException("Simulation and scripting tick rates must be positive.", nameof(game));
        }
        var hostState = s_hostStates.GetValue(game, _ => new HostState());
        if (Interlocked.CompareExchange(ref hostState.Started, 1, 0) != 0)
        {
            throw new ArgumentException("A scenario has already used this host. Create a new host even after a failed setup.", nameof(game));
        }
        var report = new SimulationScenarioReport
        {
            Scenario = scenario.Name,
            Game = game.SageGame.ToString(),
            Map = scenario.Map,
            Seed = scenario.Seed,
            MaxTicks = scenario.MaxTicks
        };
        var previousScriptValidation = game.Scripting.StrictValidation;
        var previousOrderValidation = game.NetworkMessageBuffer.StrictValidation;
        var tick = -1;
        var stage = "Setup";
        ScenarioOrigin origin = null;
        try
        {
            game.Scripting.StrictValidation = true;
            game.NetworkMessageBuffer.StrictValidation = true;
            cancellationToken.ThrowIfCancellationRequested();
            game.GameLogic.Random.Initialize(scenario.Seed);
            game.GameClient?.Random.Initialize(scenario.Seed);
            setTime(TimeInterval.Zero);
            scenario.Setup?.Invoke(game);

            var orderIndex = 0;
            var assertionIndex = 0;
            long scriptingTick = 0;
            for (tick = 0; tick < scenario.MaxTicks; tick++)
            {
                origin = null;
                stage = "Clock";
                cancellationToken.ThrowIfCancellationRequested();
                var total = TimeSpan.FromTicks((long)tick * TimeSpan.TicksPerSecond / logicRate);
                var next = TimeSpan.FromTicks(((long)tick + 1) * TimeSpan.TicksPerSecond / logicRate);
                var time = new TimeInterval(total, next - total);
                setTime(time);
                if (game.MapTime.TotalTime != total || game.MapTime.DeltaTime != time.DeltaTime)
                {
                    throw new InvalidOperationException("The host did not apply the simulation clock.");
                }

                stage = "OrderQueue";
                while (orderIndex < orders.Length && orders[orderIndex].Tick == tick)
                {
                    origin = orders[orderIndex].Origin;
                    game.NetworkMessageBuffer.AddLocalOrder(orders[orderIndex++].Order);
                    report.OrdersQueued++;
                }

                origin = null;
                stage = "Simulation";
                SimulationTick.Execute(game, time);
                stage = "Scripting";
                // Integer rate comparison avoids accumulated floating-point clock drift.
                // Match gameplay: a logic tick precedes a scripting tick due at the same time.
                while (scriptingTick * logicRate <= (long)tick * scriptingRate)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    game.Scripting.ScriptingTick();
                    scriptingTick++;
                }
                report.CompletedTicks++;

                stage = "Assertion";
                while (assertionIndex < assertions.Length && assertions[assertionIndex].Tick == tick)
                {
                    var assertion = assertions[assertionIndex++];
                    origin = assertion.Origin;
                    report.AssertionsEvaluated++;
                    if (!assertion.Check(game))
                    {
                        throw new InvalidOperationException(assertion.Description);
                    }
                }
            }
            report.Status = "Passed";
        }
        catch (Exception exception)
        {
            if (stage == "Simulation" && exception is SimulationFeatureException { Feature: var feature }
                && feature.StartsWith("Order:", StringComparison.Ordinal))
            {
                origin = orders.FirstOrDefault(x => ReferenceEquals(x.Order, game.NetworkMessageBuffer.CurrentOrder))?.Origin;
            }
            var failure = exception as SimulationFeatureException;
            report.Failures.Add(new ScenarioFailure(tick, stage,
                new ScenarioOrigin(origin?.Map ?? scenario.Map, failure?.Script ?? origin?.Script,
                    failure?.Object ?? origin?.Object, failure?.Feature ?? origin?.Feature ?? stage),
                exception.Message, exception.ToString()));
            report.Status = exception is OperationCanceledException ? "Cancelled" : "Failed";
        }
        finally
        {
            game.Scripting.StrictValidation = previousScriptValidation;
            game.NetworkMessageBuffer.StrictValidation = previousOrderValidation;
        }

        return report;
    }
}
