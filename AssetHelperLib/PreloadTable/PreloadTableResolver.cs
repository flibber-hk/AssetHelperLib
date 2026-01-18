using AssetHelperLib.Repacking;
using System.Collections.Generic;

namespace AssetHelperLib.PreloadTable;

/// <summary>
/// Represents a union of preload table resolvers.
/// 
/// The <see cref="DefaultPreloadTableResolver" /> should typically be included.
/// </summary>
public sealed class PreloadTableResolver(List<BasePreloadTableResolver> resolvers) : BasePreloadTableResolver
{
    private readonly List<BasePreloadTableResolver> _resolvers = resolvers;

    /// <inheritdoc />
    public override HashSet<(int fileId, long pathId)> BuildPreloadTable(long assetPathId, RepackingContext ctx)
    {
        HashSet<(int fileId, long pathId)> data = [];
        foreach (BasePreloadTableResolver resolver in _resolvers)
        {
            data.UnionWith(resolver.BuildPreloadTable(assetPathId, ctx));
        }

        return data;
    }
}
