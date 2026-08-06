using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Migración pura y consecutiva de menu.state v3 a v4.
///
/// V3 ya conservaba cartas por restaurante y preparación configurable, pero
/// no almacenaba las capas runtime de platos y recetas. V4 copia literalmente
/// la carta histórica e inicializa ambas colecciones de autoría vacías. Los
/// DishId sin definición siguen preservados como entradas no resueltas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu State V3 To V4 Migration")]
public sealed class BistroBuilderMenuStateV3ToV4Migration :
    MonoBehaviour,
    IBistroBuilderSaveSectionMigration
{
    public string SectionId =>
        BistroBuilderMenuSaveSectionProvider.StableSectionId;

    public int FromVersion => 3;

    public int ToVersion => 4;

    public string FromSerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public string ToSerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public bool TryMigrate(
        byte[] sourcePayload,
        out byte[] migratedPayload,
        out string error
    )
    {
        migratedPayload = null;

        if (sourcePayload == null || sourcePayload.Length == 0)
        {
            error = "menu.state v3 no contiene datos para migrar.";
            return false;
        }

        BistroBuilderMenuSaveDataV3 source;

        try
        {
            string json = Encoding.UTF8.GetString(sourcePayload);
            source = JsonUtility.FromJson<BistroBuilderMenuSaveDataV3>(json);
        }
        catch (Exception exception)
        {
            error = "No se pudo leer menu.state v3: " + exception.Message;
            return false;
        }

        if (source == null || source.schemaVersion != 3 ||
            source.restaurants == null || source.restaurants.Count == 0)
        {
            error = "menu.state v3 no cumple su contrato histórico.";
            return false;
        }

        if (!TryValidateHistoricalStructure(source, out error))
        {
            return false;
        }

        BistroBuilderMenuSaveData target = new BistroBuilderMenuSaveData
        {
            schemaVersion = 4,
            activeRestaurantId = source.activeRestaurantId,
            restaurants = CloneRestaurants(source.restaurants),
            authoredDishRecipes =
                new List<BistroBuilderDishRecipeSaveData>(),
            unresolvedAuthoredDishRecipes =
                new List<BistroBuilderDishRecipeSaveData>()
        };

        try
        {
            migratedPayload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(target, false)
            );
        }
        catch (Exception exception)
        {
            migratedPayload = null;
            error = "No se pudo escribir menu.state v4: " +
                    exception.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateHistoricalStructure(
        BistroBuilderMenuSaveDataV3 source,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(
                source.activeRestaurantId
            ))
        {
            error = "menu.state v3 contiene un restaurante activo inválido.";
            return false;
        }

        HashSet<string> restaurantIds =
            new HashSet<string>(StringComparer.Ordinal);
        bool activeFound = false;

        for (int restaurantIndex = 0;
             restaurantIndex < source.restaurants.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                source.restaurants[restaurantIndex];

            if (restaurant == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(
                    restaurant.restaurantId
                ) ||
                !restaurantIds.Add(restaurant.restaurantId) ||
                restaurant.revision < 0 ||
                restaurant.items == null ||
                restaurant.unresolvedItems == null)
            {
                error = "menu.state v3 contiene una carta inválida.";
                return false;
            }

            HashSet<string> dishIds =
                new HashSet<string>(StringComparer.Ordinal);

            if (!TryValidateItems(restaurant.items, dishIds, out error) ||
                !TryValidateItems(
                    restaurant.unresolvedItems,
                    dishIds,
                    out error
                ))
            {
                return false;
            }

            activeFound |= string.Equals(
                restaurant.restaurantId,
                source.activeRestaurantId,
                StringComparison.Ordinal
            );
        }

        if (!activeFound)
        {
            error = "menu.state v3 no contiene la carta activa.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateItems(
        List<BistroBuilderMenuItemSaveData> items,
        HashSet<string> dishIds,
        out string error
    )
    {
        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemSaveData item = items[index];

            if (item == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(item.dishId) ||
                !dishIds.Add(item.dishId) ||
                item.currentPriceCents < 0 ||
                item.currentPriceCents >
                    BistroBuilderDishDefinition.MaximumPriceCents ||
                !BistroBuilderMenuIdUtility.IsValidServiceMask(
                    (BistroBuilderMealServiceAvailability)
                        item.availableServices,
                    true
                ) ||
                item.displayOrder < 0)
            {
                error = "menu.state v3 contiene una entrada inválida.";
                return false;
            }

            bool inheritedDifficulty =
                item.preparationDifficulty ==
                    BistroBuilderMenuItemRuntimeState.InheritedPreparationValue;
            bool inheritedTime =
                item.basePreparationSeconds ==
                    BistroBuilderMenuItemRuntimeState.InheritedPreparationValue;

            if (inheritedDifficulty != inheritedTime ||
                (!inheritedDifficulty &&
                 (item.preparationDifficulty <
                      BistroBuilderDishDefinition.MinimumPreparationDifficulty ||
                  item.preparationDifficulty >
                      BistroBuilderDishDefinition.MaximumPreparationDifficulty ||
                  item.basePreparationSeconds <
                      BistroBuilderDishDefinition.MinimumPreparationSeconds ||
                  item.basePreparationSeconds >
                      BistroBuilderDishDefinition.MaximumPreparationSeconds)))
            {
                error = "menu.state v3 contiene preparación inválida para " +
                        item.dishId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static List<BistroBuilderRestaurantMenuSaveData> CloneRestaurants(
        List<BistroBuilderRestaurantMenuSaveData> source
    )
    {
        List<BistroBuilderRestaurantMenuSaveData> result =
            new List<BistroBuilderRestaurantMenuSaveData>(source.Count);

        for (int restaurantIndex = 0;
             restaurantIndex < source.Count;
             restaurantIndex++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                source[restaurantIndex];
            result.Add(
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId = restaurant.restaurantId,
                    revision = restaurant.revision,
                    items = CloneItems(restaurant.items),
                    unresolvedItems = CloneItems(
                        restaurant.unresolvedItems
                    )
                }
            );
        }

        return result;
    }

    private static List<BistroBuilderMenuItemSaveData> CloneItems(
        List<BistroBuilderMenuItemSaveData> source
    )
    {
        List<BistroBuilderMenuItemSaveData> result =
            new List<BistroBuilderMenuItemSaveData>(source.Count);

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemSaveData item = source[index];
            result.Add(
                new BistroBuilderMenuItemSaveData
                {
                    dishId = item.dishId,
                    currentPriceCents = item.currentPriceCents,
                    unlocked = item.unlocked,
                    enabled = item.enabled,
                    manuallySoldOut = item.manuallySoldOut,
                    signatureDish = item.signatureDish,
                    availableServices = item.availableServices,
                    displayOrder = item.displayOrder,
                    preparationDifficulty = item.preparationDifficulty,
                    basePreparationSeconds = item.basePreparationSeconds
                }
            );
        }

        return result;
    }
}
