using AssetHelperLib.Repacking;
using Silksong.AssetHelper.Internal;
using Silksong.AssetHelper.Plugin.Tasks;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Data about a repacked scene bundle used by AssetHelper.
/// </summary>
internal sealed class RepackedSceneBundleData
{
    /// <summary>
    /// The metadata when creating the bundle.
    /// </summary>
    public CachedFileMetadata Metadata { get; init; } = CachedFileMetadata.CreateNew();
    
    /// <summary>
    /// The name of the scene used to generate the bundle.
    /// </summary>
    public required string SceneName { get; init; }

    /// <summary>
    /// The hash of the original bundle as it appears in the default Addressables catalog.
    /// </summary>
    public string? BundleHash { get; init; }

    /// <summary>
    /// The data generated for the repacked bundle.
    /// </summary>
    public RepackedBundleData? Data { get; set; } = null;

    /// <summary>
    /// Info used to build the catalog entries for the repacked bundle.
    /// </summary>
    public SceneCatalogInfo? CatalogInfo { get; set; } = null;
}
