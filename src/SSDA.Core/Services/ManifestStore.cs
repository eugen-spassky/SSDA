using System.Text.Json;
using SSDA.Core.Crypto;
using SSDA.Core.Models;

namespace SSDA.Core.Services;

/// <summary>
/// Loads and persists the <c>manifest.json</c> + <c>{steamid}.maFile</c> directory used by
/// the original Steam Desktop Authenticator. Layout on disk:
/// <code>
/// /maFiles/manifest.json
/// /maFiles/{steamid}.maFile     ← per-account JSON, optionally AES-encrypted
/// </code>
/// Keep this class side-effect-free: pass it the concrete directory you want to manage and
/// it will neither look at <c>AppContext.BaseDirectory</c> nor at any environment variables.
/// </summary>
public sealed class ManifestStore
{
    /// <summary>Default name of the per-account directory expected by SDA.</summary>
    public const string DefaultDirectoryName = "maFiles";

    /// <summary>Default name of the manifest file inside the directory.</summary>
    public const string ManifestFileName = "manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    /// <summary>Absolute path to the maFiles directory this store is bound to.</summary>
    public string DirectoryPath { get; }

    /// <summary>Absolute path to the manifest file inside the directory.</summary>
    public string ManifestPath => Path.Combine(DirectoryPath, ManifestFileName);

    /// <summary>Initialises the store against the supplied maFiles directory.</summary>
    public ManifestStore(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path is required.", nameof(directoryPath));
        DirectoryPath = Path.GetFullPath(directoryPath);
    }

    /// <summary>True if a <c>manifest.json</c> exists in the bound directory.</summary>
    public bool ManifestExists() => File.Exists(ManifestPath);

    /// <summary>Loads the manifest from disk. Throws if it does not exist.</summary>
    public Manifest LoadManifest()
    {
        if (!ManifestExists())
            throw new FileNotFoundException("manifest.json was not found.", ManifestPath);

        var text = File.ReadAllText(ManifestPath);
        return JsonSerializer.Deserialize<Manifest>(text, JsonOptions)
            ?? throw new InvalidDataException("manifest.json is empty or malformed.");
    }

    /// <summary>Saves the manifest to disk, creating the directory if it does not exist.</summary>
    public void SaveManifest(Manifest manifest)
    {
        Directory.CreateDirectory(DirectoryPath);
        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        File.WriteAllText(ManifestPath, json);
    }

    /// <summary>
    /// Loads every account file referenced by the manifest. If the manifest is encrypted,
    /// supply the user passkey; otherwise pass <c>null</c>.
    /// </summary>
    /// <returns>Tuple of (account, manifest entry).</returns>
    /// <exception cref="InvalidPasskeyException">
    /// Thrown if at least one entry could not be decrypted with the supplied passkey.
    /// </exception>
    public IReadOnlyList<(SteamGuardAccount Account, ManifestEntry Entry)> LoadAccounts(
        Manifest manifest, string? passkey = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        if (manifest.Encrypted && string.IsNullOrEmpty(passkey))
            throw new InvalidPasskeyException("The manifest is encrypted but no passkey was provided.");

        var results = new List<(SteamGuardAccount, ManifestEntry)>(manifest.Entries.Count);
        foreach (var entry in manifest.Entries)
        {
            var path = Path.Combine(DirectoryPath, entry.Filename);
            if (!File.Exists(path))
                continue;

            var raw = File.ReadAllText(path);
            string json;
            if (manifest.Encrypted)
            {
                if (string.IsNullOrEmpty(entry.Salt) || string.IsNullOrEmpty(entry.IV))
                    throw new InvalidDataException(
                        $"Manifest entry '{entry.Filename}' is missing salt/IV but the manifest is marked encrypted.");

                var decrypted = MaFileEncryptor.TryDecrypt(passkey!, entry.Salt!, entry.IV!, raw);
                if (decrypted is null)
                    throw new InvalidPasskeyException(
                        $"Could not decrypt '{entry.Filename}'. The passkey is probably wrong.");
                json = decrypted;
            }
            else
            {
                json = raw;
            }

            var account = JsonSerializer.Deserialize<SteamGuardAccount>(json, JsonOptions);
            if (account is null)
                throw new InvalidDataException($"Account file '{entry.Filename}' is empty or malformed.");

            results.Add((account, entry));
        }

        return results;
    }

    /// <summary>
    /// Writes a single account file and updates the manifest entry for it. Encrypts the
    /// payload if a passkey is supplied. Returns the written manifest entry.
    /// </summary>
    public ManifestEntry SaveAccount(
        Manifest manifest,
        SteamGuardAccount account,
        string? passkey)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(account);

        Directory.CreateDirectory(DirectoryPath);

        var steamId = account.Session?.SteamID
            ?? throw new ArgumentException(
                "Account is missing a session / steam id; cannot determine filename.",
                nameof(account));

        var filename = $"{steamId}.maFile";
        var json = JsonSerializer.Serialize(account, JsonOptions);

        string? salt = null;
        string? iv = null;
        string body = json;
        var encrypt = !string.IsNullOrEmpty(passkey);
        if (encrypt)
        {
            salt = MaFileEncryptor.GenerateSaltBase64();
            iv = MaFileEncryptor.GenerateIvBase64();
            body = MaFileEncryptor.Encrypt(passkey!, salt, iv, json);
        }

        File.WriteAllText(Path.Combine(DirectoryPath, filename), body);

        var existing = manifest.Entries.FindIndex(e => e.SteamID == steamId);
        var entry = new ManifestEntry
        {
            SteamID = steamId,
            Filename = filename,
            IV = iv,
            Salt = salt,
        };
        if (existing >= 0)
            manifest.Entries[existing] = entry;
        else
            manifest.Entries.Add(entry);

        if (encrypt) manifest.Encrypted = true;

        return entry;
    }
}

/// <summary>Raised when a passkey cannot decrypt the supplied data.</summary>
public sealed class InvalidPasskeyException : Exception
{
    /// <inheritdoc/>
    public InvalidPasskeyException(string message) : base(message) { }
}
