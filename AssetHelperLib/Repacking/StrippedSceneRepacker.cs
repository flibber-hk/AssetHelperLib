using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetHelperLib.Util;
using System.Collections.Generic;
using System.Linq;
using GameObjectInfo = AssetHelperLib.BundleTools.GameObjectLookup.GameObjectInfo;
using AssetHelperLib.BundleTools;
using AssetHelperLib.PreloadTable;
using PPtrInfo = (int fileId, long pathId);

namespace AssetHelperLib.Repacking;

/// <summary>
/// Class that repacks scenes by taking a minimal set of objects in the scene that allow
/// all provided game objects to be loaded.
/// 
/// Any game objects whose parents are not needed will be deparented.
/// </summary>
public class StrippedSceneRepacker : SceneRepacker
{
    private BasePreloadTableResolver _preloadResolver;

    /// <summary>
    /// Create an instance of this class with the default settings.
    /// </summary>
    public StrippedSceneRepacker() : this(new DefaultPreloadTableResolver()) { }

    /// <summary>
    /// Create an instance of this class with the specified preload resolver.
    /// </summary>
    public StrippedSceneRepacker(BasePreloadTableResolver preloadResolver)
    {
        _preloadResolver = preloadResolver;
    }
    
    /// <inheritdoc />
    protected override void Run(RepackingContext ctx, RepackingParams repackingParams, RepackedBundleData outData)
    {
        List<string> objectNames = repackingParams.ObjectNames.GetHighestNodes();

        // Setup GO lookup
        ctx.GameObjLookup = GameObjectLookup.CreateFromFile(ctx.SceneAssetsManager, ctx.MainAssetsFileInstance);

        // Find internal deps, everything else can be stripped
        HashSet<long> includedPathIds = [];

        foreach (string objName in objectNames)
        {
            if (ctx.GameObjLookup.TryLookupName(objName, out GameObjectInfo? info))
            {
                includedPathIds.Add(info.GameObjectPathId);
                includedPathIds.UnionWith(ctx.AssetDeps.FindBundleDeps(info.GameObjectPathId).InternalPaths);
            }
            else
            {
                Logging.LogError($"Couldn't find game object {objName}");
            }
        }

        // Collect all game objects
        List<string> includedGos = [];
        foreach (long pathId in includedPathIds)
        {
            if (ctx.GameObjLookup.TryLookupGameObject(pathId, out GameObjectInfo? info))
            {
                includedGos.Add(info.GameObjectName);
            }
        }
        List<string> rootmostGos = includedGos.GetHighestNodes();

        // Generate a path for each rootmost go which has a child in the request
        HashSet<string> includedContainerGos = [];
        List<string> missingObjects = [];
        foreach (string objName in objectNames)
        {
            if (ObjPathUtil.TryFindAncestor(rootmostGos, objName, out string? ancestor, out _))
            {
                includedContainerGos.Add(ancestor);
            }
            else
            {
                Logging.LogWarning($"Did not find {objName} in bundle");
                missingObjects.Add(objName);
            }
        }
        outData.NonRepackedAssets = missingObjects;

        // Strip all assets that are not needed
        foreach (AssetFileInfo afileInfo in ctx.MainAssetsFileInstance.file.AssetInfos.ToList())
        {
            if (!includedPathIds.Contains(afileInfo.PathId))
            {
                ctx.MainAssetsFileInstance.file.Metadata.RemoveAssetInfo(afileInfo);
            }
        }

        // Determine the new path ID for the asset at path=1
        long newOneAssetPathId = 1;

        if (includedPathIds.Contains(1))
        {
            newOneAssetPathId = -1;
            while (includedPathIds.Contains(newOneAssetPathId))
            {
                newOneAssetPathId--;
            }
        }

        long updatedPathId(long orig) => orig == 1 ? newOneAssetPathId : orig;

        // Deparent transforms which are now rooted
        foreach (GameObjectInfo current in ctx.GameObjLookup)
        {
            if (!includedPathIds.Contains(current.TransformPathId))
            {
                continue;
            }

            if (!current.GameObjectName.TryGetParent(out string parentName))
            {
                // No need to deparent what is already a root go
                continue;
            }

            if (!ctx.GameObjLookup.TryLookupName(parentName, out GameObjectInfo? parentInfo))
            {
                Logging.LogWarning($"Unexpectedly failed to find {parentName} from {current.GameObjectName}");
                continue;
            }

            if (includedPathIds.Contains(parentInfo.TransformPathId))
            {
                continue;
            }

            // We now have to deparent the object
            AssetFileInfo afInfo = ctx.MainAssetsFileInstance.file.GetAssetInfo(current.TransformPathId);
            AssetTypeValueField transformField = ctx.SceneAssetsManager.GetBaseField(ctx.MainAssetsFileInstance, afInfo);
            transformField["m_Father.m_PathID"].AsLong = 0;
            afInfo.SetNewData(transformField);
        }

        // Set up the internal bundle
        AssetFileInfo internalBundle = ctx.SharedAssetsFileInstance.file.GetAssetsOfType(AssetClassID.AssetBundle).First();
        AssetTypeValueField iBundleData = ctx.SceneAssetsManager.GetBaseField(ctx.SharedAssetsFileInstance, internalBundle);

        // Set simple data
        iBundleData["m_Name"].AsString = outData.BundleName;
        iBundleData["m_AssetBundleName"].AsString = outData.BundleName;
        iBundleData["m_IsStreamedSceneAssetBundle"].AsBool = false;
        iBundleData["m_SceneHashes.Array"].Children.Clear();

        // Set up the container
        List<AssetTypeValueField> preloadPtrs = [];
        List<AssetTypeValueField> newChildren = [];
        Dictionary<string, string> containerPaths = [];

        foreach (string containerGo in includedContainerGos)
        {
            GameObjectInfo cgInfo = ctx.GameObjLookup.LookupName(containerGo);
            long cgPathId = cgInfo.GameObjectPathId;

            HashSet<PPtrInfo> deps = [];
            _preloadResolver.BuildPreloadTable(cgPathId, ctx, ref deps);

            int start = preloadPtrs.Count;

            foreach (PPtrInfo info in deps)
            {
                AssetTypeValueField depPtr = ValueBuilder.DefaultValueFieldFromArrayTemplate(iBundleData["m_PreloadTable.Array"]);
                depPtr["m_FileID"].AsInt = info.fileId;
                depPtr["m_PathID"].AsLong = info.pathId;
                preloadPtrs.Add(depPtr);
            }

            int count = preloadPtrs.Count - start;

            string containerPath = $"{repackingParams.ContainerPrefix}/{containerGo}.prefab";
            containerPaths[containerPath] = containerGo;

            AssetTypeValueField newChild = ValueBuilder.DefaultValueFieldFromArrayTemplate(iBundleData["m_Container.Array"]);
            newChild["first"].AsString = containerPath;
            newChild["second.preloadIndex"].AsInt = start;
            newChild["second.preloadSize"].AsInt = count;
            newChild["second.asset.m_FileID"].AsInt = 0;
            newChild["second.asset.m_PathID"].AsLong = updatedPathId(cgInfo.GameObjectPathId);
            newChildren.Add(newChild);
        }

        iBundleData["m_PreloadTable.Array"].Children.Clear();
        iBundleData["m_PreloadTable.Array"].Children.AddRange(preloadPtrs);
        iBundleData["m_Container.Array"].Children.Clear();
        iBundleData["m_Container.Array"].Children.AddRange(newChildren);
        outData.GameObjectAssets = containerPaths;

        // Move the asset at pathId = 1 to newOneAssetPathId
        if (newOneAssetPathId != 1)
        {
            int redirectCount = 0;

            AssetFileInfo toMove = ctx.MainAssetsFileInstance.file.GetAssetInfo(1);
            ctx.MainAssetsFileInstance.file.Metadata.RemoveAssetInfo(toMove);
            toMove.PathId = newOneAssetPathId;
            ctx.MainAssetsFileInstance.file.Metadata.AddAssetInfo(toMove);

            foreach (long pathId in includedPathIds)
            {
                if (pathId == 1) continue;

                if (!ctx.AssetDeps.FindImmediateDeps(pathId).InternalPaths.Contains(1))
                {
                    continue;
                }

                redirectCount += ctx.SceneAssetsManager.Redirect(
                    ctx.MainAssetsFileInstance, ctx.MainAssetsFileInstance.file.GetAssetInfo(pathId), 1, newOneAssetPathId);
            }

            int locRedirect = ctx.SceneAssetsManager.Redirect(ctx.MainAssetsFileInstance, toMove, 1, newOneAssetPathId);  // Just in case
            Logging.LogInfo($"Redirected {redirectCount} references plus {locRedirect} self-references");
        }

        // Move updated internal bundle into the main assets file
        // Copy the asset bundle type tree from the shared assets to the main bundle
        if (!ctx.MainAssetsFileInstance.file.Metadata.TypeTreeTypes.Any(x => x.TypeId == (int)AssetClassID.AssetBundle))
        {
            TypeTreeType t = ctx.SharedAssetsFileInstance.file.Metadata.TypeTreeTypes.First(x => x.TypeId == (int)AssetClassID.AssetBundle);
            ctx.MainAssetsFileInstance.file.Metadata.TypeTreeTypes.Add(t);
        }

        AssetFileInfo newInternalBundle = AssetFileInfo.Create(
            ctx.MainAssetsFileInstance.file, ctx.SceneBundleInfo.mainAfileInstIndex, (int)AssetClassID.AssetBundle);
        newInternalBundle.SetNewData(iBundleData);
        ctx.MainAssetsFileInstance.file.Metadata.AddAssetInfo(newInternalBundle);

        ctx.SceneBundleFileInstance.file.BlockAndDirInfo.DirectoryInfos[ctx.SceneBundleInfo.mainAfileInstIndex]
            .SetNewData(ctx.MainAssetsFileInstance.file);
        ctx.SceneBundleFileInstance.file.BlockAndDirInfo.DirectoryInfos[ctx.SceneBundleInfo.mainAfileInstIndex]
            .Name = outData.CabName;

        int tot = ctx.SceneBundleFileInstance.file.BlockAndDirInfo.DirectoryInfos.Count;
        for (int i = 0; i < tot; i++)
        {
            if (i == ctx.SceneBundleInfo.mainAfileInstIndex) { continue; }
            ctx.SceneBundleFileInstance.file.BlockAndDirInfo.DirectoryInfos[i].SetRemoved();
        }

        // Write the bundle
        ctx.SceneBundleFileInstance.file.WriteBundleToFile(repackingParams.OutBundlePath);
    }
}
