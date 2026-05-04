using SSDA.Core.Models;
using SSDA.Core.Services;
using Xunit;

namespace SSDA.Core.Tests.Services;

public class MaFileImporterTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _destDir;

    public MaFileImporterTests()
    {
        _sourceDir = Path.Combine(Path.GetTempPath(), "ssda-import-src-" + Guid.NewGuid());
        _destDir = Path.Combine(Path.GetTempPath(), "ssda-import-dst-" + Guid.NewGuid());
        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_sourceDir, recursive: true); } catch { /* swallow */ }
        try { Directory.Delete(_destDir, recursive: true); } catch { /* swallow */ }
    }

    [Fact]
    public void Import_copies_plaintext_accounts_into_empty_destination()
    {
        var src = NewSource(passkey: null, NewAccount(76561198000000001UL, "alice"));
        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();

        var result = MaFileImporter.Import(_sourceDir, null, destManifest, dest, destinationPasskey: null);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Empty(result.Errors);
        Assert.True(File.Exists(Path.Combine(_destDir, "76561198000000001.maFile")));
        Assert.True(File.Exists(Path.Combine(_destDir, "manifest.json")));
        Assert.False(destManifest.Encrypted);
    }

    [Fact]
    public void Import_re_encrypts_under_destination_passkey()
    {
        NewSource(passkey: "old-pk", NewAccount(76561198000000002UL, "bob"));
        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();

        var result = MaFileImporter.Import(_sourceDir, "old-pk", destManifest, dest, "new-pk");

        Assert.Equal(1, result.ImportedCount);
        Assert.True(destManifest.Encrypted);

        // Round-trip: opening the destination with the new passkey should succeed,
        // with the old passkey should fail.
        var loadedNew = dest.LoadAccounts(destManifest, "new-pk");
        Assert.Single(loadedNew);
        Assert.Equal("bob", loadedNew[0].Account.AccountName);

        Assert.Throws<InvalidPasskeyException>(() => dest.LoadAccounts(destManifest, "old-pk"));
    }

    [Fact]
    public void Import_skips_accounts_already_present_in_destination()
    {
        NewSource(passkey: null, NewAccount(76561198000000003UL, "carol"));
        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();
        // Pre-populate destination with the same SteamID so the importer must skip it.
        dest.SaveAccount(destManifest, NewAccount(76561198000000003UL, "carol-existing"), passkey: null);
        dest.SaveManifest(destManifest);

        var result = MaFileImporter.Import(_sourceDir, null, destManifest, dest, destinationPasskey: null);

        Assert.Equal(0, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        // The pre-existing account is untouched.
        var loaded = dest.LoadAccounts(destManifest);
        Assert.Single(loaded);
        Assert.Equal("carol-existing", loaded[0].Account.AccountName);
    }

    [Fact]
    public void Import_throws_when_source_is_missing()
    {
        var missing = Path.Combine(_sourceDir, "does-not-exist");
        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();

        Assert.Throws<DirectoryNotFoundException>(
            () => MaFileImporter.Import(missing, null, destManifest, dest, null));
    }

    [Fact]
    public void Import_throws_when_source_passkey_is_wrong()
    {
        NewSource(passkey: "correct-pk", NewAccount(76561198000000004UL, "dave"));
        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();

        Assert.Throws<InvalidPasskeyException>(
            () => MaFileImporter.Import(_sourceDir, "wrong-pk", destManifest, dest, null));
    }

    [Fact]
    public void Import_records_an_error_for_accounts_without_steam_id()
    {
        var manifest = new Manifest();
        var src = new ManifestStore(_sourceDir);
        // Manually craft a maFile without a session (so no SteamID). We can't go through
        // SaveAccount because that asserts a session; bypass by writing directly.
        var account = new SteamGuardAccount { AccountName = "no-id", Session = null };
        var json = System.Text.Json.JsonSerializer.Serialize(account);
        File.WriteAllText(Path.Combine(_sourceDir, "0.maFile"), json);
        manifest.Entries.Add(new ManifestEntry { SteamID = 0UL, Filename = "0.maFile" });
        src.SaveManifest(manifest);

        var dest = new ManifestStore(_destDir);
        var destManifest = new Manifest();

        var result = MaFileImporter.Import(_sourceDir, null, destManifest, dest, null);

        Assert.Equal(0, result.ImportedCount);
        Assert.Single(result.Errors);
    }

    private SteamGuardAccount NewAccount(ulong steamId, string accountName) =>
        new()
        {
            AccountName = accountName,
            SharedSecret = "Tg+0xS0vmUOBDe2vUd4kupbS5jc=",
            IdentitySecret = "AbcdEFGhij1234567890=",
            Session = new SessionData { SteamID = steamId },
        };

    private string NewSource(string? passkey, params SteamGuardAccount[] accounts)
    {
        var src = new ManifestStore(_sourceDir);
        var manifest = new Manifest();
        foreach (var a in accounts) src.SaveAccount(manifest, a, passkey);
        src.SaveManifest(manifest);
        return _sourceDir;
    }
}
