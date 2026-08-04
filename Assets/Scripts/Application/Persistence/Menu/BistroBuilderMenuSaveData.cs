using System;
using System.Collections.Generic;

/// <summary>
/// Estado persistente de un plato dentro de una carta.
/// Usa céntimos enteros y valores primitivos estables para que el formato
/// pueda migrarse sin depender de objetos Unity ni de nombres visibles.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuItemSaveData
{
    public string dishId = string.Empty;
    public int currentPriceCents;
    public bool unlocked;
    public bool enabled;
    public bool manuallySoldOut;
    public bool signatureDish;
    public int availableServices;
    public int displayOrder;
}

/// <summary>
/// Carta persistente de un restaurante concreto.
///
/// unresolvedItems conserva íntegramente las entradas cuyo DishId no existe
/// temporalmente en el catálogo actual. No se ofrecen al jugador, pero tampoco
/// se destruyen durante una carga o una actualización de contenido.
/// </summary>
[Serializable]
public sealed class BistroBuilderRestaurantMenuSaveData
{
    public string restaurantId = string.Empty;
    public int revision;

    public List<BistroBuilderMenuItemSaveData> items =
        new List<BistroBuilderMenuItemSaveData>();

    public List<BistroBuilderMenuItemSaveData> unresolvedItems =
        new List<BistroBuilderMenuItemSaveData>();
}

/// <summary>
/// Formato actual de la sección menu.state.
///
/// La versión 2 sustituye la carta global por un agregado por restaurante,
/// conserva el restaurante activo y admite entradas no resueltas sin pérdida
/// de datos. No guarda disponibilidad derivada del inventario.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveData
{
    public const int CurrentSchemaVersion = 2;

    public int schemaVersion = CurrentSchemaVersion;
    public string activeRestaurantId =
        BistroBuilderRestaurantMenuCollectionService.DefaultRestaurantId;

    public List<BistroBuilderRestaurantMenuSaveData> restaurants =
        new List<BistroBuilderRestaurantMenuSaveData>();
}

/// <summary>
/// Contrato exacto de menu.state v1. Se conserva exclusivamente para la
/// migración pura v1 -> v2; no debe volver a usarse como estado runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveDataV1
{
    public int schemaVersion = 1;

    public List<BistroBuilderMenuItemSaveData> items =
        new List<BistroBuilderMenuItemSaveData>();
}
