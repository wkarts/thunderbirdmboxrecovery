using System.Text.Json.Serialization;

namespace ThunderbirdMboxRecovery.Core;

public sealed class MboxSourceSelection
{
    public required string SourcePath { get; init; }
    public string? ArchiveEntryKey { get; init; }
    public string? ArchivePassword { get; init; }

    [JsonIgnore]
    public bool IsArchive => ArchiveService.IsArchive(SourcePath);

    public string DisplayName => string.IsNullOrWhiteSpace(ArchiveEntryKey)
        ? SourcePath
        : $"{SourcePath} :: {ArchiveEntryKey}";
}

public sealed class MboxMessageInfo
{
    public long Number { get; init; }
    public long StartOffset { get; init; }
    public long EndOffset { get; init; }
    public long SizeBytes => Math.Max(0, EndOffset - StartOffset);
    public string Subject { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public string MessageId { get; init; } = string.Empty;
    public DateTimeOffset? Date { get; init; }
    public uint MozillaStatus { get; init; }
    public uint MozillaStatus2 { get; init; }
    public bool IsDeleted { get; init; }
    public bool HasAttachment { get; init; }
    public bool HeaderTerminated { get; init; }
    public int HeaderCount { get; init; }
}

public sealed class MboxDiagnosisIssue
{
    public required string Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public long? MessageNumber { get; init; }
    public long? Offset { get; init; }
}

public sealed class MboxDiagnosisReport
{
    public required string Source { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset FinishedAt { get; init; }
    public required long InputBytes { get; init; }
    public required string Sha256 { get; init; }
    public required long PreambleBytes { get; init; }
    public required long TotalMessages { get; init; }
    public required long DeletedMessages { get; init; }
    public required long MessagesWithAttachments { get; init; }
    public required long MissingSubject { get; init; }
    public required long MissingSender { get; init; }
    public required long MissingMessageId { get; init; }
    public required long DuplicateMessageIds { get; init; }
    public required long MissingHeaderTerminator { get; init; }
    public required long MalformedMozillaStatus { get; init; }
    public required long CrLfLines { get; init; }
    public required long LfLines { get; init; }
    public required long CrLines { get; init; }
    public required IReadOnlyList<MboxMessageInfo> Messages { get; init; }
    public required IReadOnlyList<MboxDiagnosisIssue> Issues { get; init; }
    public bool MessageListTruncated { get; init; }

    [JsonIgnore]
    public bool HasCriticalIssues => Issues.Any(issue =>
        issue.Severity.Equals("erro", StringComparison.OrdinalIgnoreCase));
}

public sealed class MboxAnalysisOptions
{
    public int MaxMessagesInMemory { get; init; } = 100_000;
    public bool CalculateSha256 { get; init; } = true;
}

public sealed class MessageExtractionFilter
{
    public string? SubjectContains { get; init; }
    public string? SenderContains { get; init; }
    public string? RecipientContains { get; init; }
    public DateTimeOffset? DateFrom { get; init; }
    public DateTimeOffset? DateTo { get; init; }
    public bool OnlyDeleted { get; init; }
    public bool IncludeDeleted { get; init; } = true;
    public bool OnlyWithAttachment { get; init; }
    public IReadOnlySet<long>? MessageNumbers { get; init; }

    public bool Matches(MboxMessageInfo message)
    {
        if (MessageNumbers is { Count: > 0 } && !MessageNumbers.Contains(message.Number)) return false;
        if (!IncludeDeleted && message.IsDeleted) return false;
        if (OnlyDeleted && !message.IsDeleted) return false;
        if (OnlyWithAttachment && !message.HasAttachment) return false;
        if (DateFrom.HasValue && (!message.Date.HasValue || message.Date.Value < DateFrom.Value)) return false;
        if (DateTo.HasValue && (!message.Date.HasValue || message.Date.Value > DateTo.Value)) return false;
        if (!Contains(message.Subject, SubjectContains)) return false;
        if (!Contains(message.From, SenderContains)) return false;
        if (!Contains(message.To, RecipientContains)) return false;
        return true;
    }

    private static bool Contains(string value, string? expected) =>
        string.IsNullOrWhiteSpace(expected) ||
        value.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);
}

public sealed class MessageExtractionOptions
{
    public required MboxSourceSelection Source { get; init; }
    public required string OutputDirectory { get; init; }
    public required MessageExtractionFilter Filter { get; init; }
    public bool GenerateCsvIndex { get; init; } = true;
    public bool PreserveMozillaStatusHeaders { get; init; } = true;
}

public sealed class MessageExtractionResult
{
    public required string OutputDirectory { get; init; }
    public required long ScannedMessages { get; init; }
    public required long ExtractedMessages { get; init; }
    public required long ExtractedBytes { get; init; }
    public required string? CsvIndexPath { get; init; }
    public required IReadOnlyList<string> Files { get; init; }
}

public enum ThunderbirdArchitecture
{
    Unknown,
    X86,
    X64,
    Arm64
}

public sealed class ThunderbirdInstallation
{
    public required string ExecutablePath { get; init; }
    public required string Version { get; init; }
    public required ThunderbirdArchitecture Architecture { get; init; }
    public required string Source { get; init; }

