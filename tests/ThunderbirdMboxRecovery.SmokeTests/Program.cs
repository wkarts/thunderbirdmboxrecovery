using System.Text;
using ThunderbirdMboxRecovery.Core;

internal static class Program
{
    private static int Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "ThunderbirdRecoverySuite-SmokeTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            RunDeletedMessageRecoveryTest(root);
            RunSplitOutputTest(root);
            RunAnalyzerAndExtractorTest(root);
            RunMsfValidationTest(root);
            RunSqliteValidationTest(root);
            RunProfileBackupAndRestoreTest(root);
            Console.WriteLine("Smoke tests da Thunderbird Recovery Suite 2.0 concluídos com sucesso.");
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
        Directory.CreateDirectory(output);

        var result = new MboxSplitter().Execute(
            CreateRecoveryOptions(source, output, splitOutput: false, targetChunkBytes: 1024),
            progress: null,
            CancellationToken.None);

        Assert(result.TotalMessages == 3, "A saída única deve conter três mensagens.");
        Assert(result.Parts.Count == 1, "A saída única deve gerar exatamente um MBOX.");
        Assert(result.Parts[0].FileName == "Inbox_Recuperada", "O nome do MBOX único está incorreto.");
        Assert(result.ExpungedMessagesRecovered == 1, "A flag Expunged deveria ser removida de uma mensagem.");
        Assert(result.ImapDeletedMessagesRecovered == 1, "A flag IMAPDeleted deveria ser removida de uma mensagem.");
        Assert(result.MalformedStatusHeadersRepaired == 1, "Um cabeçalho de status malformado deveria ser reparado.");
        Assert(result.StatusHeadersInserted == 1, "Deveria ser inserido um X-Mozilla-Status2 ausente.");
        Assert(!Directory.EnumerateFiles(output, "*.msf").Any(), "O reparo não deve fabricar .msf.");
        Assert(!Directory.EnumerateFiles(output, "*.partial").Any(), "Não devem restar arquivos .partial após sucesso.");

