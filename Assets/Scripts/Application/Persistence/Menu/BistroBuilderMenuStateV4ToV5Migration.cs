using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Migración pura y consecutiva de menu.state v4 a v5.
///
/// Cada carta histórica por restaurante se convierte en una Carta principal
/// dentro de un portfolio sin reglas. De este modo una partida antigua sigue
/// mostrando exactamente la misma oferta y queda preparada para crear cartas
/// adicionales sin perder autoría ni revisiones.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu State V4 To V5 Migration")]
public sealed class BistroBuilderMenuStateV4ToV5Migration :
    MonoBehaviour,
    IBistroBuilderSaveSectionMigration
{
    public string SectionId =>
        BistroBuilderMenuSaveSectionProvider.StableSectionId;

    public int FromVersion => 4;

    public int ToVersion => 5;

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
            error = "menu.state v4 no contiene datos para migrar.";
            return false;
        }

        BistroBuilderMenuSaveDataV4 source;
        try
        {
            source = JsonUtility.FromJson<BistroBuilderMenuSaveDataV4>(
                Encoding.UTF8.GetString(sourcePayload)
            );
        }
        catch (Exception exception)
        {
            error = "No se pudo leer menu.state v4: " + exception.Message;
            return false;
        }

        if (!TryValidateV4(source, out error))
        {
            return false;
        }

        BistroBuilderMenuSaveData target = new BistroBuilderMenuSaveData
        {
            schemaVersion = 5,
            activeRestaurantId = source.activeRestaurantId,
            restaurants = CloneRestaurants(source.restaurants),
            authoredDishRecipes = ClonePairs(source.authoredDishRecipes),
            unresolvedAuthoredDishRecipes =
                ClonePairs(source.unresolvedAuthoredDishRecipes),
            portfolios = BuildDefaultPortfolios(source.restaurants),
            activeEventIds = new List<string>(),
            activePromotionIds = new List<string>()
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
            error = "No se pudo escribir menu.state v5: " +
                    exception.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateV4(
        BistroBuilderMenuSaveDataV4 source,
        out string error
    )
    {
        if (source == null || source.schemaVersion != 4 ||
            !BistroBuilderMenuIdUtility.IsValidStableId(
                source.activeRestaurantId
            ) ||
            source.restaurants == null || source.restaurants.Count == 0 ||
            source.authoredDishRecipes == null ||
            source.unresolvedAuthoredDishRecipes == null)
        {
            error = "menu.state v4 no cumple su contrato histórico.";
            return false;
        }

        HashSet<string> restaurantIds =
            new HashSet<string>(StringComparer.Ordinal);
        bool activeFound = false;

        for (int index = 0; index < source.restaurants.Count; index++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                source.restaurants[index];
            if (restaurant == null ||
                !BistroBuilderMenuIdUtility.IsValidStableId(
                    restaurant.restaurantId
                ) ||
                !restaurantIds.Add(restaurant.restaurantId) ||
                restaurant.revision < 0 ||
                restaurant.items == null ||
                restaurant.unresolvedItems == null)
            {
                error = "menu.state v4 contiene una carta inválida.";
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
            error = "menu.state v4 no contiene el restaurante activo.";
            return false;
        }

        if (!BistroBuilderDishRecipeSaveDataUtility
                .TryValidatePairCollections(
                    source.authoredDishRecipes,
                    source.unresolvedAuthoredDishRecipes,
                    out error
                ))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateItems(
        IList<BistroBuilderMenuItemSaveData> items,
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
                error = "menu.state v4 contiene una entrada de carta inválida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static List<BistroBuilderRestaurantMenuPortfolioSaveData>
        BuildDefaultPortfolios(
            IList<BistroBuilderRestaurantMenuSaveData> restaurants
        )
    {
        List<BistroBuilderRestaurantMenuPortfolioSaveData> result =
            new List<BistroBuilderRestaurantMenuPortfolioSaveData>(
                restaurants.Count
            );

        for (int index = 0; index < restaurants.Count; index++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant =
                restaurants[index];
            result.Add(
                new BistroBuilderRestaurantMenuPortfolioSaveData
                {
                    restaurantId = restaurant.restaurantId,
                    revision = 0,
                    fallbackMenuId =
                        BistroBuilderMenuPortfolioService.DefaultMenuId,
                    activeMenuId =
                        BistroBuilderMenuPortfolioService.DefaultMenuId,
                    manualOverrideMenuId = string.Empty,
                    menus = new List<BistroBuilderNamedMenuSaveData>
                    {
                        new BistroBuilderNamedMenuSaveData
                        {
                            menuId =
                                BistroBuilderMenuPortfolioService.DefaultMenuId,
                            displayName =
                                BistroBuilderMenuPortfolioService.DefaultMenuName,
                            revision = restaurant.revision,
                            items = CloneItems(restaurant.items),
                            unresolvedItems =
                                CloneItems(restaurant.unresolvedItems)
                        }
                    },
                    rules =
                        new List<BistroBuilderMenuActivationRuleSaveData>()
                }
            );
        }

        return result;
    }

    private static List<BistroBuilderRestaurantMenuSaveData> CloneRestaurants(
        IList<BistroBuilderRestaurantMenuSaveData> source
    )
    {
        List<BistroBuilderRestaurantMenuSaveData> result =
            new List<BistroBuilderRestaurantMenuSaveData>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderRestaurantMenuSaveData restaurant = source[index];
            result.Add(
                new BistroBuilderRestaurantMenuSaveData
                {
                    restaurantId = restaurant.restaurantId,
                    revision = restaurant.revision,
                    items = CloneItems(restaurant.items),
                    unresolvedItems =
                        CloneItems(restaurant.unresolvedItems)
                }
            );
        }
        return result;
    }

    private static List<BistroBuilderMenuItemSaveData> CloneItems(
        IList<BistroBuilderMenuItemSaveData> source
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

    private static List<BistroBuilderDishRecipeSaveData> ClonePairs(
        IList<BistroBuilderDishRecipeSaveData> source
    )
    {
        List<BistroBuilderDishRecipeSaveData> result =
            new List<BistroBuilderDishRecipeSaveData>(source.Count);
        for (int index = 0; index < source.Count; index++)
        {
            result.Add(BistroBuilderDishRecipeSaveDataUtility.Clone(source[index]));
        }
        return result;
    }
}
