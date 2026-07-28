using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ThunderbirdMboxRecovery.Core;

public static class ThunderbirdIndexService
{
    public static Task<ThunderbirdIndexResult> CreateIndexAsync(
        ThunderbirdIndexOptions options,
        IProgress<ThunderbirdIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        return Task.Run(() => Execute(options, progress, cancellationToken), cancellationToken);
    }

    private static ThunderbirdIndexResult Execute(
        ThunderbirdIndexOptions options,
        IProgress<ThunderbirdIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var safeMailbox = MailboxNameResolver.FromSource(options.MailboxName);
        var outputDirectory = CreateUniqueOutput(options.OutputDirectory, safeMailbox);
        var temporaryProfile = Path.Combine(outputDirectory, "perfil_temporario_indexacao");
        var localFolders = Path.Combine(temporaryProfile, "Mail", "Local Folders");
        Directory.CreateDirectory(localFolders);

        using var log = new RecoveryLogger(outputDirectory, "indexacao_thunderbird.log");
        var logPath = log.LogPath;
        Process? process = null;
        string? warning = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new ThunderbirdIndexProgress("Preparando", "Criando perfil temporário isolado."));
            WriteProfileConfiguration(temporaryProfile, localFolders);

            var temporaryMbox = Path.Combine(localFolders, safeMailbox);
            progress?.Report(new ThunderbirdIndexProgress("Copiando", "Copiando o MBOX para o perfil isolado."));
            var expectedMessages = CopyMboxAndCount(options.Source, temporaryMbox, options.KeepTemporaryProfile, progress, cancellationToken);
            log.Info($"MBOX preparado: {temporaryMbox}; mensagens reconhecidas: {expectedMessages:N0}.");

            var startInfo = new ProcessStartInfo
            {
                FileName = options.Installation.ExecutablePath,
                Arguments = $"-profile \"{temporaryProfile}\" -new-instance -no-remote",
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(options.Installation.ExecutablePath) ?? Environment.CurrentDirectory
            };
            startInfo.Environment["MOZ_NO_REMOTE"] = "1";

            progress?.Report(new ThunderbirdIndexProgress("Inicializando", "Abrindo o Thunderbird no perfil temporário."));
            var launchedAt = DateTime.UtcNow;
            process = Process.Start(startInfo) ?? throw new InvalidOperationException("O Thunderbird não pôde ser iniciado.");
            process = ResolveThunderbirdProcess(process, options.Installation.ExecutablePath, launchedAt, cancellationToken);
            log.Info($"Thunderbird iniciado. PID={process.Id}; versão={options.Installation.Version}; arquitetura={options.Installation.Architecture}.");

            if (options.TryUiAutomation)
            {
                var automationTimeout = TimeSpan.FromMinutes(Math.Min(5, Math.Max(1, options.Timeout.TotalMinutes / 4)));
                var selected = ThunderbirdUiAutomation.TrySelectFolderAsync(
                        process,
                        safeMailbox,
                        automationTimeout,
                        message => log.Info(message),
                        cancellationToken)
                    .GetAwaiter().GetResult();

                if (!selected)
                {
                    warning = $"A seleção automática da pasta '{safeMailbox}' não foi concluída. Se a janela permanecer aberta, selecione a pasta uma vez para iniciar a indexação.";
                    progress?.Report(new ThunderbirdIndexProgress("Ação necessária", warning));
                }
            }
            else
            {
                progress?.Report(new ThunderbirdIndexProgress(
                    "Ação necessária",
                    $"Na janela isolada do Thunderbird, selecione a pasta '{safeMailbox}' uma vez para iniciar a reconstrução do índice."));
            }

            var msfPath = temporaryMbox + ".msf";
            var panoramaPath = Path.Combine(temporaryProfile, "panorama.sqlite");
            var completionWarning = WaitForIndex(
                msfPath,
                panoramaPath,
                expectedMessages,
                options.Timeout,
                options.StablePeriod,
                process,
                progress,
                log,
                cancellationToken);

            if (options.CloseThunderbirdAfterIndex)
                CloseIsolatedProcess(process, log);

            process = null;
            var finalMbox = Path.Combine(outputDirectory, safeMailbox);
            TransferFile(temporaryMbox, finalMbox, options.KeepTemporaryProfile);

            string? finalMsf = null;
            string? finalPanorama = null;
            if (File.Exists(msfPath))
            {
                finalMsf = finalMbox + ".msf";
                TransferFile(msfPath, finalMsf, options.KeepTemporaryProfile);
            }
            if (File.Exists(panoramaPath))
            {
                finalPanorama = Path.Combine(outputDirectory, "panorama.sqlite");
                TransferFile(panoramaPath, finalPanorama, options.KeepTemporaryProfile);
            }

