using Microsoft.Win32;
using System.Diagnostics;

namespace ThunderbirdMboxRecovery.Core;

public static class ThunderbirdLocator
{
    public static IReadOnlyList<ThunderbirdInstallation> FindInstallations()
    {
        var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        AddCandidate(candidates, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Mozilla Thunderbird", "thunderbird.exe"), "Program Files");
        AddCandidate(candidates, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Mozilla Thunderbird", "thunderbird.exe"), "Program Files (x86)");
        AddCandidate(candidates, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla Thunderbird", "thunderbird.exe"), "LocalAppData");

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            TryReadAppPath(candidates, RegistryHive.LocalMachine, view);
            TryReadAppPath(candidates, RegistryHive.CurrentUser, view);
            TryReadUninstall(candidates, RegistryHive.LocalMachine, view);
            TryReadUninstall(candidates, RegistryHive.CurrentUser, view);
        }

        return candidates
            .Where(item => File.Exists(item.Key))
            .Select(item => CreateInstallation(item.Key, item.Value))
            .OrderByDescending(item => ParseVersion(item.Version))
            .ThenBy(item => item.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ThunderbirdInstallation FromExecutable(string executablePath, string source = "Seleção manual")
    {
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("O executável do Thunderbird não foi encontrado.", executablePath);

        return CreateInstallation(Path.GetFullPath(executablePath), source);
    }

    private static void TryReadAppPath(Dictionary<string, string> candidates, RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\thunderbird.exe");
            var value = key?.GetValue(null)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                AddCandidate(candidates, value.Trim('"'), $"Registro App Paths ({hive}/{view})");
        }
        catch
        {
            // Uma chave inacessível não impede a busca pelas demais fontes.
        }
    }

    private static void TryReadUninstall(Dictionary<string, string> candidates, RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (root is null) return;

            foreach (var subKeyName in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(subKeyName);
                var displayName = key?.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(displayName) ||
                    !displayName.Contains("Thunderbird", StringComparison.OrdinalIgnoreCase))
                    continue;

                var location = key?.GetValue("InstallLocation")?.ToString();
                if (!string.IsNullOrWhiteSpace(location))
                    AddCandidate(candidates, Path.Combine(location, "thunderbird.exe"), $"Registro Uninstall ({hive}/{view})");

                var icon = key?.GetValue("DisplayIcon")?.ToString();
                if (!string.IsNullOrWhiteSpace(icon))
                {
                    var comma = icon.LastIndexOf(',');
                    if (comma > 0 && int.TryParse(icon[(comma + 1)..], out _)) icon = icon[..comma];
                    AddCandidate(candidates, icon.Trim('"'), $"Registro DisplayIcon ({hive}/{view})");
                }
            }
        }
        catch
        {
            // Ignora chaves corrompidas ou sem permissão.
        }
    }

    private static void AddCandidate(Dictionary<string, string> candidates, string? path, string source)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
            if (!candidates.ContainsKey(fullPath)) candidates.Add(fullPath, source);
        }
        catch
        {
            // Caminho inválido proveniente do registro.
        }
    }

    private static ThunderbirdInstallation CreateInstallation(string path, string source)
    {
        var info = FileVersionInfo.GetVersionInfo(path);
        var version = info.ProductVersion ?? info.FileVersion ?? "desconhecida";
        var plus = version.IndexOf('+');
        if (plus > 0) version = version[..plus];

        return new ThunderbirdInstallation
        {
            ExecutablePath = path,
            Version = version,
            Architecture = ReadPortableExecutableArchitecture(path),
            Source = source
        };
    }

    private static ThunderbirdArchitecture ReadPortableExecutableArchitecture(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new BinaryReader(stream);
            if (reader.ReadUInt16() != 0x5A4D) return ThunderbirdArchitecture.Unknown;
            stream.Position = 0x3C;
            var peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset > stream.Length - 6) return ThunderbirdArchitecture.Unknown;
            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550) return ThunderbirdArchitecture.Unknown;
            return reader.ReadUInt16() switch
            {
                0x014C => ThunderbirdArchitecture.X86,
                0x8664 => ThunderbirdArchitecture.X64,
                0xAA64 => ThunderbirdArchitecture.Arm64,
                _ => ThunderbirdArchitecture.Unknown
            };
        }
        catch
        {
            return ThunderbirdArchitecture.Unknown;
        }
    }

    private static Version ParseVersion(string value)
    {
        var numeric = new string(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
        return Version.TryParse(numeric, out var version) ? version : new Version(0, 0);
    }
}
