using Silksong.AssetHelper.Core;

namespace Silksong.AssetHelper.Internal;

internal class CachedFileMetadata
{
    public required string SilksongVersion { get; init; }

    public required string PluginVersion { get; init; }

    public required string OSFolderName { get; init; }

    public static CachedFileMetadata CreateNew()
    {
        CachedFileMetadata data = new()
        {
            SilksongVersion = VersionData.SilksongVersion,
            PluginVersion = AssetHelperPlugin.Version,
            OSFolderName = AssetPaths.OSFolderName,
        };

        return data;
    }
}
