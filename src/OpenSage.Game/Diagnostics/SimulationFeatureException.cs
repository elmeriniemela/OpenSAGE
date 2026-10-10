using System;

namespace OpenSage.Diagnostics;

/// <summary>Preserves the simulation feature that failed across scheduler and script calls.</summary>
public sealed class SimulationFeatureException : Exception
{
    public string Script { get; }
    public string Object { get; }
    public string Feature { get; }

    internal SimulationFeatureException(string script, string gameObject, string feature, Exception innerException)
        : base($"Simulation feature '{feature}' failed: {innerException.Message}", innerException)
    {
        Script = script;
        Object = gameObject;
        Feature = feature;
    }
}
