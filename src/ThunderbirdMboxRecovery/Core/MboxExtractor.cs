using System.Text;

namespace ThunderbirdMboxRecovery.Core;

public static class MboxExtractor
{
    public static Task<MessageExtractionResult> ExtractAsync(
        MessageExtractionOptions options,
        IProgress<MboxParseProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        return Task.Run(() =>
        {
            Directory.CreateDirectory(options.OutputDirectory);
            using var handle = MboxSourceService.Open(options.Source);
            using var consumer = new ExtractionConsumer(options);
            var parsed = MboxStreamParser.Parse(
                handle.Stream,
                handle.Length,
                consumer,
                progress,
                cancellationToken);

            return consumer.Complete(parsed.Messages);
        }, cancellationToken);
    }

    private sealed class ExtractionConsumer : IMboxStreamConsumer, IDisposable
    {
        private const int MaximumHeaderBytes = 32 * 1024 * 1024;
        private readonly MessageExtractionOptions _options;
        private readonly MemoryStream _headerBytes = new();
        private readonly List<string> _files = [];
        private readonly StreamWriter? _csv;
        private FileStream? _output;
        private string? _partialPath;
        private string? _finalPath;
        private long _messageNumber;
        private long _messageStart;
        private bool _inHeaders;
        private bool _headerTerminated;
        private bool _selected;
        private MboxMessageInfo? _currentInfo;
        private long _extracted;
        private long _extractedBytes;
        private bool _disposed;

        public ExtractionConsumer(MessageExtractionOptions options)
        {
            _options = options;
            if (options.GenerateCsvIndex)
            {
                var csvPath = Path.Combine(options.OutputDirectory, "indice_mensagens_extraidas.csv");
                _csv = new StreamWriter(csvPath, false, new UTF8Encoding(true));
                _csv.WriteLine("Numero;Arquivo;Data;De;Para;Assunto;MessageId;Excluida;Anexo;TamanhoBytes");
            }
        }

        public void OnPreamble(ReadOnlySpan<byte> data, long offset)
        {
            // Bytes anteriores à primeira mensagem não pertencem a um EML.
        }

        public void OnMessageStart(ReadOnlySpan<byte> separatorLine, long offset, long messageNumber)
        {
            FinalizeCurrent(offset);
            _messageNumber = messageNumber;
            _messageStart = offset;
            _inHeaders = true;
            _headerTerminated = false;
            _selected = false;
            _currentInfo = null;
            _headerBytes.SetLength(0);
        }

        public void OnMessageData(ReadOnlySpan<byte> data, long offset)
        {
            if (_inHeaders)
            {
                if (_headerBytes.Length + data.Length > MaximumHeaderBytes)
                    throw new InvalidDataException(
                        $"O cabeçalho da mensagem {_messageNumber:N0} ultrapassou {SizeFormatter.Format(MaximumHeaderBytes)}.");

                _headerBytes.Write(data);
                if (IsBlankLine(data))
                {
                    _inHeaders = false;
                    _headerTerminated = true;
                    SelectAndOpenOutput(offset + data.Length);
                }
                return;
            }

            if (_selected && _output is not null)
                _output.Write(data);
        }

        public void OnMessageEnd(long offset) => FinalizeCurrent(offset);

        public MessageExtractionResult Complete(long scannedMessages)
        {
            FinalizeCurrent(_messageStart);
            _csv?.Flush();
            var csvPath = _csv is null
                ? null
                : Path.Combine(_options.OutputDirectory, "indice_mensagens_extraidas.csv");

            return new MessageExtractionResult
            {
                OutputDirectory = _options.OutputDirectory,
                ScannedMessages = scannedMessages,
                ExtractedMessages = _extracted,
                ExtractedBytes = _extractedBytes,
                CsvIndexPath = csvPath,
                Files = _files
            };
        }

        private void SelectAndOpenOutput(long provisionalEnd)
        {
            var parsed = MboxHeaderParser.Parse(
                _headerBytes.GetBuffer().AsSpan(0, checked((int)_headerBytes.Length)));

            _currentInfo = new MboxMessageInfo
            {
                Number = _messageNumber,
                StartOffset = _messageStart,
                EndOffset = provisionalEnd,
                Subject = parsed.Subject,
                From = parsed.From,
                To = parsed.To,
                MessageId = parsed.MessageId,
                Date = parsed.Date,
                MozillaStatus = parsed.MozillaStatus,
                MozillaStatus2 = parsed.MozillaStatus2,
                IsDeleted = parsed.IsDeleted,
                HasAttachment = parsed.HasAttachment,
                HeaderTerminated = _headerTerminated,
                HeaderCount = parsed.Headers.Count
            };

            _selected = _options.Filter.Matches(_currentInfo);
            if (!_selected)
                return;

            var fileName = BuildFileName(_currentInfo);
            _finalPath = EnsureUniquePath(Path.Combine(_options.OutputDirectory, fileName));
            _partialPath = _finalPath + ".partial";
            _output = new FileStream(
                _partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read,
                1024 * 1024,
                FileOptions.SequentialScan);

            if (_options.PreserveMozillaStatusHeaders)
            {
                _output.Write(_headerBytes.GetBuffer(), 0, checked((int)_headerBytes.Length));
            }
            else
            {
                var sanitized = RemoveMozillaStatusHeaders(
                    _headerBytes.GetBuffer().AsSpan(0, checked((int)_headerBytes.Length)));
                _output.Write(sanitized);
            }
        }

