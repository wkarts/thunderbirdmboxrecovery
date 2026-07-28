using System.Globalization;
using System.Text;

namespace ThunderbirdMboxRecovery.Core;

/// <summary>
/// Normaliza metadados internos do Thunderbird durante a reconstrução do MBOX.
/// O processamento é feito em fluxo e mantém corpos e anexos sem decodificação.
/// </summary>
internal sealed class MessageRepairWriter : IDisposable
{
    private const uint ExpungedFlag = 0x00000008;
    private const uint ImapDeletedFlag = 0x00200000;
    private const int MaximumBufferedHeaderLine = 16 * 1024 * 1024;

    private readonly ChunkWriter _chunks;
    private readonly bool _recoverDeletedMessages;
    private readonly bool _normalizeMozillaStatusHeaders;
    private readonly MemoryStream _headerLine = new();

    private bool _messageOpen;
    private bool _inHeaders;
    private bool _sawMozillaStatus;
    private bool _sawMozillaStatus2;
    private bool _longHeaderPassthrough;
    private byte[] _preferredLineEnding = "\r\n"u8.ToArray();

    public long StatusHeadersNormalized { get; private set; }
    public long StatusHeadersInserted { get; private set; }
    public long ExpungedMessagesRecovered { get; private set; }
    public long ImapDeletedMessagesRecovered { get; private set; }
    public long MalformedStatusHeadersRepaired { get; private set; }
    public long MalformedHeaderLines { get; private set; }
    public long MessagesWithoutHeaderTerminator { get; private set; }

    public MessageRepairWriter(
        ChunkWriter chunks,
        bool recoverDeletedMessages,
        bool normalizeMozillaStatusHeaders)
    {
        _chunks = chunks;
        _recoverDeletedMessages = recoverDeletedMessages;
        _normalizeMozillaStatusHeaders = normalizeMozillaStatusHeaders;
    }

    public void StartMessage(ReadOnlySpan<byte> separatorLine)
    {
        FinishPreviousMessageBeforeStartingAnother();
        UpdatePreferredLineEnding(separatorLine);
        _chunks.StartMessage(separatorLine);

        _messageOpen = true;
        _inHeaders = true;
        _sawMozillaStatus = false;
        _sawMozillaStatus2 = false;
        _longHeaderPassthrough = false;
        _headerLine.SetLength(0);
    }

    public void WriteLineFragment(ReadOnlySpan<byte> data, bool lineEnded)
    {
        if (!_messageOpen)
            throw new InvalidOperationException("Dados de mensagem recebidos antes do separador MBOX.");

        if (!_inHeaders)
        {
            _chunks.Write(data);
            return;
        }

        if (_longHeaderPassthrough)
        {
            _chunks.Write(data);
            if (lineEnded)
                _longHeaderPassthrough = false;
            return;
        }

        if (_headerLine.Length + data.Length > MaximumBufferedHeaderLine)
        {
            MalformedHeaderLines++;

            if (_headerLine.Length > 0)
            {
                _chunks.Write(
                    _headerLine
                        .GetBuffer()
                        .AsSpan(0, checked((int)_headerLine.Length)));
                _headerLine.SetLength(0);
            }

            _chunks.Write(data);
            _longHeaderPassthrough = !lineEnded;
            return;
        }

        _headerLine.Write(data);
        if (!lineEnded)
            return;

        ProcessCompletedHeaderLine(
            _headerLine
                .GetBuffer()
                .AsSpan(0, checked((int)_headerLine.Length)));

        _headerLine.SetLength(0);
    }

    public void Complete()
    {
        if (!_messageOpen)
            return;

        FlushPendingHeaderLine();

        if (_inHeaders)
        {
            MessagesWithoutHeaderTerminator++;
            EnsureMozillaStatusHeaders();
            _chunks.Write(_preferredLineEnding);
            _inHeaders = false;
        }

        _messageOpen = false;
    }

    private void FinishPreviousMessageBeforeStartingAnother()
    {
        if (!_messageOpen)
            return;

        FlushPendingHeaderLine();

        if (_inHeaders)
        {
            MessagesWithoutHeaderTerminator++;
            EnsureMozillaStatusHeaders();
            _chunks.Write(_preferredLineEnding);
            _inHeaders = false;
        }

        _messageOpen = false;
    }

    private void FlushPendingHeaderLine()
    {
        if (_headerLine.Length == 0)
            return;

        ProcessCompletedHeaderLine(
            _headerLine
                .GetBuffer()
                .AsSpan(0, checked((int)_headerLine.Length)));

        _headerLine.SetLength(0);
    }

