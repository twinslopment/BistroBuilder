using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Autoridad de lectura del catálogo de proveedores de Bistro Builder 2.3A1.
///
/// Garantías:
/// - Única autoridad runtime para proveedores/productos.
/// - Reconstrucción atómica e idempotente.
/// - Una reconstrucción idéntica no reemplaza referencias ni emite eventos.
/// - Un fallo de reconstrucción posterior conserva el último catálogo válido.
/// - Colecciones expuestas realmente de solo lectura.
/// - Ofertas disponibles y proveedor activo son conceptos separados.
/// - Comparación de precios normalizada por cantidad de envase.
/// - Ninguna referencia de escritura a inventario/recepciones.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Suppliers/Supplier Catalog Service")]
public sealed class BistroBuilderSupplierCatalogService : MonoBehaviour
{
    public const int CatalogRevision = BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion;

    private static BistroBuilderSupplierCatalogService instance;

    private readonly List<BistroBuilderSupplierDefinition> suppliers =
        new List<BistroBuilderSupplierDefinition>();
    private readonly List<BistroBuilderSupplierProductDefinition> products =
        new List<BistroBuilderSupplierProductDefinition>();
    private readonly List<BistroBuilderSupplierIngredientDescriptor> ingredients =
        new List<BistroBuilderSupplierIngredientDescriptor>();
    private readonly List<string> lastValidationWarnings = new List<string>();

    private ReadOnlyCollection<BistroBuilderSupplierDefinition> suppliersView;
    private ReadOnlyCollection<BistroBuilderSupplierProductDefinition> productsView;
    private ReadOnlyCollection<BistroBuilderSupplierIngredientDescriptor> ingredientsView;
    private ReadOnlyCollection<string> warningsView;

    private readonly Dictionary<string, BistroBuilderSupplierDefinition> supplierById =
        new Dictionary<string, BistroBuilderSupplierDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, BistroBuilderSupplierProductDefinition> productById =
        new Dictionary<string, BistroBuilderSupplierProductDefinition>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<BistroBuilderSupplierProductDefinition>> productsBySupplier =
        new Dictionary<string, List<BistroBuilderSupplierProductDefinition>>(StringComparer.Ordinal);
    private readonly Dictionary<string, List<BistroBuilderSupplierProductDefinition>> offersByIngredient =
        new Dictionary<string, List<BistroBuilderSupplierProductDefinition>>(StringComparer.Ordinal);
    private readonly Dictionary<string, ReadOnlyCollection<BistroBuilderSupplierProductDefinition>>
        productsBySupplierView =
            new Dictionary<string, ReadOnlyCollection<BistroBuilderSupplierProductDefinition>>(StringComparer.Ordinal);
    private readonly Dictionary<string, ReadOnlyCollection<BistroBuilderSupplierProductDefinition>>
        offersByIngredientView =
            new Dictionary<string, ReadOnlyCollection<BistroBuilderSupplierProductDefinition>>(StringComparer.Ordinal);

    private Coroutine initializationRoutine;
    private bool initialized;
    private string lastInitializationError = string.Empty;
    private ulong contentSignature;
    private long contentRevision;

    public static BistroBuilderSupplierCatalogService Instance => instance;
    public bool IsInitialized => initialized;
    public int Revision => CatalogRevision;
    public long ContentRevision => contentRevision;
    public int SupplierCount => suppliers.Count;
    public int ProductCount => products.Count;
    public int IngredientCount => ingredients.Count;
    public string CatalogCurrencyCode => suppliers.Count > 0 ? suppliers[0].CurrencyCode : string.Empty;
    public string LastInitializationError => lastInitializationError;
    public IReadOnlyList<BistroBuilderSupplierDefinition> Suppliers => suppliersView;
    public IReadOnlyList<BistroBuilderSupplierProductDefinition> Products => productsView;
    public IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> Ingredients => ingredientsView;
    public IReadOnlyList<string> LastValidationWarnings => warningsView;

    /// <summary>
    /// Se publica SOLO cuando el contenido canónico cambia de verdad.
    /// Una reconstrucción idempotente no dispara el evento.
    /// </summary>
    public event Action CatalogChanged;

