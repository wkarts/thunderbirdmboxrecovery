using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ThunderbirdMboxRecovery.Core;

public static class ProfileBackupService
{
    public const string ManifestEntryName = "ThunderbirdRecoverySuite/manifesto_backup.json";

    public static Task<ProfileBackupResult> CreateAsync(
        ProfileBackupOptions options,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Create(options, progress, cancellationToken), cancellationToken);
    }

    private static ProfileBackupResult Create(
        ProfileBackupOptions options,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        ThunderbirdProfileService.ValidateProfile(options.Profile.Path);
        var sourceWasInUse = ThunderbirdProfileService.IsProfileInUse(options.Profile.Path);
        if (sourceWasInUse && !options.AllowInUseProfile)
            throw new IOException("O perfil está em uso. Feche completamente o Thunderbird ou habilite explicitamente o backup com perfil aberto.");

        var destination = EnsureZipExtension(Path.GetFullPath(options.DestinationZipPath));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
            throw new IOException($"O backup já existe: {destination}");

        var partial = destination + ".partial";
        if (File.Exists(partial)) File.Delete(partial);
        var manifestEntries = new List<ProfileBackupManifestEntry>();
        long totalBytes = 0;
        long files = 0;

        try
        {
            using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8))
            {
                foreach (var sourceFile in Directory.EnumerateFiles(options.Profile.Path, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!ProfileFileClassifier.ShouldInclude(options.Profile.Path, sourceFile, options.Mode, options.Selection))
                        continue;

                    var relative = ProfileFileClassifier.NormalizeRelative(options.Profile.Path, sourceFile);
                    var info = new FileInfo(sourceFile);
                    var entry = zip.CreateEntry("profile/" + relative, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                    string? hash;
                    using (var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan))
                    using (var output = entry.Open())
                    using (var sha = options.CalculateFileHashes ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null)
                    {
                        hash = CopyWithHash(input, output, sha, cancellationToken);
                    }

                    manifestEntries.Add(new ProfileBackupManifestEntry
                    {
                        RelativePath = relative,
                        SizeBytes = info.Length,
                        Sha256 = hash,
                        LastWriteTimeUtc = info.LastWriteTimeUtc
                    });
                    files++;
                    totalBytes = checked(totalBytes + info.Length);
                    progress?.Report((files, totalBytes, relative));
                }

                var manifest = new ProfileBackupManifest
                {
                    ProfileName = options.Profile.Name,
                    OriginalProfilePath = options.Profile.Path,
                    Mode = options.Mode,
                    Files = manifestEntries,
                    SourceWasInUse = sourceWasInUse
                };
                var manifestEntry = zip.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
                using var manifestStream = manifestEntry.Open();
                JsonSerializer.Serialize(manifestStream, manifest, new JsonSerializerOptions { WriteIndented = true });
            }

            File.Move(partial, destination);
            var backupHash = CalculateSha256(destination, cancellationToken);
            var sidecar = destination + ".sha256";
            File.WriteAllText(sidecar, $"{backupHash}  {Path.GetFileName(destination)}{Environment.NewLine}", new UTF8Encoding(false));

            return new ProfileBackupResult
            {
                BackupPath = destination,
                ManifestPathInsideArchive = ManifestEntryName,
                Files = files,
                SourceBytes = totalBytes,
                BackupBytes = new FileInfo(destination).Length,
                Sha256 = backupHash
            };
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    internal static string CalculateSha256(string path, CancellationToken cancellationToken)
    {
        using var sha = SHA256.Create();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha.TransformBlock(buffer, 0, read, null, 0);
        }
        sha.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static string? CopyWithHash(Stream input, Stream output, IncrementalHash? hash, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            hash?.AppendData(buffer, 0, read);
        }
        return hash is null ? null : Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string EnsureZipExtension(string path) => path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? path : path + ".zip";
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
