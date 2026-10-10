using System.Collections.Generic;

namespace OpenSage.Diagnostics;

/// <summary>
/// An inventory, not a gameplay certification. Registered handlers remain unverified
/// until behavior scenarios provide evidence of support.
/// </summary>
public sealed class ContentManifest
{
    public int FormatVersion { get; } = 1;
    public string Game { get; init; }
    public bool CompletionGatePassed { get; } = false;
    public List<ResolvedAssetInventory> ResolvedAssets { get; } = new();
    public List<CampaignInventory> Campaigns { get; } = new();
    public List<PersonaInventory> ChallengePersonas { get; } = new();
    public List<string> PlayableFactions { get; } = new();
    public List<string> ExcludedCampaigns { get; } = new();
    public List<MapInventory> Maps { get; } = new();
    public List<MediaReference> Media { get; } = new();
    public List<InventoryFailure> Failures { get; } = new();
    public List<string> PendingCoverage { get; } = new()
    {
        "GUI entry-point and callback dependency traversal",
        "Cinematic and dynamically referenced map dependencies",
        "Object modules, orders, buildable objects and transitive asset dependencies",
        "Audio file resolution and menu media",
        "Headless gameplay scenarios and behavioral support verification"
    };
}

public sealed record CampaignInventory(string Name, string FirstMission, bool IsChallenge,
    IReadOnlyList<MissionInventory> Missions);
public sealed record MissionInventory(string Name, string Map, string NextMission);
public sealed record PersonaInventory(string Name, string Faction, string Campaign, bool Selectable);
public sealed record MediaReference(string Source, string Kind, string Name, string Path, bool? Resolved);
public sealed record InventoryFailure(string Source, string Feature, string Message);
public sealed record ResolvedAssetInventory(string Path, uint Length, string Source);
public sealed record ScriptFeatureUsage(string Script, string Branch, string Kind, string Feature,
    string InternalName, string Status, bool Enabled);

public sealed class MapInventory
{
    public string Path { get; init; }
    public bool IsSkirmish { get; set; }
    public int PlayerSlots { get; set; }
    public bool Parsed { get; set; }
    public List<string> RequiredBy { get; } = new();
    public List<string> ObjectTypes { get; } = new();
    public List<ScriptFeatureUsage> ScriptFeatures { get; } = new();
}
