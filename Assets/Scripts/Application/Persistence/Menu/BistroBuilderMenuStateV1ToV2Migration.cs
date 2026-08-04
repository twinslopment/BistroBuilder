using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Migración pura y consecutiva de menu.state v1 a v2.
///
/// V1 contenía una única carta global. V2 la envuelve en el restaurante
/// principal sin reinterpretar precios, flags, servicios ni orden. La
/// migración no consulta el catálogo: incluso un DishId ausente se conserva y
/// será clasificado como no resuelto al aplicarse la sección.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu State V1 To V2 Migration")]
public sealed class BistroBuilderMenuStateV1ToV2Migration :
    MonoBehaviour,
    IBistroBuilderSaveSectionMigration
{
    [SerializeField]
    private string defaultRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public string SectionId =>
        BistroBuilderMenuSaveSectionProvider.StableSectionId;

    public int FromVersion => 1;

    public int ToVersion => 2;

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
            error = "menu.state v1 no contiene datos para migrar.";
            return false;
        }

        string restaurantId =
            BistroBuilderMenuIdUtility.NormalizeStableId(defaultRestaurantId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(restaurantId))
        {
            error = "El RestaurantId predeterminado de la migración no es válido.";
            return false;
        }

        BistroBuilderMenuSaveDataV1 source;

        try
        {
            string json = Encoding.UTF8.GetString(sourcePayload);
            source = JsonUtility.FromJson<BistroBuilderMenuSaveDataV1>(json);
        }
        catch (Exception exception)
        {
            error = "No se pudo leer menu.state v1: " + exception.Message;
            return false;
        }

        if (source == null || source.schemaVersion != 1 || source.items == null)
        {
            error = "menu.state v1 no cumple su contrato histórico.";
            return false;
        }

        if (!TryValidateItems(source.items, out error))
        {
            return false;
        }

        BistroBuilderRestaurantMenuSaveData restaurant =
            new BistroBuilderRestaurantMenuSaveData
            {
                restaurantId = restaurantId,
                revision = 0,
                items = CloneItems(source.items),
                unresolvedItems = new List<BistroBuilderMenuItemSaveData>()
            };

        BistroBuilderMenuSaveData target = new BistroBuilderMenuSaveData
        {
            schemaVersion = BistroBuilderMenuSaveData.CurrentSchemaVersion,
            activeRestaurantId = restaurantId,
            restaurants = new List<BistroBuilderRestaurantMenuSaveData>
            {
                restaurant
            }
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
            error = "No se pudo escribir menu.state v2: " + exception.Message;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateItems(
        List<BistroBuilderMenuItemSaveData> items,
        out string error
    )
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemSaveData item = items[index];

            if (item == null)
            {
                error = "menu.state v1 contiene una entrada nula.";
                return false;
            }

            string normalized =
                BistroBuilderMenuIdUtility.NormalizeStableId(item.dishId);

            if (!BistroBuilderMenuIdUtility.IsValidStableId(normalized) ||
                !string.Equals(item.dishId, normalized, StringComparison.Ordinal))
            {
                error = "menu.state v1 contiene un DishId inválido.";
                return false;
            }

            if (!ids.Add(item.dishId))
            {
                error = "menu.state v1 contiene el DishId duplicado " +
                        item.dishId + ".";
                return false;
            }

            if (item.currentPriceCents < 0 ||
                item.currentPriceCents >
                    BistroBuilderDishDefinition.MaximumPriceCents)
            {
                error = "menu.state v1 contiene un precio inválido para " +
                        item.dishId + ".";
                return false;
            }

            if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                    (BistroBuilderMealServiceAvailability)
                        item.availableServices,
                    true
                ))
            {
                error = "menu.state v1 contiene servicios inválidos para " +
                        item.dishId + ".";
                return false;
            }

            if (item.displayOrder < 0)
            {
                error = "menu.state v1 contiene un orden negativo para " +
                        item.dishId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
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
                    displayOrder = item.displayOrder
                }
            );
        }

        return result;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        defaultRestaurantId =
            BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;
    }

    private void OnValidate()
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(defaultRestaurantId);
        defaultRestaurantId = BistroBuilderMenuIdUtility.IsValidStableId(
            normalized
        )
            ? normalized
            : BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;
    }
#endif
}
