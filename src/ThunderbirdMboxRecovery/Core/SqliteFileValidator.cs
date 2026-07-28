namespace ThunderbirdMboxRecovery.Core;

public static class SqliteFileValidator
{
    private static readonly byte[] Header = "SQLite format 3\0"u8.ToArray();

    public static bool IsValid(string path, long minimumBytes = 4096)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < minimumBytes) return false;
            Span<byte> buffer = stackalloc byte[16];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Read(buffer) == buffer.Length && buffer.SequenceEqual(Header);
        }
        catch
        {
            return false;
        }
    }
}
