using System;
using System.Globalization;

/// <summary>
/// Funciones puras de presentación para 2.1E. Se mantienen fuera de la vista
/// para poder probar formato monetario, búsqueda y filtros sin levantar UI.
/// </summary>
public static class BistroBuilderMenuEditorUtility
{
    private static readonly CultureInfo SpanishCulture =
        CultureInfo.GetCultureInfo("es-ES");

    public static string FormatMoney(int cents)
    {
        decimal value = cents / 100m;
        return value.ToString("N2", SpanishCulture) + " €";
    }

    public static string FormatEditableMoney(int cents)
    {
        decimal value = cents / 100m;
        return value.ToString("0.00", SpanishCulture);
    }

    public static bool TryParseMoney(
        string text,
        out int cents,
        out string error
    )
    {
        cents = 0;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Introduce un precio.";
            return false;
        }

        string trimmed = text.Trim()
            .Replace("€", string.Empty)
            .Trim();

        string normalized = NormalizeDecimalText(trimmed);
        bool parsed = decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out decimal value
        );

        if (!parsed || value < 0m)
        {
            error = "El precio no tiene un formato válido.";
            return false;
        }

        decimal minorUnits = decimal.Round(
            value * 100m,
            0,
            MidpointRounding.AwayFromZero
        );

        if (minorUnits > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "El precio supera el máximo permitido.";
            return false;
        }

        cents = (int)minorUnits;
        return true;
    }

    public static bool Matches(
        BistroBuilderMenuEditorDishSnapshot item,
        string categoryId,
        BistroBuilderMenuEditorFilter filter,
        string searchText
    )
    {
        if (item == null)
        {
            return false;
        }

        string normalizedCategory =
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);

        if (!string.IsNullOrEmpty(normalizedCategory) &&
            !string.Equals(
                item.CategoryId,
                normalizedCategory,
                StringComparison.Ordinal
            ))
        {
            return false;
        }

        switch (filter)
        {
            case BistroBuilderMenuEditorFilter.Included:
                if (!item.Included)
                {
                    return false;
                }
                break;

            case BistroBuilderMenuEditorFilter.Active:
                if (!item.Included || !item.Enabled || !item.Unlocked)
                {
                    return false;
                }
                break;

            case BistroBuilderMenuEditorFilter.Signature:
                if (!item.Included || !item.SignatureDish)
                {
                    return false;
                }
                break;

            case BistroBuilderMenuEditorFilter.NeedsAttention:
                if (!item.NeedsAttention)
                {
                    return false;
                }
                break;
        }

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string search = searchText.Trim();
        return ContainsIgnoreCase(item.DisplayName, search) ||
               ContainsIgnoreCase(item.DishId, search) ||
               ContainsIgnoreCase(item.Description, search) ||
               ContainsIgnoreCase(item.CategoryName, search);
    }

    public static string GetMealServiceLabel(
        BistroBuilderMealServiceAvailability service
    )
    {
        switch (service)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                return "Desayuno";
            case BistroBuilderMealServiceAvailability.Lunch:
                return "Comida";
            case BistroBuilderMealServiceAvailability.Dinner:
                return "Cena";
            default:
                return "Servicio";
        }
    }

    public static string GetServiceModeLabel(BistroBuilderServiceMode mode)
    {
        switch (mode)
        {
            case BistroBuilderServiceMode.TableService:
                return "Mesa";
            case BistroBuilderServiceMode.BarService:
                return "Barra";
            case BistroBuilderServiceMode.WaitingAtBar:
                return "Espera en barra";
            default:
                return "Modalidad";
        }
    }

    public static string GetServicesLabel(
        BistroBuilderMealServiceAvailability availability
    )
    {
        if (availability == BistroBuilderMealServiceAvailability.None)
        {
            return "Sin servicio";
        }

        if (availability == BistroBuilderMealServiceAvailability.All)
        {
            return "Desayuno · Comida · Cena";
        }

        string result = string.Empty;
        AppendService(
            ref result,
            availability,
            BistroBuilderMealServiceAvailability.Breakfast,
            "Desayuno"
        );
        AppendService(
            ref result,
            availability,
            BistroBuilderMealServiceAvailability.Lunch,
            "Comida"
        );
        AppendService(
            ref result,
            availability,
            BistroBuilderMealServiceAvailability.Dinner,
            "Cena"
        );
        return result;
    }


    private static string NormalizeDecimalText(string value)
    {
        string normalized = value
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty);
        int comma = normalized.LastIndexOf(',');
        int dot = normalized.LastIndexOf('.');

        if (comma >= 0 && dot >= 0)
        {
            if (comma > dot)
            {
                normalized = normalized.Replace(".", string.Empty);
                normalized = normalized.Replace(',', '.');
            }
            else
            {
                normalized = normalized.Replace(",", string.Empty);
            }

            return normalized;
        }

        return comma >= 0 ? normalized.Replace(',', '.') : normalized;
    }

    private static void AppendService(
        ref string result,
        BistroBuilderMealServiceAvailability availability,
        BistroBuilderMealServiceAvailability flag,
        string label
    )
    {
        if ((availability & flag) == 0)
        {
            return;
        }

        if (!string.IsNullOrEmpty(result))
        {
            result += " · ";
        }

        result += label;
    }

    private static bool ContainsIgnoreCase(string value, string search)
    {
        return !string.IsNullOrEmpty(value) &&
               value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
