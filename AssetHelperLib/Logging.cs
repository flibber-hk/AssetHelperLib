using System;

namespace AssetHelperLib;

/// <summary>
/// Class used by AssetHelperLib to write logs.
/// </summary>
public static class Logging
{
    /// <summary>
    /// On logging an "INFO" level message.
    /// </summary>
    public static event Action<string>? OnLog;

    internal static void LogInfo(string message) => OnLog?.Invoke(message);

    /// <summary>
    /// On logging an "WARNING" level message.
    /// </summary>
    public static event Action<string>? OnLogWarning;

    internal static void LogWarning(string message) => OnLogWarning?.Invoke(message);

    /// <summary>
    /// On logging an "ERROR" level message.
    /// </summary>
    public static event Action<string>? OnLogError;

    internal static void LogError(string message) => OnLogError?.Invoke(message);

    /// <summary>
    /// On logging a "DEBUG" level message.
    /// </summary>
    public static event Action<string>? OnLogDebug;

    internal static void LogDebug(string message) => OnLogDebug?.Invoke(message);
}
