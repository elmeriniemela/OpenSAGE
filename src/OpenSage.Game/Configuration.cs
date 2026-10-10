using System;
using System.IO;

namespace OpenSage;

// TODO: Should this be immutable?
// TODO: Should there be a way of merging Configuration instances?
/// <summary>
/// Contains configuration for a game instance, typically gathered from
/// command line parameters and configuration files.
/// </summary>
public sealed class Configuration
{
    /// <summary>Use right-click for contextual commands, as in Generals' alternate mouse mode.</summary>
    public bool UseAlternateMouse { get; set; }

    internal void LoadMouseOptions(string installationPath, string userDataFolder)
    {
        // Prefer personal settings; copied installations can also carry Options.ini.
        foreach (var directory in new[] { userDataFolder, installationPath, Directory.GetParent(Path.GetFullPath(installationPath))?.FullName })
        {
            if (directory == null)
            {
                continue;
            }

            foreach (var filename in new[] { "Options.ini", "options.ini" })
            {
                var path = Path.Combine(directory, filename);
                if (File.Exists(path))
                {
                    using var reader = File.OpenText(path);
                    if (LoadMouseOptions(reader))
                    {
                        return;
                    }
                }
            }
        }
    }

    internal bool LoadMouseOptions(TextReader reader)
    {
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2 || !parts[0].Trim().Equals("UseAlternateMouse", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = parts[1].Trim();
            if (value.Equals("yes", StringComparison.OrdinalIgnoreCase) || value.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                UseAlternateMouse = true;
                return true;
            }
            if (value.Equals("no", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                UseAlternateMouse = false;
                return true;
            }
        }

        return false;
    }

    public bool LoadShellMap { get; set; } = true;
    public bool UseRenderDoc { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to use a unique port for each client in a multiplayer game.
    /// Normally, <see cref="Network.Ports.SkirmishGame"/> is used, but when we want to run multiple game
    /// instances on the same machine (for debugging purposes), each client needs a different port.
    /// </summary>
    /// <value>
    ///   <c>true</c> if [use unique ports]; otherwise, <c>false</c>.
    /// </value>
    public bool UseUniquePorts { get; set; } = false;
}
