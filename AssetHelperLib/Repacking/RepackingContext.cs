using AssetHelperLib.BundleTools;
using AssetsTools.NET.Extra;

namespace AssetHelperLib.Repacking;

/// <summary>
/// Class holding the context for the current scene repacking operation.
/// </summary>
public class RepackingContext
{
    /// <summary>
    /// The AssetsManager used to load this scene bundle.
    /// This should be unloaded at the end of the repacking operation.
    /// </summary>
    public required AssetsManager SceneAssetsManager { get; set; }

    /// <summary>
    /// The bundle file instance that is being repacked.
    /// </summary>
    public required BundleFileInstance SceneBundleFileInstance { get; set; }

    /// <summary>
    /// The SharedAssets file for the scene.
    /// </summary>
    public required AssetsFileInstance SharedAssetsFileInstance { get; set; }

    /// <summary>
    /// The main assets file for the scene.
    /// </summary>
    public required AssetsFileInstance MainAssetsFileInstance { get; set; }

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
