using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderAuthoringValidationSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

[Serializable]
public sealed class BistroBuilderAuthoringValidationIssue
{
    public BistroBuilderAuthoringValidationSeverity severity;
    public string code;
    public string message;
    public string recordId;

    public BistroBuilderAuthoringValidationIssue(
        BistroBuilderAuthoringValidationSeverity severity,
        string code,
        string message,
        string recordId = null)
    {
        this.severity = severity;
        this.code = code ?? string.Empty;
        this.message = message ?? string.Empty;
        this.recordId = recordId ?? string.Empty;
    }
}

public sealed class BistroBuilderAuthoringValidationReport
{
    private readonly List<BistroBuilderAuthoringValidationIssue> issues =
        new List<BistroBuilderAuthoringValidationIssue>();

    public IReadOnlyList<BistroBuilderAuthoringValidationIssue> Issues => issues.AsReadOnly();

    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }
    public bool IsStructurallyValid => ErrorCount == 0;

    public void Add(BistroBuilderAuthoringValidationSeverity severity, string code, string message, string recordId = null)
    {
        issues.Add(new BistroBuilderAuthoringValidationIssue(severity, code, message, recordId));

        switch (severity)
        {
            case BistroBuilderAuthoringValidationSeverity.Error:
                ErrorCount++;
                break;
            case BistroBuilderAuthoringValidationSeverity.Warning:
                WarningCount++;
                break;
            default:
                InfoCount++;
                break;
        }
    }
}

/// <summary>
/// Validador puro de datos de autoría. No depende de UnityEditor y puede
/// reutilizarse posteriormente en CI, tests o herramientas de publicación.
/// </summary>
public static class BistroBuilderSupplierAuthoringValidator
{
    public static BistroBuilderAuthoringValidationReport Validate(
        BistroBuilderSupplierAuthoringDatabase supplierDatabase,
        BistroBuilderIngredientAuthoringDatabase ingredientDatabase)
    {
        BistroBuilderAuthoringValidationReport report =
            new BistroBuilderAuthoringValidationReport();

        ValidateSupplierDatabase(supplierDatabase, report);
        ValidateIngredientDatabase(ingredientDatabase, report);

        return report;
    }

    public static void ValidateSupplierDatabase(
        BistroBuilderSupplierAuthoringDatabase database,
        BistroBuilderAuthoringValidationReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (database == null)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_DB_MISSING", "No existe la base de datos maestra de proveedores.");
            return;
        }

        if (!string.Equals(database.SchemaId, BistroBuilderSupplierAuthoringDatabase.CurrentSchemaId, StringComparison.Ordinal) ||
            database.SchemaVersion != BistroBuilderSupplierAuthoringDatabase.CurrentSchemaVersion)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_SCHEMA", "El esquema de la base de datos de proveedores no coincide con 2.3A.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<BistroBuilderSupplierAuthoringRecord> suppliers = database.Suppliers;

