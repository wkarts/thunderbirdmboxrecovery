using System.Text;

namespace ThunderbirdMboxRecovery.Core;

public sealed class RecoveryLogger : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public string LogPath { get; }
    public event Action<string>? LineWritten;

    public RecoveryLogger(string outputDirectory, string fileName = "recuperacao.log")
    {
        Directory.CreateDirectory(outputDirectory);
        LogPath = Path.Combine(outputDirectory, fileName);
        _writer = new StreamWriter(
            new FileStream(LogPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 64 * 1024, FileOptions.SequentialScan),
            new UTF8Encoding(false))
        {
            AutoFlush = true
        };
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("AVISO", message);
    public void Error(string message) => Write("ERRO", message);

    private void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        lock (_sync)
        {
            if (_disposed) return;
            _writer.WriteLine(line);
        }
        LineWritten?.Invoke(line);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Dispose();
        }
    }
}
