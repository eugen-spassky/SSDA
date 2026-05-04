namespace SSDA.Core.Auth;

/// <summary>Raised when Steam rejects the credentials or the auth session fails to complete.</summary>
public sealed class SteamLoginException : Exception
{
    /// <inheritdoc/>
    public SteamLoginException(string message) : base(message) { }

    /// <inheritdoc/>
    public SteamLoginException(string message, Exception inner) : base(message, inner) { }
}
