using GameLocker.Common.Models;

namespace GameLocker.Common.Configuration;

/// <summary>
/// Why a configuration load produced the result it did.
/// </summary>
public enum ConfigLoadStatus
{
    /// <summary>The configuration was read and decrypted successfully.</summary>
    Loaded,

    /// <summary>No configuration has been saved yet.</summary>
    NotFound,

    /// <summary>A configuration exists but could not be read, decrypted, or parsed.</summary>
    Unreadable,
}

/// <summary>
/// The outcome of loading the configuration.
/// </summary>
/// <remarks>
/// <see cref="ConfigManager.LoadConfigAsync"/> used to return <c>null</c> for every failure, so a
/// corrupt file, a missing key, a DPAPI failure and a genuine first run were indistinguishable.
/// The service silently kept a stale configuration forever; the UI presented a blank form that the
/// user could then save over their real folder list.
/// See ARCHITECTURE-REVIEW.md findings [S-5] and [G-4].
/// </remarks>
/// <param name="Status">What happened.</param>
/// <param name="Config">The configuration, when <paramref name="Status"/> is
/// <see cref="ConfigLoadStatus.Loaded"/>; otherwise <c>null</c>.</param>
/// <param name="Error">A description of the failure, when one occurred.</param>
public readonly record struct ConfigLoadResult(
    ConfigLoadStatus Status,
    GameLockerConfig? Config,
    string? Error)
{
    /// <summary>Gets a value indicating whether a configuration was returned.</summary>
    public bool IsLoaded => Status == ConfigLoadStatus.Loaded && Config is not null;

    /// <summary>
    /// Gets a value indicating whether a configuration exists but is damaged. This is an error
    /// condition and must never be treated as a first run.
    /// </summary>
    public bool IsDamaged => Status == ConfigLoadStatus.Unreadable;

    /// <summary>Creates a successful result.</summary>
    /// <param name="config">The loaded configuration.</param>
    /// <returns>The result.</returns>
    public static ConfigLoadResult Loaded(GameLockerConfig config) =>
        new(ConfigLoadStatus.Loaded, config, null);

    /// <summary>Creates a result indicating no configuration has been saved.</summary>
    /// <returns>The result.</returns>
    public static ConfigLoadResult NotFound() =>
        new(ConfigLoadStatus.NotFound, null, null);

    /// <summary>Creates a result indicating the configuration is damaged.</summary>
    /// <param name="error">A description of the failure.</param>
    /// <returns>The result.</returns>
    public static ConfigLoadResult Unreadable(string error) =>
        new(ConfigLoadStatus.Unreadable, null, error);
}