        if (suppliers.Count < 6)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_SEED_COUNT", "Hay menos de seis proveedores de semilla. 2.3A está diseñado para arrancar con seis arquetipos provisionales.");
        }

        for (int index = 0; index < suppliers.Count; index++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = suppliers[index];
            if (supplier == null)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_NULL", "Existe un registro de proveedor nulo.");
                continue;
            }

            string id = supplier.SupplierId;
            if (string.IsNullOrWhiteSpace(id))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_ID", "Proveedor sin SupplierId estable.", id);
            }
            else if (!ids.Add(id))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_ID_DUP", "SupplierId duplicado: " + id + ".", id);
            }

            if (string.IsNullOrWhiteSpace(supplier.displayName))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_NAME", "Proveedor sin nombre comercial.", id);
            }

            if (supplier.logo == null)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_LOGO", "Falta el logo del proveedor. Debe asignarse desde Editor de Proveedores antes de publicar contenido final.", id);
            }

            if (supplier.catalogFlags == BistroBuilderSupplierCatalogFlags.None)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_CATALOG", "El proveedor no tiene ninguna categoría de catálogo.", id);
            }

            if (supplier.commercialModelFlags == BistroBuilderSupplierCommercialModelFlags.None)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_MODEL", "El proveedor no tiene modelo comercial.", id);
            }

            if (supplier.scopeFlags == BistroBuilderSupplierScopeFlags.None)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_SCOPE", "El proveedor no tiene alcance geográfico clasificado.", id);
            }

            if (supplier.positioningFlags == BistroBuilderSupplierPositioningFlags.None)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_POSITION", "El proveedor no tiene posicionamiento comercial clasificado.", id);
            }

            if (supplier.reliabilityValue < 0f || supplier.reliabilityValue > 1f)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_RELIABILITY", "Fiabilidad fuera del rango 0..1.", id);
            }

            if (supplier.minimumOrderValueCents < 0 || supplier.shippingCostCents < 0 || supplier.freeShippingThresholdCents < 0)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_MONEY", "Hay importes comerciales negativos.", id);
            }

            if (supplier.defaultLeadTimeGameHours <= 0f)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_LEAD", "El plazo normal de entrega debe ser mayor que cero.", id);
            }

            ValidateDeliveryWindows(supplier, report);
            ValidateProfiles(supplier, report);
        }
    }

    public static void ValidateIngredientDatabase(
        BistroBuilderIngredientAuthoringDatabase database,
        BistroBuilderAuthoringValidationReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (database == null)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "ING_DB_MISSING", "No existe la base visual/comercial de ingredientes.");
            return;
        }

        if (!string.Equals(database.SchemaId, BistroBuilderIngredientAuthoringDatabase.CurrentSchemaId, StringComparison.Ordinal) ||
            database.SchemaVersion != BistroBuilderIngredientAuthoringDatabase.CurrentSchemaVersion)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "ING_SCHEMA", "El esquema de la base visual/comercial de ingredientes no coincide con 2.3A.");
        }

        IReadOnlyList<BistroBuilderIngredientAuthoringRecord> ingredients = database.Ingredients;
        HashSet<string> ingredientIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> packageIds = new HashSet<string>(StringComparer.Ordinal);

        if (ingredients.Count == 0)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "ING_EMPTY", "Todavía no se han sincronizado ingredientes canónicos. Usa 'Sincronizar ingredientes canónicos' en Editor de Ingredientes.");
        }

        for (int index = 0; index < ingredients.Count; index++)
        {
            BistroBuilderIngredientAuthoringRecord ingredient = ingredients[index];
            if (ingredient == null)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "ING_NULL", "Existe un registro visual de ingrediente nulo.");
                continue;
            }

            string id = ingredient.IngredientId;
            if (string.IsNullOrWhiteSpace(id))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "ING_ID", "Ingrediente visual sin IngredientId canónico.");
            }
            else if (!ingredientIds.Add(id))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "ING_ID_DUP", "IngredientId duplicado en la capa de autoría: " + id + ".", id);
            }

            if (string.IsNullOrWhiteSpace(ingredient.displayNameSnapshot))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "ING_NAME", "No hay nombre canónico sincronizado.", id);
            }

            if (string.IsNullOrWhiteSpace(ingredient.canonicalUnitSnapshot))
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "ING_UNIT", "No hay unidad canónica sincronizada.", id);
            }

            if (ingredient.displayImage == null)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "ING_IMAGE", "Falta la imagen clara del ingrediente.", id);
            }

            for (int packageIndex = 0; packageIndex < ingredient.commercialPackages.Count; packageIndex++)
            {
                BistroBuilderCommercialPackageAuthoringRecord package = ingredient.commercialPackages[packageIndex];
                if (package == null)
                {
                    report.Add(BistroBuilderAuthoringValidationSeverity.Error, "PKG_NULL", "Existe un formato comercial nulo.", id);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(package.PackageFormatId))
                {
                    report.Add(BistroBuilderAuthoringValidationSeverity.Error, "PKG_ID", "Formato comercial sin PackageFormatId estable.", id);
                }
                else if (!packageIds.Add(package.PackageFormatId))
                {
                    report.Add(BistroBuilderAuthoringValidationSeverity.Error, "PKG_ID_DUP", "PackageFormatId duplicado: " + package.PackageFormatId + ".", id);
                }

                if (package.netQuantityMicrounits <= 0)
                {
                    report.Add(BistroBuilderAuthoringValidationSeverity.Error, "PKG_QTY", "La cantidad neta del formato debe ser mayor que cero.", id);
                }

                if (string.IsNullOrWhiteSpace(package.displayName))
                {
                    report.Add(BistroBuilderAuthoringValidationSeverity.Error, "PKG_NAME", "Formato comercial sin nombre.", id);
                }
            }
        }
    }

    private static void ValidateDeliveryWindows(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderAuthoringValidationReport report)
    {
        if (supplier.deliveryWindows == null || supplier.deliveryWindows.Count == 0)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_WINDOWS", "El proveedor no tiene ventanas de entrega definidas.", supplier.SupplierId);
            return;
        }

        for (int index = 0; index < supplier.deliveryWindows.Count; index++)
        {
            BistroBuilderSupplierDeliveryWindowAuthoring window = supplier.deliveryWindows[index];
            if (window == null)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_WINDOW_NULL", "Ventana de entrega nula.", supplier.SupplierId);
                continue;
            }

            if (window.startMinuteOfDay < 0 || window.endMinuteOfDay > 24 * 60 || window.endMinuteOfDay <= window.startMinuteOfDay)
            {
                report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_WINDOW_RANGE", "Ventana de entrega inválida.", supplier.SupplierId);
            }
        }
    }

    private static void ValidateProfiles(
        BistroBuilderSupplierAuthoringRecord supplier,
        BistroBuilderAuthoringValidationReport report)
    {
        if (supplier.promotionProfile == null || supplier.priceEvolutionProfile == null || supplier.availabilityProfile == null || supplier.logisticsProfile == null || supplier.unlockProfile == null)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_PROFILE_NULL", "Falta al menos un perfil estructural de proveedor.", supplier.SupplierId);
            return;
        }

        if (supplier.promotionProfile.minimumDiscountPercent > supplier.promotionProfile.maximumDiscountPercent)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_PROMO_RANGE", "El descuento mínimo supera al máximo.", supplier.SupplierId);
        }

        if (supplier.promotionProfile.minimumDurationDays > supplier.promotionProfile.maximumDurationDays)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_PROMO_DURATION", "La duración mínima de promoción supera a la máxima.", supplier.SupplierId);
        }

        if (supplier.priceEvolutionProfile.reviewEveryGameDays != 5)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Warning, "SUP_REVIEW_CYCLE", "El diseño actual de 2.3 fija la revisión comercial inicial cada 5 días.", supplier.SupplierId);
        }

        if (supplier.priceEvolutionProfile.minimumVariationPercent > 0f || supplier.priceEvolutionProfile.maximumVariationPercent < 0f)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_PRICE_RANGE", "El rango de variación de precio no contiene el precio base.", supplier.SupplierId);
        }

        if (supplier.logisticsProfile.maximumDelayMinutes < supplier.logisticsProfile.minimumDelayMinutes)
        {
            report.Add(BistroBuilderAuthoringValidationSeverity.Error, "SUP_DELAY_RANGE", "El retraso máximo es menor que el mínimo.", supplier.SupplierId);
        }
    }
}
