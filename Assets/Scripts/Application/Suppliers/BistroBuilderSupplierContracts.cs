using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Descriptor inmutable de un ingrediente canónico visto desde Proveedores.
///
/// 2.3A1 abandona el descubrimiento heurístico por nombres: la información
/// procede directamente de BistroBuilderIngredientDefinition. Se conserva
/// únicamente lo necesario para construir y validar ofertas sin duplicar la
/// autoridad de ingredientes.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierIngredientDescriptor
{
    [SerializeField] private string ingredientId;
    [SerializeField] private string displayName;
    [SerializeField] private BistroBuilderMeasurementUnit baseUnit;
    [SerializeField] private BistroBuilderIngredientCategory category;
    [SerializeField] private BistroBuilderIngredientStorageType storageType;
    [SerializeField] private bool perishable;
    [SerializeField] private long referencePackCanonicalMilliUnits;
    [SerializeField] private int referencePackPriceCents;

    public string IngredientId => ingredientId;
    public string DisplayName => displayName;
    public BistroBuilderMeasurementUnit BaseUnit => baseUnit;
    public BistroBuilderIngredientCategory Category => category;
    public BistroBuilderIngredientStorageType StorageType => storageType;
    public bool Perishable => perishable;
    public long ReferencePackCanonicalMilliUnits => referencePackCanonicalMilliUnits;
    public int ReferencePackPriceCents => referencePackPriceCents;

    public BistroBuilderSupplierIngredientDescriptor(
        string ingredientId,
        string displayName,
        BistroBuilderMeasurementUnit baseUnit,
        BistroBuilderIngredientCategory category,
        BistroBuilderIngredientStorageType storageType,
        bool perishable,
        long referencePackCanonicalMilliUnits,
        int referencePackPriceCents)
    {
        this.ingredientId = ingredientId ?? string.Empty;
        this.displayName = string.IsNullOrWhiteSpace(displayName)
            ? this.ingredientId
            : displayName.Trim();
        this.baseUnit = baseUnit;
        this.category = category;
        this.storageType = storageType;
        this.perishable = perishable;
        this.referencePackCanonicalMilliUnits = Math.Max(0L, referencePackCanonicalMilliUnits);
        this.referencePackPriceCents = Math.Max(0, referencePackPriceCents);
    }

    public BistroBuilderSupplierIngredientDescriptor Clone()
    {
        // Copia EXACTA, no una reconstrucción normalizada. Si un dato
        // serializado estuviera corrupto, el clon debe conservarlo para que el
        // validador lo detecte en vez de sanearlo silenciosamente.
        BistroBuilderSupplierIngredientDescriptor clone =
            new BistroBuilderSupplierIngredientDescriptor(
                ingredientId, displayName, baseUnit, category, storageType,
                perishable, referencePackCanonicalMilliUnits, referencePackPriceCents);
        clone.ingredientId = ingredientId;
        clone.displayName = displayName;
        clone.baseUnit = baseUnit;
        clone.category = category;
        clone.storageType = storageType;
        clone.perishable = perishable;
        clone.referencePackCanonicalMilliUnits = referencePackCanonicalMilliUnits;
        clone.referencePackPriceCents = referencePackPriceCents;
        return clone;
    }
}

