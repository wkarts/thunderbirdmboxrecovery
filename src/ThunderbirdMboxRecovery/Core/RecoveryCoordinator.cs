namespace ThunderbirdMboxRecovery.Core;

public static class RecoveryCoordinator
{
    private const long Fat32MaximumFileBytes = 4L * 1024 * 1024 * 1024 - 1;

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

    public static void ValidateDestination(
        string outputDirectory,
        long expectedInputBytes,
        bool splitOutput,
        long targetChunkBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(outputDirectory))
            ?? throw new InvalidOperationException("Não foi possível identificar a unidade de destino.");

        var drive = new DriveInfo(root);
        if (!drive.IsReady)
            throw new IOException($"A unidade de destino {drive.Name} não está pronta.");

        if (expectedInputBytes > 0)
        {
            var required = checked((long)Math.Ceiling(expectedInputBytes * 1.10));
            if (drive.AvailableFreeSpace < required)
            {
                throw new IOException(
                    $"Espaço insuficiente em {drive.Name}. Disponível: {SizeFormatter.Format(drive.AvailableFreeSpace)}; " +
                    $"recomendado: {SizeFormatter.Format(required)}.");
            }
        }

        if (!drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
            return;

        var expectedOutputFileBytes = splitOutput
            ? targetChunkBytes
            : expectedInputBytes;

        if (expectedOutputFileBytes > Fat32MaximumFileBytes)
        {
            throw new IOException(
                $"A unidade {drive.Name} está formatada como FAT32 e não aceita arquivos maiores que 4 GiB. " +
                "Escolha uma unidade NTFS/exFAT ou habilite o fracionamento com partes menores que 4 GiB.");
        }
    }
}
