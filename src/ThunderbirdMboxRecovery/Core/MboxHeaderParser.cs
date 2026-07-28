using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ThunderbirdMboxRecovery.Core;

internal sealed class ParsedMboxHeaders
{
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string Subject => Get("Subject");
    public string From => Get("From");
    public string To => Get("To");
    public string MessageId => Get("Message-ID");
    public DateTimeOffset? Date { get; init; }
    public uint MozillaStatus { get; init; }
    public uint MozillaStatus2 { get; init; }
    public bool MozillaStatusMalformed { get; init; }
    public bool MozillaStatus2Malformed { get; init; }
    public bool HasAttachment { get; init; }
    public bool IsDeleted =>
        (MozillaStatus & 0x0008U) != 0 ||
        (MozillaStatus2 & 0x00200000U) != 0;

    public string Get(string name) => Headers.TryGetValue(name, out var value) ? value : string.Empty;
}

internal static partial class MboxHeaderParser
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    [GeneratedRegex(@"=\?([^?]+)\?([bBqQ])\?([^?]*)\?=", RegexOptions.CultureInvariant)]
    private static partial Regex EncodedWordRegex();

    public static ParsedMboxHeaders Parse(ReadOnlySpan<byte> headerBytes)
    {
        var result = new ParsedMboxHeaders();
        var raw = DecodeText(headerBytes);
        var lines = raw.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        string? currentName = null;
        var currentValue = new StringBuilder();

        void Commit()
        {
            if (string.IsNullOrWhiteSpace(currentName))
                return;

            var value = DecodeEncodedWords(currentValue.ToString().Trim());
            if (result.Headers.TryGetValue(currentName, out var existing) && !string.IsNullOrEmpty(existing))
                result.Headers[currentName] = existing + ", " + value;
            else
                result.Headers[currentName] = value;

            currentName = null;
            currentValue.Clear();
        }

        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                Commit();
                break;
            }

            if ((line[0] == ' ' || line[0] == '\t') && currentName is not null)
            {
                currentValue.Append(' ').Append(line.Trim());
                continue;
            }

            Commit();
            var colon = line.IndexOf(':');
            if (colon <= 0)
                continue;

            currentName = line[..colon].Trim();
            currentValue.Append(line[(colon + 1)..].Trim());
        }

        Commit();

        var date = ParseDate(result.Get("Date"));
        var statusMalformed = !TryParseHexHeader(result.Get("X-Mozilla-Status"), 4, out var status);
        var status2Malformed = !TryParseHexHeader(result.Get("X-Mozilla-Status2"), 8, out var status2);
        var disposition = result.Get("Content-Disposition");
        var contentType = result.Get("Content-Type");
        var hasAttachment = disposition.Contains("attachment", StringComparison.OrdinalIgnoreCase)
            || disposition.Contains("filename=", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("name=", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("multipart/mixed", StringComparison.OrdinalIgnoreCase);

        var parsedResult = new ParsedMboxHeaders
        {
            Date = date,
            MozillaStatus = status,
            MozillaStatus2 = status2,
            MozillaStatusMalformed = statusMalformed && result.Headers.ContainsKey("X-Mozilla-Status"),
            MozillaStatus2Malformed = status2Malformed && result.Headers.ContainsKey("X-Mozilla-Status2"),
            HasAttachment = hasAttachment
        };

        return parsedResult.CopyHeadersFrom(result);
    }

    private static ParsedMboxHeaders CopyHeadersFrom(this ParsedMboxHeaders target, ParsedMboxHeaders source)
    {
        foreach (var pair in source.Headers)
            target.Headers[pair.Key] = pair.Value;
        return target;
    }

    private static DateTimeOffset? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out var parsed))
            return parsed;

        if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal, out parsed))
            return parsed;

        return null;
    }

    private static bool TryParseHexHeader(string value, int digits, out uint parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var token = value.Trim();
        if (token.Length < digits)
            return false;

        token = token[..digits];
        return uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsed);
    }

    private static string DecodeText(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return string.Empty;

        try
        {
            return StrictUtf8.GetString(value);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(value);
        }
    }

    public static string DecodeEncodedWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.Contains("=?", StringComparison.Ordinal))
            return value;

        return EncodedWordRegex().Replace(value, match =>
        {
            try
            {
                var charsetName = match.Groups[1].Value;
                var mode = match.Groups[2].Value;
                var encoded = match.Groups[3].Value;
                var encoding = ResolveEncoding(charsetName);
                byte[] bytes;

                if (mode.Equals("B", StringComparison.OrdinalIgnoreCase))
                {
                    bytes = Convert.FromBase64String(encoded);
                }
                else
                {
                    bytes = DecodeQuotedPrintableWord(encoded);
                }

                return encoding.GetString(bytes);
            }
            catch
            {
                return match.Value;
            }
        });
    }

    private static Encoding ResolveEncoding(string charset)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(charset.Trim());
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    private static byte[] DecodeQuotedPrintableWord(string value)
    {
        using var output = new MemoryStream(value.Length);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '_')
            {
                output.WriteByte((byte)' ');
                continue;
            }

            if (current == '=' && index + 2 < value.Length &&
                byte.TryParse(value.AsSpan(index + 1, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out var decoded))
            {
                output.WriteByte(decoded);
                index += 2;
                continue;
            }

            output.WriteByte((byte)current);
        }

        return output.ToArray();
    }
}