/// <summary>
/// Definición canónica de un proveedor.
///
/// Todo dinero autoritativo se representa en céntimos enteros. Los factores
/// porcentuales usan puntos básicos (10.000 = 100 %) para evitar que 2.3B
/// herede cálculos monetarios con float.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierDefinition
{
    public const int BasisPointsPerOne = 10000;
    public const int MaximumMinimumOrderCents = 100000000;
    public const int MaximumLeadTimeDays = 3650;
    public const int MaximumPriceFactorBasisPoints = 1000000;

    [SerializeField] private string supplierId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private bool active = true;

    [SerializeField, Min(0)]
    private int minimumOrderCents;

    [SerializeField, Min(0)]
    private int standardLeadTimeDays;

    [SerializeField]
    private string currencyCode = "EUR";

    [SerializeField, Min(1)]
    private int priceFactorBasisPoints = BasisPointsPerOne;

    /*
     * Campos de migración de supplier.catalog v1. FormerlySerializedAs hace
     * que Unity recoja los floats existentes del asset instalado antes de A1.
     * Nunca vuelven a ser autoridad una vez migrado el esquema.
     */
    [FormerlySerializedAs("minimumOrderValue")]
    [SerializeField, HideInInspector]
    private float legacyMinimumOrderValue;

    [FormerlySerializedAs("priceFactor")]
    [SerializeField, HideInInspector]
    private float legacyPriceFactor;

    public string SupplierId => supplierId;
    public string DisplayName => displayName;
    public string Description => description;

    /// <summary>
    /// Flag de AUTORÍA del catálogo: permite retirar globalmente un proveedor
    /// de contenido sin borrarlo. No representa relación, desbloqueo, sanción
    /// ni estado por partida; esos estados deberán vivir en supplier.runtime.
    /// </summary>
    public bool IsCatalogEnabled => active;

    [Obsolete("Usar IsCatalogEnabled. El estado por partida pertenecerá a supplier.runtime.")]
    public bool IsActive => active;

    public int MinimumOrderCents => minimumOrderCents;

    /// <summary>Plazo por defecto usado al sembrar SKU; cada SKU persiste su LeadTimeDays real.</summary>
    public int DefaultLeadTimeDays => standardLeadTimeDays;

    [Obsolete("Usar DefaultLeadTimeDays para la definición del proveedor.")]
    public int StandardLeadTimeDays => standardLeadTimeDays;

    public string CurrencyCode => currencyCode;

    /// <summary>Factor exclusivamente de semilla/balance inicial; 2.3B debe usar PackPriceCents del SKU.</summary>
    public int SeedPriceFactorBasisPoints => priceFactorBasisPoints;

    [Obsolete("Usar SeedPriceFactorBasisPoints. Los pedidos nunca deben recalcular precios con este factor.")]
    public int PriceFactorBasisPoints => priceFactorBasisPoints;

    /// <summary>
    /// Compatibilidad de lectura con 2.3A v1. No debe usarse como dinero
    /// autoritativo en código nuevo.
    /// </summary>
    [Obsolete("Usar MinimumOrderCents para lógica autoritativa.")]
    public float MinimumOrderValue => minimumOrderCents / 100f;

    /// <summary>
    /// Compatibilidad de lectura con 2.3A v1.
    /// </summary>
    [Obsolete("Usar PriceFactorBasisPoints para lógica autoritativa.")]
    public float PriceFactor => priceFactorBasisPoints / (float)BasisPointsPerOne;

    public BistroBuilderSupplierDefinition(
        string supplierId,
        string displayName,
        string description,
        bool active,
        int minimumOrderCents,
        int standardLeadTimeDays,
        string currencyCode,
        int priceFactorBasisPoints)
    {
        this.supplierId = supplierId ?? string.Empty;
        this.displayName = displayName != null ? displayName.Trim() : string.Empty;
        this.description = description != null ? description.Trim() : string.Empty;
        this.active = active;
        this.minimumOrderCents = Math.Max(0, minimumOrderCents);
        this.standardLeadTimeDays = Math.Max(0, standardLeadTimeDays);
        this.currencyCode = NormalizeCurrency(currencyCode);
        this.priceFactorBasisPoints = Math.Max(1, priceFactorBasisPoints);
        legacyMinimumOrderValue = this.minimumOrderCents / 100f;
        legacyPriceFactor = this.priceFactorBasisPoints / (float)BasisPointsPerOne;
    }

    /// <summary>
    /// Constructor legado conservado para no romper código temporal que aún
    /// compile contra la firma de 2.3A v1. Convierte inmediatamente a enteros.
    /// </summary>
    [Obsolete("Usar el constructor con céntimos y puntos básicos.")]
    public BistroBuilderSupplierDefinition(
        string supplierId,
        string displayName,
        string description,
        bool active,
        float minimumOrderValue,
        int standardLeadTimeDays,
        string currencyCode,
        float priceFactor)
        : this(
            supplierId,
            displayName,
            description,
            active,
            RoundCurrencyToCents(minimumOrderValue),
            standardLeadTimeDays,
            currencyCode,
            RoundFactorToBasisPoints(priceFactor))
    {
    }

    public BistroBuilderSupplierDefinition Clone()
    {
        // Copia exacta por la misma razón que en los demás contratos: Clone no
        // es una función de reparación. La validación debe ver el dato real.
        BistroBuilderSupplierDefinition clone = new BistroBuilderSupplierDefinition(
            supplierId, displayName, description, active, minimumOrderCents,
            standardLeadTimeDays, currencyCode, priceFactorBasisPoints);
        clone.supplierId = supplierId;
        clone.displayName = displayName;
        clone.description = description;
        clone.active = active;
        clone.minimumOrderCents = minimumOrderCents;
        clone.standardLeadTimeDays = standardLeadTimeDays;
        clone.currencyCode = currencyCode;
        clone.priceFactorBasisPoints = priceFactorBasisPoints;
        clone.legacyMinimumOrderValue = legacyMinimumOrderValue;
        clone.legacyPriceFactor = legacyPriceFactor;
        return clone;
    }

    /// <summary>
    /// Migra exclusivamente los campos económicos de supplier.catalog v1.
    /// Debe invocarse solo cuando el asset indique SchemaVersion &lt; 2.
    /// </summary>
    public void MigrateLegacyEconomyFields()
    {
        minimumOrderCents = RoundCurrencyToCents(legacyMinimumOrderValue);
        priceFactorBasisPoints = RoundFactorToBasisPoints(legacyPriceFactor);

        if (priceFactorBasisPoints <= 0)
        {
            priceFactorBasisPoints = BasisPointsPerOne;
        }
    }

    private static int RoundCurrencyToCents(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return 0;
        }

        decimal raw = (decimal)value * 100m;
        decimal rounded = decimal.Round(raw, 0, MidpointRounding.AwayFromZero);
        return rounded > int.MaxValue ? int.MaxValue : (int)rounded;
    }

    private static int RoundFactorToBasisPoints(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return BasisPointsPerOne;
        }

        decimal raw = (decimal)value * BasisPointsPerOne;
        decimal rounded = decimal.Round(raw, 0, MidpointRounding.AwayFromZero);
        return rounded > int.MaxValue ? int.MaxValue : Math.Max(1, (int)rounded);
    }

    private static string NormalizeCurrency(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "EUR"
            : value.Trim().ToUpperInvariant();
    }
}

