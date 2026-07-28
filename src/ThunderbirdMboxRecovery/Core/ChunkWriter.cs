using System.Security.Cryptography;

namespace ThunderbirdMboxRecovery.Core;

internal sealed class ChunkWriter : IDisposable
{
    private readonly string _outputDirectory;
    private readonly long _targetSize;
    private readonly bool _splitOutput;
    private readonly RecoveryLogger _logger;
    private readonly string _mailboxName;
    private readonly List<ChunkManifest> _parts = [];
    private FileStream? _stream;
    private IncrementalHash? _hash;
    private string? _partialPath;
    private string? _finalPath;
    private long _size;
    private long _messages;
    private int _index;
    private bool _completed;

    public IReadOnlyList<ChunkManifest> Parts => _parts;
    public int CompletedParts => _parts.Count;
    public string? CurrentFileName => _finalPath is null ? null : Path.GetFileName(_finalPath);

    public ChunkWriter(
        string outputDirectory,
        long targetSize,
        string mailboxName,
        bool splitOutput,
        RecoveryLogger logger)
    {
        _outputDirectory = outputDirectory;
        _targetSize = targetSize;
        _mailboxName = MailboxNameResolver.FromSource(mailboxName);
        _splitOutput = splitOutput;
        _logger = logger;
    }

    public void StartMessage(ReadOnlySpan<byte> separatorLine)
    {
        if (_stream is null)
        {
            OpenNew();
        }
        else if (_splitOutput && _size >= _targetSize && _messages > 0)
        {
            FinalizeCurrent();
            OpenNew();
        }

        _messages++;
        Write(separatorLine);
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_stream is null || _hash is null)
            throw new InvalidOperationException("Nenhum arquivo de saída está aberto.");

        _stream.Write(data);
        _hash.AppendData(data);
        _size += data.Length;
    }

    public void Complete()
    {
        if (_completed) return;
        FinalizeCurrent();
        _completed = true;
    }

    public void Abort()
    {
        if (_stream is null) return;

        try
        {
            _stream.Flush(true);
        }
        catch
        {
            // Preserva a exceção original da recuperação.
        }

        _stream.Dispose();
        _stream = null;
        _hash?.Dispose();
        _hash = null;
        _logger.Warning($"Arquivo incompleto preservado: {Path.GetFileName(_partialPath)}");
    }

    private void OpenNew()
    {
        _index++;
        var baseName = _splitOutput
            ? $"{_mailboxName}_Recuperada_{_index:000}"
            : $"{_mailboxName}_Recuperada";

        _finalPath = Path.Combine(_outputDirectory, baseName);
        _partialPath = _finalPath + ".partial";
        _stream = new FileStream(
            _partialPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            4 * 1024 * 1024,
            FileOptions.SequentialScan);
        _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        _size = 0;
        _messages = 0;
        _logger.Info($"Criando arquivo MBOX {baseName}.");
    }

    private void FinalizeCurrent()
    {
        if (_stream is null || _hash is null || _partialPath is null || _finalPath is null)
            return;

        _stream.Flush(true);
        _stream.Dispose();
        _stream = null;

        var hash = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
        _hash.Dispose();
        _hash = null;

        File.Move(_partialPath, _finalPath, false);

        _parts.Add(new ChunkManifest
        {
            FileName = Path.GetFileName(_finalPath) ?? _finalPath,
            SizeBytes = _size,
            EstimatedMessages = _messages,
            Sha256 = hash
        });

        _logger.Info(
            $"Arquivo concluído: {Path.GetFileName(_finalPath)} | " +
            $"{SizeFormatter.Format(_size)} | {_messages:N0} mensagens estimadas.");

        _partialPath = null;
        _finalPath = null;
        _size = 0;
        _messages = 0;
    }

    public void Dispose()
    {
        if (!_completed)
            Abort();
    }
}
