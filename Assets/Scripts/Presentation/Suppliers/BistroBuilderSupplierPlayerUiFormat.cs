using System;
using System.Globalization;
using System.Text;

/// <summary>Formateadores puros para la UI jugable 2.3K.</summary>
public static class BistroBuilderSupplierPlayerUiFormat
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public static string Money(long cents)
    {
        return (Math.Max(0L, cents) / 100m).ToString("N2", Es) + " €";
    }

    public static string NormalizedPrice(
        long cents,
        long netQuantityMicrounits,
        string canonicalUnit)
    {
        if (cents <= 0L || netQuantityMicrounits <= 0L) return "—";
        decimal baseUnits = netQuantityMicrounits / 1000000m;
        if (baseUnits <= 0m) return "—";

        string unit = canonicalUnit ?? string.Empty;
        string lower = unit.Trim().ToLowerInvariant();
        decimal euros = cents / 100m;
        decimal normalized;
        string suffix;
        if (lower.Contains("gram") || lower == "g")
        {
            normalized = euros / (baseUnits / 1000m);
            suffix = "€/kg";
        }
        else if (lower.Contains("millil") || lower == "ml")
        {
            normalized = euros / (baseUnits / 1000m);
            suffix = "€/L";
        }
        else
        {
            normalized = euros / baseUnits;
            suffix = lower.Contains("unit") || lower.Contains("pieza") || lower.Contains("unidad")
                ? "€/ud"
                : "€/" + (string.IsNullOrWhiteSpace(unit) ? "unidad" : unit);
        }

        if (normalized < 0m || normalized > 1000000000m) return "—";
        return normalized.ToString("N2", Es) + " " + suffix;
    }


    /// <summary>
    /// Convierte identificadores/enum técnicos de autoría a texto legible.
    /// Ejemplos: FrutasYVerduras -> Frutas y verduras; StockLimitado -> Stock limitado.
    /// No altera IDs persistidos: solo Presentation.
    /// </summary>
    public static string HumanizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";

        string source = value.Trim();
        StringBuilder spaced = new StringBuilder(source.Length + 8);
        char previous = '\0';
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (current == '_' || current == '-')
            {
                if (spaced.Length > 0 && spaced[spaced.Length - 1] != ' ') spaced.Append(' ');
                previous = current;
                continue;
            }

            bool boundary = index > 0 && char.IsUpper(current) &&
                            (char.IsLower(previous) ||
                             char.IsDigit(previous) ||
                             previous == 'Y');
            if (boundary && spaced.Length > 0 && spaced[spaced.Length - 1] != ' ') spaced.Append(' ');
            spaced.Append(current);
            previous = current;
        }

        string normalized = CollapseSpaces(spaced.ToString()).ToLower(Es);
        if (normalized.Length == 0) return "—";

        // Conjunciones técnicas como "Y" quedan naturalmente en minúscula al
        // normalizar toda la frase. Solo se capitaliza la primera letra.
        return char.ToUpper(normalized[0], Es) + normalized.Substring(1);
    }

    public static string HumanizeFlagsText(string rawFlags)
    {
        if (string.IsNullOrWhiteSpace(rawFlags) ||
            string.Equals(rawFlags.Trim(), "None", StringComparison.OrdinalIgnoreCase))
        {
            return "Ninguno";
        }

        string[] parts = rawFlags.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder result = new StringBuilder();
        for (int index = 0; index < parts.Length; index++)
        {
            string human = HumanizeIdentifier(parts[index]);
            if (string.IsNullOrWhiteSpace(human) || human == "—") continue;
            if (result.Length > 0) result.Append(", ");
            result.Append(human);
        }
        return result.Length > 0 ? result.ToString() : "Ninguno";
    }

    private static string CollapseSpaces(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        StringBuilder result = new StringBuilder(value.Length);
        bool previousSpace = false;
        for (int index = 0; index < value.Length; index++)
        {
            char c = value[index];
            bool space = char.IsWhiteSpace(c);
            if (space)
            {
                if (!previousSpace && result.Length > 0) result.Append(' ');
            }
            else
            {
                result.Append(c);
            }
            previousSpace = space;
        }
        return result.ToString().Trim();
    }

    public static string Availability(BistroBuilderSupplierOfferAvailability value)
    {
        switch (value)
        {
            case BistroBuilderSupplierOfferAvailability.StockLimitado:
                return "Stock limitado";
            case BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado:
                return "Temporalmente agotado";
            default:
                return "Disponible";
        }
    }

    public static string Reliability(BistroBuilderSupplierReliabilityTier value)
    {
        switch (value)
        {
            case BistroBuilderSupplierReliabilityTier.Excelente: return "Excelente";
            case BistroBuilderSupplierReliabilityTier.Alta: return "Alta";
            case BistroBuilderSupplierReliabilityTier.Normal: return "Normal";
            default: return "Irregular";
        }
    }

    public static string OrderStatus(BistroBuilderPurchaseOrderStatus status)
    {
        switch (status)
        {
            case BistroBuilderPurchaseOrderStatus.Draft: return "Borrador";
            case BistroBuilderPurchaseOrderStatus.Confirmed: return "Confirmado";
            case BistroBuilderPurchaseOrderStatus.PendingDelivery: return "Pendiente de entrega";
            case BistroBuilderPurchaseOrderStatus.InDelivery: return "En reparto";
            case BistroBuilderPurchaseOrderStatus.Delivered: return "Entregado";
            case BistroBuilderPurchaseOrderStatus.Cancelled: return "Cancelado";
            default: return status.ToString();
        }
    }

    public static string LogisticsStatus(BistroBuilderSupplierLogisticsPlanStatus status)
    {
        switch (status)
        {
            case BistroBuilderSupplierLogisticsPlanStatus.Planned: return "Planificado";
            case BistroBuilderSupplierLogisticsPlanStatus.DelayApplied: return "Retraso aplicado";
            case BistroBuilderSupplierLogisticsPlanStatus.ReadyForDispatch: return "Listo para salir";
            case BistroBuilderSupplierLogisticsPlanStatus.Dispatched: return "Despachado";
            case BistroBuilderSupplierLogisticsPlanStatus.Delivered: return "Entregado";
            case BistroBuilderSupplierLogisticsPlanStatus.Cancelled: return "Cancelado";
            default: return status.ToString();
        }
    }

    public static string Risk(BistroBuilderSmartPurchaseRisk risk)
    {
        switch (risk)
        {
            case BistroBuilderSmartPurchaseRisk.Critico: return "Crítico";
            case BistroBuilderSmartPurchaseRisk.Alto: return "Alto";
            case BistroBuilderSmartPurchaseRisk.Medio: return "Medio";
            case BistroBuilderSmartPurchaseRisk.Bajo: return "Bajo";
            default: return "Sin riesgo";
        }
    }

    public static string Strategy(BistroBuilderSmartPurchaseStrategy strategy)
    {
        switch (strategy)
        {
            case BistroBuilderSmartPurchaseStrategy.Ahorrar: return "Ahorrar";
            case BistroBuilderSmartPurchaseStrategy.Urgente: return "Urgente";
            default: return "Equilibrado";
        }
    }

    public static string Percent01(float value)
    {
        float clamped = Math.Max(0f, Math.Min(1f, value));
        return (clamped * 100f).ToString("0", Es) + "%";
    }

    public static string Hours(float gameHours)
    {
        if (gameHours < 24f)
        {
            return Math.Max(0.1f, gameHours).ToString("0.#", Es) + " h";
        }
        return (gameHours / 24f).ToString("0.#", Es) + " días";
    }

    public static string DayWindow(int gameDay, int startMinute, int endMinute)
    {
        return "Día " + gameDay + " · " + MinuteOfDay(startMinute) + "–" + MinuteOfDay(endMinute);
    }

    private static string MinuteOfDay(int minute)
    {
        int safe = Math.Max(0, Math.Min(1439, minute));
        return (safe / 60).ToString("00") + ":" + (safe % 60).ToString("00");
    }
}
