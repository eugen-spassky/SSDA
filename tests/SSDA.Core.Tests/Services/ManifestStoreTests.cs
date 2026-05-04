using SSDA.Core.Models;
using SSDA.Core.Services;

namespace SSDA.Core.Tests.Services;

public sealed class ManifestStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ManifestStore _store;

    public ManifestStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ssda-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new ManifestStore(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private SteamGuardAccount BuildAccount(ulong steamId)
    {
        return new SteamGuardAccount
        {
            AccountName = $"user-{steamId}",
            SharedSecret = "AAAAAAAAAAAAAAAAAAAAAAAAAAA=",
            IdentitySecret = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBB=",
            RevocationCode = "R12345",
            DeviceID = "android:device-id",
            Status = 1,
            FullyEnrolled = true,
            Session = new SessionData
            {
                SteamID = steamId,
                AccessToken = "jwt",
                RefreshToken = "jwt",
                SessionID = "sess",
            },
        };
    }

    [Fact]
    public void Constructor_rejects_blank_path()
    {
        Assert.Throws<ArgumentException>(() => new ManifestStore(""));
        Assert.Throws<ArgumentException>(() => new ManifestStore("   "));
    }

    [Fact]
    public void SaveAccount_then_LoadAccounts_roundtrips_in_plaintext()
    {
        var manifest = new Manifest();
        var account = BuildAccount(76561198000000001UL);

        var entry = _store.SaveAccount(manifest, account, passkey: null);
        _store.SaveManifest(manifest);

        Assert.False(manifest.Encrypted);
        Assert.Null(entry.IV);
        Assert.Null(entry.Salt);
        Assert.Equal("76561198000000001.maFile", entry.Filename);
        Assert.True(File.Exists(Path.Combine(_tempDir, entry.Filename)));

        var loadedManifest = _store.LoadManifest();
        var loaded = _store.LoadAccounts(loadedManifest);

        var pair = Assert.Single(loaded);
        Assert.Equal(account.AccountName, pair.Account.AccountName);
        Assert.Equal(account.Session!.SteamID, pair.Account.Session!.SteamID);
    }

    [Fact]
    public void SaveAccount_with_passkey_writes_an_encrypted_payload_that_LoadAccounts_can_decrypt()
    {
        var manifest = new Manifest();
        var account = BuildAccount(76561198000000002UL);

        var entry = _store.SaveAccount(manifest, account, passkey: "passkey-123");
        _store.SaveManifest(manifest);

        Assert.True(manifest.Encrypted);
        Assert.NotNull(entry.IV);
        Assert.NotNull(entry.Salt);
        var bodyOnDisk = File.ReadAllText(Path.Combine(_tempDir, entry.Filename));
        Assert.DoesNotContain(account.AccountName!, bodyOnDisk);

        var loaded = _store.LoadAccounts(_store.LoadManifest(), passkey: "passkey-123");
        var pair = Assert.Single(loaded);
        Assert.Equal(account.AccountName, pair.Account.AccountName);
    }

    [Fact]
    public void LoadAccounts_throws_for_a_wrong_passkey()
    {
        var manifest = new Manifest();
        _store.SaveAccount(manifest, BuildAccount(76561198000000003UL), passkey: "right");
        _store.SaveManifest(manifest);

        Assert.Throws<InvalidPasskeyException>(
            () => _store.LoadAccounts(_store.LoadManifest(), passkey: "wrong"));
    }

    [Fact]
    public void LoadAccounts_throws_when_manifest_is_encrypted_but_passkey_is_missing()
    {
        var manifest = new Manifest();
        _store.SaveAccount(manifest, BuildAccount(76561198000000004UL), passkey: "passkey");
        _store.SaveManifest(manifest);

        Assert.Throws<InvalidPasskeyException>(
            () => _store.LoadAccounts(_store.LoadManifest(), passkey: null));
    }

    [Fact]
    public void SaveAccount_replaces_an_existing_entry_for_the_same_steamid()
    {
        var manifest = new Manifest();
        var v1 = BuildAccount(76561198000000005UL);
        v1.AccountName = "old-name";

        _store.SaveAccount(manifest, v1, passkey: null);
        Assert.Single(manifest.Entries);

        var v2 = BuildAccount(76561198000000005UL);
        v2.AccountName = "new-name";
        _store.SaveAccount(manifest, v2, passkey: null);

        Assert.Single(manifest.Entries);
        var loaded = _store.LoadAccounts(manifest);
        Assert.Equal("new-name", loaded.Single().Account.AccountName);
    }

    [Fact]
    public void LoadManifest_throws_FileNotFound_when_no_manifest_exists()
    {
        Assert.Throws<FileNotFoundException>(() => _store.LoadManifest());
    }

    [Fact]
    public void SaveAccount_throws_when_session_steamid_is_missing()
    {
        var manifest = new Manifest();
        var account = new SteamGuardAccount { AccountName = "x" };

        Assert.Throws<ArgumentException>(
            () => _store.SaveAccount(manifest, account, passkey: null));
    }
}
