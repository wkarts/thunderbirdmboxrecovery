using SharpCompress.Archives;
using SharpCompress.Readers;

namespace ThunderbirdMboxRecovery.Core;

public static class ArchiveService
{
    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".msf", ".sqlite", ".json", ".ini", ".html", ".htm", ".xml", ".log", ".txt",
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".exe", ".dll"
    };

    private static readonly HashSet<string> PreferredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Inbox", "Sent", "Drafts", "Templates", "Trash", "Archives", "Junk", "Unsent Messages"
    };

    public static bool IsArchive(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".7z", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".tar", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".gz", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bz2", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xz", StringComparison.OrdinalIgnoreCase);
    }

    public static Task<IReadOnlyList<ArchiveEntryInfo>> InspectAsync(
        string archivePath,
        string? password,
        CancellationToken cancellationToken)
    {
        return Task.Run<IReadOnlyList<ArchiveEntryInfo>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new ReaderOptions { Password = NullIfEmpty(password) };
            using var archive = ArchiveFactory.OpenArchive(archivePath, options);

            var entries = archive.Entries
                .Where(entry => !entry.IsDirectory)
                .Select(entry => new ArchiveEntryInfo(entry.Key ?? string.Empty, Convert.ToInt64(entry.Size)))
                .Where(entry => IsLikelyMbox(entry.Key, entry.Size))
                .OrderByDescending(entry => IsPreferredName(entry.Key))
                .ThenByDescending(entry => entry.Size)
                .ToList();

            cancellationToken.ThrowIfCancellationRequested();
            return entries;
        }, cancellationToken);
    }

    public static MboxReadHandle OpenSelectedEntry(string archivePath, string selectedKey, string? password)
    {
        var options = new ReaderOptions { Password = NullIfEmpty(password) };
        var reader = ReaderFactory.OpenReader(archivePath, options);
        try
        {
            while (reader.MoveToNextEntry())
            {
                if (reader.Entry.IsDirectory) continue;
                if (!string.Equals(reader.Entry.Key, selectedKey, StringComparison.Ordinal)) continue;

                var size = Convert.ToInt64(reader.Entry.Size);
                var entryStream = reader.OpenEntryStream();
                return new MboxReadHandle(entryStream, size, reader, $"{archivePath} :: {selectedKey}");
            }

            throw new InvalidOperationException($"A entrada '{selectedKey}' não foi encontrada no arquivo compactado.");
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private static bool IsLikelyMbox(string key, long size)
    {
        if (size <= 0) return false;
        var fileName = Path.GetFileName(key.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        var extension = Path.GetExtension(fileName);
        if (ExcludedExtensions.Contains(extension)) return false;
        return string.IsNullOrEmpty(extension) || PreferredNames.Contains(fileName) || size >= 16L * 1024 * 1024;
    }

    private static bool IsPreferredName(string key)
    {
        var fileName = Path.GetFileName(key.Replace('/', Path.DirectorySeparatorChar));
        return PreferredNames.Contains(fileName);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class MboxReadHandle : IDisposable
{
    private readonly IDisposable? _owner;
    private bool _disposed;

    public Stream Stream { get; }
    public long Length { get; }
    public string DisplayName { get; }

    public MboxReadHandle(Stream stream, long length, IDisposable? owner, string displayName)
    {
        Stream = stream;
        Length = length;
        _owner = owner;
        DisplayName = displayName;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stream.Dispose();
        _owner?.Dispose();
    }
}
