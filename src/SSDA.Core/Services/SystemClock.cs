namespace SSDA.Core.Services;

/// <summary>Abstraction over <see cref="DateTimeOffset.UtcNow"/> for testability.</summary>
public interface ISystemClock
{
    /// <summary>Returns the current unix time, in seconds.</summary>
    long UtcNowUnixSeconds();
}

/// <inheritdoc cref="ISystemClock"/>
public sealed class SystemClock : ISystemClock
{
    /// <summary>Singleton instance.</summary>
    public static readonly SystemClock Instance = new();

    /// <inheritdoc/>
    public long UtcNowUnixSeconds() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
