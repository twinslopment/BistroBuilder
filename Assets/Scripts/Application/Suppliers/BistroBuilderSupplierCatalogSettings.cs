using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Asset canónico de Proveedores 2.3A1.
///
/// A diferencia de 2.3A v1, supplier.catalog v2 persiste también el catálogo
/// de productos de proveedor. Esto permite precios, formatos, disponibilidad,
/// mínimos y plazos específicos por producto sin volver a modelar el dominio
/// al llegar 2.3B/2.3D.
/// </summary>
[CreateAssetMenu(
    fileName = "BistroBuilderSupplierCatalogSettings",
    menuName = "Bistro Builder/Suppliers/Canonical Supplier Catalog Settings")]
public sealed class BistroBuilderSupplierCatalogSettings : ScriptableObject
{
    public const int CurrentSchemaVersion = 2;
    public const string ResourcesPath =
        "BistroBuilder/Suppliers/BistroBuilderSupplierCatalogSettings";

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private List<BistroBuilderSupplierDefinition> suppliers =
        new List<BistroBuilderSupplierDefinition>();

    [SerializeField]
    private List<BistroBuilderSupplierProductDefinition> products =
        new List<BistroBuilderSupplierProductDefinition>();

    [NonSerialized]
    private ReadOnlyCollection<BistroBuilderSupplierDefinition> suppliersView;

    [NonSerialized]
    private ReadOnlyCollection<BistroBuilderSupplierProductDefinition> productsView;

    public int SchemaVersion => schemaVersion;

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

    /// <summary>
    /// Migra supplier.catalog y garantiza los proveedores/productos base
    /// ausentes. Nunca sobrescribe un producto ya existente con el mismo
    /// ProductId, por lo que afinaciones de precio/formato se conservan.
    /// </summary>
    public bool TryEnsureCanonicalDefaults(
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients,
        out bool changed,
        out string error)
    {
        changed = false;
        error = string.Empty;
        EnsureCollections();

        if (schemaVersion > CurrentSchemaVersion)
        {
            error =
                "supplier.catalog usa schema v" + schemaVersion +
                ", superior al soportado v" + CurrentSchemaVersion + ".";
            return false;
        }

        if (ingredients == null || ingredients.Count == 0)
        {
            error =
                "No se puede completar supplier.catalog sin ingredientes canónicos.";
            return false;
        }

        if (schemaVersion <= 1)
        {
            for (int i = 0; i < suppliers.Count; i++)
            {
                if (suppliers[i] != null)
                {
                    suppliers[i].MigrateLegacyEconomyFields();
                }
            }
            changed = true;
        }

        List<BistroBuilderSupplierDefinition> defaults =
            BistroBuilderSupplierCatalogDefaults.CreateDefaultSuppliers();
        HashSet<string> supplierIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < suppliers.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = suppliers[i];
            if (supplier != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(supplier.SupplierId))
            {
                supplierIds.Add(supplier.SupplierId);
            }
        }

        for (int i = 0; i < defaults.Count; i++)
        {
            BistroBuilderSupplierDefinition supplier = defaults[i];
            if (!supplierIds.Contains(supplier.SupplierId))
            {
                suppliers.Add(supplier);
                supplierIds.Add(supplier.SupplierId);
                changed = true;
            }
        }

        List<BistroBuilderSupplierProductDefinition> defaultProducts =
            BistroBuilderSupplierCatalogBuilder.BuildProducts(
                suppliers,
                ingredients);

