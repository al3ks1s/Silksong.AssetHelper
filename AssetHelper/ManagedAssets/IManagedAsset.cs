namespace Silksong.AssetHelper.ManagedAssets;

/// <summary>
/// Interface representing an asset (or assets) that can be freely loaded and unloaded.
/// </summary>
public interface IManagedAsset
{
    /// <summary>
    /// Load the asset(s) managed by this instance.
    /// </summary>
    /// <returns>
    /// An object that can be "yield return"-ed in a coroutine to pause execution
    /// until the load is complete.
    /// </returns>
    object? Load();

    /// <summary>
    /// Unload the asset(s) managed by this instance.
    /// </summary>
    void Unload();
}
