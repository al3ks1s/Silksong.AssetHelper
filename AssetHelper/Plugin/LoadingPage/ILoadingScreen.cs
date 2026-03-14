using UnityEngine;

namespace Silksong.AssetHelper.Plugin.LoadingPage;

/// <summary>
/// Interface defining the contract for a loading screen.
/// </summary>
public interface ILoadingScreen
{
    /// <summary>
    /// Set the large text above the progress bar.
    /// Typically used to describe the operation that is going on (e.g. "Repacking Scenes").
    /// </summary>
    public void SetText(string text);

    /// <summary>
    /// Set the small text below the progress bar.
    /// Typically used for more detailed updates (e.g. a particular scene name).
    /// This will often be left blank.
    /// </summary>
    /// <param name="text"></param>
    public void SetSubtext(string text);

    /// <summary>
    /// Set the progress of the progress bar.
    /// </summary>
    /// <param name="progress">A float between 0 and 1 indicating the progress.</param>
    public void SetProgress(float progress);

    /// <summary>
    /// Set whether the screen should be visible.
    /// This method should rarely be used.
    /// </summary>
    public void SetVisible(bool visible);
}

internal static class LoadingScreenExtensions
{
    public static T Create<T>() where T : MonoBehaviour, ILoadingScreen
    {
        GameObject go = new("AssetHelper LoadingScreen");
        T ret = go.AddComponent<T>();
        go.SetActive(true);
        return ret;
    }

    public static void Reset(this ILoadingScreen self)
    {
        self.SetText(string.Empty);
        self.SetSubtext(string.Empty);
        self.SetProgress(0);
        self.SetVisible(true);
    }
}
