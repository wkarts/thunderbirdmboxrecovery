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
            RunSingleOutputTest(root);
            RunSplitOutputTest(root);
            Console.WriteLine("Smoke tests da linha 1.3 concluídos com sucesso.");
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

    private static void RunSingleOutputTest(string root)
    {
        var source = CreateSampleMbox(root);
        var output = Path.Combine(root, "single");

        var result = new MboxSplitter().Execute(
            CreateOptions(source, output, splitOutput: false, targetChunkBytes: 1024),
            progress: null,
            CancellationToken.None);

        Assert(result.TotalMessages == 2, "A saída única deve conter duas mensagens.");
        Assert(result.Parts.Count == 1, "A saída única deve gerar exatamente um MBOX.");
        Assert(result.Parts[0].FileName == "Inbox_Recuperada", "O nome do MBOX único está incorreto.");
        Assert(!Directory.EnumerateFiles(output, "*.msf").Any(), "A linha 1.3 não deve fabricar .msf vazio.");

        var recovered = File.ReadAllText(
            Path.Combine(output, result.Parts[0].FileName),
            Encoding.UTF8);

        Assert(CountOccurrences(recovered, "From sender") == 2, "Os dois separadores MBOX devem ser preservados.");
        Assert(recovered.Contains("Subject: Mensagem 1", StringComparison.Ordinal), "A primeira mensagem não foi preservada.");
        Assert(recovered.Contains("Subject: Mensagem 2", StringComparison.Ordinal), "A segunda mensagem não foi preservada.");
    }

    private static void RunSplitOutputTest(string root)
    {
        var source = CreateSampleMbox(root);
        var output = Path.Combine(root, "split");

        var result = new MboxSplitter().Execute(
            CreateOptions(source, output, splitOutput: true, targetChunkBytes: 1),
            progress: null,
            CancellationToken.None);

        Assert(result.Parts.Count == 2, "Com limite mínimo, cada mensagem deve ficar em uma parte.");
        Assert(result.Parts.All(part => part.EstimatedMessages == 1), "Cada parte deve conter uma mensagem completa.");
        Assert(!Directory.EnumerateFiles(output, "*.partial").Any(), "Não devem restar arquivos .partial após sucesso.");
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
            TargetChunkBytes = targetChunkBytes,
            ExpectedInputBytes = new FileInfo(source).Length
        };
    }

    private static string CreateSampleMbox(string root)
    {
        var source = Path.Combine(root, $"Inbox-{Guid.NewGuid():N}");
        var content =
            "From sender1@example.com Mon Jan 01 00:00:00 2024\r\n" +
            "Subject: Mensagem 1\r\n" +
            "From: sender1@example.com\r\n" +
            "Message-ID: <msg1@example.com>\r\n" +
            "\r\n" +
            "Corpo da primeira mensagem.\r\n" +
            "From sender2@example.com Tue Jan 02 00:00:00 2024\r\n" +
            "Subject: Mensagem 2\r\n" +
            "From: sender2@example.com\r\n" +
            "Message-ID: <msg2@example.com>\r\n" +
            "\r\n" +
            "Corpo da segunda mensagem.\r\n";

        File.WriteAllText(source, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return source;
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var offset = 0;

        while ((offset = value.IndexOf(search, offset, StringComparison.Ordinal)) >= 0)
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
