using System;
using System.Collections.Generic;

/// <summary>
/// Estado persistente de un plato dentro de la carta activa.
/// Usa enteros para dinero y enums serializados como enteros estables.
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
/// Sección menu.state de una partida.
/// No duplica definiciones de plato; conserva únicamente estado mutable.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuSaveData
{
    public int schemaVersion = 1;

    public List<BistroBuilderMenuItemSaveData> items =
        new List<BistroBuilderMenuItemSaveData>();
}
