using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairPersistenceReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const string MainCatalogPath =
            "Assets/Data/Restaurant/EditMode/Catalog/" +
            "RestaurantPlaceableCatalog_Main.asset";

        internal static SavicChairPersistenceReadinessRecord Validate(
            SavicManifest manifest,
            SavicChairAuthoringRecord plan,
            string prefabPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "Persistence validation requires a valid chair authoring plan.");
            }

            if (string.IsNullOrWhiteSpace(
                    manifest.canonicalContentId))
            {
                throw new InvalidOperationException(
                    "Persistence validation requires a canonical ContentId.");
            }

            RestaurantPlaceableCatalogDefinition mainCatalog =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableCatalogDefinition>(
                        MainCatalogPath);

            if (mainCatalog == null)
            {
                throw new InvalidOperationException(
                    "Canonical placeable catalog is missing.");
            }

            if (!mainCatalog.TryGetItem(
                    manifest.canonicalContentId,
                    out RestaurantPlaceableItemDefinition item) ||
                item == null)
            {
                throw new InvalidOperationException(
                    "Canonical catalog cannot resolve published SAVIC chair.");
            }

            if (item.Prefab == null)
            {
                throw new InvalidOperationException(
                    "Published SAVIC chair has no loadable prefab.");
            }

            string itemPrefabPath =
                AssetDatabase.GetAssetPath(item.Prefab);

            if (!string.Equals(
                    itemPrefabPath,
                    prefabPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Published SAVIC chair prefab differs from canonical catalog prefab.");
            }

            RestaurantPlaceableObject placeable =
                item.Prefab.GetComponent<RestaurantPlaceableObject>();

            RestaurantSeat seat =
                item.Prefab.GetComponent<RestaurantSeat>();

            if (placeable == null ||
                seat == null)
            {
                throw new InvalidOperationException(
                    "Published chair prefab is missing its persistence-critical placeable or seat component.");
            }

            if (!placeable.ValidateConfiguration(
                    out string placeableError))
            {
                throw new InvalidOperationException(
                    "Published chair placeable configuration is invalid: " +
                    placeableError);
            }

            if (!seat.ValidateConfiguration(
                    out string seatError))
            {
                throw new InvalidOperationException(
                    "Published chair seat configuration is invalid: " +
                    seatError);
            }

            RestaurantSeatUseProfileDefinition expectedProfile =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantSeatUseProfileDefinition>(
                        plan.seatUseProfileAssetPath);

            if (expectedProfile == null ||
                !ReferenceEquals(
                    seat.UseProfile,
                    expectedProfile))
            {
                throw new InvalidOperationException(
                    "Published chair persistence profile differs from its authoring plan.");
            }

            GameObject host =
                new GameObject(
                    "SAVIC_ChairPersistenceReadinessValidation");

            try
            {
                BistroBuilderSaveDefinitionCatalog saveCatalog =
                    host.AddComponent
                        <BistroBuilderSaveDefinitionCatalog>();

                SavicPersistenceCatalogIntegration
                    .EnsureCatalogBinding(
                        saveCatalog,
                        mainCatalog);

                if (!saveCatalog.ValidateConfiguration(
                        out string configurationError))
                {
                    throw new InvalidOperationException(
                        "Persistence catalog configuration is invalid: " +
                        configurationError);
                }

                if (!saveCatalog.TryGetDefinition(
                        manifest.canonicalContentId,
                        out RestaurantPlaceableItemDefinition resolved) ||
                    !ReferenceEquals(
                        resolved,
                        item))
                {
                    throw new InvalidOperationException(
                        "Save/Load catalog cannot resolve the published SAVIC chair.");
                }

                return new SavicChairPersistenceReadinessRecord
                {
                    validated = true,
                    validatorVersion = Version,
                    sourceCatalogAssetPath = MainCatalogPath,
                    canonicalContentId = manifest.canonicalContentId,
                    itemDefinitionAssetPath =
                        AssetDatabase.GetAssetPath(item),
                    prefabAssetPath =
                        prefabPath ?? string.Empty,
                    catalogResolvable = true,
                    prefabResolvable = true,
                    seatComponentValid = true,
                    evidence =
                        "Canonical placeable catalog resolves the SAVIC chair; " +
                        "BistroBuilderSaveDefinitionCatalog consumes the canonical " +
                        "catalog without duplicating definitions; RestaurantPlaceableObject " +
                        "and RestaurantSeat validate successfully with the planned " +
                        "SeatUseProfile, so restaurant.structure can restore the chair " +
                        "by stable ItemId while seat/table links remain a separate layer.",
                    validatedUtc = DateTime.UtcNow.ToString("O")
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
