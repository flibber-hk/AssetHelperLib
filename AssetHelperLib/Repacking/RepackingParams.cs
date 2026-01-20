using System.Collections.Generic;

namespace AssetHelperLib.Repacking;

/// <summary>
/// Params used for a repacking operation.
/// </summary>
public record RepackingParams
{
    /// <summary>
    /// A path to the scene bundle.
    /// </summary>
    public required string SceneBundlePath { get; set; }

    /// <summary>
    /// A list of game objects to include.
    /// Any game object in this list should have an ancestor which is accessible via UnityEngine.AssetBundle.LoadAsset,
    /// provided the supplied game object exists in the bundle.
    /// </summary>
    public required List<string> ObjectNames { get; set; }

    /// <summary>
    /// Prefix to use for asset paths in the container of this bundle.
    /// </summary>
    public required string ContainerPrefix { get; set; }

    /// <summary>
    /// File location for the repacked bundle.
    /// </summary>
    public required string OutBundlePath { get; set; }
}
