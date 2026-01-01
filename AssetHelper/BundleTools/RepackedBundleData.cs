using System.Collections.Generic;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Data about a repacked bundle.
/// </summary>
public class RepackedBundleData
{
    /// <summary>
    /// The name of the internal asset bundle.
    /// </summary>
    public string? BundleName { get; set; }

    /// <summary>
    /// The CAB name of the bundle file.
    /// </summary>
    public string? CabName { get; set; }

    /// <summary>
    /// A lookup {name in container -> original game object path} for game object assets in the container.
    /// </summary>
    public Dictionary<string, string>? GameObjectAssets { get; set; }
}
