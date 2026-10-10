using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OpenSage.Diagnostics;
using OpenSage.IO;
using Xunit;

namespace OpenSage.Tests.Diagnostics;

public sealed class ContentInventoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "opensage-inventory-" + Guid.NewGuid());

    public ContentInventoryTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Data", "INI"));
        Directory.CreateDirectory(Path.Combine(_root, "Maps"));
        Write("Data/INI/PlayerTemplate.ini", "PlayerTemplate FactionUSA\n PlayableSide = Yes\nEnd\n");
        Write("Data/INI/Video.ini", "");
        Write("Maps/MapCache.ini", "");
    }

    [Fact]
    public void FollowsConfiguredMissionLinksAndExcludesUnreachableDefinitions()
    {
        Write("Data/INI/Campaign.ini", """
            Campaign USA
              FirstMission Mission01
              Mission Mission01
                Map Maps/USA01/USA01.map
                IntroMovie MissingVideo
                NextMission Mission04
              End
              Mission Mission04
                Map Maps/USA08/USA08.map
              End
              Mission Unused
                Map Maps/Unused/Unused.map
              End
            End
            Campaign DEMO
              FirstMission Mission01
              Mission Mission01
                Map Maps/Demo/Demo.map
              End
            End
            """);
        using var fileSystem = new DiskFileSystem(_root);
        var manifest = ContentInventory.Create(fileSystem, SageGame.CncGenerals);

        var campaign = Assert.Single(manifest.Campaigns);
        Assert.Equal(new[] { "Mission01", "Mission04" }, campaign.Missions.Select(x => x.Name));
        Assert.Equal(2, manifest.Maps.Count);
        Assert.Contains("DEMO", manifest.ExcludedCampaigns);
        Assert.Contains("FactionUSA", manifest.PlayableFactions);
        Assert.DoesNotContain(manifest.Maps, x => x.Path.Contains("Unused") || x.Path.Contains("Demo"));
        Assert.Contains(manifest.Failures, x => x.Feature == "Map" && x.Source == "Maps/USA08/USA08.map");
        Assert.Contains(manifest.Failures, x => x.Feature == "Movie:MissingVideo" && x.Source == "USA/Mission01");
        Assert.False(manifest.CompletionGatePassed);
        Assert.Contains("\"FormatVersion\":1", JsonSerializer.Serialize(manifest));
    }

    [Theory]
    [InlineData("Mission01", "cycle")]
    [InlineData("Missing", "missing")]
    public void BrokenCampaignRoutesProduceStructuredFailures(string nextMission, string expected)
    {
        Write("Data/INI/Campaign.ini", $"""
            Campaign USA
              FirstMission Mission01
              Mission Mission01
                Map Maps/USA01/USA01.map
                NextMission {nextMission}
              End
            End
            """);
        using var fileSystem = new DiskFileSystem(_root);
        var manifest = ContentInventory.Create(fileSystem, SageGame.CncGenerals);
        Assert.Contains(manifest.Failures, x => x.Source == "USA" && x.Message.Contains(expected));
    }

    [Fact]
    public void OnlyEnabledChallengePersonasAddRequiredRoutes()
    {
        Write("Data/INI/Campaign.ini", """
            Campaign CUSTOM_ROUTE
              IsChallengeCampaign Yes
              FirstMission Entry
              Mission Entry
                Map Maps/Challenge.map
              End
            End
            Campaign DISABLED_ROUTE
              IsChallengeCampaign Yes
              FirstMission Entry
              Mission Entry
                Map Maps/Disabled.map
              End
            End
            """);
        Write("Data/INI/ChallengeMode.ini", """
            ChallengeGenerals
              GeneralPersona0
                PlayerTemplate FactionUSA
                StartsEnabled Yes
                Campaign CUSTOM_ROUTE
              End
              GeneralPersona1
                PlayerTemplate FactionUSA
                StartsEnabled No
                Campaign DISABLED_ROUTE
              End
            End
            """);
        using var fileSystem = new DiskFileSystem(_root);
        var manifest = ContentInventory.Create(fileSystem, SageGame.CncGeneralsZeroHour);

        Assert.Equal(2, manifest.ChallengePersonas.Count);
        Assert.Single(manifest.ChallengePersonas, x => x.Selectable);
        Assert.Equal("CUSTOM_ROUTE", Assert.Single(manifest.Campaigns).Name);
        Assert.Contains("DISABLED_ROUTE", manifest.ExcludedCampaigns);
        Assert.Equal("Maps/Challenge.map", Assert.Single(manifest.Maps).Path);
    }

    [Fact]
    public void ParsesMapScriptsUsingTheEngineParserWithoutDevices()
    {
        Write("Data/INI/Campaign.ini", """
            Campaign TRAINING
              FirstMission Intro
              Mission Intro
                Map Maps/Scripts.map
              End
            End
            """);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Data", "Map", "Assets", "SidesList_Scripts.map"),
            Path.Combine(_root, "Maps", "Scripts.map"));
        using var fileSystem = new DiskFileSystem(_root);
        var manifest = ContentInventory.Create(fileSystem, SageGame.CncGenerals);

        var map = Assert.Single(manifest.Maps);
        Assert.True(map.Parsed, string.Join("; ", manifest.Failures.Select(x => x.Message)));
        Assert.NotEmpty(map.ScriptFeatures);
        Assert.All(map.ScriptFeatures, x => Assert.False(string.IsNullOrEmpty(x.Script)));
        Assert.DoesNotContain(map.ScriptFeatures, x => x.Status == "Supported");
    }

    [Fact]
    public void EmptyRegisteredHandlersDoNotCountAsSupport()
    {
        var empty = typeof(ContentInventoryTests).GetMethod(nameof(EmptyHandler), BindingFlags.NonPublic | BindingFlags.Static);
        var throws = typeof(ContentInventoryTests).GetMethod(nameof(ThrowingHandler), BindingFlags.NonPublic | BindingFlags.Static);
        Assert.Equal("EmptyHandler", ContentInventory.ClassifyHandler(empty));
        Assert.Equal("RegisteredUnverified", ContentInventory.ClassifyHandler(throws));
    }

    private static void EmptyHandler() { }
    private static void ThrowingHandler() => throw new NotImplementedException();

    private void Write(string path, string text) => File.WriteAllText(Path.Combine(_root, path), text);

    public void Dispose() => Directory.Delete(_root, true);
}
