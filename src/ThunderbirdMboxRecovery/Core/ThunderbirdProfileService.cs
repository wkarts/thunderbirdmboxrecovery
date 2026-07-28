using System.Diagnostics;
using System.Text;

namespace ThunderbirdMboxRecovery.Core;

public static class ThunderbirdProfileService
{
    public static string ThunderbirdDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Thunderbird");

    public static string ThunderbirdLocalCacheRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Thunderbird");

    public static IReadOnlyList<ThunderbirdDataRootInfo> FindDataRoots()
    {
        var roots = new List<ThunderbirdDataRootInfo>();
        AddRoot(
            roots,
            "Thunderbird tradicional (Roaming)",
            ThunderbirdDataRoot,
            ThunderbirdDataRootType.TraditionalRoaming,
            ThunderbirdLocalCacheRoot,
            preferred: File.Exists(Path.Combine(ThunderbirdDataRoot, "profiles.ini")) || Directory.Exists(Path.Combine(ThunderbirdDataRoot, "Profiles")),
            includeWhenMissing: true);

        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");
        if (Directory.Exists(packagesRoot))
        {
            foreach (var package in Directory.EnumerateDirectories(packagesRoot, "*Thunderbird*", SearchOption.TopDirectoryOnly))
            {
                var roamingRoot = Path.Combine(package, "LocalCache", "Roaming", "Thunderbird");
                if (!Directory.Exists(roamingRoot) && !File.Exists(Path.Combine(roamingRoot, "profiles.ini")))
                    continue;

                AddRoot(
                    roots,
                    $"Thunderbird Microsoft Store ({Path.GetFileName(package) ?? "pacote"})",
                    roamingRoot,
                    ThunderbirdDataRootType.MicrosoftStore,
                    Path.Combine(package, "LocalCache", "Local", "Thunderbird"),
                    preferred: !roots.Any(root => root.IsPreferred));
            }
        }

        return roots
            .GroupBy(root => Path.GetFullPath(root.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(root => root.IsPreferred)
            .ThenBy(root => root.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ThunderbirdDataRootInfo GetPreferredDataRoot()
    {
        var roots = FindDataRoots();
        return roots.FirstOrDefault(root => root.IsPreferred)
            ?? roots.FirstOrDefault()
            ?? new ThunderbirdDataRootInfo
            {
                Name = "Thunderbird tradicional (Roaming)",
                Path = ThunderbirdDataRoot,
                Type = ThunderbirdDataRootType.TraditionalRoaming,
                LocalCachePath = ThunderbirdLocalCacheRoot,
                IsPreferred = true
            };
    }

    public static ThunderbirdDataRootInfo CreateCustomDataRoot(string path, string? localCachePath = null)
    {
        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        return new ThunderbirdDataRootInfo
        {
            Name = "Diretório personalizado",
            Path = fullPath,
            Type = ThunderbirdDataRootType.Custom,
            LocalCachePath = string.IsNullOrWhiteSpace(localCachePath)
                ? null
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(localCachePath!)),
            IsPreferred = false
        };
    }

    public static IReadOnlyList<ThunderbirdProfileInfo> FindProfiles()
    {
        return FindDataRoots()
            .SelectMany(FindProfiles)
            .GroupBy(profile => Path.GetFullPath(profile.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<ThunderbirdProfileInfo> FindProfiles(ThunderbirdDataRootInfo dataRoot) =>
        FindProfiles(dataRoot.Path, dataRoot.Type);

    public static IReadOnlyList<ThunderbirdProfileInfo> FindProfiles(string dataRootPath) =>
        FindProfiles(dataRootPath, ThunderbirdDataRootType.Custom);

    private static IReadOnlyList<ThunderbirdProfileInfo> FindProfiles(
        string dataRootPath,
        ThunderbirdDataRootType dataRootType)
    {
        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(dataRootPath));
        var profilesIni = Path.Combine(root, "profiles.ini");
        var profiles = new List<ThunderbirdProfileInfo>();

        if (File.Exists(profilesIni))
        {
            var sections = ParseIni(profilesIni);
            foreach (var section in sections.Where(pair => pair.Key.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)))
            {
                if (!section.Value.TryGetValue("Path", out var configuredPath) || string.IsNullOrWhiteSpace(configuredPath))
                    continue;

                var isRelative = !section.Value.TryGetValue("IsRelative", out var relativeValue) || relativeValue != "0";
                var path = isRelative
                    ? Path.Combine(root, configuredPath.Replace('/', Path.DirectorySeparatorChar))
                    : Environment.ExpandEnvironmentVariables(configuredPath);

                path = Path.GetFullPath(path);
                if (!Directory.Exists(path)) continue;

                profiles.Add(CreateProfileInfo(
                    section.Value.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name)
                        ? name
                        : Path.GetFileName(path) ?? "Perfil",
                    path,
                    section.Value.TryGetValue("Default", out var defaultValue) && defaultValue == "1",
                    isRelative,
                    root,
                    dataRootType));
            }
        }

        var profilesDirectory = Path.Combine(root, "Profiles");
        if (Directory.Exists(profilesDirectory))
        {
            foreach (var directory in Directory.EnumerateDirectories(profilesDirectory))
            {
                var fullPath = Path.GetFullPath(directory);
                if (profiles.Any(profile => profile.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))) continue;
                profiles.Add(CreateProfileInfo(
                    Path.GetFileName(directory) ?? "Perfil",
                    fullPath,
                    false,
                    true,
                    root,
                    dataRootType));
            }
        }

        return profiles
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ThunderbirdDataRootInfo? FindDataRootForProfile(ThunderbirdProfileInfo profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.DataRootPath))
        {
            return FindDataRoots().FirstOrDefault(root =>
                       Path.GetFullPath(root.Path).Equals(Path.GetFullPath(profile.DataRootPath), StringComparison.OrdinalIgnoreCase))
                   ?? CreateCustomDataRoot(profile.DataRootPath);
        }

        var profilePath = Path.GetFullPath(profile.Path);
        return FindDataRoots().FirstOrDefault(root => IsPathInside(root.Path, profilePath));
    }

    public static string CreateNewProfileDestination(
        ThunderbirdDataRootInfo dataRoot,
        string? requestedName,
        string? uniqueToken = null)
    {
        var safeName = SanitizeFolderName(string.IsNullOrWhiteSpace(requestedName)
            ? $"Restaurado_{DateTime.Now:yyyyMMdd_HHmmss}"
            : requestedName.Trim());
        var profilesDirectory = Path.Combine(dataRoot.Path, "Profiles");

        var suffix = SanitizeFolderName(string.IsNullOrWhiteSpace(uniqueToken)
            ? Guid.NewGuid().ToString("N")[..8]
            : uniqueToken.Trim());
        var candidate = Path.Combine(profilesDirectory, $"{suffix}.{safeName}");
        var number = 2;
        while (Directory.Exists(candidate) || File.Exists(candidate))
            candidate = Path.Combine(profilesDirectory, $"{suffix}.{safeName}-{number++:00}");
        return candidate;
    }

    public static void EnsureProfileSkeleton(string profilePath)
    {
        var fullPath = Path.GetFullPath(profilePath);
        Directory.CreateDirectory(fullPath);
        Directory.CreateDirectory(Path.Combine(fullPath, "Mail", "Local Folders"));

        var prefsPath = Path.Combine(fullPath, "prefs.js");
        if (!File.Exists(prefsPath))
        {
            File.WriteAllText(
                prefsPath,
                "// Criado pelo Thunderbird Recovery Suite para permitir o registro seguro do perfil restaurado." + Environment.NewLine,
                new UTF8Encoding(false));
        }
    }

    public static bool IsProfileInUse(string profilePath)
    {
        var lockNames = new[] { "parent.lock", "lock", ".parentlock" };
        return lockNames.Any(name => File.Exists(Path.Combine(profilePath, name)));
    }

    public static void ValidateProfile(string profilePath)
    {
        if (!Directory.Exists(profilePath))
            throw new DirectoryNotFoundException($"Perfil não encontrado: {profilePath}");

        var prefs = Path.Combine(profilePath, "prefs.js");
        var mail = Path.Combine(profilePath, "Mail");
        var imap = Path.Combine(profilePath, "ImapMail");
        if (!File.Exists(prefs) && !Directory.Exists(mail) && !Directory.Exists(imap))
            throw new InvalidDataException("A pasta selecionada não parece ser um perfil válido do Thunderbird.");
    }

    public static bool IsThunderbirdRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("thunderbird");
            try { return processes.Any(process => !process.HasExited); }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        catch
        {
            return false;
        }
    }

