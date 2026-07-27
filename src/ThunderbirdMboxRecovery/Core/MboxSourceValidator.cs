namespace ThunderbirdMboxRecovery.Core;

public static class MboxSourceValidator
{
    private static readonly HashSet<string> UnsupportedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "global-messages-db.sqlite",
        "prefs.js",
        "panacea.dat",
        "folderTree.json",
        "virtualFolders.dat"
    };

    public static void ValidateDirectFile(string sourcePath)
    {
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("O arquivo de origem não foi encontrado.", sourcePath);

        var fileName = Path.GetFileName(sourcePath);
        var extension = Path.GetExtension(fileName);

        if (extension.Equals(".msf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "O arquivo selecionado é um índice .msf e não contém as mensagens completas. " +
                "Selecione o arquivo de mesmo nome sem a extensão .msf.");
        }

        if (UnsupportedFileNames.Contains(fileName) ||
            extension.Equals(".sqlite", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "O arquivo selecionado é um arquivo auxiliar do Thunderbird, não uma caixa MBOX. " +
                "Selecione Inbox, Sent, Drafts, Archives, Trash ou outra pasta sem extensão, ou um arquivo .mbox exportado.");
        }

        if (new FileInfo(sourcePath).Length == 0)
            throw new InvalidDataException("O arquivo selecionado está vazio.");
    }
}
