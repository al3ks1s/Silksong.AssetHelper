using System.Collections.Generic;

namespace Silksong.AssetHelper.BundleTools;

/// <summary>
/// Class encapsulating the names of loaded asset bundles.
/// </summary>
public class LoadedBundleNames(List<string> names, List<string> unknown)
{
    /// <summary>
    /// Names given as paths relative to the bundle base dir.
    /// </summary>
    public List<string> Names = names;

    /// <summary>
    /// Names of bundles that could not be found in the bundle base dir (e.g. modded bundles).
    /// </summary>
    public List<string> Unknown = unknown;
}
