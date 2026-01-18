using AssetHelperLib.BundleTools;
using AssetsTools.NET.Extra;
using static AssetHelperLib.BundleTools.BundleUtils;

namespace AssetHelperLib.Repacking;

/// <summary>
/// Class holding the context for the current scene repacking operation.
/// </summary>
public class RepackingContext
{
    /// <summary>
    /// The AssetsManager used to load this scene bundle.
    /// This will be unloaded at the end of the repacking operation.
    /// </summary>
    public required AssetsManager SceneAssetsManager { get; init; }

    /// <summary>
    /// The bundle file instance that is being repacked.
    /// </summary>
    public required BundleFileInstance SceneBundleFileInstance { get; init; }

    /// <summary>
    /// The SharedAssets file for the scene.
    /// </summary>
    public required AssetsFileInstance SharedAssetsFileInstance { get; init; }

    /// <summary>
    /// The main assets file for the scene.
    /// </summary>
    public required AssetsFileInstance MainAssetsFileInstance { get; init; }

    /// <summary>
    /// The info about the assets file indices in the scene bundle.
    /// </summary>
    public required SceneBundleInfo SceneBundleInfo { get; init; }

    /// <summary>
    /// The Game Object Lookup for the current repacking operation.
    /// </summary>
    public GameObjectLookup? GoLookup { get; set; }

    private AssetDependencies? _assetDeps;

    /// <summary>
    /// The main dependency resolver for the current repacking operation.
    /// </summary>
    public AssetDependencies AssetDeps
    {
        get
        {
            _assetDeps ??= new(SceneAssetsManager, MainAssetsFileInstance);
            return _assetDeps;
        }
    }
}