        private void FinalizeCurrent(long endOffset)
        {
            if (_messageNumber <= 0)
                return;

            if (_inHeaders)
            {
                _headerTerminated = false;
                SelectAndOpenOutput(endOffset);
                _inHeaders = false;
            }

            if (_currentInfo is not null)
            {
                _currentInfo = new MboxMessageInfo
                {
                    Number = _currentInfo.Number,
                    StartOffset = _currentInfo.StartOffset,
                    EndOffset = endOffset,
                    Subject = _currentInfo.Subject,
                    From = _currentInfo.From,
                    To = _currentInfo.To,
                    MessageId = _currentInfo.MessageId,
                    Date = _currentInfo.Date,
                    MozillaStatus = _currentInfo.MozillaStatus,
                    MozillaStatus2 = _currentInfo.MozillaStatus2,
                    IsDeleted = _currentInfo.IsDeleted,
                    HasAttachment = _currentInfo.HasAttachment,
                    HeaderTerminated = _currentInfo.HeaderTerminated,
                    HeaderCount = _currentInfo.HeaderCount
                };
            }

            if (_selected && _output is not null && _partialPath is not null && _finalPath is not null)
            {
                _output.Flush(true);
                _output.Dispose();
                _output = null;
                File.Move(_partialPath, _finalPath, false);
                var fileInfo = new FileInfo(_finalPath);
                _extracted++;
                _extractedBytes += fileInfo.Length;
                _files.Add(_finalPath);
                WriteCsv(_currentInfo!, Path.GetFileName(_finalPath), fileInfo.Length);
            }

            _output?.Dispose();
            _output = null;
            _partialPath = null;
            _finalPath = null;
            _selected = false;
            _messageNumber = 0;
            _currentInfo = null;
            _headerBytes.SetLength(0);
        }

        private void WriteCsv(MboxMessageInfo info, string fileName, long size)
        {
            if (_csv is null)
                return;

            _csv.WriteLine(string.Join(';',
                info.Number,
                Csv(fileName),
                Csv(info.Date?.ToString("O") ?? string.Empty),
                Csv(info.From),
                Csv(info.To),
                Csv(info.Subject),
                Csv(info.MessageId),
                info.IsDeleted ? "Sim" : "Nao",
                info.HasAttachment ? "Sim" : "Nao",
                size));
        }

        private static string BuildFileName(MboxMessageInfo info)
        {
            var date = info.Date?.ToString("yyyyMMdd_HHmmss") ?? "sem_data";
            var subject = SafeFileName(string.IsNullOrWhiteSpace(info.Subject) ? "sem_assunto" : info.Subject);
            if (subject.Length > 90)
                subject = subject[..90].TrimEnd();
            return $"{info.Number:000000}_{date}_{subject}.eml";
        }

        private static string SafeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars().ToHashSet();
            var builder = new StringBuilder(value.Length);
            foreach (var character in value)
            {
                if (invalid.Contains(character) || char.IsControl(character))
                    builder.Append('_');
                else
                    builder.Append(character);
            }

            var result = builder.ToString().Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(result) ? "mensagem" : result;
        }

        private static string EnsureUniquePath(string path)
        {
            if (!File.Exists(path))
                return path;

            var directory = Path.GetDirectoryName(path)!;
            var name = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            var suffix = 2;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, $"{name}_{suffix:00}{extension}");
                suffix++;
            } while (File.Exists(candidate));

            return candidate;
        }

        private static byte[] RemoveMozillaStatusHeaders(ReadOnlySpan<byte> headers)
        {
            using var output = new MemoryStream(headers.Length);
            var start = 0;
            var skipContinuation = false;
            while (start < headers.Length)
            {
                var relativeNewline = headers[start..].IndexOf((byte)'\n');
                var length = relativeNewline >= 0 ? relativeNewline + 1 : headers.Length - start;
                var line = headers.Slice(start, length);
                var contentLength = line.Length;
                while (contentLength > 0 && (line[contentLength - 1] == (byte)'\r' || line[contentLength - 1] == (byte)'\n'))
                    contentLength--;
                var content = line[..contentLength];
                var continuation = content.Length > 0 && (content[0] == (byte)' ' || content[0] == (byte)'\t');
                var isMozillaHeader = StartsWithAsciiIgnoreCase(content, "X-Mozilla-Status:"u8) ||
                                      StartsWithAsciiIgnoreCase(content, "X-Mozilla-Status2:"u8) ||
                                      StartsWithAsciiIgnoreCase(content, "X-Mozilla-Keys:"u8);

                if (isMozillaHeader)
                {
                    skipContinuation = true;
                }
                else if (continuation && skipContinuation)
                {
                    // Continuação do cabeçalho interno removido.
                }
                else
                {
                    skipContinuation = false;
                    output.Write(line);
                }

                start += length;
            }
            return output.ToArray();
        }

        private static bool StartsWithAsciiIgnoreCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix)
        {
            if (value.Length < prefix.Length) return false;
            for (var index = 0; index < prefix.Length; index++)
            {
                var left = value[index];
                var right = prefix[index];
                if (left is >= (byte)'A' and <= (byte)'Z') left += 32;
                if (right is >= (byte)'A' and <= (byte)'Z') right += 32;
                if (left != right) return false;
            }
            return true;
        }

        private static bool IsBlankLine(ReadOnlySpan<byte> data) =>
            data.SequenceEqual("\r\n"u8) ||
            data.SequenceEqual("\n"u8) ||
            data.SequenceEqual("\r"u8) ||
            data.IsEmpty;

        private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                _output?.Dispose();
                if (_partialPath is not null && File.Exists(_partialPath))
                    File.Delete(_partialPath);
            }
            finally
            {
                _csv?.Dispose();
                _headerBytes.Dispose();
            }
        }
    }
}
