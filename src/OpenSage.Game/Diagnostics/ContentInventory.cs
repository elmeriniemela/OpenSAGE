using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenSage.Content;
using OpenSage.Data;
using OpenSage.Data.Ini;
using OpenSage.Data.Map;
using OpenSage.IO;
using OpenSage.Logic;
using OpenSage.Scripting;

namespace OpenSage.Diagnostics;

/// <summary>Reads resolved installation assets without creating graphics or audio devices.</summary>
public static class ContentInventory
{
    public static ContentManifest Create(GameInstallation installation)
    {
        using var fileSystem = installation.CreateFileSystem();
        return Create(fileSystem, installation.Game.Game);
    }

    public static ContentManifest Create(FileSystem fileSystem, SageGame game)
    {
        if (game != SageGame.CncGenerals && game != SageGame.CncGeneralsZeroHour)
        {
            throw new ArgumentOutOfRangeException(nameof(game));
        }

        var manifest = new ContentManifest { Game = game.ToString() };
        manifest.ResolvedAssets.AddRange(fileSystem.GetFilesInDirectory("", "*", SearchOption.AllDirectories)
            .OrderBy(x => CanonicalPath(x.FilePath), StringComparer.Ordinal)
            .Select(x => new ResolvedAssetInventory(CanonicalPath(x.FilePath), x.Length, x.SourcePath)));
        var assets = new AssetStore(game, fileSystem, null, null, null, null, null, OnDemandAssetLoadStrategy.None);
        assets.PushScope();
        try
        {
            var dataContext = new IniDataContext();
            foreach (var path in new[] { "Data/INI/Campaign.ini", "Data/INI/PlayerTemplate.ini", "Data/INI/Video.ini", "Maps/MapCache.ini" })
            {
                LoadDefinitions(path);
            }
            if (game == SageGame.CncGeneralsZeroHour)
            {
                LoadDefinitions("Data/INI/ChallengeMode.ini");
            }

            var campaignNames = new HashSet<string>(new[] { "TRAINING", "USA", "GLA", "China" }, StringComparer.OrdinalIgnoreCase);
            if (assets.ChallengeGenerals.Current != null)
            {
                foreach (var (name, persona) in assets.ChallengeGenerals.Current.Personas.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    manifest.ChallengePersonas.Add(new PersonaInventory(name, persona.PlayerTemplate, persona.Campaign, persona.StartsEnabled));
                    if (persona.StartsEnabled)
                    {
                        campaignNames.Add(persona.Campaign);
                    }
                }
            }

            var maps = new Dictionary<string, MapInventory>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in campaignNames.OrderBy(x => x, StringComparer.Ordinal))
            {
                var campaign = assets.CampaignTemplates.GetByName(name);
                if (campaign == null)
                {
                    manifest.Failures.Add(new InventoryFailure("Data/INI/Campaign.ini", name, "Required campaign definition missing."));
                    continue;
                }

                // Follow configured identifiers; stock campaigns deliberately skip some numbers.
                var missions = new List<MissionInventory>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var missionName = campaign.FirstMission;
                while (!string.IsNullOrEmpty(missionName))
                {
                    if (!visited.Add(missionName))
                    {
                        manifest.Failures.Add(new InventoryFailure(name, missionName, "Campaign route contains a cycle."));
                        break;
                    }
                    if (!campaign.Missions.TryGetValue(missionName, out var mission))
                    {
                        manifest.Failures.Add(new InventoryFailure(name, missionName, "Mission definition missing."));
                        break;
                    }

                    missions.Add(new MissionInventory(mission.Name, CanonicalPath(mission.Map), mission.NextMission));
                    RequireMap(mission.Map, $"{name}/{mission.Name}");
                    AddMedia($"{name}/{mission.Name}", "Movie", mission.IntroMovie);
                    AddMedia($"{name}/{mission.Name}", "Speech", mission.BriefingVoice);
                    missionName = mission.NextMission;
                }
                AddMedia(name, "Movie", campaign.FinalMovie);
                manifest.Campaigns.Add(new CampaignInventory(name, campaign.FirstMission, campaign.IsChallengeCampaign, missions));
            }

            manifest.ExcludedCampaigns.AddRange(assets.CampaignTemplates.Where(x => !campaignNames.Contains(x.Name))
                .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
            manifest.PlayableFactions.AddRange(assets.PlayerTemplates.Where(x => x.PlayableSide)
                .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));

            foreach (var map in assets.MapCaches.Where(x => x.IsOfficial && x.IsMultiplayer).OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                var entry = RequireMap(map.Name, "Skirmish");
                entry.IsSkirmish = true;
                entry.PlayerSlots = map.NumPlayers;
            }

            var actions = GetActionHandlers(game);
            var conditions = GetConditionHandlers(game);
            foreach (var map in maps.Values.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                manifest.Maps.Add(map);
                var entry = fileSystem.GetFile(map.Path);
                if (entry == null)
                {
                    manifest.Failures.Add(new InventoryFailure(map.Path, "Map", "Required map file missing."));
                    continue;
                }
                try
                {
                    var parsed = MapFile.FromFileSystemEntry(entry);
                    map.ObjectTypes.AddRange((parsed.ObjectsList?.Objects ?? Array.Empty<MapObject>())
                        .Select(x => x.TypeName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal));
                    var lists = parsed.SidesList?.Players.Select(x => (Scripts: x.Scripts, Owner: x.Name))
                        ?? Enumerable.Empty<(ScriptList Scripts, string Owner)>();
                    if (parsed.PlayerScriptsList != null)
                    {
                        lists = lists.Concat(parsed.PlayerScriptsList.ScriptLists.Select((list, index) =>
                            (Scripts: list, Owner: $"PlayerScripts[{index}]")));
                    }
                    foreach (var (list, owner) in lists)
                    {
                        foreach (var (script, location) in EnumerateScripts(list))
                        {
                            var scriptPath = $"{owner}/{location}";
                            foreach (var action in script.ActionsIfTrue)
                            {
                                AddAction(action, scriptPath, "True");
                            }
                            foreach (var action in script.ActionsIfFalse)
                            {
                                AddAction(action, scriptPath, "False");
                            }
                            foreach (var condition in script.OrConditions.SelectMany(x => x.Conditions))
                            {
                                var type = ResolveType(condition.ContentType, condition.InternalName?.Name);
                                var status = conditions.TryGetValue(type, out var handler) ? ClassifyHandler(handler) : "MissingHandler";
                                map.ScriptFeatures.Add(new ScriptFeatureUsage(scriptPath, "Condition", "Condition", type.ToString(),
                                    condition.InternalName?.Name, status, condition.Enabled));
                            }
                        }
                    }
                    map.Parsed = true;

                    void AddAction(ScriptAction action, string script, string branch)
                    {
                        var type = ResolveType(action.ContentType, action.InternalName?.Name);
                        var status = actions.TryGetValue(type, out var handler) ? ClassifyHandler(handler) : "MissingHandler";
                        map.ScriptFeatures.Add(new ScriptFeatureUsage(script, branch, "Action", type.ToString(),
                            action.InternalName?.Name, status, action.Enabled));
                        foreach (var argument in action.Arguments)
                        {
                            var kind = argument.ArgumentType switch
                            {
                                ScriptArgumentType.MovieName => "Movie",
                                ScriptArgumentType.SpeechName => "Speech",
                                ScriptArgumentType.MusicName => "Music",
                                ScriptArgumentType.SoundName => "Sound",
                                _ => null
                            };
                            if (kind != null)
                            {
                                AddMedia($"{map.Path}/{script}", kind, argument.StringValue);
                            }
                        }
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    manifest.Failures.Add(new InventoryFailure(map.Path, "MapParse", exception.Message));
                }
            }

            return manifest;

            void LoadDefinitions(string path)
            {
                var entry = fileSystem.GetFile(path);
                if (entry == null)
                {
                    manifest.Failures.Add(new InventoryFailure(path, "Definitions", "Required definition file missing."));
                    return;
                }
                try
                {
                    // Includes must resolve through the same composite view as gameplay assets.
                    var resolved = new FileSystemEntry(fileSystem, entry.FilePath, entry.Length, entry.Open, entry.SourcePath);
                    var parser = new IniParser(resolved, assets, game, dataContext, Encoding.Latin1);
                    parser.ParseFile();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    manifest.Failures.Add(new InventoryFailure(path, "DefinitionParse", exception.Message));
                }
            }

            MapInventory RequireMap(string path, string source)
            {
                path = CanonicalPath(path);
                if (!maps.TryGetValue(path, out var map))
                {
                    map = new MapInventory { Path = path };
                    maps.Add(path, map);
                }
                map.RequiredBy.Add(source);
                return map;
            }

            void AddMedia(string source, string kind, string name)
            {
                if (string.IsNullOrEmpty(name))
                {
                    return;
                }
                string path = null;
                bool? resolved = null;
                if (kind == "Movie")
                {
                    var video = assets.Videos.GetByName(name);
                    if (video != null)
                    {
                        path = CanonicalPath($"Data/Movies/{video.Filename}.bik");
                    }
                    resolved = path != null && fileSystem.GetFile(path) != null;
                    if (resolved == false)
                    {
                        manifest.Failures.Add(new InventoryFailure(source, $"Movie:{name}",
                            path == null ? "Video definition missing." : $"Video file missing: {path}"));
                    }
                }
                manifest.Media.Add(new MediaReference(source, kind, name, path, resolved));
            }
        }
        finally
        {
            assets.PopScope();
        }
    }

