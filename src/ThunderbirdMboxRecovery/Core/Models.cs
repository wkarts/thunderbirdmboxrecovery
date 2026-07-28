using System.Reflection;
using System.Text.Json.Serialization;

namespace ThunderbirdMboxRecovery.Core;

public sealed record ArchiveEntryInfo(string Key, long Size)
{
    public string DisplayText => $"{Key}  ({SizeFormatter.Format(Size)})";
}

public sealed class RecoveryOptions
{
    public required string SourcePath { get; init; }
    public string? ArchiveEntryKey { get; init; }
    public string? ArchivePassword { get; init; }
    public required string OutputDirectory { get; init; }
    public required string MailboxName { get; init; }
    public required bool SplitOutput { get; init; }
    public required bool RecoverDeletedMessages { get; init; }
    public required bool NormalizeMozillaStatusHeaders { get; init; }
    public required long TargetChunkBytes { get; init; }
    public required long ExpectedInputBytes { get; init; }
}

public sealed record RecoveryProgress(
    string Stage,
    long ProcessedBytes,
    long TotalBytes,
    long Messages,
    int CompletedParts,
    string? CurrentFile,
    string? Detail = null);

public sealed class RecoveryResult
{
    public required string OutputDirectory { get; init; }
    public required string MailboxName { get; init; }
    public required long InputBytes { get; init; }
    public required string InputSha256 { get; init; }
    public required long PrefixBytes { get; init; }
    public required long TotalMessages { get; init; }
    public required IReadOnlyList<ChunkManifest> Parts { get; init; }
    public required long ExpungedMessagesRecovered { get; init; }
    public required long ImapDeletedMessagesRecovered { get; init; }
    public required long StatusHeadersNormalized { get; init; }
    public required long StatusHeadersInserted { get; init; }
    public required long MalformedStatusHeadersRepaired { get; init; }
    public required long MalformedHeaderLines { get; init; }
    public required long MessagesWithoutHeaderTerminator { get; init; }
    public required string ManifestPath { get; init; }
    public required string LogPath { get; init; }
}

public sealed class RecoveryManifest
{
    [JsonPropertyName("aplicacao")]
    public string Application { get; init; } = "Thunderbird Recovery Suite";

    [JsonPropertyName("versao")]
    public string Version { get; init; } = ApplicationVersion.Current;

    [JsonPropertyName("criado_em")]
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    [JsonPropertyName("origem")]
    public required string Source { get; init; }

    [JsonPropertyName("entrada_arquivo_compactado")]
    public string? ArchiveEntry { get; init; }

    [JsonPropertyName("nome_caixa_origem")]
    public required string MailboxName { get; init; }

    [JsonPropertyName("tamanho_entrada_bytes")]
    public required long InputSizeBytes { get; init; }

    [JsonPropertyName("sha256_entrada_descompactada")]
    public required string InputSha256 { get; init; }

    [JsonPropertyName("saida_fracionada")]
    public required bool SplitOutput { get; init; }

    [JsonPropertyName("estrategia_indice_msf")]
    public string MsfIndexStrategy { get; init; } = "reconstrucao_pelo_thunderbird";

    [JsonPropertyName("criou_msf_artificial")]
    public bool CreatedArtificialMsf { get; init; }

    [JsonPropertyName("recuperou_mensagens_excluidas")]
    public required bool RecoveredDeletedMessages { get; init; }

    [JsonPropertyName("normalizou_x_mozilla_status")]
    public required bool NormalizedMozillaStatusHeaders { get; init; }

    [JsonPropertyName("mensagens_expurgadas_recuperadas")]
    public required long ExpungedMessagesRecovered { get; init; }

    [JsonPropertyName("mensagens_imap_excluidas_recuperadas")]
    public required long ImapDeletedMessagesRecovered { get; init; }

    [JsonPropertyName("cabecalhos_status_normalizados")]
    public required long StatusHeadersNormalized { get; init; }

    [JsonPropertyName("cabecalhos_status_inseridos")]
    public required long StatusHeadersInserted { get; init; }

    [JsonPropertyName("cabecalhos_status_malformados_reparados")]
    public required long MalformedStatusHeadersRepaired { get; init; }

    [JsonPropertyName("linhas_cabecalho_malformadas")]
    public required long MalformedHeaderLines { get; init; }

    [JsonPropertyName("mensagens_sem_terminador_cabecalho")]
    public required long MessagesWithoutHeaderTerminator { get; init; }

    [JsonPropertyName("tamanho_alvo_parte_bytes")]
    public long? TargetChunkBytes { get; init; }

    [JsonPropertyName("prefixo_nao_reconhecido_bytes")]
    public required long PrefixBytes { get; init; }

    [JsonPropertyName("mensagens_estimadas")]
    public required long EstimatedMessages { get; init; }

    [JsonPropertyName("total_partes")]
    public required int TotalParts { get; init; }

    [JsonPropertyName("partes")]
    public required IReadOnlyList<ChunkManifest> Parts { get; init; }
}

public sealed class ChunkManifest
{
    [JsonPropertyName("arquivo")]
    public required string FileName { get; init; }

    [JsonPropertyName("tamanho_bytes")]
    public required long SizeBytes { get; init; }

    [JsonPropertyName("mensagens_estimadas")]
    public required long EstimatedMessages { get; init; }

    [JsonPropertyName("sha256")]
    public required string Sha256 { get; init; }
}

public static class ApplicationVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataSeparator = informationalVersion.IndexOf('+');
            return metadataSeparator > 0
                ? informationalVersion[..metadataSeparator]
                : informationalVersion;
        }

        return assembly.GetName().Version?.ToString(3) ?? "2.2.0";
    }
}

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB", "TiB"];

    public static string Format(long value)
    {
        double size = value;
        var unit = 0;

        while (size >= 1024 && unit < Units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:N2} {Units[unit]}";
    }
}
