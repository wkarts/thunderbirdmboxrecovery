using System.Text;
using ThunderbirdMboxRecovery.Core;

internal static class Program
{
    private static int Main()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "ThunderbirdMboxRecovery-SmokeTests",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(root);
            RunDeletedMessageRecoveryTest(root);
            RunSplitOutputTest(root);
            Console.WriteLine("Smoke tests da linha 1.4 concluídos com sucesso.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void RunDeletedMessageRecoveryTest(string root)
    {
        var source = CreateSampleMbox(root);
        var output = Path.Combine(root, "single");

        var result = new MboxSplitter().Execute(
            CreateOptions(source, output, splitOutput: false, targetChunkBytes: 1024),
            progress: null,
            CancellationToken.None);

        Assert(result.TotalMessages == 3, "A saída única deve conter três mensagens.");
        Assert(result.Parts.Count == 1, "A saída única deve gerar exatamente um MBOX.");
        Assert(result.Parts[0].FileName == "Inbox_Recuperada", "O nome do MBOX único está incorreto.");
        Assert(result.ExpungedMessagesRecovered == 1, "A flag Expunged deveria ser removida de uma mensagem.");
        Assert(result.ImapDeletedMessagesRecovered == 1, "A flag IMAPDeleted deveria ser removida de uma mensagem.");
        Assert(result.MalformedStatusHeadersRepaired == 1, "Um cabeçalho de status malformado deveria ser reparado.");
        Assert(result.StatusHeadersInserted == 1, "Deveria ser inserido um X-Mozilla-Status2 ausente.");
        Assert(!Directory.EnumerateFiles(output, "*.msf").Any(), "A linha 1.4 não deve fabricar .msf.");
        Assert(!Directory.EnumerateFiles(output, "*.partial").Any(), "Não devem restar arquivos .partial após sucesso.");

        var recovered = File.ReadAllText(
            Path.Combine(output, result.Parts[0].FileName),
            Encoding.UTF8);

        Assert(CountOccurrences(recovered, "From sender") == 3, "Os três separadores MBOX devem ser preservados.");
        Assert(!recovered.Contains("X-Mozilla-Status: 0008", StringComparison.OrdinalIgnoreCase),
            "A flag Expunged não pode permanecer na saída reparada.");
        Assert(!recovered.Contains("X-Mozilla-Status2: 00200000", StringComparison.OrdinalIgnoreCase),
            "A flag IMAPDeleted não pode permanecer na saída reparada.");
        Assert(!recovered.Contains("X-Mozilla-Status: ZZZZ", StringComparison.OrdinalIgnoreCase),
            "O cabeçalho de status malformado não pode permanecer na saída reparada.");
        Assert(recovered.Contains("X-Mozilla-Status: 0001", StringComparison.OrdinalIgnoreCase),
            "A flag de mensagem lida deve ser preservada.");
        Assert(CountOccurrences(recovered, "X-Mozilla-Status2:") == 3,
            "Cada mensagem deve terminar com um X-Mozilla-Status2 válido.");
    }

    private static void RunSplitOutputTest(string root)
    {
        var source = CreateSampleMbox(root);
        var output = Path.Combine(root, "split");

        var result = new MboxSplitter().Execute(
            CreateOptions(source, output, splitOutput: true, targetChunkBytes: 1),
            progress: null,
            CancellationToken.None);

        Assert(result.Parts.Count == 3, "Com limite mínimo, cada mensagem deve ficar em uma parte.");
        Assert(result.Parts.All(part => part.EstimatedMessages == 1), "Cada parte deve conter uma mensagem completa.");
        Assert(!Directory.EnumerateFiles(output, "*.partial").Any(), "Não devem restar arquivos .partial após sucesso.");
        Assert(!Directory.EnumerateFiles(output, "*.msf").Any(), "Nenhum .msf artificial deve ser criado.");
    }

    private static RecoveryOptions CreateOptions(
        string source,
        string output,
        bool splitOutput,
        long targetChunkBytes)
    {
        return new RecoveryOptions
        {
            SourcePath = source,
            OutputDirectory = output,
            MailboxName = "Inbox",
            SplitOutput = splitOutput,
            RecoverDeletedMessages = true,
            NormalizeMozillaStatusHeaders = true,
            TargetChunkBytes = targetChunkBytes,
            ExpectedInputBytes = new FileInfo(source).Length
        };
    }

    private static string CreateSampleMbox(string root)
    {
        var source = Path.Combine(root, $"Inbox-{Guid.NewGuid():N}");
        var content =
            "From sender1@example.com Mon Jan 01 00:00:00 2024\r\n" +
            "Subject: Mensagem expurgada\r\n" +
            "From: sender1@example.com\r\n" +
            "Message-ID: <msg1@example.com>\r\n" +
            "X-Mozilla-Status: 0008\r\n" +
            "X-Mozilla-Status2: 00200000\r\n" +
            "\r\n" +
            "Corpo da primeira mensagem.\r\n" +
            "From sender2@example.com Tue Jan 02 00:00:00 2024\r\n" +
            "Subject: Mensagem lida\r\n" +
            "From: sender2@example.com\r\n" +
            "Message-ID: <msg2@example.com>\r\n" +
            "X-Mozilla-Status: 0001\r\n" +
            "X-Mozilla-Status2: 00000000\r\n" +
            "\r\n" +
            "Corpo da segunda mensagem.\r\n" +
            "From sender3@example.com Wed Jan 03 00:00:00 2024\r\n" +
            "Subject: Cabeçalho malformado\r\n" +
            "From: sender3@example.com\r\n" +
            "Message-ID: <msg3@example.com>\r\n" +
            "X-Mozilla-Status: ZZZZ\r\n" +
            "\r\n" +
            "Corpo da terceira mensagem.\r\n";

        File.WriteAllText(source, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return source;
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(search, offset, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            offset += search.Length;
        }

        return count;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // A limpeza não deve mascarar o resultado do teste.
        }
    }
}
