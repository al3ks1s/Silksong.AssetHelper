using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Silksong.AssetHelper.Internal;

namespace Silksong.AssetHelper.Plugin;

internal class AssetRequest
{
    public Dictionary<string, HashSet<string>> SceneAssets { get; set; } = [];

    [JsonConverter(typeof(DictListConverter<(string bundleName, string assetName), Type>))]
    public Dictionary<(string bundleName, string assetName), Type> NonSceneAssets { get; set; } = [];

    [JsonIgnore]
    public bool AnyRequestMade => SceneAssets.Count > 0 || NonSceneAssets.Count > 0;
}