    private void Awake()
    {
        EnsureViews();

        // Un componente deshabilitado no debe secuestrar el Singleton. Awake
        // puede ejecutarse aunque enabled=false sobre un GameObject activo.
        if (isActiveAndEnabled)
        {
            TryClaimRuntimeAuthority();
        }
    }

    private void OnEnable()
    {
        if (!TryClaimRuntimeAuthority())
        {
            return;
        }

        if (initializationRoutine == null && !initialized)
        {
            initializationRoutine = StartCoroutine(InitializeWhenReady());
        }
    }

    private void OnDisable()
    {
        if (initializationRoutine != null)
        {
            StopCoroutine(initializationRoutine);
            initializationRoutine = null;
        }

        // Instance nunca debe apuntar a una autoridad deshabilitada. Si este
        // componente se reactiva, OnEnable volverá a reclamarla de forma segura.
        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private bool TryClaimRuntimeAuthority()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (instance != null && instance != this)
        {
            // Nunca destruir el GameObject completo: un servicio duplicado
            // podría haberse añadido por error a un objeto que también aloja
            // otros sistemas válidos. Se elimina únicamente este componente.
            Debug.LogWarning(
                "Se detectó un SupplierCatalogService runtime duplicado. " +
                "Se conservará la autoridad existente y se retirará solo el componente duplicado.",
                this);
            enabled = false;
            Destroy(this);
            return false;
        }

        instance = this;
        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
        return true;
    }

    private IEnumerator InitializeWhenReady()
    {
        const int maximumAttempts = 12;

        // Todas las dependencias deberían haber ejecutado Awake antes de este
        // primer intento. Evita depender del orden arbitrario de OnEnable entre
        // componentes de scripts distintos.
        yield return null;

        for (int attempt = 0; attempt < maximumAttempts; attempt++)
        {
            if (TryRebuildCatalog(out string error))
            {
                initializationRoutine = null;
                yield break;
            }

            lastInitializationError = error;
            yield return null;
        }

        initializationRoutine = null;
        Debug.LogError(
            "2.3A1 no pudo inicializar el catálogo de proveedores: " +
            lastInitializationError,
            this);
    }

