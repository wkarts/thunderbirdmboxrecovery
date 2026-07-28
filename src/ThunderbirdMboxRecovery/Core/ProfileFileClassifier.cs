namespace ThunderbirdMboxRecovery.Core;

public static class ProfileFileClassifier
{
    private static readonly HashSet<string> TransientNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "parent.lock", ".parentlock", "lock", "sessionCheckpoints.json", "Telemetry.FailedProfileLocks.txt"
    };

    public static bool ShouldInclude(string profilePath, string filePath, ProfileBackupMode mode, ProfileBackupSelection selection)
    {
        var relative = NormalizeRelative(profilePath, filePath);
        return ShouldIncludeRelative(relative, mode, selection);
    }

    public static bool ShouldIncludeRelative(string relativePath, ProfileBackupMode mode, ProfileBackupSelection selection)
    {
        var relative = Normalize(relativePath);
        if (string.IsNullOrWhiteSpace(relative)) return false;

        var first = relative.Split('/')[0];
        var fileName = Path.GetFileName(relative);

        if (TransientNames.Contains(fileName) || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            return false;

        var isIndex = fileName.Equals("global-messages-db.sqlite", StringComparison.OrdinalIgnoreCase) ||
                      fileName.Equals("panorama.sqlite", StringComparison.OrdinalIgnoreCase) ||
                      fileName.EndsWith(".msf", StringComparison.OrdinalIgnoreCase);

        if (mode == ProfileBackupMode.Complete)
            return !IsCache(relative) || selection.Cache;

        if (isIndex)
            return mode == ProfileBackupMode.Selective && selection.SearchIndexes;

        if (mode == ProfileBackupMode.MessagesOnly)
            return first.Equals("Mail", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("ImapMail", StringComparison.OrdinalIgnoreCase) ||
                   first.Equals("News", StringComparison.OrdinalIgnoreCase);

        if (IsCache(relative)) return selection.Cache;
        if (first.Equals("Mail", StringComparison.OrdinalIgnoreCase)) return selection.Mail;
        if (first.Equals("ImapMail", StringComparison.OrdinalIgnoreCase) || first.Equals("News", StringComparison.OrdinalIgnoreCase)) return selection.ImapMail;
        if (first.Equals("extensions", StringComparison.OrdinalIgnoreCase) || fileName.Equals("extensions.json", StringComparison.OrdinalIgnoreCase)) return selection.Extensions;
        if (IsAddressBook(fileName)) return selection.AddressBooks;
        if (IsCalendar(relative, fileName)) return selection.Calendars;
        if (IsCredential(fileName)) return selection.PasswordsAndCertificates;
        if (IsPreference(fileName)) return selection.Preferences;
        return false;
    }

    public static string NormalizeRelative(string profilePath, string filePath) =>
        Normalize(Path.GetRelativePath(profilePath, filePath));

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static bool IsCache(string relative) =>
        relative.StartsWith("cache2/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("startupCache/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("shader-cache/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("thumbnails/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("minidumps/", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("crashes/", StringComparison.OrdinalIgnoreCase);

    private static bool IsAddressBook(string fileName) =>
        fileName.StartsWith("abook", StringComparison.OrdinalIgnoreCase) ||
        fileName.StartsWith("history", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".mab", StringComparison.OrdinalIgnoreCase);

    private static bool IsCalendar(string relative, string fileName) =>
        relative.StartsWith("calendar-data/", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("local.sqlite", StringComparison.OrdinalIgnoreCase);

    private static bool IsCredential(string fileName) => fileName.Equals("logins.json", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("key4.db", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("cert9.db", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("pkcs11.txt", StringComparison.OrdinalIgnoreCase);

    private static bool IsPreference(string fileName) => fileName.Equals("prefs.js", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("user.js", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("handlers.json", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("permissions.sqlite", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("cookies.sqlite", StringComparison.OrdinalIgnoreCase) ||
        fileName.Equals("mimeTypes.rdf", StringComparison.OrdinalIgnoreCase);
}
