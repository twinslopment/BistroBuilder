#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Capa de presentación humana para los editores de 2.3.
///
/// Nunca modifica los identificadores/enums canónicos: únicamente convierte
/// valores técnicos estables en texto amigable para biblioteca y previews.
/// </summary>
internal static class BistroBuilderSupplierAuthoringPresentation23A3
{
    private static readonly Dictionary<string, string> UnitLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Gram", "g" },
            { "Grams", "g" },
            { "G", "g" },
            { "Kilogram", "kg" },
            { "Kilograms", "kg" },
            { "Kg", "kg" },
            { "Milliliter", "ml" },
            { "Milliliters", "ml" },
            { "Ml", "ml" },
            { "Liter", "L" },
            { "Liters", "L" },
            { "Litre", "L" },
            { "Litres", "L" },
            { "L", "L" },
            { "Unit", "ud." },
            { "Units", "ud." },
            { "Piece", "ud." },
            { "Pieces", "ud." }
        };

    private static readonly Dictionary<string, string> CategoryLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Meat", "Carnes" },
            { "Produce", "Frutas y verduras" },
            { "DryGoods", "Productos secos" },
            { "DairyAndEggs", "Lácteos y huevos" },
            { "FishAndSeafood", "Pescados y mariscos" },
            { "Condiment", "Condimentos" },
            { "Condiments", "Condimentos" },
            { "Beverage", "Bebidas" },
            { "Beverages", "Bebidas" },
            { "PreparedProduct", "Productos preparados" },
            { "PreparedProducts", "Productos preparados" },
            { "Bakery", "Panadería" },
            { "Other", "Otros" },
            { "Others", "Otros" }
        };

    private static readonly Dictionary<string, string> TokenLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "None", "Sin clasificar" },
            { "Generalista", "Generalista" },
            { "FrutasYVerduras", "Frutas y verduras" },
            { "Carnes", "Carnes" },
            { "PescadosYMariscos", "Pescados y mariscos" },
            { "Lacteos", "Lácteos" },
            { "Panaderia", "Panadería" },
            { "Bebidas", "Bebidas" },
            { "Secos", "Productos secos" },
            { "AceitesYCondimentos", "Aceites y condimentos" },
            { "Otros", "Otros" },
            { "Mayorista", "Mayorista" },
            { "Especialista", "Especialista" },
            { "Express", "Express" },
            { "ProductorLocal", "Productor local" },
            { "Distribuidor", "Distribuidor" },
            { "Premium", "Premium" },
            { "Local", "Local" },
            { "Regional", "Regional" },
            { "Nacional", "Nacional" },
            { "Internacional", "Internacional" },
            { "Economico", "Económico" },
            { "Equilibrado", "Equilibrado" },
            { "Irregular", "Irregular" },
            { "Normal", "Normal" },
            { "Alta", "Alta" },
            { "Excelente", "Excelente" },
            { "Estable", "Estable" },
            { "Moderado", "Moderado" },
            { "Variable", "Variable" },
            { "MuyEstable", "Muy estable" },
            { "Estacional", "Estacional" },
            { "MuyBaja", "Muy baja" },
            { "Baja", "Baja" },
            { "Media", "Media" },
            { "Automatico", "Automático" },
            { "Furgoneta", "Furgoneta" },
            { "CamionLigero", "Camión ligero" },
            { "Pequeno", "Pequeño" },
            { "Medio", "Medio" },
            { "Grande", "Grande" },
            { "Ninguna", "Ninguna" },
            { "DiasAbierto", "Días abierto" },
            { "VolumenComprasCentimos", "Volumen de compras" },
            { "FacturacionCentimos", "Facturación" },
            { "Reputacion", "Reputación" },
            { "TamanoRestaurante", "Tamaño del restaurante" },
            { "CategoriaCulinaria", "Categoría culinaria" },
            { "ConsumoFamiliaIngrediente", "Consumo de familia de ingrediente" }
        };

    public static string Unit(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "—";
        }

        string trimmed = raw.Trim();
        return UnitLabels.TryGetValue(trimmed, out string label)
            ? label
            : HumanizeToken(trimmed);
    }

    public static string Category(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Sin categoría";
        }

        string trimmed = raw.Trim();
        return CategoryLabels.TryGetValue(trimmed, out string label)
            ? label
            : HumanizeToken(trimmed);
    }

    public static string Flags(Enum value)
    {
        return value == null ? "Sin clasificar" : Flags(value.ToString());
    }

    public static string Flags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Sin clasificar";
        }

        string[] parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder builder = new StringBuilder();

        for (int index = 0; index < parts.Length; index++)
        {
            string label = HumanizeToken(parts[index].Trim());
            if (string.Equals(label, "Sin clasificar", StringComparison.Ordinal) && parts.Length > 1)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(label);
        }

        return builder.Length == 0 ? "Sin clasificar" : builder.ToString();
    }

    public static string HumanizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "—";
        }

        string trimmed = token.Trim();
        if (TokenLabels.TryGetValue(trimmed, out string explicitLabel))
        {
            return explicitLabel;
        }

        // Fallback conservador para futuros enums CamelCase: separa palabras
        // sin alterar el valor almacenado ni pretender traducir términos desconocidos.
        StringBuilder builder = new StringBuilder(trimmed.Length + 8);
        for (int index = 0; index < trimmed.Length; index++)
        {
            char current = trimmed[index];
            if (index > 0 && char.IsUpper(current) && !char.IsWhiteSpace(trimmed[index - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    public static string IngredientSummary(BistroBuilderIngredientAuthoringRecord ingredient)
    {
        if (ingredient == null)
        {
            return "Ingrediente no disponible";
        }

        string visual = ingredient.displayImage == null ? " · Imagen pendiente" : string.Empty;
        return Unit(ingredient.canonicalUnitSnapshot) + " · " +
               Category(ingredient.categorySnapshot) + visual;
    }

    public static string SupplierVisualStatus(BistroBuilderSupplierAuthoringRecord supplier)
    {
        if (supplier == null)
        {
            return "Proveedor no disponible";
        }

        // Los productos/ofertas por proveedor se activan en 2.3B. Aquí no se
        // inventa un contador de SKU que todavía no forma parte de 2.3A.
        return supplier.logo == null
            ? "Logo pendiente · Catálogo en 2.3B"
            : "Logo asignado · Catálogo en 2.3B";
    }
}
#endif