    private void ProcessCompletedHeaderLine(ReadOnlySpan<byte> completeLine)
    {
        UpdatePreferredLineEnding(completeLine);
        var content = StripLineEnding(completeLine, out var lineEnding);

        if (content.Length == 0)
        {
            EnsureMozillaStatusHeaders();
            _chunks.Write(lineEnding.Length > 0 ? lineEnding : _preferredLineEnding);
            _inHeaders = false;
            return;
        }

        if (_normalizeMozillaStatusHeaders &&
            StartsWithAsciiIgnoreCase(content, "X-Mozilla-Status:"u8))
        {
            _sawMozillaStatus = true;

            if (TryParseHexValue(content, expectedDigits: 4, out var value))
            {
                var normalized = value;

                if (_recoverDeletedMessages && (normalized & ExpungedFlag) != 0)
                {
                    normalized &= ~ExpungedFlag;
                    ExpungedMessagesRecovered++;
                }

                WriteAsciiLine(
                    $"X-Mozilla-Status: {normalized & 0xFFFF:X4}",
                    lineEnding);

                StatusHeadersNormalized++;
                return;
            }

            if (_recoverDeletedMessages)
            {
                WriteAsciiLine("X-Mozilla-Status: 0000", lineEnding);
                StatusHeadersNormalized++;
                MalformedStatusHeadersRepaired++;
                return;
            }

            _chunks.Write(completeLine);
            return;
        }

        if (_normalizeMozillaStatusHeaders &&
            StartsWithAsciiIgnoreCase(content, "X-Mozilla-Status2:"u8))
        {
            _sawMozillaStatus2 = true;

            if (TryParseHexValue(content, expectedDigits: 8, out var value))
            {
                var normalized = value;

                if (_recoverDeletedMessages && (normalized & ImapDeletedFlag) != 0)
                {
                    normalized &= ~ImapDeletedFlag;
                    ImapDeletedMessagesRecovered++;
                }

                WriteAsciiLine(
                    $"X-Mozilla-Status2: {normalized:X8}",
                    lineEnding);

                StatusHeadersNormalized++;
                return;
            }

            if (_recoverDeletedMessages)
            {
                WriteAsciiLine("X-Mozilla-Status2: 00000000", lineEnding);
                StatusHeadersNormalized++;
                MalformedStatusHeadersRepaired++;
                return;
            }

            _chunks.Write(completeLine);
            return;
        }

        _chunks.Write(completeLine);
    }

    private void EnsureMozillaStatusHeaders()
    {
        if (!_normalizeMozillaStatusHeaders)
            return;

        if (!_sawMozillaStatus)
        {
            WriteAsciiLine("X-Mozilla-Status: 0000", _preferredLineEnding);
            _sawMozillaStatus = true;
            StatusHeadersInserted++;
        }

        if (!_sawMozillaStatus2)
        {
            WriteAsciiLine("X-Mozilla-Status2: 00000000", _preferredLineEnding);
            _sawMozillaStatus2 = true;
            StatusHeadersInserted++;
        }
    }

    private void WriteAsciiLine(string value, ReadOnlySpan<byte> lineEnding)
    {
        _chunks.Write(Encoding.ASCII.GetBytes(value));
        _chunks.Write(lineEnding.Length > 0 ? lineEnding : _preferredLineEnding);
    }

    private static bool TryParseHexValue(
        ReadOnlySpan<byte> content,
        int expectedDigits,
        out uint value)
    {
        value = 0;
        var colon = content.IndexOf((byte)':');
        if (colon < 0)
            return false;

        var raw = Encoding.ASCII
            .GetString(content[(colon + 1)..])
            .Trim();

        if (raw.Length < expectedDigits)
            return false;

        raw = raw[..expectedDigits];

        return uint.TryParse(
            raw,
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static ReadOnlySpan<byte> StripLineEnding(
        ReadOnlySpan<byte> line,
        out ReadOnlySpan<byte> lineEnding)
    {
        if (line.EndsWith("\r\n"u8))
        {
            lineEnding = line[^2..];
            return line[..^2];
        }

        if (line.EndsWith("\n"u8) || line.EndsWith("\r"u8))
        {
            lineEnding = line[^1..];
            return line[..^1];
        }

        lineEnding = ReadOnlySpan<byte>.Empty;
        return line;
    }

    private void UpdatePreferredLineEnding(ReadOnlySpan<byte> line)
    {
        if (line.EndsWith("\r\n"u8))
            _preferredLineEnding = "\r\n"u8.ToArray();
        else if (line.EndsWith("\n"u8))
            _preferredLineEnding = "\n"u8.ToArray();
        else if (line.EndsWith("\r"u8))
            _preferredLineEnding = "\r"u8.ToArray();
    }

    private static bool StartsWithAsciiIgnoreCase(
        ReadOnlySpan<byte> value,
        ReadOnlySpan<byte> prefix)
    {
        if (value.Length < prefix.Length)
            return false;

        for (var index = 0; index < prefix.Length; index++)
        {
            var left = value[index];
            var right = prefix[index];

            if (left >= (byte)'A' && left <= (byte)'Z')
                left = (byte)(left + 32);

            if (right >= (byte)'A' && right <= (byte)'Z')
                right = (byte)(right + 32);

            if (left != right)
                return false;
        }

        return true;
    }

    public void Dispose()
    {
        _headerLine.Dispose();
    }
}
