using SharpCompress.Common;
using SharpCompress.Writers.SevenZip;
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
        ArgumentNullException.ThrowIfNull(options);

        var sourceWasInUse = ValidateAndCheckSource(options);
        var destination = NormalizeArchivePath(Path.GetFullPath(options.DestinationArchivePath), options.ArchiveFormat);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
            throw new IOException($"O backup já existe: {destination}");

        var partial = destination + ".partial";
        TryDelete(partial);
        var manifestEntries = new List<ProfileBackupManifestEntry>();
        long totalBytes = 0;
        long files = 0;

        try
        {
            var sourceFiles = BuildSourceItems(options, cancellationToken);
            if (sourceFiles.Count == 0)
                throw new InvalidOperationException("Nenhum arquivo elegível foi encontrado para o backup selecionado.");

            if (options.ArchiveFormat == ProfileBackupArchiveFormat.SevenZip)
            {
                CreateSevenZipBackup(
                    partial,
                    options,
                    sourceFiles,
                    sourceWasInUse,
                    manifestEntries,
                    progress,
                    cancellationToken,
                    ref files,
                    ref totalBytes);
            }
            else
            {
                CreateZipBackup(
                    partial,
                    options,
                    sourceFiles,
                    sourceWasInUse,
                    manifestEntries,
                    progress,
                    cancellationToken,
                    ref files,
                    ref totalBytes);
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
                Sha256 = backupHash,
                ArchiveFormat = options.ArchiveFormat
            };
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    private static bool ValidateAndCheckSource(ProfileBackupOptions options)
    {
        bool sourceWasInUse;
        if (options.Scope == ProfileBackupScope.ThunderbirdDataRoot)
        {
            var dataRoot = options.DataRoot ?? throw new InvalidOperationException("Selecione o diretório de dados do Thunderbird.");
            if (!Directory.Exists(dataRoot.Path))
                throw new DirectoryNotFoundException($"Diretório de dados não encontrado: {dataRoot.Path}");
            sourceWasInUse = ThunderbirdProfileService.IsThunderbirdRunning() ||
                             ThunderbirdProfileService.FindProfiles(dataRoot).Any(profile => profile.IsInUse);
        }
        else
        {
            ThunderbirdProfileService.ValidateProfile(options.Profile.Path);
            sourceWasInUse = ThunderbirdProfileService.IsProfileInUse(options.Profile.Path);
        }

        if (sourceWasInUse && !options.AllowInUseProfile)
            throw new IOException("O Thunderbird ou o perfil selecionado está em uso. Feche completamente o Thunderbird ou habilite explicitamente o backup com dados em uso.");
        return sourceWasInUse;
    }

    private static IReadOnlyList<BackupSourceItem> BuildSourceItems(
        ProfileBackupOptions options,
        CancellationToken cancellationToken)
    {
        var result = new List<BackupSourceItem>();
        if (options.Scope == ProfileBackupScope.SelectedProfile)
        {
            foreach (var sourceFile in Directory.EnumerateFiles(options.Profile.Path, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ProfileFileClassifier.ShouldInclude(options.Profile.Path, sourceFile, options.Mode, options.Selection))
                    continue;
                var relative = ProfileFileClassifier.NormalizeRelative(options.Profile.Path, sourceFile);
                result.Add(new BackupSourceItem(sourceFile, "profile/" + relative, relative));
            }
            return result;
        }

        var dataRoot = options.DataRoot ?? throw new InvalidOperationException("Diretório de dados do Thunderbird não informado.");
        foreach (var sourceFile in Directory.EnumerateFiles(dataRoot.Path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ShouldIncludeDataRootFile(dataRoot.Path, sourceFile, options.Selection)) continue;
            var relative = Normalize(Path.GetRelativePath(dataRoot.Path, sourceFile));
            result.Add(new BackupSourceItem(sourceFile, "thunderbird-root/" + relative, "thunderbird-root/" + relative));
        }

        var localCachePath = dataRoot.LocalCachePath;
        if (options.IncludeLocalCache && !string.IsNullOrWhiteSpace(localCachePath) && Directory.Exists(localCachePath))
        {
            foreach (var sourceFile in Directory.EnumerateFiles(localCachePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsLockFile(sourceFile)) continue;
                var relative = Normalize(Path.GetRelativePath(localCachePath, sourceFile));
                result.Add(new BackupSourceItem(sourceFile, "local-cache/" + relative, "local-cache/" + relative));
            }
        }

        return result;
    }

    private static bool ShouldIncludeDataRootFile(
        string dataRootPath,
        string sourceFile,
        ProfileBackupSelection selection)
    {
        if (IsLockFile(sourceFile)) return false;
        var relative = Normalize(Path.GetRelativePath(dataRootPath, sourceFile));
        if (relative.EndsWith(".partial", StringComparison.OrdinalIgnoreCase) ||
            relative.EndsWith(".restore-partial", StringComparison.OrdinalIgnoreCase))
            return false;

        var profilesPrefix = "Profiles/";
        if (!relative.StartsWith(profilesPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        var afterProfiles = relative[profilesPrefix.Length..];
        var separator = afterProfiles.IndexOf('/');
        if (separator <= 0) return true;
        var profileRelative = afterProfiles[(separator + 1)..];
        return ProfileFileClassifier.ShouldIncludeRelative(profileRelative, ProfileBackupMode.Complete, selection);
    }

    private static void CreateZipBackup(
        string partial,
        ProfileBackupOptions options,
        IReadOnlyList<BackupSourceItem> sourceFiles,
        bool sourceWasInUse,
        List<ProfileBackupManifestEntry> manifestEntries,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken,
        ref long files,
        ref long totalBytes)
    {
        using var file = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
        using var zip = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);

        foreach (var item in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(item.SourcePath);
            var entry = zip.CreateEntry(item.ArchivePath, CompressionLevel.Optimal);
            entry.LastWriteTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
            string? hash;
            using (var input = OpenProfileFile(item.SourcePath))
            using (var output = entry.Open())
            using (var sha = options.CalculateFileHashes ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null)
            {
                hash = CopyWithHash(input, output, sha, cancellationToken);
            }

            AddManifestEntry(manifestEntries, item.ManifestPath, info, hash);
            files++;
            totalBytes = checked(totalBytes + info.Length);
            progress?.Report((files, totalBytes, item.ManifestPath));
        }

        var manifest = CreateManifest(options, sourceWasInUse, manifestEntries);
        var manifestEntry = zip.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        using var manifestStream = manifestEntry.Open();
        JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
    }

    private static void CreateSevenZipBackup(
        string partial,
        ProfileBackupOptions options,
        IReadOnlyList<BackupSourceItem> sourceFiles,
        bool sourceWasInUse,
        List<ProfileBackupManifestEntry> manifestEntries,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken,
        ref long files,
        ref long totalBytes)
    {
        using var file = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
        using var writer = new SevenZipWriter(file, new SevenZipWriterOptions(CompressionType.LZMA2) { LeaveStreamOpen = true });

        foreach (var item in sourceFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(item.SourcePath);
            var hash = options.CalculateFileHashes ? CalculateSha256(item.SourcePath, cancellationToken) : null;
            using var input = OpenProfileFile(item.SourcePath);
            writer.Write(item.ArchivePath, input, info.LastWriteTimeUtc);

            AddManifestEntry(manifestEntries, item.ManifestPath, info, hash);
            files++;
            totalBytes = checked(totalBytes + info.Length);
            progress?.Report((files, totalBytes, item.ManifestPath));
        }

        var manifest = CreateManifest(options, sourceWasInUse, manifestEntries);
        using var manifestStream = new MemoryStream();
        JsonSerializer.Serialize(manifestStream, manifest, JsonOptions);
        manifestStream.Position = 0;
        writer.Write(ManifestEntryName, manifestStream, DateTime.UtcNow);
    }

    private static ProfileBackupManifest CreateManifest(
        ProfileBackupOptions options,
        bool sourceWasInUse,
        IReadOnlyList<ProfileBackupManifestEntry> manifestEntries)
    {
        var dataRoot = options.DataRoot ?? ThunderbirdProfileService.FindDataRootForProfile(options.Profile);
        var profileRelativePaths = dataRoot is null
            ? Array.Empty<string>()
            : ThunderbirdProfileService.FindProfiles(dataRoot)
                .Select(profile => Normalize(Path.GetRelativePath(dataRoot.Path, profile.Path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

        return new ProfileBackupManifest
        {
            ProfileName = options.Scope == ProfileBackupScope.ThunderbirdDataRoot
                ? "Thunderbird completo"
                : options.Profile.Name,
            OriginalProfilePath = options.Profile.Path,
            Mode = options.Mode,
            Scope = options.Scope,
            ArchiveFormat = options.ArchiveFormat,
            Files = manifestEntries,
            SourceWasInUse = sourceWasInUse,
            OriginalDataRootPath = dataRoot?.Path,
            DataRootType = dataRoot?.Type,
            OriginalLocalCachePath = dataRoot?.LocalCachePath,
            IncludedLocalCache = options.Scope == ProfileBackupScope.ThunderbirdDataRoot && options.IncludeLocalCache,
            ProfileRelativePaths = profileRelativePaths
        };
    }

    private static void AddManifestEntry(
        ICollection<ProfileBackupManifestEntry> entries,
        string relative,
        FileInfo info,
        string? hash)
    {
        entries.Add(new ProfileBackupManifestEntry
        {
            RelativePath = relative,
            SizeBytes = info.Length,
            Sha256 = hash,
            LastWriteTimeUtc = info.LastWriteTimeUtc
        });
    }

    internal static string CreateDirectorySnapshot(
        string sourceDirectory,
        string destinationPath,
        ProfileBackupArchiveFormat format,
        CancellationToken cancellationToken,
        string archiveRootPrefix = "profile")
    {
        var normalizedPrefix = string.IsNullOrWhiteSpace(archiveRootPrefix)
            ? "profile"
            : archiveRootPrefix.Trim().Trim('/', '\\');
        var destination = NormalizeArchivePath(destinationPath, format);
        var partial = destination + ".partial";
        TryDelete(partial);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        try
        {
            var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !IsLockFile(path))
                .ToList();

            if (format == ProfileBackupArchiveFormat.SevenZip)
            {
                using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
                using var writer = new SevenZipWriter(output, new SevenZipWriterOptions(CompressionType.LZMA2) { LeaveStreamOpen = true });
                foreach (var source in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = ProfileFileClassifier.NormalizeRelative(sourceDirectory, source);
                    var info = new FileInfo(source);
                    using var input = OpenProfileFile(source);
                    writer.Write(normalizedPrefix + "/" + relative, input, info.LastWriteTimeUtc);
                }
            }
            else
            {
                using var output = new FileStream(partial, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, FileOptions.SequentialScan);
                using var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: false, Encoding.UTF8);
                foreach (var source in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relative = ProfileFileClassifier.NormalizeRelative(sourceDirectory, source);
                    var entry = zip.CreateEntry(normalizedPrefix + "/" + relative, CompressionLevel.Optimal);
                    using var input = OpenProfileFile(source);
                    using var entryStream = entry.Open();
                    input.CopyTo(entryStream);
                }
            }

            File.Move(partial, destination);
            return destination;
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    internal static string ArchiveExtension(ProfileBackupArchiveFormat format) =>
        format == ProfileBackupArchiveFormat.SevenZip ? ".7z" : ".zip";

    internal static string NormalizeArchivePath(string path, ProfileBackupArchiveFormat format)
    {
        var expected = ArchiveExtension(format);
        return path.EndsWith(expected, StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, expected);
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

    private static FileStream OpenProfileFile(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite | FileShare.Delete,
        1024 * 1024,
        FileOptions.SequentialScan);

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

    private static bool IsLockFile(string path)
    {
        var name = Path.GetFileName(path) ?? string.Empty;
        return name.Equals("parent.lock", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("lock", StringComparison.OrdinalIgnoreCase) ||
               name.Equals(".parentlock", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private sealed record BackupSourceItem(string SourcePath, string ArchivePath, string ManifestPath);
}
