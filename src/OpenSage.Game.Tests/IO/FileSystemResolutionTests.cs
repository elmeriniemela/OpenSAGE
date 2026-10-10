using System;
using System.IO;
using System.Linq;
using System.Text;
using OpenSage.Data;
using OpenSage.IO;
using OpenSage.Mods.BuiltIn;
using Xunit;

namespace OpenSage.Tests.IO;

public sealed class FileSystemResolutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "opensage-filesystem-" + Guid.NewGuid());

    [Fact]
    public void LooseAssetLookupAndEnumerationIgnoreDirectoryAndFileCase()
    {
        var directory = Path.Combine(_root, "Data", "INI");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Campaign.INI"), "loose");
        using var fileSystem = new DiskFileSystem(_root);

        Assert.Equal("loose", Read(fileSystem.GetFile(@"data\ini\CAMPAIGN.ini")));
        Assert.Single(fileSystem.GetFilesInDirectory("DATA/ini", "*.ini"));
        Assert.Null(fileSystem.GetFile("data/ini/missing.ini"));
        Assert.Empty(fileSystem.GetFilesInDirectory("missing", "*.ini"));
    }

    [Fact]
    public void InstallationResolvesLooseThenPatchThenExpansionThenBaseAndEnumeratesEachAssetOnce()
    {
        var basePath = Path.Combine(_root, "Generals");
        var expansionPath = Path.Combine(_root, "ZeroHour");
        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(expansionPath);
        WriteBig(Path.Combine(basePath, "INI.BIG"), @"Data\INI\Campaign.ini", "base");
        WriteBig(Path.Combine(basePath, "Core.BIG"), @"Data\INI\base-archive.ini", "base archive");
        WriteBig(Path.Combine(expansionPath, "INIZH.big"), @"DATA\ini\campaign.INI", "expansion");
        WriteBig(Path.Combine(expansionPath, "Patch.big"), "data/INI/CAMPAIGN.ini", "patch");
        var baseLoosePath = Path.Combine(basePath, "Data", "INI");
        Directory.CreateDirectory(baseLoosePath);
        File.WriteAllText(Path.Combine(baseLoosePath, "base-only.ini"), "base loose");

        var baseGame = new GameInstallation(GameDefinition.FromGame(SageGame.CncGenerals), basePath);
        var expansion = new GameInstallation(GameDefinition.FromGame(SageGame.CncGeneralsZeroHour), expansionPath, baseGame);
        using (var fileSystem = expansion.CreateFileSystem())
        {
            Assert.Equal("patch", Read(fileSystem.GetFile("data/ini/campaign.ini")));
            Assert.Equal("base loose", Read(fileSystem.GetFile("DATA/ini/BASE-ONLY.ini")));
            Assert.Equal("base archive", Read(fileSystem.GetFile("data/ini/base-archive.ini")));
            Assert.Equal(3, fileSystem.GetFilesInDirectory("data/ini", "*.ini").Count());
        }

        var loosePath = Path.Combine(expansionPath, "Data", "INI");
        Directory.CreateDirectory(loosePath);
        File.WriteAllText(Path.Combine(loosePath, "Campaign.ini"), "loose");
        using (var fileSystem = expansion.CreateFileSystem())
        {
            Assert.Equal("loose", Read(fileSystem.GetFile(@"DATA\ini\CAMPAIGN.ini")));
            var files = fileSystem.GetFilesInDirectory("Data/INI", "*.ini").ToArray();
            Assert.Equal(3, files.Length);
            foreach (var file in files)
            {
                Assert.Equal(Read(file), Read(fileSystem.GetFile(file.FilePath)));
            }
        }

        File.Delete(Path.Combine(loosePath, "Campaign.ini"));
        File.Delete(Path.Combine(expansionPath, "Patch.big"));
        using (var fileSystem = expansion.CreateFileSystem())
        {
            Assert.Equal("expansion", Read(fileSystem.GetFile("data/ini/campaign.ini")));
        }
        File.Delete(Path.Combine(expansionPath, "INIZH.big"));
        using (var fileSystem = expansion.CreateFileSystem())
        {
            Assert.Equal("base", Read(fileSystem.GetFile("data/ini/campaign.ini")));
        }
    }

    private static string Read(FileSystemEntry entry)
    {
        Assert.NotNull(entry);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Tiny synthetic BIGF archives keep these tests independent of proprietary assets.
    private static void WriteBig(string path, string name, string content)
    {
        var nameBytes = Encoding.ASCII.GetBytes(name);
        var contentBytes = Encoding.ASCII.GetBytes(content);
        var offset = 16 + 8 + nameBytes.Length + 1;
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(Encoding.ASCII.GetBytes("BIGF"));
        WriteUInt32((uint)(offset + contentBytes.Length));
        WriteUInt32(1);
        WriteUInt32((uint)offset);
        WriteUInt32((uint)offset);
        WriteUInt32((uint)contentBytes.Length);
        writer.Write(nameBytes);
        writer.Write((byte)0);
        writer.Write(contentBytes);

        void WriteUInt32(uint value)
        {
            writer.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }
}
