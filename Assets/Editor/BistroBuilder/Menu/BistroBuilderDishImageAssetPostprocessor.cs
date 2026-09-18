using System;
using UnityEditor;

public sealed class BistroBuilderDishImageAssetPostprocessor : AssetPostprocessor
{
    private static bool rebuildPending;

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths
    )
    {
        if (rebuildPending ||
            (!ContainsDishImage(importedAssets) &&
             !ContainsDishImage(deletedAssets) &&
             !ContainsDishImage(movedAssets) &&
             !ContainsDishImage(movedFromAssetPaths)))
        {
            return;
        }

        rebuildPending = true;
        EditorApplication.delayCall += RebuildDelayed;
    }

    private static bool ContainsDishImage(string[] paths)
    {
        if (paths == null)
        {
            return false;
        }

        for (int i = 0; i < paths.Length; i++)
        {
            if (BistroBuilderDishImageCatalogImporter.IsSupportedDishImagePath(paths[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void RebuildDelayed()
    {
        rebuildPending = false;
        try
        {
            BistroBuilderDishImageCatalogImporter.Rebuild();
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogException(exception);
        }
    }
}