    /// <summary>
    /// Reconstruye desde datos canónicos actuales. La sustitución solo se hace
    /// después de construir y validar completamente un candidato.
    /// </summary>
    public bool TryRebuildCatalog(out string error)
    {
        error = string.Empty;

        if (!isActiveAndEnabled)
        {
            error = "SupplierCatalogService está deshabilitado y no puede actuar como autoridad runtime.";
            lastInitializationError = error;
            return false;
        }

        if (instance == null)
        {
            if (!TryClaimRuntimeAuthority())
            {
                error = "SupplierCatalogService no pudo reclamar la autoridad runtime.";
                lastInitializationError = error;
                return false;
            }
        }
        else if (instance != this)
        {
            error = "Existe otro SupplierCatalogService que ya es la autoridad runtime.";
            lastInitializationError = error;
            return false;
        }

        if (!BistroBuilderCanonicalIngredientDiscovery.TryDiscover(
                out List<BistroBuilderSupplierIngredientDescriptor> discovered,
                out error))
        {
            lastInitializationError = error;
            return false;
        }

        BistroBuilderSupplierCatalogSettings settings =
            Resources.Load<BistroBuilderSupplierCatalogSettings>(
                BistroBuilderSupplierCatalogSettings.ResourcesPath);

        if (settings == null)
        {
            error =
                "Falta el asset canónico de supplier.catalog en Resources. " +
                "Ejecuta el instalador 2.3A antes de iniciar Proveedores.";
            lastInitializationError = error;
            return false;
        }

        if (settings.SchemaVersion != BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion)
        {
            error =
                "supplier.catalog usa schema v" + settings.SchemaVersion +
                " y runtime exige v" +
                BistroBuilderSupplierCatalogSettings.CurrentSchemaVersion +
                ". Ejecuta el instalador/migrador 2.3A.";
            lastInitializationError = error;
            return false;
        }

        if (settings.Suppliers == null || settings.Suppliers.Count == 0)
        {
            error = "supplier.catalog existe pero no contiene proveedores.";
            lastInitializationError = error;
            return false;
        }

        if (settings.Products == null || settings.Products.Count == 0)
        {
            error =
                "supplier.catalog v2 existe pero no contiene productos. " +
                "Ejecuta el instalador para sembrar el catálogo inicial.";
            lastInitializationError = error;
            return false;
        }

        List<BistroBuilderSupplierDefinition> sourceSuppliers =
            new List<BistroBuilderSupplierDefinition>(settings.Suppliers.Count);
        for (int i = 0; i < settings.Suppliers.Count; i++)
        {
            sourceSuppliers.Add(
                settings.Suppliers[i] != null
                    ? settings.Suppliers[i].Clone()
                    : null);
        }
        sourceSuppliers.Sort(
            (left, right) => string.Compare(
                left != null ? left.SupplierId : string.Empty,
                right != null ? right.SupplierId : string.Empty,
                StringComparison.Ordinal));

        List<BistroBuilderSupplierProductDefinition> sourceProducts =
            new List<BistroBuilderSupplierProductDefinition>(settings.Products.Count);
        for (int i = 0; i < settings.Products.Count; i++)
        {
            sourceProducts.Add(
                settings.Products[i] != null
                    ? settings.Products[i].Clone()
                    : null);
        }
        sourceProducts.Sort(
            (left, right) => string.Compare(
                left != null ? left.ProductId : string.Empty,
                right != null ? right.ProductId : string.Empty,
                StringComparison.Ordinal));

        return TryApplyOwnedCandidate(
            sourceSuppliers,
            sourceProducts,
            discovered,
            out error);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Seam de prueba SOLO Editor. Permite someter el mismo camino atómico de
    /// validación/aplicación a candidatos sintéticos sin tocar el asset de
    /// Resources, inventario ni partidas. El llamador debe restaurar después
    /// el catálogo real mediante TryRebuildCatalog. No existe en builds.
    /// </summary>
    public bool TryApplyCatalogCandidateForEditorTests(
        IReadOnlyList<BistroBuilderSupplierDefinition> candidateSuppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> candidateProducts,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> candidateIngredients,
        out string error)
    {
        error = string.Empty;

        if (!isActiveAndEnabled || instance != this)
        {
            error = "Solo la autoridad runtime activa puede aplicar candidatos de prueba.";
            return false;
        }

        List<BistroBuilderSupplierDefinition> ownedSuppliers =
            new List<BistroBuilderSupplierDefinition>();
        if (candidateSuppliers != null)
        {
            for (int i = 0; i < candidateSuppliers.Count; i++)
                ownedSuppliers.Add(candidateSuppliers[i] != null ? candidateSuppliers[i].Clone() : null);
        }

        List<BistroBuilderSupplierProductDefinition> ownedProducts =
            new List<BistroBuilderSupplierProductDefinition>();
        if (candidateProducts != null)
        {
            for (int i = 0; i < candidateProducts.Count; i++)
                ownedProducts.Add(candidateProducts[i] != null ? candidateProducts[i].Clone() : null);
        }

        List<BistroBuilderSupplierIngredientDescriptor> ownedIngredients =
            new List<BistroBuilderSupplierIngredientDescriptor>();
        if (candidateIngredients != null)
        {
            for (int i = 0; i < candidateIngredients.Count; i++)
                ownedIngredients.Add(candidateIngredients[i] != null ? candidateIngredients[i].Clone() : null);
        }

        return TryApplyOwnedCandidate(ownedSuppliers, ownedProducts, ownedIngredients, out error);
    }
#endif

    /// <summary>
    /// Único punto de aplicación de contenido. Recibe colecciones propiedad
    /// del servicio/caller de test, las ordena, valida por completo y solo
    /// sustituye la autoridad después de superar todas las invariantes.
    /// </summary>
    private bool TryApplyOwnedCandidate(
        List<BistroBuilderSupplierDefinition> candidateSuppliers,
        List<BistroBuilderSupplierProductDefinition> candidateProducts,
        List<BistroBuilderSupplierIngredientDescriptor> candidateIngredients,
        out string error)
    {
        error = string.Empty;

        if (candidateSuppliers == null || candidateProducts == null || candidateIngredients == null)
        {
            error = "El candidato de supplier.catalog contiene colecciones nulas.";
            lastInitializationError = error;
            return false;
        }

        candidateSuppliers.Sort((left, right) => string.Compare(
            left != null ? left.SupplierId : string.Empty,
            right != null ? right.SupplierId : string.Empty,
            StringComparison.Ordinal));
        candidateProducts.Sort((left, right) => string.Compare(
            left != null ? left.ProductId : string.Empty,
            right != null ? right.ProductId : string.Empty,
            StringComparison.Ordinal));
        candidateIngredients.Sort((left, right) => string.Compare(
            left != null ? left.IngredientId : string.Empty,
            right != null ? right.IngredientId : string.Empty,
            StringComparison.Ordinal));

        BistroBuilderSupplierCatalogValidationResult validation =
            BistroBuilderSupplierCatalogValidator.Validate(
                candidateSuppliers,
                candidateProducts,
                candidateIngredients,
                BistroBuilderSupplierCatalogBuilder.RecommendedDistinctSuppliersPerIngredient,
                reportOperationalGapsAsWarnings: true);

        if (!validation.IsValid)
        {
            error = validation.Errors.Count > 0
                ? validation.Errors[0]
                : "El catálogo de proveedores no superó la validación.";
            lastInitializationError = error;
            return false;
        }

        ulong newSignature = CalculateContentSignature(
            candidateSuppliers, candidateProducts, candidateIngredients);

        // La firma es solo un pre-filtro. La igualdad profunda evita que una
        // improbable colisión de hash se interprete como idempotencia.
        if (initialized && newSignature == contentSignature &&
            DeepEqualCatalog(candidateSuppliers, candidateProducts, candidateIngredients))
        {
            ReplaceValidationWarnings(validation);
            lastInitializationError = string.Empty;
            return true;
        }

        ReplaceRuntimeCatalog(candidateSuppliers, candidateProducts, candidateIngredients);
        contentSignature = newSignature;
        ReplaceValidationWarnings(validation);
        if (contentRevision < long.MaxValue)
        {
            contentRevision++;
        }
        initialized = true;
        lastInitializationError = string.Empty;

        Debug.Log(
            "2.3A1 proveedores canónicos inicializados: " +
            suppliers.Count + " proveedor(es), " +
            products.Count + " producto(s), " +
            ingredients.Count + " ingrediente(s), revisión de contenido " +
            contentRevision + ".",
            this);

        PublishCatalogChanged();
        return true;
    }

    private void ReplaceValidationWarnings(BistroBuilderSupplierCatalogValidationResult validation)
    {
        lastValidationWarnings.Clear();
        if (validation == null) return;
        for (int i = 0; i < validation.Warnings.Count; i++)
            lastValidationWarnings.Add(validation.Warnings[i]);
    }

    public bool TryGetSupplier(
        string supplierId,
        out BistroBuilderSupplierDefinition supplier)
    {
        supplier = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(supplierId);
        return !string.IsNullOrWhiteSpace(normalized) &&
               supplierById.TryGetValue(normalized, out supplier);
    }

    public bool TryGetProduct(
        string productId,
        out BistroBuilderSupplierProductDefinition product)
    {
        product = null;
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(productId);
        return !string.IsNullOrWhiteSpace(normalized) &&
               productById.TryGetValue(normalized, out product);
    }

    public IReadOnlyList<BistroBuilderSupplierProductDefinition>
        GetProductsForSupplier(string supplierId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(supplierId);
        if (string.IsNullOrWhiteSpace(normalized) ||
            !productsBySupplierView.TryGetValue(
                normalized,
                out ReadOnlyCollection<BistroBuilderSupplierProductDefinition> view))
        {
            return Array.Empty<BistroBuilderSupplierProductDefinition>();
        }

        return view;
    }

    public IReadOnlyList<BistroBuilderSupplierProductDefinition>
        GetOffersForIngredient(string ingredientId)
    {
        string normalized = BistroBuilderMenuIdUtility.NormalizeStableId(ingredientId);
        if (string.IsNullOrWhiteSpace(normalized) ||
            !offersByIngredientView.TryGetValue(
                normalized,
                out ReadOnlyCollection<BistroBuilderSupplierProductDefinition> view))
        {
            return Array.Empty<BistroBuilderSupplierProductDefinition>();
        }

        return view;
    }

    public bool IsOfferPurchasable(BistroBuilderSupplierProductDefinition offer)
    {
        if (offer == null ||
            !TryGetProduct(offer.ProductId, out BistroBuilderSupplierProductDefinition canonicalOffer) ||
            canonicalOffer == null ||
            !canonicalOffer.IsCatalogAvailable)
        {
            return false;
        }

        // El objeto recibido funciona como referencia por ProductId. Toda la
        // decisión se toma contra la instancia canónica actual, de modo que un
        // clon/snapshot externo no pueda falsificar disponibilidad, proveedor
        // ni condiciones comerciales.
        return TryGetSupplier(
                   canonicalOffer.SupplierId,
                   out BistroBuilderSupplierDefinition supplier) &&
               supplier != null &&
               supplier.IsCatalogEnabled;
    }

    /// <summary>
    /// Devuelve la oferta comprable de menor coste normalizado. El resultado
    /// es estable incluso si en el futuro los proveedores venden envases de
    /// tamaños diferentes.
    /// </summary>
    public bool TryFindLowestUnitCostPurchasableOffer(
        string ingredientId,
        out BistroBuilderSupplierProductDefinition offer)
    {
        offer = null;
        IReadOnlyList<BistroBuilderSupplierProductDefinition> offers =
            GetOffersForIngredient(ingredientId);

        for (int i = 0; i < offers.Count; i++)
        {
            BistroBuilderSupplierProductDefinition candidate = offers[i];
            if (!IsOfferPurchasable(candidate))
            {
                continue;
            }

            if (offer == null ||
                BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost(
                    candidate,
                    offer) < 0)
            {
                offer = candidate;
            }
        }

        return offer != null;
    }

    [Obsolete("Usar TryFindLowestUnitCostPurchasableOffer. El precio unitario no implica menor coste total de pedido.")]
    public bool TryFindCheapestActiveOffer(
        string ingredientId,
        out BistroBuilderSupplierProductDefinition offer)
    {
        return TryFindLowestUnitCostPurchasableOffer(ingredientId, out offer);
    }

    /// <summary>
    /// Copia sin asignaciones internas las ofertas actualmente comprables a
    /// un buffer propiedad del llamador. Preparado para UI 2.3D.
    /// </summary>
    public void CopyPurchasableOffersForIngredient(
        string ingredientId,
        List<BistroBuilderSupplierProductDefinition> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        IReadOnlyList<BistroBuilderSupplierProductDefinition> offers =
            GetOffersForIngredient(ingredientId);

        for (int i = 0; i < offers.Count; i++)
        {
            if (IsOfferPurchasable(offers[i]))
            {
                destination.Add(offers[i]);
            }
        }
    }

    public BistroBuilderSupplierCatalogSnapshot CreateSnapshot()
    {
        return new BistroBuilderSupplierCatalogSnapshot(
            CatalogRevision,
            contentRevision,
            suppliers,
            products,
            ingredients);
    }

    private void ReplaceRuntimeCatalog(
        List<BistroBuilderSupplierDefinition> newSuppliers,
        List<BistroBuilderSupplierProductDefinition> newProducts,
        List<BistroBuilderSupplierIngredientDescriptor> newIngredients)
    {
        suppliers.Clear();
        products.Clear();
        ingredients.Clear();
        supplierById.Clear();
        productById.Clear();
        productsBySupplier.Clear();
        offersByIngredient.Clear();
        productsBySupplierView.Clear();
        offersByIngredientView.Clear();

        suppliers.AddRange(newSuppliers);
        products.AddRange(newProducts);
        ingredients.AddRange(newIngredients);

        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = suppliers[i];
            supplierById.Add(supplier.SupplierId, supplier);
            productsBySupplier.Add(
                supplier.SupplierId,
                new List<BistroBuilderSupplierProductDefinition>());
        }

        for (int i = 0; i < products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = products[i];
            productById.Add(product.ProductId, product);

            if (!productsBySupplier.TryGetValue(
                    product.SupplierId,
                    out List<BistroBuilderSupplierProductDefinition> supplierProducts))
            {
                supplierProducts = new List<BistroBuilderSupplierProductDefinition>();
                productsBySupplier.Add(product.SupplierId, supplierProducts);
            }
            supplierProducts.Add(product);

            if (!offersByIngredient.TryGetValue(
                    product.IngredientId,
                    out List<BistroBuilderSupplierProductDefinition> ingredientOffers))
            {
                ingredientOffers = new List<BistroBuilderSupplierProductDefinition>();
                offersByIngredient.Add(product.IngredientId, ingredientOffers);
            }
            ingredientOffers.Add(product);
        }

        foreach (KeyValuePair<string, List<BistroBuilderSupplierProductDefinition>> pair
                 in productsBySupplier)
        {
            pair.Value.Sort(
                (left, right) => string.Compare(
                    left.ProductId,
                    right.ProductId,
                    StringComparison.Ordinal));
            productsBySupplierView.Add(pair.Key, pair.Value.AsReadOnly());
        }

        foreach (KeyValuePair<string, List<BistroBuilderSupplierProductDefinition>> pair
                 in offersByIngredient)
        {
            pair.Value.Sort(BistroBuilderSupplierCatalogBuilder.CompareNormalizedUnitCost);
            offersByIngredientView.Add(pair.Key, pair.Value.AsReadOnly());
        }
    }

