using System.Text.Json;
using OpenSage;
using OpenSage.Data;
using OpenSage.Diagnostics;
using OpenSage.Mods.BuiltIn;

if (args.Length != 3)
{
    Console.Error.WriteLine("Usage: ContentInventory <generals-directory> <zero-hour-directory> <output.json>");
    return 2;
}

foreach (var path in args.Take(2))
{
    if (!Directory.Exists(path))
    {
        Console.Error.WriteLine($"Asset validation skipped: installation directory missing: {path}");
        return 2;
    }
}

try
{
    var generals = new GameInstallation(GameDefinition.FromGame(SageGame.CncGenerals), args[0]);
    var zeroHour = new GameInstallation(GameDefinition.FromGame(SageGame.CncGeneralsZeroHour), args[1], generals);
    var manifests = new[] { ContentInventory.Create(generals), ContentInventory.Create(zeroHour) };
    var json = JsonSerializer.Serialize(manifests, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(args[2], json);
    foreach (var manifest in manifests)
    {
        Console.WriteLine($"{manifest.Game}: {manifest.Campaigns.Count} campaigns, {manifest.Maps.Count} maps, " +
            $"{manifest.Failures.Count} inventory failures. Full-content gate: pending.");
        var usages = manifest.Maps.SelectMany(x => x.ScriptFeatures).ToArray();
        Console.WriteLine($"Script usages: {usages.Count(x => x.Status == "MissingHandler")} missing, " +
            $"{usages.Count(x => x.Status == "EmptyHandler")} empty, " +
            $"{usages.Count(x => x.Status == "RegisteredUnverified")} registered but unverified.");
    }
    // Success means the inventory ran; it never certifies full gameplay support.
    return manifests.Any(x => x.Failures.Count > 0) ? 1 : 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Inventory failed: {exception.Message}");
    return 1;
}
