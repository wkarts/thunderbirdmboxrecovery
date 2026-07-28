using System.Text;
using System.Text.RegularExpressions;

namespace ThunderbirdMboxRecovery.Core;

public interface IMboxStreamConsumer
{
    void OnPreamble(ReadOnlySpan<byte> data, long offset);
    void OnMessageStart(ReadOnlySpan<byte> separatorLine, long offset, long messageNumber);
    void OnMessageData(ReadOnlySpan<byte> data, long offset);
    void OnMessageEnd(long offset);
}

public sealed record MboxParseProgress(long ProcessedBytes, long TotalBytes, long Messages);

public sealed class MboxParseResult
{
    public required long ProcessedBytes { get; init; }
    public required long Messages { get; init; }
    public required long PreambleBytes { get; init; }
}

public static partial class MboxStreamParser
{
    private const int BufferSize = 4 * 1024 * 1024;

    [GeneratedRegex(@"^From\s+(?:-|\S+)\s+(?:Mon|Tue|Wed|Thu|Fri|Sat|Sun)\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorRegex();

    public static MboxParseResult Parse(
        Stream stream,
        long totalBytes,
        IMboxStreamConsumer consumer,
        IProgress<MboxParseProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(consumer);

        using var reader = new BinaryLineReader(stream, BufferSize);
        var messageOpen = false;
        long messages = 0;
        long preambleBytes = 0;
        long nextProgress = 64L * 1024 * 1024;

        while (reader.ReadLine(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSeparator(line.Bytes))
            {
                if (messageOpen)
                    consumer.OnMessageEnd(line.Offset);

                messageOpen = true;
                messages++;
                consumer.OnMessageStart(line.Bytes, line.Offset, messages);
            }
            else if (messageOpen)
            {
                consumer.OnMessageData(line.Bytes, line.Offset);
            }
            else
            {
                preambleBytes += line.Bytes.Length;
                consumer.OnPreamble(line.Bytes, line.Offset);
            }

            if (reader.Position >= nextProgress || reader.Position == totalBytes)
            {
                progress?.Report(new MboxParseProgress(reader.Position, totalBytes, messages));
                nextProgress = reader.Position + 64L * 1024 * 1024;
            }
        }

        if (messageOpen)
            consumer.OnMessageEnd(reader.Position);

        progress?.Report(new MboxParseProgress(reader.Position, totalBytes, messages));

        return new MboxParseResult
        {
            ProcessedBytes = reader.Position,
            Messages = messages,
            PreambleBytes = preambleBytes
        };
    }

    public static bool IsSeparator(ReadOnlySpan<byte> line)
    {
        if (line.Length < 20 || !line.StartsWith("From "u8))
            return false;

        var length = Math.Min(line.Length, 512);
        var probe = Encoding.ASCII.GetString(line[..length]).TrimEnd('\r', '\n');
        return SeparatorRegex().IsMatch(probe);
    }

    private sealed class BinaryLineReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _position;
        private int _length;
        private bool _endOfStream;

        public long Position { get; private set; }

        public BinaryLineReader(Stream stream, int bufferSize)
        {
            _stream = stream;
            _buffer = new byte[bufferSize];
        }

        public BinaryLine? ReadLine(CancellationToken cancellationToken)
        {
            if (_endOfStream && _position >= _length)
                return null;

            var offset = Position;
            MemoryStream? overflow = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_position >= _length)
                {
                    _length = _stream.Read(_buffer, 0, _buffer.Length);
                    _position = 0;
                    if (_length == 0)
                    {
                        _endOfStream = true;
                        if (overflow is null || overflow.Length == 0)
                        {
                            overflow?.Dispose();
                            return null;
                        }

                        var finalBytes = overflow.ToArray();
                        overflow.Dispose();
                        Position += finalBytes.Length;
                        return new BinaryLine(offset, finalBytes);
                    }
                }

                var newline = Array.IndexOf(_buffer, (byte)'\n', _position, _length - _position);
                if (newline >= 0)
                {
                    var count = newline - _position + 1;
                    if (overflow is null)
                    {
                        var bytes = new byte[count];
                        Buffer.BlockCopy(_buffer, _position, bytes, 0, count);
                        _position += count;
                        Position += count;
                        return new BinaryLine(offset, bytes);
                    }

                    overflow.Write(_buffer, _position, count);
                    _position += count;
                    Position += overflow.Length;
                    var combined = overflow.ToArray();
                    overflow.Dispose();
                    return new BinaryLine(offset, combined);
                }

                var remaining = _length - _position;
                overflow ??= new MemoryStream(Math.Max(remaining * 2, 4096));
                overflow.Write(_buffer, _position, remaining);
                _position = _length;
            }
        }

        public void Dispose() => _stream.Dispose();
    }

    private sealed record BinaryLine(long Offset, byte[] Bytes);
}
