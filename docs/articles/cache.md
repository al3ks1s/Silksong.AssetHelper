# Caching

AssetHelper caches a number of files to reduce the computation burden of future times the game is
loaded up. In some cases there is a technical consideration, since Addressables does not expect
to load asset bundles and catalogs from memory.

The caches are stored in the BepInEx/cache/AssetHelper directory.
If you have been doing a lot of testing, it can be good to test your mod with a fresh AssetHelper cache
(for example, rename the AssetHelper folder to AssetHelperBackup - or just delete it)
to verify that things should work for your users - although generally this is not necessary.
It may also be the case that existing cached files are broken - in this case the user might be able
to fix the issue by clearing the cache.

## Bundle metadata lookups

These can include lookups of the following metadata:
* CAB names - these are internal bundle names that Unity uses to resolve dependencies.
* Bundle names - these give the value of the runtime `AssetBundle.Name` property.
* Bundle dependencies - this enumerates bundles which a given bundle depends on.

In general these are not too costly to compute but it is still noticeably more efficient to cache the outputs.
These are invalidated each time Silksong updates.

## Repacked scene bundles

These bundles contain assets from scene bundles that have been repacked into non-scene bundles
for easier access. There is one bundle per scene, and these bundles are shared between all mods.

Any assets which are requested will never be removed. For example, if mod X requests an asset from
a scene, and is then uninstalled and replaced with mod Y (which requests a different asset from that scene),
then the asset requested by mod X will remain in the bundle, to avoid paying the cost if the user
decides to re-install mod X. 

These bundles will be invalidated if the hash of the bundle is changed (in other words, if the
content of the base game bundle is changed). If Silksong is updated in such a way that the
base game bundle is not modified, then any existing repacked bundles will remain valid.

## Addressables catalogs

AssetHelper writes an Addressables catalog for repacked scene assets and a catalog for requested
non-scene assets.

The scene catalog contains all requested and repacked scene assets. Note that this includes assets
which have previously been requested by mods that have since been uninstalled. Obviously this behaviour
should not be relied upon, and any assets that need to be used should be requested. The scene catalog
is invalidated (and regenerated) if any bundle is repacked.
In particular, if a new mod is added which requests an asset which already exists in the last
catalog, the catalog will not be regenerated.

The non-scene catalog contains only the assets which have been requested this time the game was loaded.
If no new assets have been added since the last time the game was loaded, the catalog will not be rewritten.

All catalogs will be regenerated when the Silksong version changes.

## Downpatching

Changing Silksong version can and will cause cached data to be rewritten. If a player is going to be
downpatching their Silksong version frequently, then they should take care to use a separate
profile for each Silksong version they want to play with, to avoid any unnecessary additional computation.