    public string DisplayText => $"Thunderbird {Version} ({Architecture}) — {ExecutablePath}";
}


public enum ThunderbirdDataRootType
{
    TraditionalRoaming,
    MicrosoftStore,
    Custom
}

public sealed class ThunderbirdDataRootInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public ThunderbirdDataRootType Type { get; init; } = ThunderbirdDataRootType.Custom;
    public string? LocalCachePath { get; init; }
    public bool IsPreferred { get; init; }
    public bool Exists => Directory.Exists(Path);
    public string ProfilesIniPath => System.IO.Path.Combine(Path, "profiles.ini");
    public string InstallsIniPath => System.IO.Path.Combine(Path, "installs.ini");
    public string DisplayText => $"{Name}{(IsPreferred ? " [preferencial]" : string.Empty)}{(!Exists ? " [será criado]" : string.Empty)} — {Path}";
}

public sealed class ThunderbirdProfileInfo
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required bool IsDefault { get; init; }
    public required bool IsRelative { get; init; }
    public long EstimatedBytes { get; init; }
    public string? DataRootPath { get; init; }
    public ThunderbirdDataRootType DataRootType { get; init; } = ThunderbirdDataRootType.Custom;
    public bool IsInUse { get; init; }
    public bool HasMail { get; init; }
    public bool HasImapMail { get; init; }
    public DateTimeOffset? LastWriteTimeUtc { get; init; }

    public string DisplayText => $"{Name}{(IsDefault ? " [padrão]" : string.Empty)}{(IsInUse ? " [em uso]" : string.Empty)} — {Path}";
}


public sealed class ProfileRegistrationResult
{
    public required bool Registered { get; init; }
    public required bool AlreadyRegistered { get; init; }
    public required string ProfileName { get; init; }
    public required string ProfilePath { get; init; }
    public required string ProfilesIniPath { get; init; }
    public string? BackupPath { get; init; }
}

