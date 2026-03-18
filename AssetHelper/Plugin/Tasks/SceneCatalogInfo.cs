using System.Collections.Generic;
using System.Linq;

namespace Silksong.AssetHelper.Plugin.Tasks;

/// <summary>
/// Class containing the data used to construct the catalog entries for a single repacked scene.
/// </summary>
internal class SceneCatalogInfo
{
    public record GameObjectInfo(string ObjPath, string ContainerPath, long TransformPathId);
    public record ChildGameObjectInfo(
        string ObjPath,
        long TransformPathId,
        string AncestorObjPath,
        string RelativePath,
        long AncestorTransformPathId);

    /// <summary>
    /// Game objects which are at the root of the repacked bundle (not necessarily a root in the original scene).
    /// </summary>
    public List<GameObjectInfo> RootGameObjects { get; init; } = [];

    /// <summary>
    /// Game objects which are not at the root in the repacked bundle, but should still be made addressable.
    /// </summary>
    public List<ChildGameObjectInfo> ChildGameObjects { get; init; } = [];

    public IEnumerable<string> LoadableAssets => [
        .. RootGameObjects.Select(x => x.ObjPath),
        .. ChildGameObjects.Select(x => x.ObjPath)
        ];
}
