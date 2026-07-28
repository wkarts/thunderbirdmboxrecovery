using SharpCompress.Archives;
using SharpCompress.Readers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ThunderbirdMboxRecovery.Core;

public static class ProfileRestoreService
{
    public static Task<ProfileRestoreResult> RestoreAsync(
        ProfileRestoreOptions options,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Restore(options, progress, cancellationToken), cancellationToken);
    }

    private static ProfileRestoreResult Restore(
        ProfileRestoreOptions options,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.BackupPath))
            throw new FileNotFoundException("O arquivo de backup não foi encontrado.", options.BackupPath);

        var destination = Path.GetFullPath(options.DestinationProfilePath);
        Directory.CreateDirectory(destination);
        if (ThunderbirdProfileService.IsProfileInUse(destination))
            throw new IOException("O perfil de destino parece estar em uso. Feche completamente o Thunderbird antes da restauração.");

        string? safetyBackup = null;
        if (options.CreateSafetyBackup && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            var parent = Path.GetDirectoryName(destination) ?? destination;
            var extension = ProfileBackupService.ArchiveExtension(options.SafetyBackupFormat);
            var path = Path.Combine(parent, $"Backup_Seguranca_{Path.GetFileName(destination)}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
            safetyBackup = ProfileBackupService.CreateDirectorySnapshot(destination, path, options.SafetyBackupFormat, cancellationToken);
        }

        var warnings = new List<string>();
        var readerOptions = new ReaderOptions { Password = string.IsNullOrWhiteSpace(options.ArchivePassword) ? null : options.ArchivePassword };
        var manifest = TryReadManifest(options.BackupPath, readerOptions, warnings);
        var expected = manifest?.Files.ToDictionary(file => Normalize(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ProfileBackupManifestEntry>(StringComparer.OrdinalIgnoreCase);

        long restoredFiles = 0;
        long restoredBytes = 0;
        long skippedFiles = 0;
        long verifiedFiles = 0;

        using var archive = ArchiveFactory.OpenArchive(options.BackupPath, readerOptions);
        var stripPrefix = DetermineArchivePrefix(archive.Entries.Where(item => !item.IsDirectory).Select(item => Normalize(item.Key ?? string.Empty)).ToList());
        foreach (var entry in archive.Entries.Where(item => !item.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Normalize(entry.Key ?? string.Empty);
            if (key.Equals(ProfileBackupService.ManifestEntryName, StringComparison.OrdinalIgnoreCase)) continue;

            var relative = key.StartsWith("profile/", StringComparison.OrdinalIgnoreCase) ? key[8..] : key;
            if (!string.IsNullOrEmpty(stripPrefix) && relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
                relative = relative[stripPrefix.Length..];
            if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)) continue;
            if (!ProfileFileClassifier.ShouldIncludeRelative(relative, options.Mode, options.Selection))
            {
                skippedFiles++;
                continue;
            }

            var target = GetSafeDestination(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target) && !options.OverwriteExistingFiles)
            {
                skippedFiles++;
                warnings.Add($"Mantido arquivo existente: {relative}");
                continue;
            }

            var partial = target + ".restore-partial";
            TryDelete(partial);
            string? restoredHash;
            long bytes;
            using (var source = entry.OpenEntryStream())
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                bytes = CopyWithHash(source, output, hash, cancellationToken, out restoredHash);
                output.Flush(true);
            }

            if (options.VerifyHashes && expected.TryGetValue(relative, out var manifestEntry) && !string.IsNullOrWhiteSpace(manifestEntry.Sha256))
            {
                if (!string.Equals(manifestEntry.Sha256, restoredHash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(partial);
                    throw new InvalidDataException($"Falha de integridade ao restaurar {relative}. SHA-256 diferente do manifesto.");
                }
                verifiedFiles++;
            }

            File.Move(partial, target, overwrite: true);
            if (expected.TryGetValue(relative, out var metadata))
                File.SetLastWriteTimeUtc(target, metadata.LastWriteTimeUtc.UtcDateTime);

            restoredFiles++;
            restoredBytes = checked(restoredBytes + bytes);
            progress?.Report((restoredFiles, restoredBytes, relative));
        }

        var profileRegistered = false;
        string? registeredProfileName = null;
        if (options.RegisterProfile)
        {
            var requestedName = string.IsNullOrWhiteSpace(options.RegisteredProfileName)
                ? manifest?.ProfileName ?? Path.GetFileName(destination) ?? "Perfil restaurado"
                : options.RegisteredProfileName.Trim();
            var registration = ThunderbirdProfileService.RegisterProfile(
                destination,
                requestedName,
                options.MakeRegisteredProfileDefault);
            profileRegistered = registration.Registered || registration.AlreadyRegistered;
            registeredProfileName = registration.ProfileName;
            if (registration.AlreadyRegistered)
                warnings.Add($"O perfil já estava registrado como '{registration.ProfileName}'.");
            else if (registration.BackupPath is not null)
                warnings.Add($"Backup do profiles.ini criado em: {registration.BackupPath}");
        }

        return new ProfileRestoreResult
        {
            DestinationProfilePath = destination,
            SafetyBackupPath = safetyBackup,
            RestoredFiles = restoredFiles,
            RestoredBytes = restoredBytes,
            SkippedFiles = skippedFiles,
            VerifiedFiles = verifiedFiles,
            Warnings = warnings,
            ProfileRegistered = profileRegistered,
            RegisteredProfileName = registeredProfileName
        };
    }

    private static ProfileBackupManifest? TryReadManifest(string archivePath, ReaderOptions readerOptions, List<string> warnings)
    {
        try
        {
            using var archive = ArchiveFactory.OpenArchive(archivePath, readerOptions);
            var entry = archive.Entries.FirstOrDefault(item =>
                !item.IsDirectory && Normalize(item.Key ?? string.Empty).Equals(ProfileBackupService.ManifestEntryName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                warnings.Add("Backup sem manifesto da suíte; a restauração será feita sem validação individual de hashes.");
                return null;
            }

            using var stream = entry.OpenEntryStream();
            return JsonSerializer.Deserialize<ProfileBackupManifest>(stream);
        }
        catch (Exception exception)
        {
            warnings.Add($"Não foi possível ler o manifesto: {exception.Message}");
            return null;
        }
    }

    private static string GetSafeDestination(string root, string relative)
    {
        if (Path.IsPathRooted(relative))
            throw new InvalidDataException($"Entrada absoluta rejeitada: {relative}");

        var candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Entrada com travessia de diretórios rejeitada: {relative}");
        return candidate;
    }

    private static long CopyWithHash(Stream input, Stream output, IncrementalHash hash, CancellationToken cancellationToken, out string sha256)
    {
        var buffer = new byte[1024 * 1024];
        long total = 0;
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            hash.AppendData(buffer, 0, read);
            total = checked(total + read);
        }
        sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return total;
    }

    private static string DetermineArchivePrefix(IReadOnlyList<string> keys)
    {
        var usable = keys
            .Where(key => !key.Equals(ProfileBackupService.ManifestEntryName, StringComparison.OrdinalIgnoreCase))
            .Where(key => !key.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (usable.Any(key => key.StartsWith("profile/", StringComparison.OrdinalIgnoreCase))) return string.Empty;
        if (usable.Count == 0) return string.Empty;

        var firstSegments = usable
            .Select(key => key.Split('/', 2)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (firstSegments.Count != 1) return string.Empty;

        var prefix = firstSegments[0] + "/";
        var relativeKeys = usable.Where(key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(key => key[prefix.Length..]).ToList();
        var resemblesProfile = relativeKeys.Any(key => key.Equals("prefs.js", StringComparison.OrdinalIgnoreCase)) ||
                              relativeKeys.Any(key => key.StartsWith("Mail/", StringComparison.OrdinalIgnoreCase)) ||
                              relativeKeys.Any(key => key.StartsWith("ImapMail/", StringComparison.OrdinalIgnoreCase));
        return resemblesProfile ? prefix : string.Empty;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
