using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using OpenSage.Logic.Orders;

namespace OpenSage.Diagnostics;

/// <summary>A bounded, fixed-seed scenario. Ticks are zero-based; checks run after that tick.</summary>
public sealed class SimulationScenario
{
    public string Name { get; init; }
    public string Map { get; init; }
    public uint Seed { get; init; }
    public int MaxTicks { get; init; }

    /// <summary>Runs after RNG initialization, before the first tick. Load scenario objects here.</summary>
    public Action<IGame> Setup { get; init; }

    public List<ScenarioOrder> Orders { get; } = new();
    public List<ScenarioAssertion> Assertions { get; } = new();
}

public sealed record ScenarioOrigin(string Map = null, string Script = null, string Object = null, string Feature = null);

public sealed record ScenarioOrder(int Tick, Order Order, ScenarioOrigin Origin = null);

public sealed record ScenarioAssertion(int Tick, string Description, Func<IGame, bool> Check, ScenarioOrigin Origin = null);

public sealed record ScenarioFailure(int Tick, string Stage, ScenarioOrigin Origin, string Message, string Exception);

public sealed class SimulationScenarioReport
{
    public int FormatVersion => 1;
    public string Scenario { get; init; }
    public string Game { get; init; }
    public string Map { get; init; }
    public uint Seed { get; init; }
    public int MaxTicks { get; init; }
    public int CompletedTicks { get; internal set; }
    public int OrdersQueued { get; internal set; }
    public int AssertionsEvaluated { get; internal set; }
    public string Status { get; internal set; } = "Running";
    public bool CompletionGatePassed => false;
    public List<ScenarioFailure> Failures { get; } = new();

    public void WriteTo(string path)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
