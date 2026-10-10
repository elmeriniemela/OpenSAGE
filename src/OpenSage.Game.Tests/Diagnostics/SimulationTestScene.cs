using System;
using System.Collections.Generic;
using OpenSage.Audio;
using OpenSage.Content.Loaders;
using OpenSage.Data.Map;
using OpenSage.DataStructures;
using OpenSage.Graphics.Cameras;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Graphics.Rendering;
using OpenSage.Graphics.Rendering.Shadows;
using OpenSage.Graphics.Rendering.Water;
using OpenSage.Gui;
using OpenSage.Gui.DebugUI;
using OpenSage.Logic;
using OpenSage.Logic.Object;
using OpenSage.Mathematics;
using OpenSage.Rendering;
using OpenSage.Scripting;
using OpenSage.Settings;
using OpenSage.Terrain;
using OpenSage.Terrain.Roads;
using Player = OpenSage.Logic.Player;

namespace OpenSage.Tests.Diagnostics;

// Synthetic scene data for public CPU scenarios. Production state and tick methods
// are used throughout; presentation entry points fail if a test accidentally calls them.
internal sealed class SimulationTestScene : IScene3D
{
    public IGame Game { get; }
    public IGameEngine GameEngine => Game.GameEngine;
    public IGameObjectCollection GameObjects => Game.GameLogic;
    public IReadOnlyList<Player> Players => Game.PlayerManager.Players;
    public Player LocalPlayer => Game.PlayerManager.LocalPlayer;
    public MapFile MapFile { get; } = new();
    public PlayerScriptsList PlayerScripts { get; } = new() { ScriptLists = Array.Empty<ScriptList>() };
    public SelectionGui SelectionGui { get; } = new();
    public Radar Radar { get; } = new();
    public IQuadtree<GameObject> Quadtree { get; } = new Quadtree<GameObject>(new RectangleF(0, 0, 20, 20));
    public GameObject BuildPreviewObject { get; set; }
    public bool ShowTerrain { get; set; }
    public bool ShowWater { get; set; }
    public bool ShowRoads { get; set; }
    public bool ShowBridges { get; set; }
    public bool FrustumCulling { get; set; }
    public bool ShowObjects { get; set; }
    public IEditorCameraController EditorCameraController => throw new NotSupportedException();
    public DebugOverlay DebugOverlay => throw new NotSupportedException();
    public ParticleSystemManager ParticleSystemManager => throw new NotSupportedException();
    public Camera Camera => throw new NotSupportedException();
    public TacticalView TacticalView => throw new NotSupportedException();
    public OpenSage.Terrain.Terrain Terrain => throw new NotSupportedException();
    public WaterAreaCollection WaterAreas => throw new NotSupportedException();
    public RoadCollection Roads => throw new NotSupportedException();
    public Bridge[] Bridges => throw new NotSupportedException();
    public CameraCollection Cameras => throw new NotSupportedException();
    public WaypointCollection Waypoints => throw new NotSupportedException();
    public WorldLighting Lighting => throw new NotSupportedException();
    public ShadowSettings Shadows => throw new NotSupportedException();
    public WaterSettings Waters => throw new NotSupportedException();
    public OpenSage.Navigation.Navigation Navigation => throw new NotSupportedException();
    public AudioSystem Audio => throw new NotSupportedException();
    public AssetLoadContext AssetLoadContext => Game.AssetStore.LoadContext;
    public RenderScene RenderScene => throw new NotSupportedException();
    public RadarDrawUtil RadarDrawUtil => throw new NotSupportedException();

    internal SimulationTestScene(IGame game)
    {
        Game = game;
        var heights = HeightMapData.Create(0, new ushort[2, 2]);
        typeof(HeightMapData).GetProperty(nameof(HeightMapData.Borders))
            .SetValue(heights, new[] { new HeightMapBorder { X = 2, Y = 2 } });
        typeof(MapFile).GetProperty(nameof(MapFile.HeightMapData)).SetValue(MapFile, heights);
        game.TerrainLogic.SetHeightMapData(heights);
    }

    public int GetPlayerIndex(Player player) => Game.PlayerManager.GetPlayerIndex(player);
    public void LogicTick(in TimeInterval time) => SimulationTick.UpdateScene(Game, time);
    public void LocalLogicTick(in TimeInterval time, float tickT) => throw new NotSupportedException();
    public void BuildRenderList(RenderList list, Camera camera, in TimeInterval time) => throw new NotSupportedException();
    public void Render(DrawingContext2D context) => throw new NotSupportedException();
    public GameObject CreateSkirmishPlayerStartingBuilding(in PlayerSetting setting, Player player) => throw new NotSupportedException();
    public void Dispose() { }
}
