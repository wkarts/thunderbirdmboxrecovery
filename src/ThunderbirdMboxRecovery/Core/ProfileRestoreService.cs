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

    public static ProfileBackupInspection InspectBackup(string backupPath, string? archivePassword = null)
    {
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("O arquivo de backup não foi encontrado.", backupPath);

        var warnings = new List<string>();
        var readerOptions = new ReaderOptions { Password = string.IsNullOrWhiteSpace(archivePassword) ? null : archivePassword };
        var manifest = TryReadManifest(backupPath, readerOptions, warnings);
        if (manifest is not null)
        {
            return new ProfileBackupInspection
            {
                BackupPath = backupPath,
                IsSuiteBackup = true,
                Scope = manifest.Scope,
                Manifest = manifest,
                ProfileRelativePaths = manifest.ProfileRelativePaths,
                Warnings = warnings
            };
        }

        using var archive = ArchiveFactory.OpenArchive(backupPath, readerOptions);
        var keys = archive.Entries
            .Where(entry => !entry.IsDirectory)
            .Select(entry => Normalize(entry.Key ?? string.Empty))
            .ToList();
        var scope = keys.Any(key => key.StartsWith("thunderbird-root/", StringComparison.OrdinalIgnoreCase))
            ? ProfileBackupScope.ThunderbirdDataRoot
            : ProfileBackupScope.SelectedProfile;
        return new ProfileBackupInspection
        {
            BackupPath = backupPath,
            IsSuiteBackup = false,
            Scope = scope,
            Manifest = null,
            ProfileRelativePaths = Array.Empty<string>(),
            Warnings = warnings
        };
    }

    private static ProfileRestoreResult Restore(
        ProfileRestoreOptions options,
        IProgress<(long Files, long Bytes, string Current)>? progress,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(options.BackupPath))
            throw new FileNotFoundException("O arquivo de backup não foi encontrado.", options.BackupPath);

        var warnings = new List<string>();
        var readerOptions = new ReaderOptions { Password = string.IsNullOrWhiteSpace(options.ArchivePassword) ? null : options.ArchivePassword };
        var manifest = TryReadManifest(options.BackupPath, readerOptions, warnings);
        var backupScope = manifest?.Scope ?? DetectScope(options.BackupPath, readerOptions);
        ValidateTargetCompatibility(backupScope, options.TargetMode);

        var destinationProfile = Path.GetFullPath(options.DestinationProfilePath);
        var destinationDataRoot = string.IsNullOrWhiteSpace(options.DestinationDataRootPath)
            ? null
            : Path.GetFullPath(options.DestinationDataRootPath!);
        var destinationLocalCache = string.IsNullOrWhiteSpace(options.DestinationLocalCachePath)
            ? null
            : Path.GetFullPath(options.DestinationLocalCachePath!);

        var primaryDestination = options.TargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot
            ? destinationDataRoot ?? throw new InvalidOperationException("O diretório de dados de destino não foi informado.")
            : destinationProfile;

        Directory.CreateDirectory(primaryDestination);
        if (options.TargetMode != ProfileRestoreTargetMode.RestoreThunderbirdDataRoot && ThunderbirdProfileService.IsProfileInUse(destinationProfile))
            throw new IOException("O perfil de destino parece estar em uso. Feche completamente o Thunderbird antes da restauração.");
        if (ThunderbirdProfileService.IsThunderbirdRunning() &&
            (options.TargetMode is ProfileRestoreTargetMode.ReplaceExistingProfile or ProfileRestoreTargetMode.RestoreThunderbirdDataRoot))
            throw new IOException("Feche completamente o Thunderbird antes de substituir um perfil ou o diretório de dados.");

        var destinationHasData = Directory.EnumerateFileSystemEntries(primaryDestination).Any();
        var destructiveTarget = options.TargetMode is ProfileRestoreTargetMode.ReplaceExistingProfile or ProfileRestoreTargetMode.RestoreThunderbirdDataRoot;
        if (destructiveTarget && IsPathInside(primaryDestination, options.BackupPath))
            throw new InvalidOperationException("O arquivo de backup está dentro do destino que será substituído. Mova o backup para outro diretório antes de continuar.");
        if (options.TargetMode == ProfileRestoreTargetMode.CreateNewProfile && destinationHasData)
            throw new InvalidOperationException("O destino calculado para o novo perfil já contém dados. Escolha outro nome ou outro diretório para não sobrescrever um perfil existente.");
        if (destructiveTarget && destinationHasData && !options.CreateSafetyBackup)
            throw new InvalidOperationException("O backup de segurança é obrigatório antes de substituir dados existentes.");
        if (destructiveTarget && destinationHasData && !options.OverwriteExistingFiles)
            throw new InvalidOperationException("A substituição exige autorização explícita para sobrescrever os arquivos existentes.");

        string? safetyBackup = null;
        if (options.CreateSafetyBackup && destinationHasData)
        {
            var parent = Path.GetDirectoryName(primaryDestination) ?? primaryDestination;
            var extension = ProfileBackupService.ArchiveExtension(options.SafetyBackupFormat);
            var prefix = options.TargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot
                ? "Backup_Seguranca_Thunderbird"
                : $"Backup_Seguranca_{Path.GetFileName(primaryDestination)}";
            var path = Path.Combine(parent, $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
            var safetyPrefix = options.TargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot
                ? "thunderbird-root"
                : "profile";
            safetyBackup = ProfileBackupService.CreateDirectorySnapshot(
                primaryDestination,
                path,
                options.SafetyBackupFormat,
                cancellationToken,
                safetyPrefix);
        }

        var expected = manifest?.Files.ToDictionary(file => Normalize(file.RelativePath), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ProfileBackupManifestEntry>(StringComparer.OrdinalIgnoreCase);

        var directorySwaps = new List<DirectorySwapTransaction>();
        var extractionProfile = destinationProfile;
        var extractionDataRoot = destinationDataRoot;
        var extractionLocalCache = destinationLocalCache;

        if (options.TargetMode == ProfileRestoreTargetMode.ReplaceExistingProfile)
        {
            extractionProfile = CreateStagingDirectory(destinationProfile);
            directorySwaps.Add(new DirectorySwapTransaction(extractionProfile, destinationProfile));
        }
        else if (options.TargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
        {
            var stagingDataRoot = CreateStagingDirectory(destinationDataRoot!);
            extractionDataRoot = stagingDataRoot;
            directorySwaps.Add(new DirectorySwapTransaction(stagingDataRoot, destinationDataRoot!));
            extractionLocalCache = null;
        }

        long restoredFiles = 0;
        long restoredBytes = 0;
        long skippedFiles = 0;
        long verifiedFiles = 0;
        var messageContainerMarkers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var archive = ArchiveFactory.OpenArchive(options.BackupPath, readerOptions);
        var allKeys = archive.Entries
            .Where(item => !item.IsDirectory)
            .Select(item => Normalize(item.Key ?? string.Empty))
            .ToList();
        if (options.TargetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot &&
            options.RestoreLocalCache &&
            !string.IsNullOrWhiteSpace(destinationLocalCache) &&
            allKeys.Any(key => key.StartsWith("local-cache/", StringComparison.OrdinalIgnoreCase)))
        {
            var stagingLocalCache = CreateStagingDirectory(destinationLocalCache!);
            extractionLocalCache = stagingLocalCache;
            directorySwaps.Add(new DirectorySwapTransaction(stagingLocalCache, destinationLocalCache!));
        }
        var stripPrefix = DetermineArchivePrefix(allKeys);

        foreach (var entry in archive.Entries.Where(item => !item.IsDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Normalize(entry.Key ?? string.Empty);
            if (key.Equals(ProfileBackupService.ManifestEntryName, StringComparison.OrdinalIgnoreCase)) continue;

            var mapping = MapEntry(
                key,
                stripPrefix,
                backupScope,
                options,
                extractionProfile,
                extractionDataRoot,
                extractionLocalCache,
                messageContainerMarkers);
            if (mapping is null)
            {
                skippedFiles++;
                continue;
            }

            var (targetRoot, targetRelative, manifestKey) = mapping.Value;
            if (!ShouldRestoreEntry(targetRelative, backupScope, options))
            {
                skippedFiles++;
                continue;
            }

            var target = GetSafeDestination(targetRoot, targetRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target) && !options.OverwriteExistingFiles)
            {
                skippedFiles++;
                warnings.Add($"Mantido arquivo existente: {targetRelative}");
                continue;
            }

            var partial = target + ".restore-partial";
            TryDelete(partial);
            string restoredHash;
            long bytes;
            using (var source = entry.OpenEntryStream())
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                bytes = CopyWithHash(source, output, hash, cancellationToken, out restoredHash);
                output.Flush(true);
            }

            if (options.VerifyHashes && expected.TryGetValue(manifestKey, out var manifestEntry) && !string.IsNullOrWhiteSpace(manifestEntry.Sha256))
            {
                if (!string.Equals(manifestEntry.Sha256, restoredHash, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(partial);
                    throw new InvalidDataException($"Falha de integridade ao restaurar {manifestKey}. SHA-256 diferente do manifesto.");
                }
                verifiedFiles++;
            }

            File.Move(partial, target, overwrite: true);
            if (expected.TryGetValue(manifestKey, out var metadata))
                File.SetLastWriteTimeUtc(target, metadata.LastWriteTimeUtc.UtcDateTime);

            restoredFiles++;
            restoredBytes = checked(restoredBytes + bytes);
            progress?.Report((restoredFiles, restoredBytes, targetRelative));
        }

        foreach (var marker in messageContainerMarkers)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(marker)!);
            if (!File.Exists(marker)) File.WriteAllBytes(marker, Array.Empty<byte>());
        }

            ApplyDirectorySwaps(directorySwaps, warnings);
        }
        catch
        {
            foreach (var swap in directorySwaps)
                swap.RemoveStaging(warnings);
            throw;
        }

        var profileRegistered = false;
        string? registeredProfileName = null;
        if (options.RegisterProfile && options.TargetMode != ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
        {
            ThunderbirdProfileService.EnsureProfileSkeleton(destinationProfile);
            var requestedName = string.IsNullOrWhiteSpace(options.RegisteredProfileName)
                ? manifest?.ProfileName ?? Path.GetFileName(destinationProfile) ?? "Perfil restaurado"
                : options.RegisteredProfileName.Trim();
            var registration = ThunderbirdProfileService.RegisterProfile(
                destinationProfile,
                requestedName,
                options.MakeRegisteredProfileDefault,
                destinationDataRoot);
            profileRegistered = registration.Registered || registration.AlreadyRegistered;
            registeredProfileName = registration.ProfileName;
            if (registration.AlreadyRegistered)
                warnings.Add($"O perfil já estava registrado como '{registration.ProfileName}'.");
            else if (registration.BackupPath is not null)
                warnings.Add($"Backup do profiles.ini criado em: {registration.BackupPath}");
        }

        return new ProfileRestoreResult
        {
            DestinationProfilePath = destinationProfile,
            DestinationDataRootPath = destinationDataRoot,
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

    private static void ValidateTargetCompatibility(ProfileBackupScope backupScope, ProfileRestoreTargetMode targetMode)
    {
        if (backupScope == ProfileBackupScope.ThunderbirdDataRoot && targetMode != ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
            throw new InvalidOperationException("Este backup contém o diretório completo do Thunderbird. Selecione 'Restaurar o Thunderbird completo'.");
        if (backupScope == ProfileBackupScope.SelectedProfile && targetMode == ProfileRestoreTargetMode.RestoreThunderbirdDataRoot)
            throw new InvalidOperationException("Este backup contém somente um perfil. Escolha criar, substituir ou restaurar mensagens em um perfil.");
    }

    private static ProfileBackupScope DetectScope(string archivePath, ReaderOptions readerOptions)
    {
        using var archive = ArchiveFactory.OpenArchive(archivePath, readerOptions);
        return archive.Entries.Any(entry =>
            !entry.IsDirectory && Normalize(entry.Key ?? string.Empty).StartsWith("thunderbird-root/", StringComparison.OrdinalIgnoreCase))
            ? ProfileBackupScope.ThunderbirdDataRoot
            : ProfileBackupScope.SelectedProfile;
    }

    private static (string Root, string Relative, string ManifestKey)? MapEntry(
        string key,
        string stripPrefix,
        ProfileBackupScope backupScope,
        ProfileRestoreOptions options,
        string destinationProfile,
        string? destinationDataRoot,
        string? destinationLocalCache,
        ISet<string> messageContainerMarkers)
    {
        if (backupScope == ProfileBackupScope.ThunderbirdDataRoot)
        {
            if (key.StartsWith("thunderbird-root/", StringComparison.OrdinalIgnoreCase))
            {
                var relative = key[17..];
                return (destinationDataRoot!, relative, key);
            }

            if (key.StartsWith("local-cache/", StringComparison.OrdinalIgnoreCase))
            {
                if (!options.RestoreLocalCache || string.IsNullOrWhiteSpace(destinationLocalCache)) return null;
                var relative = key[12..];
                return (destinationLocalCache, relative, key);
            }
            return null;
        }

        var relative = key.StartsWith("profile/", StringComparison.OrdinalIgnoreCase) ? key[8..] : key;
        if (!string.IsNullOrEmpty(stripPrefix) && relative.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase))
            relative = relative[stripPrefix.Length..];
        if (string.IsNullOrWhiteSpace(relative) || relative.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)) return null;

        var manifestKey = relative;
        if (options.TargetMode == ProfileRestoreTargetMode.RestoreMessagesToExisting)
        {
            relative = MapMessagesRelative(relative, options.MessagesSubfolderName, destinationProfile, messageContainerMarkers);
            if (string.IsNullOrWhiteSpace(relative)) return null;
        }
        return (destinationProfile, relative, manifestKey);
    }

    private static string? MapMessagesRelative(
        string relative,
        string? requestedFolderName,
        string destinationProfile,
        ISet<string> markerFiles)
    {
        var normalized = Normalize(relative);
        if (IsThunderbirdMessageMetadata(normalized)) return null;

        var folderName = SanitizeMailboxName(string.IsNullOrWhiteSpace(requestedFolderName)
            ? $"Restaurado_{DateTime.Now:yyyyMMdd_HHmmss}"
            : requestedFolderName);
        var localFolders = Path.Combine(destinationProfile, "Mail", "Local Folders");
        markerFiles.Add(Path.Combine(localFolders, folderName));
        var rootPrefix = $"Mail/Local Folders/{folderName}.sbd/";

        if (normalized.StartsWith("Mail/Local Folders/", StringComparison.OrdinalIgnoreCase))
            return rootPrefix + normalized[19..];

        if (normalized.StartsWith("Mail/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = normalized[5..];
            var separator = rest.IndexOf('/');
            if (separator <= 0) return null;
            var account = SanitizeMailboxName("POP_" + rest[..separator]);
            markerFiles.Add(Path.Combine(localFolders, folderName + ".sbd", account));
            return rootPrefix + account + ".sbd/" + rest[(separator + 1)..];
        }

        if (normalized.StartsWith("ImapMail/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = normalized[9..];
            var separator = rest.IndexOf('/');
            if (separator <= 0) return null;
            var account = SanitizeMailboxName("IMAP_" + rest[..separator]);
            markerFiles.Add(Path.Combine(localFolders, folderName + ".sbd", account));
            return rootPrefix + account + ".sbd/" + rest[(separator + 1)..];
        }

        if (normalized.StartsWith("News/", StringComparison.OrdinalIgnoreCase))
        {
            var rest = normalized[5..];
            var separator = rest.IndexOf('/');
            if (separator <= 0) return null;
            var account = SanitizeMailboxName("NEWS_" + rest[..separator]);
            markerFiles.Add(Path.Combine(localFolders, folderName + ".sbd", account));
            return rootPrefix + account + ".sbd/" + rest[(separator + 1)..];
        }

        return null;
    }


    private static bool IsThunderbirdMessageMetadata(string relative)
    {
        var fileName = Path.GetFileName(relative) ?? string.Empty;
        if (fileName.EndsWith(".msf", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite-wal", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".sqlite-shm", StringComparison.OrdinalIgnoreCase))
            return true;

        return fileName.Equals("popstate.dat", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("msgFilterRules.dat", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("filterlog.html", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("feeds.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("feeditems.json", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("folderTree.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRestoreEntry(
        string relative,
        ProfileBackupScope backupScope,
        ProfileRestoreOptions options)
    {
        if (backupScope == ProfileBackupScope.ThunderbirdDataRoot)
            return true;
        if (options.TargetMode == ProfileRestoreTargetMode.RestoreMessagesToExisting)
            return true;
        return ProfileFileClassifier.ShouldIncludeRelative(relative, options.Mode, options.Selection);
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

        var normalizedRoot = Path.GetFullPath(root);
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) ? normalizedRoot : normalizedRoot + Path.DirectorySeparatorChar;
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
        if (usable.Any(key => key.StartsWith("profile/", StringComparison.OrdinalIgnoreCase)) ||
            usable.Any(key => key.StartsWith("thunderbird-root/", StringComparison.OrdinalIgnoreCase)))
            return string.Empty;
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

    private static string CreateStagingDirectory(string destination)
    {
        var fullDestination = Path.GetFullPath(destination);
        var parent = Path.GetDirectoryName(fullDestination)
            ?? throw new InvalidOperationException($"Não foi possível determinar o diretório pai de {fullDestination}.");
        Directory.CreateDirectory(parent);

        var name = Path.GetFileName(fullDestination) ?? "Thunderbird";
        string candidate;
        do
        {
            candidate = Path.Combine(parent, $".{name}.restore-staging-{Guid.NewGuid():N}");
        } while (Directory.Exists(candidate) || File.Exists(candidate));

        Directory.CreateDirectory(candidate);
        return candidate;
    }

    private static void ApplyDirectorySwaps(
        IReadOnlyList<DirectorySwapTransaction> swaps,
        ICollection<string> warnings)
    {
        if (swaps.Count == 0) return;
        var applied = new List<DirectorySwapTransaction>();
        try
        {
            foreach (var swap in swaps)
            {
                swap.Apply();
                applied.Add(swap);
            }
        }
        catch
        {
            foreach (var swap in applied.AsEnumerable().Reverse())
                swap.Rollback(warnings);
            foreach (var swap in swaps.Except(applied))
                swap.RemoveStaging(warnings);
            throw;
        }

        foreach (var swap in applied)
            swap.Complete(warnings);
    }

    private static bool IsPathInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DirectorySwapTransaction
    {
        private string? _previousPath;
        private bool _applied;

        public DirectorySwapTransaction(string stagingPath, string destinationPath)
        {
            StagingPath = Path.GetFullPath(stagingPath);
            DestinationPath = Path.GetFullPath(destinationPath);
        }

        public string StagingPath { get; }
        public string DestinationPath { get; }

        public void Apply()
        {
            if (!Directory.Exists(StagingPath))
                throw new DirectoryNotFoundException($"Diretório temporário da restauração não encontrado: {StagingPath}");

            if (Directory.Exists(DestinationPath))
            {
                _previousPath = CreatePreviousPath(DestinationPath);
                Directory.Move(DestinationPath, _previousPath);
            }

            try
            {
                Directory.Move(StagingPath, DestinationPath);
                _applied = true;
            }
            catch
            {
                if (_previousPath is not null &&
                    Directory.Exists(_previousPath) &&
                    !Directory.Exists(DestinationPath))
                {
                    Directory.Move(_previousPath, DestinationPath);
                    _previousPath = null;
                }
                throw;
            }
        }

        public void Rollback(ICollection<string> warnings)
        {
            try
            {
                if (_applied && Directory.Exists(DestinationPath))
                    Directory.Delete(DestinationPath, recursive: true);
                if (_previousPath is not null && Directory.Exists(_previousPath))
                    Directory.Move(_previousPath, DestinationPath);
                _previousPath = null;
                _applied = false;
            }
            catch (Exception exception)
            {
                warnings.Add($"Falha ao reverter a troca do diretório {DestinationPath}: {exception.Message}");
            }
        }

        public void Complete(ICollection<string> warnings)
        {
            if (_previousPath is null) return;
            try
            {
                if (Directory.Exists(_previousPath))
                    Directory.Delete(_previousPath, recursive: true);
                _previousPath = null;
            }
            catch (Exception exception)
            {
                warnings.Add($"O diretório anterior foi preservado para remoção manual em {_previousPath}: {exception.Message}");
            }
        }

        public void RemoveStaging(ICollection<string> warnings)
        {
            try
            {
                if (Directory.Exists(StagingPath))
                    Directory.Delete(StagingPath, recursive: true);
            }
            catch (Exception exception)
            {
                warnings.Add($"Não foi possível remover o diretório temporário {StagingPath}: {exception.Message}");
            }
        }

        private static string CreatePreviousPath(string destination)
        {
            var parent = Path.GetDirectoryName(destination)
                ?? throw new InvalidOperationException($"Não foi possível determinar o diretório pai de {destination}.");
            var name = Path.GetFileName(destination) ?? "Thunderbird";
            string candidate;
            do
            {
                candidate = Path.Combine(parent, $".{name}.restore-previous-{Guid.NewGuid():N}");
            } while (Directory.Exists(candidate) || File.Exists(candidate));
            return candidate;
        }
    }

    private static string SanitizeMailboxName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Restaurado" : sanitized.Trim();
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