public sealed class ThunderbirdIndexOptions
{
    public required MboxSourceSelection Source { get; init; }
    public required ThunderbirdInstallation Installation { get; init; }
    public required string OutputDirectory { get; init; }
    public required string MailboxName { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromHours(6);
    public TimeSpan StablePeriod { get; init; } = TimeSpan.FromSeconds(30);
    public bool TryUiAutomation { get; init; } = true;
    public bool KeepTemporaryProfile { get; init; }
    public bool CloseThunderbirdAfterIndex { get; init; } = true;
}

public sealed class ThunderbirdIndexResult
{
    public required string OutputDirectory { get; init; }
    public required string MboxPath { get; init; }
    public required string? MsfPath { get; init; }
    public required string TemporaryProfilePath { get; init; }
    public required bool MsfCreated { get; init; }
    public required bool MsfValidated { get; init; }
    public required long ExpectedMessages { get; init; }
    public required long? IndexedMessages { get; init; }
    public required string ThunderbirdVersion { get; init; }
    public required ThunderbirdArchitecture ThunderbirdArchitecture { get; init; }
    public required TimeSpan Duration { get; init; }
    public required string LogPath { get; init; }
    public string? Warning { get; init; }
}

public enum ProfileBackupMode
{
    Complete,
    MessagesOnly,
    Selective
}

public enum ProfileBackupScope
{
    SelectedProfile,
    ThunderbirdDataRoot
}

public enum ProfileRestoreTargetMode
{
    CreateNewProfile,
    ReplaceExistingProfile,
    RestoreMessagesToExisting,
    RestoreThunderbirdDataRoot,
    ManualFolder
}

public enum ProfileBackupArchiveFormat
{
    Zip,
    SevenZip
}

public sealed class ProfileBackupSelection
{
    public bool Mail { get; init; } = true;
    public bool ImapMail { get; init; } = true;
    public bool Preferences { get; init; } = true;
    public bool AddressBooks { get; init; } = true;
    public bool Calendars { get; init; } = true;
    public bool PasswordsAndCertificates { get; init; } = true;
    public bool Extensions { get; init; } = true;
    public bool SearchIndexes { get; init; }
    public bool Cache { get; init; }
}

public sealed class ProfileBackupOptions
{
    public required ThunderbirdProfileInfo Profile { get; init; }
    public ThunderbirdDataRootInfo? DataRoot { get; init; }
    public ProfileBackupScope Scope { get; init; } = ProfileBackupScope.SelectedProfile;
    public required string DestinationArchivePath { get; init; }
    public ProfileBackupArchiveFormat ArchiveFormat { get; init; } = ProfileBackupArchiveFormat.Zip;
    public ProfileBackupMode Mode { get; init; } = ProfileBackupMode.Complete;
    public ProfileBackupSelection Selection { get; init; } = new();
    public bool CalculateFileHashes { get; init; } = true;
    public bool AllowInUseProfile { get; init; }
    public bool IncludeLocalCache { get; init; }
}

public sealed class ProfileBackupResult
{
    public required string BackupPath { get; init; }
    public required string ManifestPathInsideArchive { get; init; }
    public required long Files { get; init; }
    public required long SourceBytes { get; init; }
    public required long BackupBytes { get; init; }
    public required string Sha256 { get; init; }
    public required ProfileBackupArchiveFormat ArchiveFormat { get; init; }
}

public sealed class ProfileRestoreOptions
{
    public required string BackupPath { get; init; }
    public required string DestinationProfilePath { get; init; }
    public string? DestinationDataRootPath { get; init; }
    public string? DestinationLocalCachePath { get; init; }
    public string? ArchivePassword { get; init; }
    public ProfileRestoreTargetMode TargetMode { get; init; } = ProfileRestoreTargetMode.CreateNewProfile;
    public bool CreateSafetyBackup { get; init; } = true;
    public ProfileBackupArchiveFormat SafetyBackupFormat { get; init; } = ProfileBackupArchiveFormat.Zip;
    public bool OverwriteExistingFiles { get; init; }
    public bool VerifyHashes { get; init; } = true;
    public ProfileBackupMode Mode { get; init; } = ProfileBackupMode.Complete;
    public ProfileBackupSelection Selection { get; init; } = new();
    public bool RegisterProfile { get; init; }
    public string? RegisteredProfileName { get; init; }
    public bool MakeRegisteredProfileDefault { get; init; }
    public string? MessagesSubfolderName { get; init; }
    public bool RestoreLocalCache { get; init; }
}

public sealed class ProfileRestoreResult
{
    public required string DestinationProfilePath { get; init; }
    public string? DestinationDataRootPath { get; init; }
    public required string? SafetyBackupPath { get; init; }
    public required long RestoredFiles { get; init; }
    public required long RestoredBytes { get; init; }
    public required long SkippedFiles { get; init; }
    public required long VerifiedFiles { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
    public required bool ProfileRegistered { get; init; }
    public string? RegisteredProfileName { get; init; }
}

public sealed class ProfileBackupManifest
{
    public string Application { get; init; } = "Thunderbird Recovery Suite";
    public string Version { get; init; } = ApplicationVersion.Current;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public required string ProfileName { get; init; }
    public required string OriginalProfilePath { get; init; }
    public required ProfileBackupMode Mode { get; init; }
    public ProfileBackupScope Scope { get; init; } = ProfileBackupScope.SelectedProfile;
    public ProfileBackupArchiveFormat ArchiveFormat { get; init; } = ProfileBackupArchiveFormat.Zip;
    public required IReadOnlyList<ProfileBackupManifestEntry> Files { get; init; }
    public bool SourceWasInUse { get; init; }
    public string? OriginalDataRootPath { get; init; }
    public ThunderbirdDataRootType? DataRootType { get; init; }
    public string? OriginalLocalCachePath { get; init; }
    public bool IncludedLocalCache { get; init; }
    public IReadOnlyList<string> ProfileRelativePaths { get; init; } = Array.Empty<string>();
}

public sealed class ProfileBackupInspection
{
    public required string BackupPath { get; init; }
    public required bool IsSuiteBackup { get; init; }
    public required ProfileBackupScope Scope { get; init; }
    public ProfileBackupManifest? Manifest { get; init; }
    public required IReadOnlyList<string> ProfileRelativePaths { get; init; }
    public required IReadOnlyList<string> Warnings { get; init; }
}

public sealed class ProfileBackupManifestEntry
{
    public required string RelativePath { get; init; }
    public required long SizeBytes { get; init; }
    public string? Sha256 { get; init; }
    public required DateTimeOffset LastWriteTimeUtc { get; init; }
}

public sealed class MsfValidationResult
{
    public required string Path { get; init; }
    public required bool Exists { get; init; }
    public required bool IsValidMork { get; init; }
    public required long SizeBytes { get; init; }
    public long? IndexedMessages { get; init; }
    public string? Error { get; init; }
}

public sealed record ThunderbirdIndexProgress(
    string Stage,
    string Detail,
    long ProcessedBytes = 0,
    long TotalBytes = 0,
    long Messages = 0);