        HashSet<string> productIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < products.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = products[i];
            if (product != null &&
                BistroBuilderMenuIdUtility.IsValidStableId(product.ProductId))
            {
                productIds.Add(product.ProductId);
            }
        }

        for (int i = 0; i < defaultProducts.Count; i++)
        {
            BistroBuilderSupplierProductDefinition product = defaultProducts[i];
            if (!productIds.Contains(product.ProductId))
            {
                products.Add(product);
                productIds.Add(product.ProductId);
                changed = true;
            }
        }

        if (SortSuppliersIfNeeded()) changed = true;
        if (SortProductsIfNeeded()) changed = true;

        if (schemaVersion != CurrentSchemaVersion)
        {
            schemaVersion = CurrentSchemaVersion;
            changed = true;
        }

        if (changed)
        {
            suppliersView = null;
            productsView = null;
        }

        return true;
    }

    /// <summary>
    /// Crea desde cero los cuatro proveedores y las dos ofertas base por
    /// ingrediente. Solo debe usarse al crear el asset o en pruebas aisladas.
    /// </summary>
    public bool ResetToCanonicalDefaults(
        IReadOnlyList<BistroBuilderSupplierIngredientDescriptor> ingredients,
        out string error)
    {
        error = string.Empty;
        if (ingredients == null || ingredients.Count == 0)
        {
            error = "No existen ingredientes para crear supplier.catalog.";
            return false;
        }

        schemaVersion = CurrentSchemaVersion;
        suppliers = BistroBuilderSupplierCatalogDefaults.CreateDefaultSuppliers();
        products = BistroBuilderSupplierCatalogBuilder.BuildProducts(
            suppliers,
            ingredients);

        // Reset debe entregar ya el mismo estado normalizado que espera
        // TryEnsureCanonicalDefaults. Si dejásemos la semilla en orden de
        // autoría, el primer Ensure posterior tendría que reordenarla y
        // reportaría un cambio fantasma, rompiendo la idempotencia del
        // contrato Reset -> Ensure.
        SortSuppliersIfNeeded();
        SortProductsIfNeeded();

        suppliersView = null;
        productsView = null;
        return products.Count > 0;
    }

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
    }

    private bool SortSuppliersIfNeeded()
    {
        for (int i = 1; i < suppliers.Count; i++)
        {
            string previous = suppliers[i - 1] != null
                ? suppliers[i - 1].SupplierId
                : string.Empty;
            string current = suppliers[i] != null
                ? suppliers[i].SupplierId
                : string.Empty;
            if (string.Compare(previous, current, StringComparison.Ordinal) > 0)
            {
                suppliers.Sort((a, b) => string.Compare(
                    a != null ? a.SupplierId : string.Empty,
                    b != null ? b.SupplierId : string.Empty,
                    StringComparison.Ordinal));
                return true;
            }
        }
        return false;
    }

    private bool SortProductsIfNeeded()
    {
        for (int i = 1; i < products.Count; i++)
        {
            string previous = products[i - 1] != null
                ? products[i - 1].ProductId
                : string.Empty;
            string current = products[i] != null
                ? products[i].ProductId
                : string.Empty;
            if (string.Compare(previous, current, StringComparison.Ordinal) > 0)
            {
                products.Sort((a, b) => string.Compare(
                    a != null ? a.ProductId : string.Empty,
                    b != null ? b.ProductId : string.Empty,
                    StringComparison.Ordinal));
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Semillas iniciales de proveedores. Los productos concretos quedan
/// persistidos en supplier.catalog v2 después del instalador.
/// </summary>
public static class BistroBuilderSupplierCatalogDefaults
{
    public const string GeneralSupplierId = "supplier_hosteleria_total";
    public const string FreshSupplierId = "supplier_norte_frescos";
    public const string PantrySupplierId = "supplier_despensa_profesional";
    public const string PremiumSupplierId = "supplier_cantabrico_seleccion";

    public static List<BistroBuilderSupplierDefinition> CreateDefaultSuppliers()
    {
        return new List<BistroBuilderSupplierDefinition>
        {
            new BistroBuilderSupplierDefinition(
                GeneralSupplierId,
                "Hostelería Total",
                "Proveedor generalista con cobertura amplia y condiciones equilibradas.",
                true,
                5000,
                2,
                "EUR",
                10000),

            new BistroBuilderSupplierDefinition(
                FreshSupplierId,
                "Norte Frescos",
                "Especialista en producto fresco con entrega rápida.",
                true,
                6000,
                1,
                "EUR",
                10600),

            new BistroBuilderSupplierDefinition(
                PantrySupplierId,
                "Despensa Profesional",
                "Especialista en secos, básicos y formatos de hostelería.",
                true,
                4000,
                2,
                "EUR",
                9400),

            new BistroBuilderSupplierDefinition(
                PremiumSupplierId,
                "Cantábrico Selección",
                "Proveedor de producto seleccionado con precio superior y plazo corto.",
                true,
                8000,
                1,
                "EUR",
                11400)
        };
    }
}