            var validation = finalMsf is null
                ? new MsfValidationResult { Path = finalMbox + ".msf", Exists = false, IsValidMork = false, SizeBytes = 0, Error = "Índice MSF não produzido." }
                : MsfValidator.Validate(finalMsf);

            if (!validation.IsValidMork && finalPanorama is null)
                throw new InvalidDataException("O Thunderbird encerrou o processamento sem produzir um índice MSF Mork válido nem panorama.sqlite.");

            var result = new ThunderbirdIndexResult
            {
                OutputDirectory = outputDirectory,
                MboxPath = finalMbox,
                MsfPath = finalMsf,
                TemporaryProfilePath = temporaryProfile,
                MsfCreated = finalMsf is not null,
                MsfValidated = validation.IsValidMork,
                ExpectedMessages = expectedMessages,
                IndexedMessages = validation.IndexedMessages,
                ThunderbirdVersion = options.Installation.Version,
                ThunderbirdArchitecture = options.Installation.Architecture,
                Duration = DateTimeOffset.Now - started,
                LogPath = logPath,
                Warning = CombineWarnings(warning, completionWarning, finalPanorama is not null && finalMsf is null
                    ? "A instalação utilizou Panorama/SQLite. O arquivo panorama.sqlite foi preservado, pois não existe um .msf autônomo equivalente nesse modo."
                    : null)
            };

