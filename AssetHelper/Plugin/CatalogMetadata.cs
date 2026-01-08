using System;
using System.Collections.Generic;

namespace Silksong.AssetHelper.Plugin;

internal abstract class CatalogMetadata
{
    public string SilksongVersion { get; set; } = AssetPaths.SilksongVersion;

    public string PluginVersion { get; set; } = AssetHelperPlugin.Version;
}

/// <summary>
/// Data about the scene asset catalog written by AssetHelper.
/// </summary>
internal class SceneCatalogMetadata : CatalogMetadata
{
    // TODO - list catalogued objects
}

internal class NonSceneCatalogMetadata : CatalogMetadata
{
    public List<(string bundleName, string assetName, Type assetType)> CatalogAssets { get; set; } = [];
}