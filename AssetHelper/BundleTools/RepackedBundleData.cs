using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using UnityEngine.AddressableAssets.ResourceLocators;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Data about a repacked bundle.
/// </summary>
public class RepackedBundleData
{
    /// <summary>
    /// The Silksong version used to create this bundle.
    /// </summary>
    public string? SilksongVersion { get; set; }

    /// <summary>
    /// The Asset Helper version used to create this bundle.
    /// </summary>
    public string? PluginVersion { get; set; }
    
    /// <summary>
    /// Construct an instance of this class with default version parameters.
    /// </summary>
    public RepackedBundleData()
    {
        SilksongVersion = AssetPaths.SilksongVersion;
        PluginVersion = AssetHelperPlugin.Version;
    }

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

    /// <summary>
    /// A list of content catalog entries to be serialized. Must contain the assets entries and all the necessary dependencies.
    /// </summary>
    public List<ContentCatalogDataEntry>? CatalogDataEntries { get; set; }

    /// <summary>
    /// Assets which were requested but failed to be repacked.
    /// </summary>
    public List<string>? NonRepackedAssets { get; set; }
}
