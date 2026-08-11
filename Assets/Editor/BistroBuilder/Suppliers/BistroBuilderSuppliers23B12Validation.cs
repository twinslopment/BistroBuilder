#if UNITY_EDITOR
using System;
using System.Collections.Generic;

internal enum BistroBuilderSuppliers23B12IssueSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

internal sealed class BistroBuilderSuppliers23B12Issue
{
    public BistroBuilderSuppliers23B12IssueSeverity severity;
    public string code;
    public string recordId;
    public string message;
}

internal sealed class BistroBuilderSuppliers23B12ValidationReport
{
    private readonly List<BistroBuilderSuppliers23B12Issue> issues =
        new List<BistroBuilderSuppliers23B12Issue>();

    public IReadOnlyList<BistroBuilderSuppliers23B12Issue> Issues => issues;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }

    public void Add(
        BistroBuilderSuppliers23B12IssueSeverity severity,
        string code,
        string message,
        string recordId = null)
    {
        issues.Add(new BistroBuilderSuppliers23B12Issue
        {
            severity = severity,
            code = code ?? string.Empty,
            recordId = recordId ?? string.Empty,
            message = message ?? string.Empty
        });

        if (severity == BistroBuilderSuppliers23B12IssueSeverity.Error)
        {
            ErrorCount++;
        }
        else if (severity == BistroBuilderSuppliers23B12IssueSeverity.Warning)
        {
            WarningCount++;
        }
        else
        {
            InfoCount++;
        }
    }
}

/// <summary>
/// Validador estructural conjunto de B1+B2.
/// No consulta ni modifica inventario, pedidos o recepciones.
/// </summary>
internal static class BistroBuilderSuppliers23B12Validator
{
    private sealed class PackageOwner
    {
        public BistroBuilderIngredientAuthoringRecord ingredient;
        public BistroBuilderCommercialPackageAuthoringRecord package;
    }

