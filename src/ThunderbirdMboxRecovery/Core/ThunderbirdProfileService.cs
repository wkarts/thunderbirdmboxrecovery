using System.Diagnostics;
using System.Text;

namespace ThunderbirdMboxRecovery.Core;

public static class ThunderbirdProfileService
{
    public static string ThunderbirdDataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Thunderbird");

    public static IReadOnlyList<ThunderbirdProfileInfo> FindProfiles()
    {
        var profilesIni = Path.Combine(ThunderbirdDataRoot, "profiles.ini");
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
                    ? Path.Combine(ThunderbirdDataRoot, configuredPath.Replace('/', Path.DirectorySeparatorChar))
                    : configuredPath;

                path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                if (!Directory.Exists(path)) continue;

                profiles.Add(new ThunderbirdProfileInfo
                {
                    Name = section.Value.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name)
                        ? name
                        : Path.GetFileName(path),
                    Path = path,
                    IsDefault = section.Value.TryGetValue("Default", out var defaultValue) && defaultValue == "1",
                    IsRelative = isRelative,
                    EstimatedBytes = EstimateDirectoryBytes(path)
                });
            }
        }

        var profilesDirectory = Path.Combine(ThunderbirdDataRoot, "Profiles");
        if (Directory.Exists(profilesDirectory))
        {
            foreach (var directory in Directory.EnumerateDirectories(profilesDirectory))
            {
                var fullPath = Path.GetFullPath(directory);
                if (profiles.Any(profile => profile.Path.Equals(fullPath, StringComparison.OrdinalIgnoreCase))) continue;
                profiles.Add(new ThunderbirdProfileInfo
                {
                    Name = Path.GetFileName(directory),
                    Path = fullPath,
                    IsDefault = false,
                    IsRelative = true,
                    EstimatedBytes = EstimateDirectoryBytes(directory)
                });
            }
        }

        return profiles
            .OrderByDescending(profile => profile.IsDefault)
            .ThenBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
        bool makeDefault = false)
    {
        ValidateProfile(profilePath);
        if (IsThunderbirdRunning())
            throw new IOException("Feche todas as instâncias do Thunderbird antes de registrar o perfil restaurado.");

        var fullProfilePath = Path.GetFullPath(profilePath);
        var root = Path.GetFullPath(ThunderbirdDataRoot);
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
                : Path.GetFileName(fullProfilePath);
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
                catch { /* arquivo transitório ou protegido */ }
            }
        }
        catch
        {
            // Retorna a estimativa parcial.
        }
        return total;
    }
}
