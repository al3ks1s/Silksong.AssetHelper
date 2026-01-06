using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace Silksong.AssetHelper.CatalogTools;

/// <summary>
/// Resource provider that returns a child gameobject of its first dependency.
/// 
/// The internal ID must be the path to the child relative to the parent.
/// </summary>
internal class ChildGameObjectProvider : ResourceProviderBase
{
    public static string ClassProviderId => "Silksong.AssetHelper.CatalogTools.ChildObjectProvider";

    public override Type GetDefaultType(IResourceLocation location)
    {
        return typeof(GameObject);
    }

    public override string ProviderId => ClassProviderId;

    public override void Provide(ProvideHandle provideHandle)
    {
                
        List<object> deps = [];
        provideHandle.GetDependencies(deps);

        foreach (var thing in deps)
        {
            AssetHelperPlugin.InstanceLogger.LogInfo(thing);
        }

        string childPath = provideHandle.Location.InternalId;

        GameObject parent = provideHandle.GetDependency<GameObject>(0);

        if (parent != null)
        {
            Transform childTransform = parent.transform.Find(childPath);
            if (childTransform != null)
            {
                provideHandle.Complete(childTransform.gameObject, true, null);
            }
            else
            {
                provideHandle.Complete<GameObject>(null!, false, new Exception($"Child '{childPath}' not found in {parent.name}"));
            }
        }
        else
        {
            provideHandle.Complete<GameObject>(null!, false, new Exception("Parent dependency failed to load or is not a GameObject."));
        }
    }
}