    public static BistroBuilderSuppliers23B12ValidationReport Validate(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients)
    {
        BistroBuilderSuppliers23B12ValidationReport report =
            new BistroBuilderSuppliers23B12ValidationReport();

        if (suppliers == null)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Error,
                "SUP_DB",
                "No existe supplier.authoring.");
        }

        if (ingredients == null)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Error,
                "ING_DB",
                "No existe ingredient.authoring.");
        }

        if (suppliers == null || ingredients == null)
        {
            return report;
        }

        if (suppliers.SchemaVersion !=
            BistroBuilderSupplierAuthoringDatabase.CurrentSchemaVersion)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Error,
                "SUP_SCHEMA",
                "supplier.authoring no está migrado al schema actual.");
        }

        if (ingredients.SchemaVersion !=
            BistroBuilderIngredientAuthoringDatabase.CurrentSchemaVersion)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Error,
                "ING_SCHEMA",
                "ingredient.authoring no está migrado al schema actual.");
        }

        Dictionary<string, PackageOwner> packageIndex =
            BuildPackageIndex(ingredients, report);

        ValidateOffers(suppliers, ingredients, packageIndex, report);
        ValidateCompetition(suppliers, ingredients, report);
        ValidateSupplierCoverage(suppliers, report);
        ValidateVisualContent(suppliers, ingredients, report);

        if (ingredients.Ingredients.Count < 22)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Error,
                "ING_BASELINE",
                "La línea base actual debe conservar al menos 22 ingredientes canónicos.");
        }
        else
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Info,
                "ING_COUNT",
                "Ingredientes de autoría disponibles: " + ingredients.Ingredients.Count + ".");
        }

        return report;
    }

    private static Dictionary<string, PackageOwner> BuildPackageIndex(
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSuppliers23B12ValidationReport report)
    {
        Dictionary<string, PackageOwner> result =
            new Dictionary<string, PackageOwner>(StringComparer.Ordinal);

        for (int ingredientIndex = 0;
             ingredientIndex < ingredients.Ingredients.Count;
             ingredientIndex++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient =
                ingredients.Ingredients[ingredientIndex];

            if (ingredient == null || !ingredient.isActive)
            {
                continue;
            }

            int activePackages = 0;
            if (ingredient.commercialPackages != null)
            {
                for (int packageIndex = 0;
                     packageIndex < ingredient.commercialPackages.Count;
                     packageIndex++)
                {
                    BistroBuilderCommercialPackageAuthoringRecord package =
                        ingredient.commercialPackages[packageIndex];

                    if (package == null)
                    {
                        report.Add(
                            BistroBuilderSuppliers23B12IssueSeverity.Error,
                            "PKG_NULL",
                            "Existe un formato comercial nulo.",
                            ingredient.IngredientId);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(package.PackageFormatId))
                    {
                        report.Add(
                            BistroBuilderSuppliers23B12IssueSeverity.Error,
                            "PKG_ID",
                            "Formato comercial sin PackageFormatId estable.",
                            ingredient.IngredientId);
                        continue;
                    }

                    if (result.ContainsKey(package.PackageFormatId))
                    {
                        report.Add(
                            BistroBuilderSuppliers23B12IssueSeverity.Error,
                            "PKG_DUP",
                            "PackageFormatId duplicado: " + package.PackageFormatId + ".",
                            ingredient.IngredientId);
                        continue;
                    }

                    if (package.netQuantityMicrounits <= 0)
                    {
                        report.Add(
                            BistroBuilderSuppliers23B12IssueSeverity.Error,
                            "PKG_QTY",
                            "La cantidad neta debe ser mayor que cero.",
                            package.PackageFormatId);
                    }

                    if (string.IsNullOrWhiteSpace(package.displayName) ||
                        string.IsNullOrWhiteSpace(package.packageType))
                    {
                        report.Add(
                            BistroBuilderSuppliers23B12IssueSeverity.Error,
                            "PKG_LABEL",
                            "El formato necesita nombre y tipo de envase.",
                            package.PackageFormatId);
                    }

                    result.Add(
                        package.PackageFormatId,
                        new PackageOwner
                        {
                            ingredient = ingredient,
                            package = package
                        });

                    if (package.isActive)
                    {
                        activePackages++;
                    }
                }
            }

            if (activePackages == 0)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Error,
                    "ING_NO_PACKAGE",
                    "El ingrediente no tiene ningún formato comercial activo.",
                    ingredient.IngredientId);
            }
            else if (activePackages < 2)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Warning,
                    "ING_ONE_PACKAGE",
                    "Solo existe un formato activo; es válido, pero reduce las alternativas comerciales.",
                    ingredient.IngredientId);
            }
        }

        return result;
    }

    private static void ValidateOffers(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        Dictionary<string, PackageOwner> packageIndex,
        BistroBuilderSuppliers23B12ValidationReport report)
    {
        HashSet<string> offerIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int supplierIndex = 0;
             supplierIndex < suppliers.Suppliers.Count;
             supplierIndex++)
        {
            BistroBuilderSupplierAuthoringRecord supplier =
                suppliers.Suppliers[supplierIndex];

            if (supplier == null)
            {
                continue;
            }

            HashSet<string> supplierPackages =
                new HashSet<string>(StringComparer.Ordinal);

            if (supplier.baseOffers == null)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Error,
                    "SUP_OFFERS_NULL",
                    "La colección de ofertas base es nula.",
                    supplier.SupplierId);
                continue;
            }

            for (int offerIndex = 0;
                 offerIndex < supplier.baseOffers.Count;
                 offerIndex++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                    supplier.baseOffers[offerIndex];

                if (offer == null)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_NULL",
                        "Existe una oferta base nula.",
                        supplier.SupplierId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(offer.SupplierOfferId) ||
                    !offerIds.Add(offer.SupplierOfferId))
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_ID",
                        "SupplierOfferId vacío o duplicado.",
                        supplier.SupplierId);
                }

                if (string.IsNullOrWhiteSpace(offer.packageFormatId) ||
                    !packageIndex.TryGetValue(
                        offer.packageFormatId,
                        out PackageOwner owner))
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_PACKAGE",
                        "La oferta referencia un PackageFormatId inexistente.",
                        offer.SupplierOfferId);
                    continue;
                }

                if (!supplierPackages.Add(offer.packageFormatId))
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_DUP_PACKAGE",
                        "El proveedor contiene dos ofertas para el mismo formato.",
                        offer.SupplierOfferId);
                }

                if (!string.Equals(
                        offer.ingredientId,
                        owner.ingredient.IngredientId,
                        StringComparison.Ordinal))
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_INGREDIENT",
                        "IngredientId de la oferta no coincide con el propietario del formato.",
                        offer.SupplierOfferId);
                }

                if (offer.isActive && !owner.package.isActive)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_INACTIVE_PACKAGE",
                        "Una oferta activa no puede apuntar a un formato desactivado.",
                        offer.SupplierOfferId);
                }

                if (offer.basePriceCents <= 0)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_PRICE",
                        "El precio base debe ser mayor que cero.",
                        offer.SupplierOfferId);
                }

                if (offer.minimumPackageCount <= 0 || offer.orderIncrement <= 0)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_ORDER_RULE",
                        "Mínimo e incremento deben ser mayores que cero.",
                        offer.SupplierOfferId);
                }

                if (offer.minimumMarketVariationPercent > 0f ||
                    offer.maximumMarketVariationPercent < 0f ||
                    offer.minimumMarketVariationPercent >
                    offer.maximumMarketVariationPercent)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_MARKET_RANGE",
                        "El rango futuro de mercado debe contener el precio base.",
                        offer.SupplierOfferId);
                }

                if (offer.overrideLeadTime && offer.leadTimeOverrideGameHours <= 0f)
                {
                    report.Add(
                        BistroBuilderSuppliers23B12IssueSeverity.Error,
                        "OFFER_LEAD",
                        "El plazo específico debe ser mayor que cero.",
                        offer.SupplierOfferId);
                }
            }
        }
    }

    private static void ValidateCompetition(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSuppliers23B12ValidationReport report)
    {
        for (int ingredientIndex = 0;
             ingredientIndex < ingredients.Ingredients.Count;
             ingredientIndex++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient =
                ingredients.Ingredients[ingredientIndex];

            if (ingredient == null || !ingredient.isActive)
            {
                continue;
            }

            int supplierCount =
                BistroBuilderSuppliers23B12ContentSeed.CountActiveOffersForIngredient(
                    suppliers,
                    ingredient.IngredientId);

            if (supplierCount < 2)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Error,
                    "ING_COMPETITION",
                    "Todo ingrediente comprable necesita al menos dos proveedores activos. Encontrados: " + supplierCount + ".",
                    ingredient.IngredientId);
            }
        }
    }

    private static void ValidateSupplierCoverage(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderSuppliers23B12ValidationReport report)
    {
        int activeSuppliers = 0;
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier == null || !supplier.isActive)
            {
                continue;
            }

            activeSuppliers++;
            int activeOffers = 0;
            if (supplier.baseOffers != null)
            {
                for (int offerIndex = 0; offerIndex < supplier.baseOffers.Count; offerIndex++)
                {
                    BistroBuilderSupplierBaseOfferAuthoringRecord offer =
                        supplier.baseOffers[offerIndex];
                    if (offer != null && offer.isActive)
                    {
                        activeOffers++;
                    }
                }
            }

            if (activeOffers == 0)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Error,
                    "SUP_EMPTY_CATALOG",
                    "Proveedor activo sin ofertas base.",
                    supplier.SupplierId);
            }
        }

        if (activeSuppliers < 6)
        {
            report.Add(
                BistroBuilderSuppliers23B12IssueSeverity.Warning,
                "SUP_BASELINE",
                "Hay menos de seis proveedores activos; la línea base provisional parte de seis.");
        }
    }

    private static void ValidateVisualContent(
        BistroBuilderSupplierAuthoringDatabase suppliers,
        BistroBuilderIngredientAuthoringDatabase ingredients,
        BistroBuilderSuppliers23B12ValidationReport report)
    {
        for (int index = 0; index < suppliers.Suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers.Suppliers[index];
            if (supplier != null && supplier.logo == null)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Warning,
                    "SUP_LOGO",
                    "Logo pendiente de producción/asignación.",
                    supplier.SupplierId);
            }
        }

        for (int index = 0; index < ingredients.Ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients.Ingredients[index];
            if (ingredient != null && ingredient.displayImage == null)
            {
                report.Add(
                    BistroBuilderSuppliers23B12IssueSeverity.Warning,
                    "ING_IMAGE",
                    "Imagen de ingrediente pendiente de producción/asignación.",
                    ingredient.IngredientId);
            }
        }
    }
}
#endif