    private static string CanonicalPath(string path) => FileSystem.NormalizeFilePath(path).Replace('\\', '/');

    private static IEnumerable<(Script Script, string Location)> EnumerateScripts(ScriptList list)
    {
        foreach (var group in list.ScriptGroups)
        {
            foreach (var script in EnumerateGroup(group, group.Name))
            {
                yield return script;
            }
        }
        foreach (var script in list.Scripts)
        {
            yield return (script, script.Name);
        }
    }

    private static IEnumerable<(Script Script, string Location)> EnumerateGroup(ScriptGroup group, string path)
    {
        foreach (var child in group.Groups)
        {
            foreach (var script in EnumerateGroup(child, $"{path}/{child.Name}"))
            {
                yield return script;
            }
        }
        foreach (var script in group.Scripts)
        {
            yield return (script, $"{path}/{script.Name}");
        }
    }

    private static T ResolveType<T>(T type, string internalName) where T : struct, Enum
    {
        // Match runtime dispatch, which gives a recognized internal name precedence.
        if (internalName != null && IniParser.GetEnumMap<T>().TryGetValue(internalName, out var resolved))
        {
            return (T)resolved;
        }
        return type;
    }

    internal static Dictionary<ScriptActionType, MethodInfo> GetActionHandlers(SageGame game) => typeof(ScriptActions)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(x => x.GetCustomAttribute<ScriptActionAttribute>() != null)
        .Where(x => x.GetCustomAttribute<ScriptActionAttribute>().Games.Length == 0 ||
            x.GetCustomAttribute<ScriptActionAttribute>().Games.Contains(game))
        .GroupBy(x => x.GetCustomAttribute<ScriptActionAttribute>().ActionType)
        .ToDictionary(x => x.Key, x => x.OrderByDescending(method =>
            method.GetCustomAttribute<ScriptActionAttribute>().Games.Length > 0).First());

    internal static Dictionary<ScriptConditionType, MethodInfo> GetConditionHandlers(SageGame game) => typeof(ScriptConditions)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Where(x => x.GetCustomAttribute<ScriptConditionAttribute>() != null)
        .Where(x => x.GetCustomAttribute<ScriptConditionAttribute>().Games.Length == 0 ||
            x.GetCustomAttribute<ScriptConditionAttribute>().Games.Contains(game))
        .GroupBy(x => x.GetCustomAttribute<ScriptConditionAttribute>().ConditionType)
        .ToDictionary(x => x.Key, x => x.OrderByDescending(method =>
            method.GetCustomAttribute<ScriptConditionAttribute>().Games.Length > 0).First());

    internal static string ClassifyHandler(MethodInfo handler)
    {
        var body = handler.GetMethodBody()?.GetILAsByteArray();
        // A void method containing only nop/ret is silently ignored, even when registered.
        if (handler.ReturnType == typeof(void) && body != null && body.All(x => x == 0x00 || x == 0x2a))
        {
            return "EmptyHandler";
        }
        return "RegisteredUnverified";
    }
}
