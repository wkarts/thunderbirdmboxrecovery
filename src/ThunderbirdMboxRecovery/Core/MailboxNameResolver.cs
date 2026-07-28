namespace ThunderbirdMboxRecovery.Core;

public static class MailboxNameResolver
{
    private static readonly HashSet<string> ReservedOutputNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "manifesto_recuperacao",
        "recuperacao",
        "prefixo_nao_reconhecido",
        "COMO_IMPORTAR_NO_THUNDERBIRD",
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string FromSource(string sourcePath, string? archiveEntryKey = null)
    {
        var rawName = string.IsNullOrWhiteSpace(archiveEntryKey)
            ? Path.GetFileName(sourcePath)
            : Path.GetFileName(archiveEntryKey.Replace('/', Path.DirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(rawName))
            rawName = "Caixa_MBOX";

        // Aceita MBOX sem extensão e também arquivos exportados como .mbox.
        if (string.Equals(Path.GetExtension(rawName), ".mbox", StringComparison.OrdinalIgnoreCase))
            rawName = Path.GetFileNameWithoutExtension(rawName) ?? "Caixa_MBOX";

        var invalid = Path.GetInvalidFileNameChars();
        var normalized = new string(rawName
            .Select(character => invalid.Contains(character) || char.IsControl(character) || character == '.' ? '_' : character)
            .ToArray())
            .Trim(' ', '.');

        if (string.IsNullOrWhiteSpace(normalized))
            normalized = "Caixa_MBOX";

        if (ReservedOutputNames.Contains(normalized))
            normalized += "_MBOX";

        return normalized.Length <= 80 ? normalized : normalized[..80];
    }
}
