namespace ThunderbirdMboxRecovery.Core;

public static class RecoveryCoordinator
{
    public static string CreateOutputDirectory(string outputRoot, string mailboxName)
    {
        var fullRoot = Path.GetFullPath(outputRoot);
        Directory.CreateDirectory(fullRoot);
        var safeMailboxName = MailboxNameResolver.FromSource(mailboxName);
        var baseName = $"Recuperacao_{safeMailboxName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        var candidate = Path.Combine(fullRoot, baseName);
        var suffix = 1;
        while (Directory.Exists(candidate))
        {
            candidate = Path.Combine(fullRoot, $"{baseName}_{suffix:00}");
            suffix++;
        }
        Directory.CreateDirectory(candidate);
        return candidate;
    }

    public static void ValidateFreeSpace(string outputDirectory, long expectedInputBytes)
    {
        if (expectedInputBytes <= 0) return;
        var root = Path.GetPathRoot(Path.GetFullPath(outputDirectory))
            ?? throw new InvalidOperationException("Não foi possível identificar a unidade de destino.");
        var drive = new DriveInfo(root);
        var required = checked((long)Math.Ceiling(expectedInputBytes * 1.10));
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException(
                $"Espaço insuficiente em {drive.Name}. Disponível: {SizeFormatter.Format(drive.AvailableFreeSpace)}; " +
                $"recomendado: {SizeFormatter.Format(required)}.");
        }
    }
}
