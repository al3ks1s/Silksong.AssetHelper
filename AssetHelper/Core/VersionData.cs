using System.Reflection;

namespace Silksong.AssetHelper.Core;

internal static class VersionData
{
    #region Silksong Version
    /// <summary>
    /// The Silksong version. This is calculated using reflection to avoid it being inlined.
    /// </summary>
    public static string SilksongVersion
    {
        get
        {
            _silksongVersion ??= GetSilksongVersion();
            return _silksongVersion;
        }
    }

    private static string? _silksongVersion;

    private static string GetSilksongVersion() => typeof(Constants)
        .GetField(nameof(Constants.GAME_VERSION), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
        ?.GetRawConstantValue()
        as string
        ?? "UNKNOWN";
    #endregion


}
