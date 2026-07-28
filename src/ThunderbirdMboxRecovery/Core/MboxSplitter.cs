using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThunderbirdMboxRecovery.Core;

public sealed partial class MboxSplitter
{
    private const int ReadBufferSize = 4 * 1024 * 1024;
    private const int ProbeLimit = 512;
    private const long ProgressInterval = 64L * 1024 * 1024;

    [GeneratedRegex(@"^From\s+(?:-|\S+)\s+(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    public RecoveryResult Execute(
        RecoveryOptions options,
        IProgress<RecoveryProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputDirectory);
        using var logger = new RecoveryLogger(options.OutputDirectory);
        logger.Info("Início da recuperação.");
        logger.Info($"Origem: {options.SourcePath}");
        if (!string.IsNullOrWhiteSpace(options.ArchiveEntryKey))
            logger.Info($"Entrada selecionada no arquivo compactado: {options.ArchiveEntryKey}");
        logger.Info($"Caixa MBOX identificada como: {options.MailboxName}");
        logger.Info($"Destino: {options.OutputDirectory}");
        logger.Info(options.SplitOutput
            ? $"Modo de saída: fracionado em partes de aproximadamente {SizeFormatter.Format(options.TargetChunkBytes)}."
            : "Modo de saída: arquivo único, sem fracionamento.");
        logger.Info(
            "O aplicativo não criará um .msf artificial. O Thunderbird reconstruirá o índice válido após a importação.");

        MboxReadHandle? handle = null;
        FileStream? directStream = null;
        Stream? input = null;
        ChunkWriter? chunks = null;
        FileStream? prefix = null;
        string? prefixPartialPath = null;
        string? prefixFinalPath = null;

        try
        {
            if (ArchiveService.IsArchive(options.SourcePath))
            {
                progress?.Report(new RecoveryProgress("Abrindo arquivo compactado", 0, options.ExpectedInputBytes, 0, 0, null));
                handle = ArchiveService.OpenSelectedEntry(options.SourcePath, options.ArchiveEntryKey!, options.ArchivePassword);
                input = handle.Stream;
                logger.Info($"Fluxo descompactado aberto: {handle.DisplayName} ({SizeFormatter.Format(handle.Length)})." );
            }
            else
            {
                directStream = new FileStream(
                    options.SourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    ReadBufferSize,
                    FileOptions.SequentialScan);
                input = directStream;
                logger.Info($"MBOX aberto somente para leitura ({SizeFormatter.Format(directStream.Length)})." );
            }

            var totalBytes = options.ExpectedInputBytes > 0
                ? options.ExpectedInputBytes
                : handle?.Length ?? directStream?.Length ?? 0;

            chunks = new ChunkWriter(
                options.OutputDirectory,
                options.TargetChunkBytes,
                options.MailboxName,
                options.SplitOutput,
                logger);
            prefixPartialPath = Path.Combine(options.OutputDirectory, "prefixo_nao_reconhecido.bin.partial");
            prefixFinalPath = Path.Combine(options.OutputDirectory, "prefixo_nao_reconhecido.bin");

            using var sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var readBuffer = new byte[ReadBufferSize];
            var probe = new byte[ProbeLimit];
            var probeLength = 0;
            var awaitingLineDecision = true;
            var foundFirstMessage = false;
            long prefixBytes = 0;
            long processed = 0;
            long totalMessages = 0;
            long nextProgress = ProgressInterval;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = input.Read(readBuffer, 0, readBuffer.Length);
                if (read == 0) break;

                sourceHash.AppendData(readBuffer, 0, read);
                processed += read;
                var offset = 0;

                while (offset < read)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (awaitingLineDecision)
                    {
                        var newlineIndex = Array.IndexOf(readBuffer, (byte)'\n', offset, read - offset);
                        var availableToNewline = newlineIndex >= 0 ? newlineIndex - offset + 1 : read - offset;
                        var copyLength = Math.Min(ProbeLimit - probeLength, availableToNewline);
                        Buffer.BlockCopy(readBuffer, offset, probe, probeLength, copyLength);
                        probeLength += copyLength;
                        offset += copyLength;

                        var reachedNewline = probeLength > 0 && probe[probeLength - 1] == (byte)'\n';
                        var probeFull = probeLength == ProbeLimit;
                        if (!reachedNewline && !probeFull) continue;

                        var isSeparator = IsSeparator(probe.AsSpan(0, probeLength));
                        if (isSeparator)
                        {
                            if (!foundFirstMessage)
                            {
                                foundFirstMessage = true;
                                if (prefix is not null)
                                {
                                    prefix.Flush(true);
                                    prefix.Dispose();
                                    prefix = null;
                                    File.Move(prefixPartialPath, prefixFinalPath, false);
                                    logger.Warning($"Foram preservados {SizeFormatter.Format(prefixBytes)} antes da primeira mensagem reconhecida.");
                                }
                            }

                            chunks.StartMessage(probe.AsSpan(0, probeLength));
                            totalMessages++;
                        }
                        else if (foundFirstMessage)
                        {
                            chunks.Write(probe.AsSpan(0, probeLength));
                        }
                        else
                        {
                            prefix ??= new FileStream(prefixPartialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
                            prefix.Write(probe, 0, probeLength);
                            prefixBytes += probeLength;
                        }

                        if (reachedNewline)
                        {
                            probeLength = 0;
                            awaitingLineDecision = true;
                        }
                        else
                        {
                            probeLength = 0;
                            awaitingLineDecision = false;
                        }
                    }
                    else
                    {
                        var newlineIndex = Array.IndexOf(readBuffer, (byte)'\n', offset, read - offset);
                        var segmentLength = newlineIndex >= 0 ? newlineIndex - offset + 1 : read - offset;
                        var segment = readBuffer.AsSpan(offset, segmentLength);

                        if (foundFirstMessage)
                        {
                            chunks.Write(segment);
                        }
                        else
                        {
                            prefix ??= new FileStream(prefixPartialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
                            prefix.Write(segment);
                            prefixBytes += segmentLength;
                        }

                        offset += segmentLength;
                        if (newlineIndex >= 0)
                        {
                            awaitingLineDecision = true;
                            probeLength = 0;
                        }
                    }
                }

                if (processed >= nextProgress || processed == totalBytes)
                {
                    progress?.Report(new RecoveryProgress(
                        "Recuperando mensagens",
                        processed,
                        totalBytes,
                        totalMessages,
                        chunks.CompletedParts,
                        chunks.CurrentFileName,
                        $"{SizeFormatter.Format(processed)} processados"));
                    nextProgress = processed + ProgressInterval;
                }
            }

            if (probeLength > 0)
            {
                var isSeparator = awaitingLineDecision && IsSeparator(probe.AsSpan(0, probeLength));
                if (isSeparator)
                {
                    if (!foundFirstMessage)
                    {
                        foundFirstMessage = true;
                        if (prefix is not null)
                        {
                            prefix.Flush(true);
                            prefix.Dispose();
                            prefix = null;
                            File.Move(prefixPartialPath, prefixFinalPath, false);
                        }
                    }
                    chunks.StartMessage(probe.AsSpan(0, probeLength));
                    totalMessages++;
                }
                else if (foundFirstMessage)
                {
                    chunks.Write(probe.AsSpan(0, probeLength));
                }
                else
                {
                    prefix ??= new FileStream(prefixPartialPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
                    prefix.Write(probe, 0, probeLength);
                    prefixBytes += probeLength;
                }
            }

            if (prefix is not null)
            {
                prefix.Flush(true);
                prefix.Dispose();
                prefix = null;
                File.Move(prefixPartialPath, prefixFinalPath, false);
            }

            if (!foundFirstMessage)
            {
                chunks.Abort();
                throw new InvalidDataException(
                    "Nenhum separador MBOX reconhecido foi encontrado. O conteúdo descompactado foi preservado em prefixo_nao_reconhecido.bin para análise.");
            }

            chunks.Complete();
            var inputHash = Convert.ToHexString(sourceHash.GetHashAndReset()).ToLowerInvariant();
            var manifest = new RecoveryManifest
            {
                Source = Path.GetFullPath(options.SourcePath),
                ArchiveEntry = options.ArchiveEntryKey,
                MailboxName = options.MailboxName,
                InputSizeBytes = processed,
                InputSha256 = inputHash,
                SplitOutput = options.SplitOutput,
                CreatedArtificialMsf = false,
                TargetChunkBytes = options.SplitOutput ? options.TargetChunkBytes : null,
                PrefixBytes = prefixBytes,
                EstimatedMessages = totalMessages,
                TotalParts = chunks.Parts.Count,
                Parts = chunks.Parts
            };

            var manifestPath = Path.Combine(options.OutputDirectory, "manifesto_recuperacao.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            }), new UTF8Encoding(false));

            WriteImportInstructions(
                options.OutputDirectory,
                options.MailboxName,
                chunks.Parts,
                options.SplitOutput);
            logger.Info($"SHA-256 da entrada descompactada: {inputHash}");
            logger.Info($"Recuperação concluída: {chunks.Parts.Count} arquivo(s) MBOX e {totalMessages:N0} mensagens estimadas.");

            progress?.Report(new RecoveryProgress(
                "Concluído",
                processed,
                processed,
                totalMessages,
                chunks.Parts.Count,
                null,
                "Recuperação finalizada com sucesso."));

            return new RecoveryResult
            {
                OutputDirectory = options.OutputDirectory,
                MailboxName = options.MailboxName,
                InputBytes = processed,
                InputSha256 = inputHash,
                PrefixBytes = prefixBytes,
                TotalMessages = totalMessages,
                Parts = chunks.Parts,
                ManifestPath = manifestPath,
                LogPath = logger.LogPath
            };
        }
        catch (OperationCanceledException)
        {
            logger.Warning("Operação cancelada pelo usuário. Arquivos .partial foram preservados e não devem ser importados.");
            chunks?.Abort();
            throw;
        }
        catch (Exception ex)
        {
            logger.Error(ex.ToString());
            chunks?.Abort();
            throw;
        }
        finally
        {
            prefix?.Dispose();
            directStream?.Dispose();
            handle?.Dispose();
            chunks?.Dispose();
        }
    }

    private static bool IsSeparator(ReadOnlySpan<byte> lineStart)
    {
        var length = Math.Min(lineStart.Length, ProbeLimit);
        var text = Encoding.ASCII.GetString(lineStart[..length]);
        return SeparatorRegex().IsMatch(text);
    }

    private static void WriteImportInstructions(
        string outputDirectory,
        string mailboxName,
        IReadOnlyList<ChunkManifest> outputs,
        bool splitOutput)
    {
        var path = Path.Combine(outputDirectory, "COMO_IMPORTAR_NO_THUNDERBIRD.txt");
        var safeMailboxName = MailboxNameResolver.FromSource(mailboxName);
        var outputFiles = string.Join(
            Environment.NewLine,
            outputs.Select(item => $"   {item.FileName}"));

        var modeDescription = splitOutput
            ? $"Foram gerados {outputs.Count} arquivos MBOX fracionados."
            : $"Foi gerado um único arquivo MBOX: {safeMailboxName}_Recuperada.";

        var text = $"""
RECUPERAÇÃO CONCLUÍDA

{modeDescription}

ARQUIVOS MBOX GERADOS

{outputFiles}

IMPORTAÇÃO CORRETA NO THUNDERBIRD

1. Feche completamente o Thunderbird e confirme no Gerenciador de Tarefas que thunderbird.exe não está em execução.
2. Abra a pasta do perfil e entre em:
   Mail\Local Folders\
3. Copie somente os arquivos MBOX listados acima.
4. Não copie para a pasta da conta POP/IMAP e não coloque dentro de ImapMail.
5. Não crie nem copie um arquivo .msf vazio. Se existir um .msf com o mesmo nome, exclua somente o .msf.
6. Abra o Thunderbird e aguarde a criação/reconstrução do índice .msf.
7. Em caixas muito grandes, a reconstrução pode permanecer em segundo plano por bastante tempo.
8. Enquanto o Thunderbird informar que outra operação está usando a pasta, não use Reparar pasta.
9. Confira as mensagens antes de excluir qualquer backup.

VALIDAÇÃO DE CAMPO

Em teste real com uma caixa de aproximadamente 27,4 GiB, recuperada pela versão 1.2.0,
o Thunderbird criou/reconstruiu o arquivo .msf sozinho depois de permanecer aberto por algum tempo.
Esse comportamento confirma que a ausência inicial do .msf não significa falha imediata: é necessário
aguardar o término da indexação e evitar iniciar reparos concorrentes.

LIMITAÇÃO DESTA LINHA 1.3

Esta versão reconstrói o arquivo MBOX e seus limites de mensagens, mas não altera flags internas
de mensagens marcadas como excluídas ou expurgadas. Essa recuperação lógica é implementada na linha 1.4.

SEGURANÇA

- O arquivo de origem foi aberto somente para leitura.
- Não compacte nem altere o arquivo MBOX original durante a recuperação.
- Preserve o arquivo original e qualquer backup existente até validar a recuperação.
- Consulte manifesto_recuperacao.json e recuperacao.log.
""";

        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
}