/// <summary>
/// Producto ofrecido por un proveedor y enlazado a un único ingrediente.
///
/// La cantidad de envase se guarda ya normalizada a milésimas de la unidad
/// base canónica de inventario. Esto permite que 2.3B/2.3C multipliquen packs
/// y entreguen exactamente la misma magnitud a 2.2B, sin conversiones float.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierProductDefinition
{
    public const int MaximumPackPriceCents = 100000000;

    // Contrato adelantado para 2.3B: una línea de pedido no admitirá más de
    // un millón de packs. Mantener esta cota aquí permite garantizar desde
    // 2.3A que cantidadPorPack * packs nunca desborde el rango canónico.
    public const int MaximumPacksPerOrderLine = 1000000;
    public const int MaximumMinimumPacks = MaximumPacksPerOrderLine;

    public const long MaximumPackageCanonicalMilliUnits =
        BistroBuilderMeasurementUtility.MaximumCanonicalMilliUnits /
        MaximumPacksPerOrderLine;

    public const int MaximumLeadTimeDays = 3650;

    [SerializeField] private string productId;
    [SerializeField] private string supplierId;
    [SerializeField] private string ingredientId;
    [SerializeField] private string displayName;
    [SerializeField] private string packageLabel;
    [SerializeField] private BistroBuilderMeasurementUnit baseUnit;
    [SerializeField] private long packageCanonicalMilliUnits;
    [SerializeField, Min(1)] private int packPriceCents;
    [SerializeField, Min(1)] private int minimumPacks;
    [SerializeField, Min(0)] private int leadTimeDays;
    [SerializeField] private bool available = true;
    [SerializeField] private string currencyCode = "EUR";

    public string ProductId => productId;
    public string SupplierId => supplierId;
    public string IngredientId => ingredientId;
    public string DisplayName => displayName;
    public string PackageLabel => packageLabel;
    public BistroBuilderMeasurementUnit BaseUnit => baseUnit;
    public long PackageCanonicalMilliUnits => packageCanonicalMilliUnits;
    public int PackPriceCents => packPriceCents;
    public int MinimumPacks => minimumPacks;
    public int LeadTimeDays => leadTimeDays;

    /// <summary>
    /// Flag de AUTORÍA del SKU. No representa una rotura de stock temporal ni
    /// una incidencia de una partida; 2.3C deberá modelar eso en supplier.runtime.
    /// </summary>
    public bool IsCatalogAvailable => available;

    [Obsolete("Usar IsCatalogAvailable. La disponibilidad temporal pertenecerá a supplier.runtime.")]
    public bool IsAvailable => available;
    public string CurrencyCode => currencyCode;

    /// <summary>
    /// Compatibilidad visual con 2.3A v1. No usar para cálculos de compra.
    /// </summary>
    [Obsolete("Usar PackPriceCents para lógica monetaria.")]
    public float UnitPrice => packPriceCents / 100f;

    /// <summary>
    /// Compatibilidad visual. La cantidad autoritativa es
    /// PackageCanonicalMilliUnits.
    /// </summary>
    [Obsolete("Usar PackageCanonicalMilliUnits para lógica de cantidad.")]
    public float PackageQuantity => (float)
        BistroBuilderMeasurementUtility.ConvertCanonicalMilliUnitsToDisplayAmount(
            packageCanonicalMilliUnits,
            baseUnit);

    [Obsolete("Usar BaseUnit y BistroBuilderMeasurementUtility.GetSymbol.")]
    public string PackageUnit => BistroBuilderMeasurementUtility.GetSymbol(baseUnit);

    public BistroBuilderSupplierProductDefinition(
        string productId,
        string supplierId,
        string ingredientId,
        string displayName,
        string packageLabel,
        BistroBuilderMeasurementUnit baseUnit,
        long packageCanonicalMilliUnits,
        int packPriceCents,
        int minimumPacks,
        int leadTimeDays,
        bool available,
        string currencyCode)
    {
        this.productId = productId ?? string.Empty;
        this.supplierId = supplierId ?? string.Empty;
        this.ingredientId = ingredientId ?? string.Empty;
        this.displayName = displayName != null ? displayName.Trim() : string.Empty;
        this.packageLabel = packageLabel != null ? packageLabel.Trim() : string.Empty;
        this.baseUnit = baseUnit;
        this.packageCanonicalMilliUnits = Math.Max(0L, packageCanonicalMilliUnits);
        this.packPriceCents = Math.Max(0, packPriceCents);
        this.minimumPacks = Math.Max(1, minimumPacks);
        this.leadTimeDays = Math.Max(0, leadTimeDays);
        this.available = available;
        this.currencyCode = string.IsNullOrWhiteSpace(currencyCode)
            ? "EUR"
            : currencyCode.Trim().ToUpperInvariant();
    }

    public BistroBuilderSupplierProductDefinition Clone()
    {
        BistroBuilderSupplierProductDefinition clone =
            new BistroBuilderSupplierProductDefinition(
                productId, supplierId, ingredientId, displayName, packageLabel,
                baseUnit, packageCanonicalMilliUnits, packPriceCents, minimumPacks,
                leadTimeDays, available, currencyCode);
        clone.productId = productId;
        clone.supplierId = supplierId;
        clone.ingredientId = ingredientId;
        clone.displayName = displayName;
        clone.packageLabel = packageLabel;
        clone.baseUnit = baseUnit;
        clone.packageCanonicalMilliUnits = packageCanonicalMilliUnits;
        clone.packPriceCents = packPriceCents;
        clone.minimumPacks = minimumPacks;
        clone.leadTimeDays = leadTimeDays;
        clone.available = available;
        clone.currencyCode = currencyCode;
        return clone;
    }
}

