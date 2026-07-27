namespace ThunderbirdMboxRecovery.Core;

internal static class MsfIndexService
{
    public static string CreateRebuildPlaceholder(string mboxPath)
    {
        var msfPath = mboxPath + ".msf";

        // O .msf é um banco de resumo interno do Thunderbird. Um índice completo
        // não deve ser fabricado externamente. O arquivo vazio força o Thunderbird
        // a reconhecer que o índice precisa ser criado/reconstruído a partir do MBOX.
        using var stream = new FileStream(
            msfPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read);

        stream.Flush(true);
        return msfPath;
    }
}
