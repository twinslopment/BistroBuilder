using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicPersistenceCatalogIntegration
    {
        internal const string Version = "1.0.0";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        internal static int EnsureProjectSceneBindings()
        {
            RestaurantPlaceableCatalogDefinition mainCatalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (mainCatalog == null)
            {
                throw new InvalidOperationException(
                    "Canonical placeable catalog is missing: " +
                    MainCatalogPath);
            }

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();

            int changedSceneCount = 0;

            try
            {
                string[] sceneGuids =
                    AssetDatabase.FindAssets(
                        "t:Scene",
                        new[] { "Assets/Scenes" });

                Array.Sort(
                    sceneGuids,
                    StringComparer.Ordinal);

                for (int index = 0;
                     index < sceneGuids.Length;
                     index++)
                {
                    string scenePath =
                        AssetDatabase.GUIDToAssetPath(
                            sceneGuids[index]);

                    if (string.IsNullOrWhiteSpace(scenePath))
                        continue;

                    Scene scene =
                        EditorSceneManager.OpenScene(
                            scenePath,
                            OpenSceneMode.Single);

                    BistroBuilderSaveDefinitionCatalog[] catalogs =
                        UnityEngine.Object.FindObjectsByType
                            <BistroBuilderSaveDefinitionCatalog>(
                                FindObjectsInactive.Include,
                                FindObjectsSortMode.None);

                    bool sceneChanged = false;

                    for (int catalogIndex = 0;
                         catalogIndex < catalogs.Length;
                         catalogIndex++)
                    {
                        BistroBuilderSaveDefinitionCatalog catalog =
                            catalogs[catalogIndex];

                        if (catalog == null ||
                            catalog.gameObject.scene != scene)
                        {
                            continue;
                        }

                        if (EnsureCatalogBinding(
                                catalog,
                                mainCatalog))
                        {
                            sceneChanged = true;
                        }

                        if (!catalog.ValidateConfiguration(
                                out string error))
                        {
                            throw new InvalidOperationException(
                                "Save definition catalog remains invalid in " +
                                scenePath +
                                ": " +
                                error);
                        }
                    }

                    if (sceneChanged)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);

                        if (!EditorSceneManager.SaveScene(scene))
                        {
                            throw new InvalidOperationException(
                                "Unity could not save persistence catalog integration in " +
                                scenePath);
                        }

                        changedSceneCount++;
                    }
                }
            }
            finally
            {
                if (previousSetup != null &&
                    previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        previousSetup);
                }
            }

            return changedSceneCount;
        }

        internal static bool EnsureCatalogBinding(
            BistroBuilderSaveDefinitionCatalog saveCatalog,
            RestaurantPlaceableCatalogDefinition sourceCatalog)
        {
            if (saveCatalog == null)
                throw new ArgumentNullException(nameof(saveCatalog));

            if (sourceCatalog == null)
                throw new ArgumentNullException(nameof(sourceCatalog));

            SerializedObject serialized =
                new SerializedObject(saveCatalog);

            SerializedProperty sources =
                serialized.FindProperty("sourceCatalogs");

            if (sources == null ||
                !sources.isArray)
            {
                throw new InvalidOperationException(
                    "BistroBuilderSaveDefinitionCatalog sourceCatalogs contract is unavailable.");
            }

            for (int index = 0;
                 index < sources.arraySize;
                 index++)
            {
                if (ReferenceEquals(
                        sources
                            .GetArrayElementAtIndex(index)
                            .objectReferenceValue,
                        sourceCatalog))
                {
                    saveCatalog.RebuildIndex();
                    return false;
                }
            }

            int insertIndex =
                sources.arraySize;

            sources.InsertArrayElementAtIndex(
                insertIndex);

            sources
                .GetArrayElementAtIndex(insertIndex)
                .objectReferenceValue =
                    sourceCatalog;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            saveCatalog.RebuildIndex();
            EditorUtility.SetDirty(saveCatalog);

            return true;
        }

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Integration/Repair Persistence Catalog Bindings",
            false,
            150)]
        private static void RepairFromMenu()
        {
            int changed =
                EnsureProjectSceneBindings();

            Debug.Log(
                "[SAVIC] Persistence catalog integration completed. " +
                "Changed scenes: " +
                changed +
                ".");
        }

        public static void RunFromCommandLine()
        {
            int changed =
                EnsureProjectSceneBindings();

            Debug.Log(
                "[SAVIC] PERSISTENCE CATALOG INTEGRATION - PASS\n" +
                "Changed scenes: " +
                changed);
        }
    }
}
