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
        logger.Info($"Tamanho alvo por parte: {SizeFormatter.Format(options.TargetChunkBytes)}");

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

            chunks = new ChunkWriter(options.OutputDirectory, options.TargetChunkBytes, options.MailboxName, logger);
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
                TargetChunkBytes = options.TargetChunkBytes,
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

            WriteImportInstructions(options.OutputDirectory, options.MailboxName, chunks.Parts.Count);
            logger.Info($"SHA-256 da entrada descompactada: {inputHash}");
            logger.Info($"Recuperação concluída: {chunks.Parts.Count} partes e {totalMessages:N0} mensagens estimadas.");

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
        if (lineStart.StartsWith("From - "u8)) return true;
        var length = Math.Min(lineStart.Length, ProbeLimit);
        var text = Encoding.ASCII.GetString(lineStart[..length]);
        return SeparatorRegex().IsMatch(text);
    }

    private static void WriteImportInstructions(string outputDirectory, string mailboxName, int parts)
    {
        var path = Path.Combine(outputDirectory, "COMO_IMPORTAR_NO_THUNDERBIRD.txt");
        var safeMailboxName = MailboxNameResolver.FromSource(mailboxName);
        var text = $"""
RECUPERAÇÃO CONCLUÍDA

Foram geradas {parts} partes MBOX sem extensão.

IMPORTAÇÃO RECOMENDADA

1. Crie um perfil separado no Thunderbird para conferência.
2. Abra o Thunderbird uma vez e feche-o completamente.
3. Localize a pasta do perfil e entre em:
   Mail\Local Folders\
4. Copie para essa pasta os arquivos:
   {safeMailboxName}_Recuperada_001
   {safeMailboxName}_Recuperada_002
   ...
5. Não copie arquivos com extensão .partial.
6. Abra o Thunderbird e aguarde a criação dos índices .msf.
7. Confira as mensagens antes de excluir qualquer backup.

SEGURANÇA

- O arquivo de origem foi aberto somente para leitura.
- Não compacte nem altere o arquivo MBOX original durante a recuperação.
- Preserve o arquivo original e qualquer backup existente até validar todas as partes.
- Consulte manifesto_recuperacao.json e recuperacao.log.
""";
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }
}
