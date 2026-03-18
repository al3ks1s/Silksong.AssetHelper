using Silksong.AssetHelper.Plugin.LoadingPage;
using System.Collections;

namespace Silksong.AssetHelper.Plugin;

/// <summary>
/// Base class for tasks that happen at startup.
/// </summary>
public abstract class BaseStartupTask
{
    // Note - for tasks defined by AssetHelper the enumerator will be executed safely
    // and the objects yielded will be passed to unity.
    /// <summary>
    /// Run the startup task. The enumerator will be executed by unity.
    /// </summary>
    /// <param name="loadingScreen">A loading screen.</param>
    /// <remarks>
    /// Tasks executed this way will usually be fairly slow and cpu-intensive tasks.
    /// There is not a good way to decide when to yield, except that:
    /// - After updating the loading screen, you should yield or the change may not be visible
    /// - You should yield after every chunk of work, so that Unity knows your program hasn't hung
    /// </remarks>
    public abstract IEnumerator Run(ILoadingScreen loadingScreen);
}