        var recovered = File.ReadAllText(Path.Combine(output, result.Parts[0].FileName), Encoding.UTF8);
        Assert(CountOccurrences(recovered, "From sender") == 3, "Os três separadores MBOX devem ser preservados.");
        Assert(!recovered.Contains("X-Mozilla-Status: 0008", StringComparison.OrdinalIgnoreCase), "A flag Expunged não pode permanecer.");
        Assert(!recovered.Contains("X-Mozilla-Status2: 00200000", StringComparison.OrdinalIgnoreCase), "A flag IMAPDeleted não pode permanecer.");
        Assert(!recovered.Contains("X-Mozilla-Status: ZZZZ", StringComparison.OrdinalIgnoreCase), "Status malformado não pode permanecer.");
        Assert(recovered.Contains("X-Mozilla-Status: 0001", StringComparison.OrdinalIgnoreCase), "A flag de mensagem lida deve ser preservada.");
    }

    private static void RunSplitOutputTest(string root)
    {
        var source = CreateSampleMbox(root);
        var output = Path.Combine(root, "split");
        Directory.CreateDirectory(output);
        var result = new MboxSplitter().Execute(
            CreateRecoveryOptions(source, output, splitOutput: true, targetChunkBytes: 1),
            progress: null,
            CancellationToken.None);

        Assert(result.Parts.Count == 3, "Com limite mínimo, cada mensagem deve ficar em uma parte.");
        Assert(result.Parts.All(part => part.EstimatedMessages == 1), "Cada parte deve conter uma mensagem completa.");
        Assert(!Directory.EnumerateFiles(output, "*.partial").Any(), "Não devem restar arquivos .partial.");
    }

    private static void RunAnalyzerAndExtractorTest(string root)
    {
        var sourcePath = CreateSampleMbox(root);
        var source = new MboxSourceSelection { SourcePath = sourcePath };
        var report = MboxAnalyzer.AnalyzeAsync(source, new MboxAnalysisOptions { MaxMessagesInMemory = 100 }, null, CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert(report.TotalMessages == 3, "O analisador deve reconhecer três mensagens.");
        Assert(report.DeletedMessages == 1, "O analisador deve reconhecer uma mensagem excluída.");
        Assert(report.Messages.Count == 3, "A lista de exploração deve conter três mensagens.");
        Assert(!string.IsNullOrWhiteSpace(report.Sha256) && report.Sha256.Length == 64, "O SHA-256 do diagnóstico é inválido.");

        var extractionDirectory = Path.Combine(root, "eml");
        var extraction = MboxExtractor.ExtractAsync(new MessageExtractionOptions
        {
            Source = source,
            OutputDirectory = extractionDirectory,
            Filter = new MessageExtractionFilter { OnlyDeleted = true, IncludeDeleted = true },
            GenerateCsvIndex = true,
            PreserveMozillaStatusHeaders = false
        }, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert(extraction.ExtractedMessages == 1, "Deve ser extraída somente a mensagem excluída.");
        Assert(extraction.Files.Count == 1 && File.Exists(extraction.Files[0]), "O arquivo EML não foi gerado.");
        Assert(File.Exists(extraction.CsvIndexPath), "O índice CSV dos EMLs não foi gerado.");
        var eml = File.ReadAllText(extraction.Files[0], Encoding.UTF8);
        Assert(!eml.Contains("X-Mozilla-Status", StringComparison.OrdinalIgnoreCase), "O EML sanitizado não deve conter status Mozilla.");
    }

    private static void RunMsfValidationTest(string root)
    {
        var msf = Path.Combine(root, "sample.msf");
        File.WriteAllText(msf,
            "// <!-- <mdb:mork:z v=\"1.4\"/> -->\n" +
            "< <(A1=numMsgs)> >\n" +
            "{1:^80 {(k^A1=^3)}}\n",
            new UTF8Encoding(false));
        var validation = MsfValidator.Validate(msf);
        Assert(validation.Exists, "O validador deve localizar o MSF sintético.");
        Assert(validation.IsValidMork, "O MSF sintético deve ser reconhecido como Mork.");
        Assert(validation.IndexedMessages == 3, "O campo numMsgs deveria ser lido como 3.");
    }

    private static void RunSqliteValidationTest(string root)
    {
        var sqlite = Path.Combine(root, "panorama.sqlite");
        var data = new byte[4096];
        "SQLite format 3\0"u8.CopyTo(data);
        File.WriteAllBytes(sqlite, data);
        Assert(SqliteFileValidator.IsValid(sqlite), "O cabeçalho SQLite do Panorama deve ser reconhecido.");
    }

    private static void RunProfileBackupAndRestoreTest(string root)
    {
        var profilePath = Path.Combine(root, "profile-source");
        Directory.CreateDirectory(Path.Combine(profilePath, "Mail", "Local Folders"));
        File.WriteAllText(Path.Combine(profilePath, "prefs.js"), "user_pref(\"mail.accountmanager.accounts\", \"\");", new UTF8Encoding(false));
        File.Copy(CreateSampleMbox(root), Path.Combine(profilePath, "Mail", "Local Folders", "Inbox"));
        File.WriteAllText(Path.Combine(profilePath, "abook.sqlite"), "catalogo", new UTF8Encoding(false));

        var backupPath = Path.Combine(root, "profile-backup.zip");
        var backup = ProfileBackupService.CreateAsync(new ProfileBackupOptions
        {
            Profile = new ThunderbirdProfileInfo
            {
                Name = "teste",
                Path = profilePath,
                IsDefault = false,
                IsRelative = false,
                EstimatedBytes = 0
            },
            DestinationZipPath = backupPath,
            Mode = ProfileBackupMode.Complete,
            Selection = new ProfileBackupSelection { Cache = false, SearchIndexes = true },
            CalculateFileHashes = true
        }, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert(File.Exists(backup.BackupPath), "O ZIP de backup não foi criado.");
        Assert(backup.Files >= 3, "O backup deveria conter os arquivos do perfil sintético.");
        Assert(backup.Sha256.Length == 64, "O SHA-256 do backup é inválido.");

        var destination = Path.Combine(root, "profile-restored");
        var restore = ProfileRestoreService.RestoreAsync(new ProfileRestoreOptions
        {
            BackupPath = backup.BackupPath,
            DestinationProfilePath = destination,
            CreateSafetyBackup = false,
            OverwriteExistingFiles = true,
            VerifyHashes = true
        }, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert(restore.RestoredFiles == backup.Files, "A restauração deve recuperar todos os arquivos do manifesto.");
        Assert(restore.VerifiedFiles == backup.Files, "Todos os arquivos devem ter o SHA-256 validado.");
        Assert(File.Exists(Path.Combine(destination, "Mail", "Local Folders", "Inbox")), "A caixa Inbox não foi restaurada.");
        Assert(File.Exists(Path.Combine(destination, "prefs.js")), "prefs.js não foi restaurado.");
        Assert(!restore.ProfileRegistered, "O smoke test não deve registrar um perfil real no profiles.ini.");

        var messagesOnlyDestination = Path.Combine(root, "profile-restored-messages-only");
        var messagesOnlyRestore = ProfileRestoreService.RestoreAsync(new ProfileRestoreOptions
        {
            BackupPath = backup.BackupPath,
            DestinationProfilePath = messagesOnlyDestination,
            CreateSafetyBackup = false,
            OverwriteExistingFiles = true,
            VerifyHashes = true,
            Mode = ProfileBackupMode.MessagesOnly,
            RegisterProfile = false
        }, null, CancellationToken.None).GetAwaiter().GetResult();

        Assert(messagesOnlyRestore.RestoredFiles >= 1, "A restauração somente de mensagens deve recuperar pelo menos uma caixa.");
        Assert(File.Exists(Path.Combine(messagesOnlyDestination, "Mail", "Local Folders", "Inbox")), "O modo somente mensagens não restaurou a Inbox.");
        Assert(!File.Exists(Path.Combine(messagesOnlyDestination, "prefs.js")), "O modo somente mensagens não deve restaurar prefs.js.");
        Assert(!messagesOnlyRestore.ProfileRegistered, "A restauração somente de mensagens não deve registrar perfil.");
    }

    private static RecoveryOptions CreateRecoveryOptions(string source, string output, bool splitOutput, long targetChunkBytes) => new()
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

    private static string CreateSampleMbox(string root)
    {
        var source = Path.Combine(root, $"Inbox-{Guid.NewGuid():N}");
        var content =
            "From sender1@example.com Mon Jan 01 00:00:00 2024\r\n" +
            "Subject: Mensagem expurgada\r\n" +
            "From: sender1@example.com\r\n" +
            "To: recipient@example.com\r\n" +
            "Date: Mon, 1 Jan 2024 00:00:00 +0000\r\n" +
            "Message-ID: <msg1@example.com>\r\n" +
            "Content-Type: multipart/mixed; boundary=x\r\n" +
            "X-Mozilla-Status: 0008\r\n" +
            "X-Mozilla-Status2: 00200000\r\n\r\n" +
            "Corpo da primeira mensagem.\r\n" +
            "From sender2@example.com Tue Jan 02 00:00:00 2024\r\n" +
            "Subject: Mensagem lida\r\n" +
            "From: sender2@example.com\r\n" +
            "To: recipient@example.com\r\n" +
            "Date: Tue, 2 Jan 2024 00:00:00 +0000\r\n" +
            "Message-ID: <msg2@example.com>\r\n" +
            "X-Mozilla-Status: 0001\r\n" +
            "X-Mozilla-Status2: 00000000\r\n\r\n" +
            "Corpo da segunda mensagem.\r\n" +
            "From sender3@example.com Wed Jan 03 00:00:00 2024\r\n" +
            "Subject: Cabeçalho malformado\r\n" +
            "From: sender3@example.com\r\n" +
            "To: recipient@example.com\r\n" +
            "Date: Wed, 3 Jan 2024 00:00:00 +0000\r\n" +
            "Message-ID: <msg3@example.com>\r\n" +
            "X-Mozilla-Status: ZZZZ\r\n\r\n" +
            "Corpo da terceira mensagem.\r\n";
        File.WriteAllText(source, content, new UTF8Encoding(false));
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
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }
}
