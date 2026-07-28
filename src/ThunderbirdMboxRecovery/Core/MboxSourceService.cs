namespace ThunderbirdMboxRecovery.Core;

public static class MboxSourceService
{
    public static MboxReadHandle Open(MboxSourceSelection source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.IsArchive)
        {
            if (string.IsNullOrWhiteSpace(source.ArchiveEntryKey))
                throw new InvalidOperationException("Selecione a caixa MBOX dentro do arquivo compactado.");

            return ArchiveService.OpenSelectedEntry(
                source.SourcePath,
                source.ArchiveEntryKey,
                source.ArchivePassword);
        }

        MboxSourceValidator.ValidateDirectFile(source.SourcePath);
        var stream = new FileStream(
            source.SourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            4 * 1024 * 1024,
            FileOptions.SequentialScan);

        return new MboxReadHandle(
            stream,
            stream.Length,
            owner: null,
            displayName: source.SourcePath);
    }
}
