using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicRealisticTestPackV1Installer
    {
        private const string InstallMenu =
            "Tools/Bistro Builder/SAVIC/Packs/Realistic Test Pack V1/Install or Repair";
        private const string ValidateMenu =
            "Tools/Bistro Builder/SAVIC/Packs/Realistic Test Pack V1/Validate";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset";

        private static readonly string[] ExistingItemIds =
        {
            "factory_test_plant",
            "pf_bb_chair_master_001_olive",
            "pf_bb_chair_master_001_red",
            "pf_bb_chair_master_001_white",
            "pf_bb_chair_master_001_yellow",
            "chair_bistro_01",
            "bb_chair_master_002",
            "table_basic",
            "table_basic_4",
            "bb_table_b90c47bde3e949918ec76a13dc17c61c"
        };

        private static readonly VariantEntry[] ChairVariants =
        {
            new VariantEntry(
                "Assets/Art/Blender/Placeables/Dining/chair_bistro_01/Generated/VisualVariants/chair_bistro_01__oak_warm.prefab",
                "chair_bistro_01_oak_warm",
                "Silla bistró — Roble cálido",
                "Silla bistró de madera con acabado roble cálido.",
                38),
            new VariantEntry(
                "Assets/Art/Blender/Placeables/Dining/chair_bistro_01/Generated/VisualVariants/chair_bistro_01__painted_black.prefab",
                "chair_bistro_01_painted_black",
                "Silla bistró — Negro",
                "Silla bistró de madera con acabado negro pintado.",
                40),
            new VariantEntry(
                "Assets/Art/Blender/Placeables/Dining/chair_bistro_01/Generated/VisualVariants/chair_bistro_01__painted_white.prefab",
                "chair_bistro_01_painted_white",
                "Silla bistró — Blanco",
                "Silla bistró de madera con acabado blanco pintado.",
                40),
            new VariantEntry(
                "Assets/Art/Blender/Placeables/Dining/chair_bistro_01/Generated/VisualVariants/chair_bistro_01__sage_green.prefab",
                "chair_bistro_01_sage_green",
                "Silla bistró — Verde salvia",
                "Silla bistró de madera con acabado verde salvia.",
                42),
            new VariantEntry(
                "Assets/Art/Blender/Placeables/Dining/chair_bistro_01/Generated/VisualVariants/chair_bistro_01__walnut_dark.prefab",
                "chair_bistro_01_walnut_dark",
                "Silla bistró — Nogal oscuro",
                "Silla bistró de madera con acabado nogal oscuro.",
                44)
        };

        private static readonly string[] ConstructionAssetPaths =
        {
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Pared_0.5m.prefab",
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Pared_1.0m.prefab",
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Pared_2.0m.prefab",
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Pared_4.0m.prefab",
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Puerta_roble_abierta.prefab",
            "Assets/Resources/BistroBuilder/Construction/Prefabs/Ventana_marco_grafito.prefab"
        };

        [MenuItem(InstallMenu, false, 151)]
        public static void InstallOrRepairFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Realistic Test Pack V1 must be installed outside Play Mode.");
            }

            List<string> failures = new List<string>();

            for (int index = 0; index < ChairVariants.Length; index++)
            {
                InstallVariant(
                    ChairVariants[index],
                    failures);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!TryValidate(
                    out string validationMessage))
            {
                failures.Add(validationMessage);
            }

            if (failures.Count > 0)
            {
                string failureMessage =
                    "[SAVIC] REALISTIC TEST PACK V1 - INSTALL FAILED\n" +
                    string.Join("\n", failures);

                Debug.LogError(failureMessage);
                throw new InvalidOperationException(failureMessage);
            }

            Debug.Log(
                "[SAVIC] REALISTIC TEST PACK V1 - PASS\n" +
                "Placeables ready: 15\n" +
                "Construction assets ready: 6\n" +
                "Total curated assets: 21");
        }

        [MenuItem(ValidateMenu, false, 152)]
        public static void ValidateFromMenu()
        {
            if (!TryValidate(
                    out string message))
            {
                Debug.LogError(
                    "[SAVIC] REALISTIC TEST PACK V1 - FAIL\n" +
                    message);

                throw new InvalidOperationException(message);
            }

            Debug.Log(
                "[SAVIC] REALISTIC TEST PACK V1 - PASS\n" +
                message);
        }

        public static bool TryValidate(
            out string message)
        {
            List<string> failures = new List<string>();

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>(
                    MainCatalogPath);

            if (catalog == null)
            {
                failures.Add(
                    "Main placeable catalog is missing.");
            }
            else
            {
                ValidateItemIds(
                    catalog,
                    ExistingItemIds,
                    failures);

                for (int index = 0;
                     index < ChairVariants.Length;
                     index++)
                {
                    ValidateItemId(
                        catalog,
                        ChairVariants[index].ItemId,
                        failures);
                }
            }

            ValidateConstruction(
                failures);

            if (failures.Count > 0)
            {
                message =
                    string.Join("\n", failures);
                return false;
            }

            message =
                "15 placeables resolve with prefab + catalog preview; " +
                "4 wall modules, 1 door and 1 window resolve from the canonical construction content.";
            return true;
        }

        private static void InstallVariant(
            VariantEntry entry,
            List<string> failures)
        {
            GameObject source =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    entry.SourcePath);

            if (source == null)
            {
                failures.Add(
                    "Missing source variant: " +
                    entry.SourcePath);
                return;
            }

            RestaurantPlaceableCatalogDefinition catalog =
                AssetDatabase.LoadAssetAtPath<RestaurantPlaceableCatalogDefinition>(
                    MainCatalogPath);

            if (catalog != null &&
                catalog.TryGetItem(
                    entry.ItemId,
                    out RestaurantPlaceableItemDefinition existing) &&
                existing != null)
            {
                return;
            }

            BistroBuilderPlaceableFactorySettings settings =
                new BistroBuilderPlaceableFactorySettings
                {
                    Preset =
                        BistroBuilderPlaceableFactoryPreset.Chair,
                    PurchasePrice = entry.PurchasePrice,
                    CanMove = true,
                    CanRotate = true,
                    RotationStepDegrees = 90f,
                    MinimumClearance = 0f,
                    GenerateColliderWhenMissing = true,
                    AddToMainCatalog = true,
                    RunProjectHealthAfterCreation = false,
                    PreventDuplicateDisplayNames = true,
                    SingleDisplayNameOverride =
                        entry.DisplayName,
                    SingleDescriptionOverride =
                        entry.Description
                };

            BistroBuilderPlaceableFactoryEngine
                .ApplyPresetCapabilities(
                    settings);

            List<GameObject> sources =
                new List<GameObject>
                {
                    source
                };

            List<BistroBuilderPlaceableFactoryPlan> plans =
                BistroBuilderPlaceableFactoryEngine
                    .AnalyzeSelection(
                        sources,
                        settings);

            if (plans.Count != 1)
            {
                failures.Add(
                    "Factory did not produce exactly one plan for " +
                    entry.ItemId +
                    ".");
                return;
            }

            BistroBuilderPlaceableFactoryPlan plan =
                plans[0];

            if (plan.Status ==
                BistroBuilderPlaceableFactoryPlanStatus.AlreadyConfigured)
            {
                if (!string.Equals(
                        plan.ItemId,
                        entry.ItemId,
                        StringComparison.Ordinal))
                {
                    failures.Add(
                        entry.ItemId +
                        " collided with existing catalog item " +
                        plan.ItemId +
                        ".");
                }

                return;
            }

            if (plan.Status !=
                BistroBuilderPlaceableFactoryPlanStatus.Ready)
            {
                failures.Add(
                    entry.ItemId +
                    " blocked by Placeable Factory: " +
                    plan.StatusMessage);
                return;
            }

            if (!string.Equals(
                    plan.ItemId,
                    entry.ItemId,
                    StringComparison.Ordinal))
            {
                failures.Add(
                    "Unexpected generated ItemId for " +
                    entry.SourcePath +
                    ": expected " +
                    entry.ItemId +
                    ", got " +
                    plan.ItemId +
                    ".");
                return;
            }

            BistroBuilderPlaceableFactoryBatchResult result =
                BistroBuilderPlaceableFactoryEngine
                    .ExecutePlans(
                        plans,
                        settings);

            if (result.FailedCount > 0 ||
                result.CreatedCount != 1)
            {
                failures.Add(
                    entry.ItemId +
                    " factory execution failed: " +
                    result.BuildSummary() +
                    " " +
                    string.Join(" | ", result.Messages));
            }
        }

        private static void ValidateItemIds(
            RestaurantPlaceableCatalogDefinition catalog,
            IReadOnlyList<string> itemIds,
            List<string> failures)
        {
            for (int index = 0;
                 index < itemIds.Count;
                 index++)
            {
                ValidateItemId(
                    catalog,
                    itemIds[index],
                    failures);
            }
        }

        private static void ValidateItemId(
            RestaurantPlaceableCatalogDefinition catalog,
            string itemId,
            List<string> failures)
        {
            if (!catalog.TryGetItem(
                    itemId,
                    out RestaurantPlaceableItemDefinition definition) ||
                definition == null)
            {
                failures.Add(
                    "Catalog item missing: " +
                    itemId);
                return;
            }

            if (!definition.HasValidPrefab ||
                definition.Prefab == null)
            {
                failures.Add(
                    itemId +
                    " has no playable prefab.");
            }

            if (definition.CatalogIcon == null ||
                definition.InspectorPreview == null)
            {
                failures.Add(
                    itemId +
                    " has no catalog/inspector preview.");
            }

            if (definition.EditableDefinition == null)
            {
                failures.Add(
                    itemId +
                    " has no EditableObjectDefinition.");
            }
        }

        private static void ValidateConstruction(
            List<string> failures)
        {
            for (int index = 0;
                 index < ConstructionAssetPaths.Length;
                 index++)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(
                        ConstructionAssetPaths[index]) == null)
                {
                    failures.Add(
                        "Construction asset missing: " +
                        ConstructionAssetPaths[index]);
                }
            }

            BistroBuilderConstructionAssetKit kit =
                BistroBuilderConstructionAssetKit.Load();

            if (kit == null)
            {
                failures.Add(
                    "ConstructionAssetKit is missing.");
                return;
            }

            if (kit.doorPrefab == null)
            {
                failures.Add(
                    "ConstructionAssetKit has no default door.");
            }

            if (kit.windowPrefab == null)
            {
                failures.Add(
                    "ConstructionAssetKit has no default window.");
            }

            if (kit.wallModules == null ||
                kit.wallModules.Length < 4)
            {
                failures.Add(
                    "ConstructionAssetKit does not expose the four wall modules.");
                return;
            }

            for (int index = 0;
                 index < kit.wallModules.Length;
                 index++)
            {
                if (kit.wallModules[index] == null)
                {
                    failures.Add(
                        "ConstructionAssetKit contains a null wall module.");
                }
            }
        }

        private readonly struct VariantEntry
        {
            internal VariantEntry(
                string sourcePath,
                string itemId,
                string displayName,
                string description,
                int purchasePrice)
            {
                SourcePath = sourcePath;
                ItemId = itemId;
                DisplayName = displayName;
                Description = description;
                PurchasePrice = purchasePrice;
            }

            internal string SourcePath { get; }
            internal string ItemId { get; }
            internal string DisplayName { get; }
            internal string Description { get; }
            internal int PurchasePrice { get; }
        }
    }
}
