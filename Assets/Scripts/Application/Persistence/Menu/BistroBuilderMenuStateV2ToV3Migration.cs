using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Migración pura y consecutiva de menu.state v2 a v3.
///
/// V2 no almacenaba dificultad ni tiempo runtime. JsonUtility inicializa esos
/// campos a 0 y V3 interpreta 0/0 como herencia del catálogo, preservando el
/// comportamiento exacto de la partida antigua incluso para DishId ausentes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu State V2 To V3 Migration")]
public sealed class BistroBuilderMenuStateV2ToV3Migration :
    MonoBehaviour,
    IBistroBuilderSaveSectionMigration
{
    public string SectionId =>
        BistroBuilderMenuSaveSectionProvider.StableSectionId;
    public int FromVersion => 2;
    public int ToVersion => 3;
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
            error = "menu.state v2 no contiene datos para migrar.";
            return false;
        }

        BistroBuilderMenuSaveData source;

        try
        {
            string json = Encoding.UTF8.GetString(sourcePayload);
            source = JsonUtility.FromJson<BistroBuilderMenuSaveData>(json);
        }
        catch (Exception exception)
        {
            error = "No se pudo leer menu.state v2: " + exception.Message;
            return false;
        }

        if (source == null || source.schemaVersion != 2 ||
            source.restaurants == null || source.restaurants.Count == 0)
        {
            error = "menu.state v2 no cumple su contrato histórico.";
            return false;
        }

        if (!TryValidateHistoricalStructure(source, out error))
        {
            return false;
        }

        source.schemaVersion = 3;

        try
        {
            migratedPayload = Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(source, false)
            );
        }
        catch (Exception exception)
        {
            migratedPayload = null;
            error = "No se pudo escribir menu.state v3: " +
                    exception.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateHistoricalStructure(
        BistroBuilderMenuSaveData source,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(
                source.activeRestaurantId
            ))
        {
            error = "menu.state v2 contiene un restaurante activo inválido.";
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
                error = "menu.state v2 contiene una carta inválida.";
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
            error = "menu.state v2 no contiene la carta activa.";
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
                error = "menu.state v2 contiene una entrada de plato inválida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
