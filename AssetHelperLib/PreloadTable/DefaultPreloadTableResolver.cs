using AssetHelperLib.Repacking;
using System.Collections.Generic;
using System.Linq;
using PPtrData = AssetHelperLib.BundleTools.AssetDependencies.PPtrData;

namespace AssetHelperLib.PreloadTable;

/// <summary>
/// Get object dependencies by following all direct dependencies
/// within the current bundle, and any external dependencies reached get added to the preload table.
/// </summary>
public sealed class DefaultPreloadTableResolver : BasePreloadTableResolver
{
    /// <inheritdoc />
    public override void BuildPreloadTable(long assetPathId, RepackingContext ctx, ref HashSet<(int fileId, long pathId)> tableInfos)
    {
        HashSet<PPtrData> deps = ctx.AssetDeps.FindBundleDeps(assetPathId).ExternalPaths;

        tableInfos.UnionWith(deps.Select(dep => (dep.FileId, dep.PathId)));
    }
}
