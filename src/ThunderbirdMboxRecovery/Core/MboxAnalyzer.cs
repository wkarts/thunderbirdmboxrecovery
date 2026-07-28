using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ThunderbirdMboxRecovery.Core;

public static class MboxAnalyzer
{
    public static Task<MboxDiagnosisReport> AnalyzeAsync(
        MboxSourceSelection source,
        MboxAnalysisOptions? options,
        IProgress<MboxParseProgress>? progress,
        CancellationToken cancellationToken)
    {
        options ??= new MboxAnalysisOptions();

        return Task.Run(() =>
        {
            var started = DateTimeOffset.Now;
            using var handle = MboxSourceService.Open(source);
            using var consumer = new AnalysisConsumer(options);
            var parseResult = MboxStreamParser.Parse(
                handle.Stream,
                handle.Length,
                consumer,
                progress,
                cancellationToken);

            var report = consumer.CreateReport(
                source.DisplayName,
                started,
                DateTimeOffset.Now,
                parseResult.ProcessedBytes,
                parseResult.PreambleBytes,
                parseResult.Messages);

            return report;
        }, cancellationToken);
    }

    public static void SaveJson(MboxDiagnosisReport report, string path)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }

    public static void SaveCsv(MboxDiagnosisReport report, string path)
    {
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Numero;Inicio;Fim;TamanhoBytes;Data;De;Para;Assunto;MessageId;Excluida;Anexo;CabecalhoValido");
        foreach (var message in report.Messages)
        {
            writer.WriteLine(string.Join(';',
                message.Number,
                message.StartOffset,
                message.EndOffset,
                message.SizeBytes,
                Csv(message.Date?.ToString("O") ?? string.Empty),
                Csv(message.From),
                Csv(message.To),
                Csv(message.Subject),
                Csv(message.MessageId),
                message.IsDeleted ? "Sim" : "Nao",
                message.HasAttachment ? "Sim" : "Nao",
                message.HeaderTerminated ? "Sim" : "Nao"));
        }
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

    private sealed class AnalysisConsumer : IMboxStreamConsumer, IDisposable
    {
        private const int MaxHeaderBytes = 16 * 1024 * 1024;
        private readonly MboxAnalysisOptions _options;
        private readonly IncrementalHash? _hash;
        private readonly MemoryStream _headers = new();
        private readonly List<MboxMessageInfo> _messages = [];
        private readonly List<MboxDiagnosisIssue> _issues = [];
        private readonly HashSet<string> _messageIds = new(StringComparer.OrdinalIgnoreCase);
        private long _currentNumber;
        private long _currentStart;
        private bool _inHeaders;
        private bool _headerTerminated;
        private bool _headerOverflow;
        private long _deleted;
        private long _attachments;
        private long _missingSubject;
        private long _missingSender;
        private long _missingMessageId;
        private long _duplicateMessageIds;
        private long _missingHeaderTerminator;
        private long _malformedStatus;
        private long _crlf;
        private long _lf;
        private long _cr;

        public AnalysisConsumer(MboxAnalysisOptions options)
        {
            _options = options;
            _hash = options.CalculateSha256
                ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
                : null;
        }

        public void OnPreamble(ReadOnlySpan<byte> data, long offset)
        {
            _hash?.AppendData(data);
            CountLineEnding(data);
        }

        public void OnMessageStart(ReadOnlySpan<byte> separatorLine, long offset, long messageNumber)
        {
            _hash?.AppendData(separatorLine);
            CountLineEnding(separatorLine);
            _currentNumber = messageNumber;
            _currentStart = offset;
            _inHeaders = true;
            _headerTerminated = false;
            _headerOverflow = false;
            _headers.SetLength(0);
        }

        public void OnMessageData(ReadOnlySpan<byte> data, long offset)
        {
            _hash?.AppendData(data);
            CountLineEnding(data);

            if (!_inHeaders)
                return;

            if (_headers.Length + data.Length <= MaxHeaderBytes)
                _headers.Write(data);
            else
                _headerOverflow = true;

            if (IsBlankLine(data))
            {
                _inHeaders = false;
                _headerTerminated = true;
            }
        }

        public void OnMessageEnd(long offset)
        {
            if (_currentNumber <= 0)
                return;

            var parsed = MboxHeaderParser.Parse(_headers.GetBuffer().AsSpan(0, checked((int)_headers.Length)));
            var info = new MboxMessageInfo
            {
                Number = _currentNumber,
                StartOffset = _currentStart,
                EndOffset = offset,
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

            if (_messages.Count < _options.MaxMessagesInMemory)
                _messages.Add(info);

            if (info.IsDeleted) _deleted++;
            if (info.HasAttachment) _attachments++;
            if (string.IsNullOrWhiteSpace(info.Subject)) _missingSubject++;
            if (string.IsNullOrWhiteSpace(info.From)) _missingSender++;

            if (string.IsNullOrWhiteSpace(info.MessageId))
            {
                _missingMessageId++;
            }
            else if (!_messageIds.Add(info.MessageId))
            {
                _duplicateMessageIds++;
                AddIssue("aviso", "MESSAGE_ID_DUPLICADO",
                    $"Message-ID duplicado: {info.MessageId}", info.Number, info.StartOffset);
            }

            if (!_headerTerminated)
            {
                _missingHeaderTerminator++;
                AddIssue("erro", "CABECALHO_SEM_TERMINADOR",
                    "A mensagem não possui linha vazia separando cabeçalho e corpo.", info.Number, info.StartOffset);
            }

            if (_headerOverflow)
            {
                AddIssue("erro", "CABECALHO_EXCESSIVO",
                    $"O cabeçalho ultrapassou {SizeFormatter.Format(MaxHeaderBytes)} e foi truncado para análise.",
                    info.Number, info.StartOffset);
            }

            if (parsed.MozillaStatusMalformed || parsed.MozillaStatus2Malformed)
            {
                _malformedStatus++;
                AddIssue("aviso", "STATUS_MOZILLA_MALFORMADO",
                    "X-Mozilla-Status ou X-Mozilla-Status2 contém valor inválido.", info.Number, info.StartOffset);
            }
        }

        public MboxDiagnosisReport CreateReport(
            string source,
            DateTimeOffset started,
            DateTimeOffset finished,
            long inputBytes,
            long preambleBytes,
            long totalMessages)
        {
            if (totalMessages == 0)
                AddIssue("erro", "SEM_MENSAGENS", "Nenhum separador MBOX válido foi encontrado.", null, null);

            if (preambleBytes > 0)
                AddIssue("aviso", "PREFIXO_NAO_RECONHECIDO",
                    $"Existem {SizeFormatter.Format(preambleBytes)} antes da primeira mensagem reconhecida.", null, 0);

            var lineEndingKinds = new[] { _crlf > 0, _lf > 0, _cr > 0 }.Count(value => value);
            if (lineEndingKinds > 1)
                AddIssue("aviso", "QUEBRAS_DE_LINHA_MISTAS",
                    "O arquivo mistura CRLF, LF e/ou CR. Isso pode indicar concatenações de origens diferentes.", null, null);

            if (_missingHeaderTerminator > 0)
                AddIssue("erro", "ESTRUTURA_CABECALHO",
                    $"{_missingHeaderTerminator:N0} mensagens não possuem terminador de cabeçalho.", null, null);

            if (_malformedStatus > 0)
                AddIssue("aviso", "STATUS_MOZILLA",
                    $"{_malformedStatus:N0} mensagens possuem cabeçalhos de status malformados.", null, null);

            return new MboxDiagnosisReport
            {
                Source = source,
                StartedAt = started,
                FinishedAt = finished,
                InputBytes = inputBytes,
                Sha256 = _hash is null
                    ? string.Empty
                    : Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant(),
                PreambleBytes = preambleBytes,
                TotalMessages = totalMessages,
                DeletedMessages = _deleted,
                MessagesWithAttachments = _attachments,
                MissingSubject = _missingSubject,
                MissingSender = _missingSender,
                MissingMessageId = _missingMessageId,
                DuplicateMessageIds = _duplicateMessageIds,
                MissingHeaderTerminator = _missingHeaderTerminator,
                MalformedMozillaStatus = _malformedStatus,
                CrLfLines = _crlf,
                LfLines = _lf,
                CrLines = _cr,
                Messages = _messages,
                Issues = _issues,
                MessageListTruncated = totalMessages > _messages.Count
            };
        }

        private void AddIssue(string severity, string code, string message, long? number, long? offset)
        {
            if (_issues.Count >= 10_000)
                return;

            _issues.Add(new MboxDiagnosisIssue
            {
                Severity = severity,
                Code = code,
                Message = message,
                MessageNumber = number,
                Offset = offset
            });
        }

        private void CountLineEnding(ReadOnlySpan<byte> data)
        {
            if (data.EndsWith("\r\n"u8)) _crlf++;
            else if (data.EndsWith("\n"u8)) _lf++;
            else if (data.EndsWith("\r"u8)) _cr++;
        }

        private static bool IsBlankLine(ReadOnlySpan<byte> data) =>
            data.SequenceEqual("\r\n"u8) ||
            data.SequenceEqual("\n"u8) ||
            data.SequenceEqual("\r"u8) ||
            data.IsEmpty;

        public void Dispose()
        {
            _headers.Dispose();
            _hash?.Dispose();
        }
    }
}
