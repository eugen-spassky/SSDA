using SSDA.Core.Models;

namespace SSDA.Core.Services;

/// <summary>
/// Result of an import operation. <see cref="ImportedCount"/> reflects the number of
/// accounts successfully written into the destination directory; <see cref="SkippedCount"/>
/// reflects accounts that already existed there (matched by SteamID) and were left
/// untouched; <see cref="Errors"/> holds any per-entry failure messages.
/// </summary>
public sealed class ImportResult
{
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Migrates a legacy Steam Desktop Authenticator <c>maFiles/</c> directory into the
/// SSDA store. The on-disk format is identical, so import boils down to: load the
/// source manifest with its passkey, then re-write each account through the destination
/// store under the new passkey.
/// </summary>
public static class MaFileImporter
{
    /// <summary>
    /// Decrypts every account in <paramref name="sourceDirectory"/> using
    /// <paramref name="sourcePasskey"/> (or <c>null</c> if it's plaintext), then writes
    /// each into <paramref name="destinationStore"/> under <paramref name="destinationPasskey"/>.
    /// Existing accounts in the destination (matched by SteamID) are skipped, never
    /// overwritten — the user must remove them manually first.
    /// </summary>
    public static ImportResult Import(
        string sourceDirectory,
        string? sourcePasskey,
        Manifest destinationManifest,
        ManifestStore destinationStore,
        string? destinationPasskey)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory))
            throw new ArgumentException("Source directory is required.", nameof(sourceDirectory));
        ArgumentNullException.ThrowIfNull(destinationManifest);
        ArgumentNullException.ThrowIfNull(destinationStore);

        if (!Directory.Exists(sourceDirectory))
            throw new DirectoryNotFoundException(
                $"Source directory does not exist: {sourceDirectory}");

        var source = new ManifestStore(sourceDirectory);
        if (!source.ManifestExists())
            throw new FileNotFoundException(
                "manifest.json was not found in the source directory.",
                source.ManifestPath);

        var sourceManifest = source.LoadManifest();
        var sourceAccounts = source.LoadAccounts(sourceManifest, sourcePasskey);

        var existingIds = new HashSet<ulong>(destinationManifest.Entries.Select(e => e.SteamID));
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var (account, _) in sourceAccounts)
        {
            var steamId = account.Session?.SteamID ?? 0UL;
            if (steamId == 0)
            {
                errors.Add($"Аккаунт «{account.AccountName ?? "?"}» без SteamID — пропущен.");
                continue;
            }

            if (existingIds.Contains(steamId))
            {
                skipped++;
                continue;
            }

            try
            {
                destinationStore.SaveAccount(destinationManifest, account, destinationPasskey);
                existingIds.Add(steamId);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Не удалось импортировать {steamId}: {ex.Message}");
            }
        }

        if (imported > 0)
            destinationStore.SaveManifest(destinationManifest);

        return new ImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped,
            Errors = errors,
        };
    }
}