            var json = JsonSerializer.Serialize(new
            {
                aplicacao = "Thunderbird Recovery Suite",
                versao = ApplicationVersion.Current,
                criado_em = DateTimeOffset.Now,
                tecnologia_indice = finalMsf is not null ? "Mork/MSF" : "Panorama/SQLite",
                origem = options.Source.DisplayName,
                thunderbird = new
                {
                    options.Installation.ExecutablePath,
                    options.Installation.Version,
                    arquitetura = options.Installation.Architecture.ToString()
                },
                mbox = Path.GetFileName(finalMbox),
                msf = finalMsf is null ? null : Path.GetFileName(finalMsf),
                panorama = finalPanorama is null ? null : Path.GetFileName(finalPanorama),
                mensagens_esperadas = expectedMessages,
                mensagens_indice = validation.IndexedMessages,
                msf_valido = validation.IsValidMork,
                duracao = result.Duration,
                aviso = result.Warning
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(outputDirectory, "manifesto_indexacao.json"), json, new UTF8Encoding(false));
            log.Info("Indexação assistida concluída.");
            progress?.Report(new ThunderbirdIndexProgress("Concluído", $"Índice entregue em {outputDirectory}.", Messages: expectedMessages));
            return result;
        }
        finally
        {
            if (process is not null)
                CloseIsolatedProcess(process, log);

            if (!options.KeepTemporaryProfile)
                TryDeleteDirectory(temporaryProfile, log);
        }
    }

    private static string? WaitForIndex(
        string msfPath,
        string panoramaPath,
        long expectedMessages,
        TimeSpan timeout,
        TimeSpan stablePeriod,
        Process process,
        IProgress<ThunderbirdIndexProgress>? progress,
        RecoveryLogger log,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;
        var lastMsfLength = -1L;
        var lastMsfWrite = DateTime.MinValue;
        var stableSince = DateTime.UtcNow;
        DateTime? mismatchStableSince = null;
        var mismatchGrace = TimeSpan.FromMinutes(2);
        var lastReport = DateTime.MinValue;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            process.Refresh();
            if (process.HasExited)
                throw new InvalidOperationException($"O Thunderbird foi encerrado antes da conclusão da indexação. Código: {process.ExitCode}.");

            var msf = MsfValidator.Validate(msfPath);
            var panoramaExists = SqliteFileValidator.IsValid(panoramaPath);
            var length = msf.Exists ? msf.SizeBytes : panoramaExists ? new FileInfo(panoramaPath).Length : 0;
            var write = msf.Exists ? File.GetLastWriteTimeUtc(msfPath) : panoramaExists ? File.GetLastWriteTimeUtc(panoramaPath) : DateTime.MinValue;

            if (length != lastMsfLength || write != lastMsfWrite)
            {
                lastMsfLength = length;
                lastMsfWrite = write;
                stableSince = DateTime.UtcNow;
                mismatchStableSince = null;
            }

            if ((msf.IsValidMork || panoramaExists) && DateTime.UtcNow - stableSince >= stablePeriod)
            {
                var countsMatch = panoramaExists || !msf.IndexedMessages.HasValue || expectedMessages <= 0 || msf.IndexedMessages.Value == expectedMessages;
                if (countsMatch)
                {
                    log.Info($"Índice estabilizado. MSF válido={msf.IsValidMork}; Panorama={panoramaExists}; tamanho={length:N0} bytes.");
                    return null;
                }

                mismatchStableSince ??= DateTime.UtcNow;
                var indexed = msf.IndexedMessages!.Value;
                if (DateTime.UtcNow - mismatchStableSince.Value >= mismatchGrace)
                {
                    var countWarning = $"O índice estabilizou com {indexed:N0} mensagens, enquanto o MBOX possui {expectedMessages:N0} separadores reconhecidos. Revise o relatório antes de substituir uma caixa em produção.";
                    log.Info($"Índice estabilizado com divergência persistente. MSF={indexed:N0}; MBOX={expectedMessages:N0}; tamanho={length:N0} bytes.");
                    return countWarning;
                }

                progress?.Report(new ThunderbirdIndexProgress(
                    "Validando contagem",
                    $"O MSF está estável, mas ainda informa {indexed:N0} de {expectedMessages:N0} mensagens. Aguardando confirmação por até {mismatchGrace.TotalMinutes:N0} minutos.",
                    Messages: expectedMessages));
            }

            if (DateTime.UtcNow - lastReport >= TimeSpan.FromSeconds(2))
            {
                var elapsed = timeout - (deadline - DateTime.UtcNow);
                progress?.Report(new ThunderbirdIndexProgress(
                    "Indexando",
                    $"Aguardando o Thunderbird reconstruir o índice — decorrido {elapsed:hh\\:mm\\:ss}; índice {SizeFormatter.Format(length)}.",
                    Messages: expectedMessages));
                lastReport = DateTime.UtcNow;
            }

            Thread.Sleep(1000);
        }

        throw new TimeoutException($"O índice não estabilizou dentro do limite de {timeout}. O perfil temporário pode ser preservado para diagnóstico habilitando essa opção.");
    }

    private static long CopyMboxAndCount(
        MboxSourceSelection source,
        string destination,
        bool keepTemporaryProfile,
        IProgress<ThunderbirdIndexProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var handle = MboxSourceService.Open(source);
        ValidateFreeSpace(destination, handle.Length, keepTemporaryProfile);
        using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, FileOptions.SequentialScan);
        var consumer = new CopyConsumer(output);
        var parserProgress = new Progress<MboxParseProgress>(value =>
            progress?.Report(new ThunderbirdIndexProgress("Copiando", "Preparando caixa para indexação.", value.ProcessedBytes, value.TotalBytes, value.Messages)));
        var result = MboxStreamParser.Parse(handle.Stream, handle.Length, consumer, parserProgress, cancellationToken);
        output.Flush(true);
        if (result.Messages == 0)
            throw new InvalidDataException("Nenhuma mensagem MBOX válida foi reconhecida na origem selecionada.");
        return result.Messages;
    }


    private static void ValidateFreeSpace(string destinationPath, long inputBytes, bool keepTemporaryProfile)
    {
        if (inputBytes <= 0) return;
        var root = Path.GetPathRoot(Path.GetFullPath(destinationPath))
            ?? throw new IOException("Não foi possível identificar a unidade da indexação.");
        var drive = new DriveInfo(root);
        if (!drive.IsReady) throw new IOException($"A unidade {drive.Name} não está pronta.");
        var multiplier = keepTemporaryProfile ? 2.15 : 1.20;
        var required = checked((long)Math.Ceiling(inputBytes * multiplier));
        if (drive.AvailableFreeSpace < required)
        {
            throw new IOException($"Espaço insuficiente para a indexação assistida em {drive.Name}. " +
                                  $"Disponível: {SizeFormatter.Format(drive.AvailableFreeSpace)}; " +
                                  $"recomendado: {SizeFormatter.Format(required)}.");
        }
    }

    private static void TransferFile(string source, string destination, bool preserveSource)
    {
        if (preserveSource) File.Copy(source, destination, overwrite: false);
        else File.Move(source, destination);
    }
    private sealed class CopyConsumer(Stream output) : IMboxStreamConsumer
    {
        public void OnPreamble(ReadOnlySpan<byte> data, long offset) => output.Write(data);
        public void OnMessageStart(ReadOnlySpan<byte> separatorLine, long offset, long messageNumber) => output.Write(separatorLine);
        public void OnMessageData(ReadOnlySpan<byte> data, long offset) => output.Write(data);
        public void OnMessageEnd(long offset) { }
    }

    private static void WriteProfileConfiguration(string profilePath, string localFolders)
    {
        Directory.CreateDirectory(profilePath);
        var escapedDirectory = EscapeJs(localFolders);
        var prefs = $$"""
user_pref("browser.shell.checkDefaultBrowser", false);
user_pref("datareporting.healthreport.uploadEnabled", false);
user_pref("datareporting.policy.dataSubmissionEnabled", false);
user_pref("mail.account.account1.identities", "id1");
user_pref("mail.account.account1.server", "server1");
user_pref("mail.accountmanager.accounts", "account1");
user_pref("mail.accountmanager.defaultaccount", "account1");
user_pref("mail.accountmanager.localfoldersserver", "server1");
user_pref("mail.identity.id1.fullName", "Thunderbird Recovery Suite");
user_pref("mail.identity.id1.useremail", "recovery@invalid.local");
user_pref("mail.identity.id1.valid", true);
user_pref("mail.panorama.enabled", false);
user_pref("mail.provider.suppress_dialog_on_startup", true);
user_pref("mail.rights.override", true);
user_pref("mail.server.server1.directory", "{{escapedDirectory}}");
user_pref("mail.server.server1.hostname", "Local Folders");
user_pref("mail.server.server1.name", "Pastas Locais");
user_pref("mail.server.server1.storeContractID", "@mozilla.org/msgstore/berkeleystore;1");
user_pref("mail.server.server1.type", "none");
user_pref("mail.server.server1.userName", "nobody");
user_pref("mailnews.database.global.indexer.enabled", false);
user_pref("mailnews.start_page.enabled", false);
user_pref("mailnews.start_page.override_url", "about:blank");
user_pref("mailnews.start_page.url", "about:blank");
user_pref("mail.spotlight.firstRunDone", true);
user_pref("mail.winsearch.firstRunDone", true);
user_pref("offline.startup_state", 4);
user_pref("toolkit.telemetry.reportingpolicy.firstRun", false);
""";
        File.WriteAllText(Path.Combine(profilePath, "prefs.js"), prefs, new UTF8Encoding(false));
    }


    private static Process ResolveThunderbirdProcess(
        Process launchedProcess,
        string executablePath,
        DateTime launchedAtUtc,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                launchedProcess.Refresh();
                if (!launchedProcess.HasExited) return launchedProcess;
            }
            catch
            {
                // Procura o processo filho iniciado pelo launcher.
            }

            var candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executablePath))
                .Select(process => new { Process = process, Matches = ProcessMatches(process, executablePath, launchedAtUtc) })
                .Where(item => item.Matches)
                .Select(item => item.Process)
                .OrderByDescending(process => SafeStartTime(process))
                .ToList();
            if (candidates.Count > 0)
            {
                launchedProcess.Dispose();
                foreach (var extra in candidates.Skip(1)) extra.Dispose();
                return candidates[0];
            }

            Thread.Sleep(500);
        }

        return launchedProcess;
    }

    private static bool ProcessMatches(Process process, string executablePath, DateTime launchedAtUtc)
    {
        try
        {
            if (process.StartTime.ToUniversalTime() < launchedAtUtc.AddSeconds(-5)) return false;
            try
            {
                var modulePath = process.MainModule?.FileName;
                return string.IsNullOrWhiteSpace(modulePath) ||
                       string.Equals(modulePath, executablePath, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // A leitura de MainModule pode falhar entre processos de arquiteturas diferentes.
                return true;
            }
        }
        catch
        {
            process.Dispose();
            return false;
        }
    }

    private static DateTime SafeStartTime(Process process)
    {
        try { return process.StartTime.ToUniversalTime(); }
        catch { return DateTime.MinValue; }
    }
    private static string CreateUniqueOutput(string root, string mailboxName)
    {
        Directory.CreateDirectory(root);
        var baseName = $"Indice_{mailboxName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        var result = Path.Combine(root, baseName);
        var number = 2;
        while (Directory.Exists(result)) result = Path.Combine(root, $"{baseName}_{number++:00}");
        Directory.CreateDirectory(result);
        return result;
    }

    private static string EscapeJs(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void CloseIsolatedProcess(Process process, RecoveryLogger log)
    {
        try
        {
            if (process.HasExited) return;
            log.Info("Encerrando a instância isolada do Thunderbird.");
            process.CloseMainWindow();
            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch (Exception exception)
        {
            log.Info($"Aviso ao encerrar Thunderbird: {exception.Message}");
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void TryDeleteDirectory(string path, RecoveryLogger log)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (attempt < 5)
            {
                log.Info($"Tentativa {attempt} de remover perfil temporário falhou: {exception.Message}");
                Thread.Sleep(1000 * attempt);
            }
        }
    }

    private static string? CombineWarnings(params string?[] warnings)
    {
        var values = warnings.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray();
        return values.Length == 0 ? null : string.Join(" ", values);
    }
}
