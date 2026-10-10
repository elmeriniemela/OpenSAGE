using System;
using OpenSage.Diagnostics;

namespace OpenSage.Logic;

/// <summary>The simulation pipeline shared by windowed gameplay and bounded scenarios.</summary>
internal static class SimulationTick
{
    internal static void Execute(IGame game, in TimeInterval time)
    {
        game.GameLogic.Update();

        try
        {
            game.NetworkMessageBuffer?.Tick();
        }
        catch (Exception exception) when (exception is not SimulationFeatureException)
        {
            throw new SimulationFeatureException(null, null,
                $"Order:{game.NetworkMessageBuffer?.CurrentOrder?.OrderType}", exception);
        }

        game.Scene3D?.LogicTick(time);
        game.PartitionCellManager.Update();
    }

    internal static void UpdateScene(IGame game, in TimeInterval time)
    {
        game.PlayerManager.LogicTick();

        foreach (var gameObject in game.GameLogic.Objects)
        {
            try
            {
                gameObject.LogicTick(time);
            }
            catch (Exception exception) when (exception is not SimulationFeatureException)
            {
                throw new SimulationFeatureException(null,
                    $"{gameObject.Definition.Name}/{gameObject.Name}/{gameObject.Id.Index}",
                    "Object.LogicTick", exception);
            }
        }

        game.GameLogic.DeleteDestroyed();
    }
}