    public static ProfileRegistrationResult RegisterProfile(
        string profilePath,
        string profileName,
        bool makeDefault = false,
        string? dataRootPath = null)
    {
        ValidateProfile(profilePath);
        if (IsThunderbirdRunning())
            throw new IOException("Feche todas as instâncias do Thunderbird antes de registrar o perfil restaurado.");

        var fullProfilePath = Path.GetFullPath(profilePath);
        var root = Path.GetFullPath(string.IsNullOrWhiteSpace(dataRootPath) ? ThunderbirdDataRoot : dataRootPath!);
        Directory.CreateDirectory(root);
        var profilesIni = Path.Combine(root, "profiles.ini");
        var sections = File.Exists(profilesIni)
            ? ParseIni(profilesIni)
            : new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in sections.Where(pair => pair.Key.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)))
        {
            if (!section.Value.TryGetValue("Path", out var configuredPath) || string.IsNullOrWhiteSpace(configuredPath))
                continue;

            var isRelative = !section.Value.TryGetValue("IsRelative", out var relativeValue) || relativeValue != "0";
            var registeredPath = isRelative
                ? Path.GetFullPath(Path.Combine(root, configuredPath.Replace('/', Path.DirectorySeparatorChar)))
                : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuredPath));

            if (!registeredPath.Equals(fullProfilePath, StringComparison.OrdinalIgnoreCase))
                continue;

            var existingName = section.Value.TryGetValue("Name", out var configuredName) && !string.IsNullOrWhiteSpace(configuredName)
                ? configuredName
                : Path.GetFileName(fullProfilePath) ?? "Perfil";
            return new ProfileRegistrationResult
            {
                Registered = false,
                AlreadyRegistered = true,
                ProfileName = existingName,
                ProfilePath = fullProfilePath,
                ProfilesIniPath = profilesIni
            };
        }

        var backupPath = File.Exists(profilesIni)
            ? CreateUniqueSiblingPath(profilesIni, $".backup-{DateTime.Now:yyyyMMdd-HHmmss}")
            : null;
        if (backupPath is not null)
            File.Copy(profilesIni, backupPath, overwrite: false);

        var relativePath = Path.GetRelativePath(root, fullProfilePath).Replace(Path.DirectorySeparatorChar, '/');
        var insideRoot = !relativePath.Equals("..", StringComparison.Ordinal) &&
                         !relativePath.StartsWith("../", StringComparison.Ordinal) &&
                         !Path.IsPathRooted(relativePath);
        var configured = insideRoot ? relativePath : fullProfilePath;
        var safeName = SanitizeIniValue(string.IsNullOrWhiteSpace(profileName) ? (Path.GetFileName(fullProfilePath) ?? "Perfil restaurado") : profileName.Trim());
        var safePath = SanitizeIniValue(configured);
        var nextNumber = sections.Keys
            .Where(name => name.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            .Select(name => int.TryParse(name[7..], out var number) ? number : -1)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var originalLines = File.Exists(profilesIni)
            ? File.ReadAllLines(profilesIni, Encoding.UTF8).ToList()
            : new List<string>
            {
                "[General]",
                "StartWithLastProfile=1",
                "Version=2",
                string.Empty
            };

        if (makeDefault)
            ClearExistingDefaultFlags(originalLines);

        if (originalLines.Count > 0 && originalLines[^1].Length != 0)
            originalLines.Add(string.Empty);
        originalLines.Add($"[Profile{nextNumber}]");
        originalLines.Add($"Name={safeName}");
        originalLines.Add($"IsRelative={(insideRoot ? "1" : "0")}");
        originalLines.Add($"Path={safePath}");
        if (makeDefault) originalLines.Add("Default=1");
        originalLines.Add(string.Empty);

        var temporary = CreateUniqueSiblingPath(profilesIni, ".new");
        try
        {
            File.WriteAllLines(temporary, originalLines, new UTF8Encoding(false));
            File.Move(temporary, profilesIni, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
            throw;
        }

        return new ProfileRegistrationResult
        {
            Registered = true,
            AlreadyRegistered = false,
            ProfileName = safeName,
            ProfilePath = fullProfilePath,
            ProfilesIniPath = profilesIni,
            BackupPath = backupPath
        };
    }

    private static ThunderbirdProfileInfo CreateProfileInfo(
        string name,
        string path,
        bool isDefault,
        bool isRelative,
        string dataRootPath,
        ThunderbirdDataRootType dataRootType)
    {
        DateTimeOffset? lastWrite = null;
        try { lastWrite = Directory.GetLastWriteTimeUtc(path); } catch { }
        return new ThunderbirdProfileInfo
        {
            Name = name,
            Path = path,
            IsDefault = isDefault,
            IsRelative = isRelative,
            EstimatedBytes = EstimateDirectoryBytes(path),
            DataRootPath = dataRootPath,
            DataRootType = dataRootType,
            IsInUse = IsProfileInUse(path),
            HasMail = Directory.Exists(Path.Combine(path, "Mail")),
            HasImapMail = Directory.Exists(Path.Combine(path, "ImapMail")),
            LastWriteTimeUtc = lastWrite
        };
    }

    private static void AddRoot(
        ICollection<ThunderbirdDataRootInfo> roots,
        string name,
        string path,
        ThunderbirdDataRootType type,
        string? localCachePath,
        bool preferred,
        bool includeWhenMissing = false)
    {
        var fullPath = Path.GetFullPath(path);
        if (!includeWhenMissing && !Directory.Exists(fullPath) && !File.Exists(Path.Combine(fullPath, "profiles.ini")))
            return;
        roots.Add(new ThunderbirdDataRootInfo
        {
            Name = name,
            Path = fullPath,
            Type = type,
            LocalCachePath = string.IsNullOrWhiteSpace(localCachePath) ? null : Path.GetFullPath(localCachePath!),
            IsPreferred = preferred
        });
    }

    private static bool IsPathInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFolderName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? $"Restaurado_{DateTime.Now:yyyyMMdd_HHmmss}" : sanitized;
    }

    private static string CreateUniqueSiblingPath(string basePath, string suffix)
    {
        var candidate = basePath + suffix;
        var number = 2;
        while (File.Exists(candidate) || Directory.Exists(candidate))
            candidate = basePath + suffix + $"-{number++:00}";
        return candidate;
    }

    private static void ClearExistingDefaultFlags(List<string> lines)
    {
        var inProfile = false;
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                var section = trimmed[1..^1];
                inProfile = section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inProfile && trimmed.Equals("Default=1", StringComparison.OrdinalIgnoreCase))
                lines[index] = "Default=0";
        }
    }

    private static string SanitizeIniValue(string value) => value.Replace("\r", string.Empty).Replace("\n", string.Empty);

    private static Dictionary<string, Dictionary<string, string>> ParseIni(string path)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        string? section = null;

        foreach (var rawLine in File.ReadLines(path, Encoding.UTF8))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (!result.ContainsKey(section))
                    result[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (section is null) continue;
            var equals = line.IndexOf('=');
            if (equals <= 0) continue;
            result[section][line[..equals].Trim()] = line[(equals + 1)..].Trim();
        }

        return result;
    }

    private static long EstimateDirectoryBytes(string path)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total = checked(total + new FileInfo(file).Length); }
                catch { }
            }
        }
        catch
        {
        }
        return total;
    }
}
