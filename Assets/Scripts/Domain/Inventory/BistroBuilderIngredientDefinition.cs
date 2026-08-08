using System;
using UnityEngine;

/// <summary>
/// Clasificación operativa de un ingrediente.
/// </summary>
public enum BistroBuilderIngredientCategory
{
    Produce = 0,
    Meat = 1,
    FishAndSeafood = 2,
    DairyAndEggs = 3,
    DryGoods = 4,
    Condiment = 5,
    Beverage = 6,
    PreparedProduct = 7,
    Other = 8
}

/// <summary>
/// Almacenamiento recomendado. Se conserva desde 368A para que inventario,
/// proveedores, lotes y caducidad compartan el mismo contrato de datos.
/// </summary>
public enum BistroBuilderIngredientStorageType
{
    DryStorage = 0,
    Refrigerated = 1,
    Frozen = 2,
    BeverageCellar = 3,
    Ambient = 4
}

/// <summary>
/// Definición canónica de un ingrediente.
///
/// El precio de referencia se expresa como un envase de compra, por ejemplo:
/// 1 kg por 6,00 €, 750 ml por 5,00 € o 24 unidades por 9,60 €.
/// Esto permite calcular escandallos sin guardar dinero en float y deja que
/// los futuros proveedores sustituyan el coste real del envase.
/// </summary>
[CreateAssetMenu(
    fileName = "IngredientDefinition",
    menuName = "Bistro Builder/Inventory/Ingredient Definition",
    order = 200
)]
public sealed class BistroBuilderIngredientDefinition : ScriptableObject
{
    public const int MaximumReferencePackPriceCents = 100000000;
    public const int MaximumShelfLifeDays = 36500;
    public const long MicroCentsPerCent = 10000L;

    [Header("Identidad estable")]

    [SerializeField]
    private string ingredientId = string.Empty;

    [SerializeField]
    private string displayName = string.Empty;

    [Header("Clasificación")]

    [SerializeField]
    private BistroBuilderIngredientCategory category =
        BistroBuilderIngredientCategory.Other;

    [SerializeField]
    private BistroBuilderIngredientStorageType storageType =
        BistroBuilderIngredientStorageType.DryStorage;

    [Tooltip(
        "Unidad base autoritativa del inventario. Debe ser g, ml, ud o " +
        "ración; kg y l se admiten como unidades de entrada, no como base."
    )]
    [SerializeField]
    private BistroBuilderMeasurementUnit baseUnit =
        BistroBuilderMeasurementUnit.Gram;

    [Header("Coste de referencia por envase")]

    [SerializeField]
    [Min(0.001f)]
    private double referencePackAmount = 1d;

    [SerializeField]
    private BistroBuilderMeasurementUnit referencePackUnit =
        BistroBuilderMeasurementUnit.Kilogram;

    [SerializeField]
    [Min(0)]
    private int referencePackPriceCents;

    [Header("Preparado para lotes y caducidad")]

    [SerializeField]
    [Min(0)]
    private int defaultShelfLifeDays;

    [SerializeField]
    private bool perishable;

    public string IngredientId => ingredientId;

    public string DisplayName => displayName;

    public BistroBuilderIngredientCategory Category => category;

    public BistroBuilderIngredientStorageType StorageType => storageType;

    public BistroBuilderMeasurementUnit BaseUnit => baseUnit;

    public double ReferencePackAmount => referencePackAmount;

    public BistroBuilderMeasurementUnit ReferencePackUnit =>
        referencePackUnit;

    public int ReferencePackPriceCents => referencePackPriceCents;

    public int DefaultShelfLifeDays => defaultShelfLifeDays;

    public bool Perishable => perishable;