    private void EnsureViews()
    {
        if (suppliersView == null)
        {
            suppliersView = suppliers.AsReadOnly();
            productsView = products.AsReadOnly();
            ingredientsView = ingredients.AsReadOnly();
            warningsView = lastValidationWarnings.AsReadOnly();
        }
    }

    private void PublishCatalogChanged()
    {
        Action handlers = CatalogChanged;
        if (handlers == null)
        {
            return;
        }

        Delegate[] invocationList = handlers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action)invocationList[i]).Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private bool DeepEqualCatalog(
        IReadOnlyList<BistroBuilderSupplierDefinition> candidateSuppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> candidateProducts,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> candidateIngredients)
    {
        if (candidateSuppliers == null || candidateProducts == null || candidateIngredients == null ||
            candidateSuppliers.Count != suppliers.Count ||
            candidateProducts.Count != products.Count ||
            candidateIngredients.Count != ingredients.Count)
        {
            return false;
        }

        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition a = suppliers[i];
            BistroBuilderSupplierDefinition b = candidateSuppliers[i];
            if (a == null || b == null)
            {
                if (!ReferenceEquals(a, b)) return false;
                continue;
            }

            if (a.SupplierId != b.SupplierId ||
                a.DisplayName != b.DisplayName ||
                a.Description != b.Description ||
                a.IsCatalogEnabled != b.IsCatalogEnabled ||
                a.MinimumOrderCents != b.MinimumOrderCents ||
                a.DefaultLeadTimeDays != b.DefaultLeadTimeDays ||
                a.CurrencyCode != b.CurrencyCode ||
                a.SeedPriceFactorBasisPoints != b.SeedPriceFactorBasisPoints)
            {
                return false;
            }
        }

        for (int i = 0; i < products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition a = products[i];
            BistroBuilderSupplierProductDefinition b = candidateProducts[i];
            if (a == null || b == null)
            {
                if (!ReferenceEquals(a, b)) return false;
                continue;
            }

            if (a.ProductId != b.ProductId ||
                a.SupplierId != b.SupplierId ||
                a.IngredientId != b.IngredientId ||
                a.DisplayName != b.DisplayName ||
                a.PackageLabel != b.PackageLabel ||
                a.BaseUnit != b.BaseUnit ||
                a.PackageCanonicalMilliUnits != b.PackageCanonicalMilliUnits ||
                a.PackPriceCents != b.PackPriceCents ||
                a.MinimumPacks != b.MinimumPacks ||
                a.LeadTimeDays != b.LeadTimeDays ||
                a.IsCatalogAvailable != b.IsCatalogAvailable ||
                a.CurrencyCode != b.CurrencyCode)
            {
                return false;
            }
        }

        for (int i = 0; i < ingredients.Count; i++)
        {
            BistroBuilderSupplierIngredientDescriptor a = ingredients[i];
            BistroBuilderSupplierIngredientDescriptor b = candidateIngredients[i];
            if (a == null || b == null)
            {
                if (!ReferenceEquals(a, b)) return false;
                continue;
            }

            if (a.IngredientId != b.IngredientId ||
                a.DisplayName != b.DisplayName ||
                a.BaseUnit != b.BaseUnit ||
                a.Category != b.Category ||
                a.StorageType != b.StorageType ||
                a.Perishable != b.Perishable ||
                a.ReferencePackCanonicalMilliUnits != b.ReferencePackCanonicalMilliUnits ||
                a.ReferencePackPriceCents != b.ReferencePackPriceCents)
            {
                return false;
            }
        }

        return true;
    }

    private static ulong CalculateContentSignature(
        IReadOnlyList<BistroBuilderSupplierDefinition> suppliers,
        IReadOnlyList<BistroBuilderSupplierProductDefinition> products,
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients)
    {
        ulong hash = 14695981039346656037UL;

        if (suppliers != null)
        {
            for (int i = 0; i < suppliers.Count; i++)
            {
                BistroBuilderSupplierDefinition s = suppliers[i];
                Hash(ref hash, s != null ? s.SupplierId : "<null>");
                if (s == null) continue;
                Hash(ref hash, s.DisplayName);
                Hash(ref hash, s.Description);
                Hash(ref hash, s.IsCatalogEnabled ? 1L : 0L);
                Hash(ref hash, s.MinimumOrderCents);
                Hash(ref hash, s.DefaultLeadTimeDays);
                Hash(ref hash, s.CurrencyCode);
                Hash(ref hash, s.SeedPriceFactorBasisPoints);
            }
        }

        if (products != null)
        {
            for (int i = 0; i < products.Count; i++)
            {
                BistroBuilderSupplierProductDefinition p = products[i];
                Hash(ref hash, p != null ? p.ProductId : "<null>");
                if (p == null) continue;
                Hash(ref hash, p.SupplierId);
                Hash(ref hash, p.IngredientId);
                Hash(ref hash, p.DisplayName);
                Hash(ref hash, p.PackageLabel);
                Hash(ref hash, (long)p.BaseUnit);
                Hash(ref hash, p.PackageCanonicalMilliUnits);
                Hash(ref hash, p.PackPriceCents);
                Hash(ref hash, p.MinimumPacks);
                Hash(ref hash, p.LeadTimeDays);
                Hash(ref hash, p.IsCatalogAvailable ? 1L : 0L);
                Hash(ref hash, p.CurrencyCode);
            }
        }

        if (ingredients != null)
        {
            for (int i = 0; i < ingredients.Count; i++)
            {
                BistroBuilderSupplierIngredientDescriptor ingredient = ingredients[i];
                Hash(ref hash, ingredient != null ? ingredient.IngredientId : "<null>");
                if (ingredient == null) continue;
                Hash(ref hash, ingredient.DisplayName);
                Hash(ref hash, (long)ingredient.BaseUnit);
                Hash(ref hash, (long)ingredient.Category);
                Hash(ref hash, (long)ingredient.StorageType);
                Hash(ref hash, ingredient.Perishable ? 1L : 0L);
                Hash(ref hash, ingredient.ReferencePackCanonicalMilliUnits);
                Hash(ref hash, ingredient.ReferencePackPriceCents);
            }
        }

        return hash;
    }

    private static void Hash(ref ulong hash, string value)
    {
        unchecked
        {
            const ulong prime = 1099511628211UL;
            string safe = value ?? string.Empty;
            for (int i = 0; i < safe.Length; i++)
            {
                hash ^= safe[i];
                hash *= prime;
            }
            hash ^= 0xff;
            hash *= prime;
        }
    }

    private static void Hash(ref ulong hash, long value)
    {
        unchecked
        {
            const ulong prime = 1099511628211UL;
            ulong raw = (ulong)value;
            for (int i = 0; i < 8; i++)
            {
                hash ^= (byte)(raw & 0xffUL);
                hash *= prime;
                raw >>= 8;
            }
        }
    }
}
