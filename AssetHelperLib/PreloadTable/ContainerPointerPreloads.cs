using AssetHelperLib.BundleTools;
using AssetHelperLib.Repacking;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AssetHelperLib.PreloadTable;

/// <summary>
/// Data cache for an asset bundle for the <see cref="ContainerPointerPreloads"/> table resolver.
/// </summary>
public sealed class ContainerPointerPreloadsBundleData
{
    /// <summary>
    /// List of path ids to assets in the container of the asset bundle.
    /// </summary>
    public required List<long> ContainerPaths { get; set; }

    /// <summary>
    /// If a key is in the container, then the corresponding value is
    /// the internal dependencies of the key.
    /// </summary>
    public Dictionary<long, HashSet<long>> ContainerInternalDeps { get; set; } = [];

    /// <summary>
    /// Load the given bundle file and compute the required data.
    /// </summary>
    public static ContainerPointerPreloadsBundleData FromFile(string bundlePath)
    {
        AssetsManager mgr = BundleUtils.CreateDefaultManager();
        
        using MemoryStream ms = new(File.ReadAllBytes(bundlePath));
        BundleFileInstance bunInst = mgr.LoadBundleFile(ms, bundlePath);
        AssetsFileInstance afi = mgr.LoadAssetsFileFromBundle(bunInst, 0);

        AssetTypeValueField iBundle = mgr.GetBaseField(afi, 1);  // Internal bundle always at pathid = 1 for non-scene bundles
        List<long> containerPaths = iBundle["m_Container.Array"]
            .Children
            .Select(x => x["second.asset.m_PathID"].AsLong)
            .ToList();

        ContainerPointerPreloadsBundleData result = new() { ContainerPaths = containerPaths };

        AssetDependencies deps = new(mgr, afi);
        foreach (long cPathId in containerPaths)
        {
            result.ContainerInternalDeps[cPathId] = deps.FindBundleDeps(cPathId).InternalPaths;
        }

        mgr.UnloadAll();
        return result;
    }
}

/// <summary>
/// Augment the existing preload table as follows.
/// If (fileId, pathId) is in the preload table but is not a container path,
/// then let cPathId be a container path that has pathId as an internal dependency.
/// Then (fileId, cPathId) will be added to the preload table.
/// 
/// If multiple container paths satisfy this property, then only one will be chosen, and
/// which one should be treated as undefined.
/// 
/// The rationale for this class is that if pathId is not in the container, then Unity may struggle to
/// preload it. But if cPathId is in the container, then Unity *should* have less trouble preloading it,
/// and then obviously pathId should be loaded by virtue of being an internal dependency.
/// </summary>
public class ContainerPointerPreloads(
    ContainerPointerPreloads.CabResolver cabResolver) : BasePreloadTableResolver
{
    /// <summary>
    /// Given a CAB name, return the path to the corresponding asset bundle.
    /// </summary>
    /// <param name="cabName">The lower-case cab name of the bundle.</param>
    /// <param name="bundlePath">The path to the bundle. This should be null if the cab is recognized
    /// but the bundle should be excluded from the preload table population operation.</param>
    /// <returns>True if the bundle was resolved, false otherwise.</returns>
    public delegate bool CabResolver(string cabName, out string? bundlePath);

    private readonly CabResolver _cabResolver = cabResolver;

    /// <summary>
    /// Cached data used by this instance.
    /// Key: cab name (lower case).
    /// Value: Cached data for the cab file.
    /// </summary>
    public Dictionary<string, ContainerPointerPreloadsBundleData> Cache { get; init; } = [];


    /// <inheritdoc />
    public override void BuildPreloadTable(long assetPathId, RepackingContext ctx, ref HashSet<(int fileId, long pathId)> tableInfos)
    {
        List<(int fileId, HashSet<long> pathIds)> groupedTableinfos = tableInfos
            .GroupBy(item => item.fileId)
            .Select(group => (
                group.Key,
                new HashSet<long>(group.Select(g => g.pathId))
            ))
            .ToList();

        foreach ((int fileId, HashSet<long> pathIds) in groupedTableinfos)
        {
            AssetsFileExternal extFile = ctx.MainAssetsFileInstance.file.Metadata.Externals[fileId - 1];
            string cabName = extFile.OriginalPathName.Split("/").Last().ToLowerInvariant();

            if (!_cabResolver.Invoke(cabName, out string? bundlePath))
            {
                Logging.LogWarning(
                    $"Unexpectedly failed to resolve cab name {cabName} for {nameof(ContainerPointerPreloads)} " +
                    $"while running for {ctx.MainAssetsFileInstance.name}");
                continue;
            }
            if (bundlePath == null)
            {
                continue;
            }

            if (!Cache.TryGetValue(cabName, out ContainerPointerPreloadsBundleData data))
            {
                Logging.LogInfo($"Building cache for {cabName}");
                data = ContainerPointerPreloadsBundleData.FromFile(bundlePath);
                Cache[cabName] = data;
            }

            foreach (long pathId in pathIds)
            {
                if (data.ContainerPaths.Contains(pathId))
                {
                    // Already in the container
                    continue;
                }

                bool found = false;
                foreach ((long cPathId, HashSet<long> cDeps) in data.ContainerInternalDeps)
                {
                    if (cDeps.Contains(pathId))
                    {
                        found = true;
                        
                        if (tableInfos.Add((fileId, cPathId)))
                        {
                            Logging.LogDebug($"Added new {cPathId} for {pathId} within {cabName}, for {ctx.MainAssetsFileInstance.name} :: {assetPathId}");
                        }
                        
                        break;
                    }
                }
                if (found)
                {
                    continue;
                }

                Logging.LogWarning($"Failed to find container asset for cab {cabName}, path {pathId} while running for {ctx.MainAssetsFileInstance.name}");
            }
        }
    }
}