    public bool TryGetReferencePackCanonicalMilliUnits(
        out long canonicalMilliUnits,
        out string error
    )
    {
        canonicalMilliUnits = 0L;
        error = string.Empty;

        if (!BistroBuilderMeasurementUtility.AreCompatible(
                baseUnit,
                referencePackUnit
            ))
        {
            error = "El envase de " + ingredientId +
                    " usa una unidad incompatible con su unidad base.";
            return false;
        }

        return BistroBuilderMeasurementUtility
            .TryConvertToCanonicalMilliUnits(
                referencePackAmount,
                referencePackUnit,
                out canonicalMilliUnits,
                out error
            );
    }

    /// <summary>
    /// Calcula el coste proporcional con precisión de 1/10.000 de céntimo.
    /// El redondeo a céntimos se realiza una sola vez al cerrar el escandallo.
    /// </summary>
    public bool TryCalculateCostMicroCents(
        long requiredCanonicalMilliUnits,
        out long costMicroCents,
        out string error
    )
    {
        costMicroCents = 0L;
        error = string.Empty;

        if (requiredCanonicalMilliUnits <= 0L)
        {
            error = "La cantidad requerida de " + ingredientId +
                    " debe ser mayor que cero.";
            return false;
        }

        if (!TryGetReferencePackCanonicalMilliUnits(
                out long packCanonicalMilliUnits,
                out error
            ))
        {
            return false;
        }

        decimal rawCost =
            (decimal)requiredCanonicalMilliUnits *
            referencePackPriceCents *
            MicroCentsPerCent /
            packCanonicalMilliUnits;

        decimal rounded = decimal.Round(
            rawCost,
            0,
            MidpointRounding.AwayFromZero
        );

        if (rounded < 0m || rounded > long.MaxValue)
        {
            error = "El coste calculado de " + ingredientId +
                    " queda fuera de rango.";
            return false;
        }

        costMicroCents = (long)rounded;
        return true;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(ingredientId))
        {
            error = "El IngredientId '" + ingredientId +
                    "' no es estable o válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            error = "El ingrediente " + ingredientId +
                    " no tiene nombre visible.";
            return false;
        }

        if (!Enum.IsDefined(
                typeof(BistroBuilderIngredientCategory),
                category
            ) ||
            !Enum.IsDefined(
                typeof(BistroBuilderIngredientStorageType),
                storageType
            ))
        {
            error = "El ingrediente " + ingredientId +
                    " contiene una clasificación desconocida.";
            return false;
        }

        if (!BistroBuilderMeasurementUtility
                .IsCanonicalBaseUnit(baseUnit))
        {
            error = "El ingrediente " + ingredientId +
                    " debe usar g, ml, ud o ración como unidad base.";
            return false;
        }

        if (!BistroBuilderMeasurementUtility.IsDefined(referencePackUnit) ||
            !BistroBuilderMeasurementUtility.AreCompatible(
                baseUnit,
                referencePackUnit
            ))
        {
            error = "El envase de referencia de " + ingredientId +
                    " usa una unidad incompatible.";
            return false;
        }

        if (!TryGetReferencePackCanonicalMilliUnits(out _, out error))
        {
            return false;
        }

        if (referencePackPriceCents < 0 ||
            referencePackPriceCents > MaximumReferencePackPriceCents)
        {
            error = "El precio de referencia de " + ingredientId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (defaultShelfLifeDays < 0 ||
            defaultShelfLifeDays > MaximumShelfLifeDays)
        {
            error = "La vida útil de " + ingredientId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (perishable && defaultShelfLifeDays <= 0)
        {
            error = "El ingrediente perecedero " + ingredientId +
                    " debe declarar una vida útil positiva.";
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ingredientId = BistroBuilderMenuIdUtility.NormalizeStableId(
            ingredientId
        );
        displayName = displayName != null
            ? displayName.Trim()
            : string.Empty;
        referencePackAmount = Math.Max(0.001d, referencePackAmount);
        referencePackPriceCents = Mathf.Clamp(
            referencePackPriceCents,
            0,
            MaximumReferencePackPriceCents
        );
        defaultShelfLifeDays = Mathf.Clamp(
            defaultShelfLifeDays,
            0,
            MaximumShelfLifeDays
        );
    }
#endif
}
