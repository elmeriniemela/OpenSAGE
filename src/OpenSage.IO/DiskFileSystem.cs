namespace OpenSage.IO;

public sealed class DiskFileSystem : FileSystem
{
    public readonly string RootDirectory;

    public DiskFileSystem(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public override IEnumerable<FileSystemEntry> GetFilesInDirectory(
        string directoryPath,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var childDirectory = ResolvePath(directoryPath);

        var childDirectoryInfo = new DirectoryInfo(childDirectory);
        if (!childDirectoryInfo.Exists)
        {
            yield break;
        }

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
            MatchCasing = MatchCasing.CaseInsensitive,
        };

        foreach (var fileInfo in childDirectoryInfo.EnumerateFiles(searchPattern, enumerationOptions))
        {
            yield return CreateFileSystemEntry(fileInfo);
        }
    }

    public override FileSystemEntry? GetFile(string filePath)
    {
        var fullFilePath = ResolvePath(filePath);
        var fileInfo = new FileInfo(fullFilePath);
        if (!fileInfo.Exists)
        {
            return null;
        }

        return CreateFileSystemEntry(fileInfo);
    }

    private FileSystemEntry CreateFileSystemEntry(FileInfo fileInfo)
    {
        var fullPath = fileInfo.FullName;

        var relativePath = Path.GetRelativePath(
            RootDirectory,
            fullPath);

        return new FileSystemEntry(
            this,
            relativePath,
            (uint)fileInfo.Length,
            () => File.OpenRead(fullPath),
            fullPath);
    }

    // SAGE asset paths are case-insensitive, including directory names on Linux.
    // Keep the common exact-case lookup fast and resolve components only on a miss.
    private string ResolvePath(string path)
    {
        var fullPath = Path.GetFullPath(Path.Combine(RootDirectory, NormalizeFilePath(path)));
        if (File.Exists(fullPath) || Directory.Exists(fullPath))
        {
            return fullPath;
        }

        var root = Path.GetPathRoot(fullPath)!;
        var current = root;
        foreach (var component in fullPath[root.Length..].Split(Path.DirectorySeparatorChar))
        {
            if (!Directory.Exists(current))
            {
                return fullPath;
            }

            var match = Directory.EnumerateFileSystemEntries(current)
                .Where(entry => string.Equals(Path.GetFileName(entry), component, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match == null)
            {
                return fullPath;
            }

            current = match;
        }

        return current;
    }

    public string GetFullPath(FileSystemEntry entry)
    {
        return Path.Combine(RootDirectory, entry.FilePath);
    }
}
