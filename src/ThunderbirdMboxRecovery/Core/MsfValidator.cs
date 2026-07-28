using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ThunderbirdMboxRecovery.Core;

public static partial class MsfValidator
{
    [GeneratedRegex(@"\(([0-9A-Fa-f]+)=numMsgs\)", RegexOptions.CultureInvariant)]
    private static partial Regex NumMessagesAtomRegex();

    public static MsfValidationResult Validate(string path)
    {
        if (!File.Exists(path))
        {
            return new MsfValidationResult
            {
                Path = path,
                Exists = false,
                IsValidMork = false,
                SizeBytes = 0,
                Error = "O arquivo .msf ainda não foi criado."
            };
        }

        try
        {
            var info = new FileInfo(path);
            if (info.Length < 32)
            {
                return new MsfValidationResult
                {
                    Path = path,
                    Exists = true,
                    IsValidMork = false,
                    SizeBytes = info.Length,
                    Error = "O índice é pequeno demais para ser um banco Mork válido."
                };
            }

            var probeLength = checked((int)Math.Min(info.Length, 8L * 1024 * 1024));
            var bytes = new byte[probeLength];
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var read = 0;
                while (read < bytes.Length)
                {
                    var count = stream.Read(bytes, read, bytes.Length - read);
                    if (count == 0) break;
                    read += count;
                }
            }

            var text = Encoding.Latin1.GetString(bytes);
            var valid = text.Contains("// <!-- <mdb:mork:z v=\"1.4\"", StringComparison.Ordinal) ||
                        text.Contains("<mdb:mork:z", StringComparison.OrdinalIgnoreCase);

            return new MsfValidationResult
            {
                Path = path,
                Exists = true,
                IsValidMork = valid,
                SizeBytes = info.Length,
                IndexedMessages = valid ? TryReadNumMessages(text) : null,
                Error = valid ? null : "A assinatura Mork não foi encontrada no índice."
            };
        }
        catch (Exception exception)
        {
            return new MsfValidationResult
            {
                Path = path,
                Exists = true,
                IsValidMork = false,
                SizeBytes = SafeLength(path),
                Error = exception.Message
            };
        }
    }

    private static long? TryReadNumMessages(string text)
    {
        var atomMatch = NumMessagesAtomRegex().Match(text);
        if (!atomMatch.Success) return null;

        var atom = Regex.Escape(atomMatch.Groups[1].Value);
        // Em Mork, a propriedade pode aparecer dentro de uma célula com um
        // identificador anterior, por exemplo: (k^A1=^3). Por isso a busca
        // não exige que ^A1 seja o primeiro conteúdo entre parênteses.
        var valueRegex = new Regex($@"\^{atom}\s*=\s*\^?([0-9A-Fa-f]+)", RegexOptions.CultureInvariant);
        var matches = valueRegex.Matches(text);
        if (matches.Count == 0) return null;

        var value = matches[^1].Groups[1].Value;
        return long.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0; }
    }
}