/// <summary>
/// Snapshot profundo y serializable del catálogo. Es una instantánea de
/// diagnóstico/compatibilidad, no una nueva autoridad ni una sección de save.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierCatalogSnapshot
{
    [SerializeField] private int schemaVersion;
    [SerializeField] private long contentRevision;
    [SerializeField] private List<BistroBuilderSupplierDefinition> suppliers =
        new List<BistroBuilderSupplierDefinition>();
    [SerializeField] private List<BistroBuilderSupplierProductDefinition> products =
        new List<BistroBuilderSupplierProductDefinition>();
    [SerializeField] private List<BistroBuilderSupplierIngredientDescriptor> ingredients =
        new List<BistroBuilderSupplierIngredientDescriptor>();

    [NonSerialized] private ReadOnlyCollection<BistroBuilderSupplierDefinition> suppliersView;
    [NonSerialized] private ReadOnlyCollection<BistroBuilderSupplierProductDefinition> productsView;
    [NonSerialized] private ReadOnlyCollection<BistroBuilderSupplierIngredientDescriptor> ingredientsView;

    public int SchemaVersion => schemaVersion;
    public int Revision => schemaVersion; // alias de compatibilidad 2.3A v1
    public long ContentRevision => contentRevision;

    public IReadOnlyList<BistroBuilderSupplierDefinition> Suppliers
    {
        get
        {
            EnsureCollections();
            return suppliersView ?? (suppliersView = suppliers.AsReadOnly());
        }
    }

    public IReadOnlyList<BistroBuilderSupplierProductDefinition> Products
    {
        get
        {
            EnsureCollections();
            return productsView ?? (productsView = products.AsReadOnly());
        }
    }

    public IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> Ingredients
    {
        get
        {
            EnsureCollections();
            return ingredientsView ?? (ingredientsView = ingredients.AsReadOnly());
        }
    }

    public BistroBuilderSupplierCatalogSnapshot(
        int schemaVersion,
        long contentRevision,
        IEnumerable<BistroBuilderSupplierDefinition> suppliers,
        IEnumerable<BistroBuilderSupplierProductDefinition> products,
        IEnumerable<BistroBuilderSupplierIngredientDescriptor> ingredients)
    {
        this.schemaVersion = Math.Max(0, schemaVersion);
        this.contentRevision = Math.Max(0L, contentRevision);

        if (suppliers != null)
        {
            foreach (BistroBuilderSupplierDefinition supplier in suppliers)
            {
                if (supplier != null)
                {
                    this.suppliers.Add(supplier.Clone());
                }
            }
        }

        if (products != null)
        {
            foreach (BistroBuilderSupplierProductDefinition product in products)
            {
                if (product != null)
                {
                    this.products.Add(product.Clone());
                }
            }
        }

        if (ingredients != null)
        {
            foreach (BistroBuilderSupplierIngredientDescriptor ingredient in ingredients)
            {
                if (ingredient != null)
                {
                    this.ingredients.Add(ingredient.Clone());
                }
            }
        }
    }

    /// <summary>
    /// JsonUtility puede materializar snapshots antiguos o parciales sin
    /// ejecutar el constructor. Las vistas nunca deben lanzar NullReference
    /// por una colección ausente: un consumidor puede inspeccionar el snapshot
    /// y dejar que el validador decida después si su contenido es suficiente.
    /// </summary>
    private void EnsureCollections()
    {
        if (suppliers == null)
        {
            suppliers = new List<BistroBuilderSupplierDefinition>();
            suppliersView = null;
        }

        if (products == null)
        {
            products = new List<BistroBuilderSupplierProductDefinition>();
            productsView = null;
        }

        if (ingredients == null)
        {
            ingredients = new List<BistroBuilderSupplierIngredientDescriptor>();
            ingredientsView = null;
        }
    }
}

/// <summary>
/// Resultado estructurado de validación del catálogo.
/// </summary>
public sealed class BistroBuilderSupplierCatalogValidationResult
{
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly ReadOnlyCollection<string> errorsView;
    private readonly ReadOnlyCollection<string> warningsView;

    public BistroBuilderSupplierCatalogValidationResult()
    {
        errorsView = errors.AsReadOnly();
        warningsView = warnings.AsReadOnly();
    }

    public IReadOnlyList<string> Errors => errorsView;
    public IReadOnlyList<string> Warnings => warningsView;
    public int ErrorCount => errors.Count;
    public int WarningCount => warnings.Count;
    public bool IsValid => errors.Count == 0;

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            errors.Add(message.Trim());
        }
    }

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            warnings.Add(message.Trim());
        }
    }
}
