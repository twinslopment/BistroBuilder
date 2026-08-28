/// <summary>
/// Reglas puras que traducen un cobro de servicio a un movimiento financiero.
/// No consulta escena, comandas ni caja runtime.
/// </summary>
public static class BistroBuilderSalesRevenuePolicy
{
    public const string SourceSystemId = "service.payment";

    public static bool IsPayableServiceMode(BistroBuilderServiceMode mode)
    {
        return mode == BistroBuilderServiceMode.TableService ||
               mode == BistroBuilderServiceMode.BarService;
    }

    public static bool TryCalculateTablePaymentAmount(
        int canonicalOrderCents,
        int transferredBarCents,
        out long amountCents,
        out string error)
    {
        amountCents = 0L;

        if (canonicalOrderCents < 0)
        {
            error = "La comanda de mesa contiene un importe negativo.";
            return false;
        }

        if (transferredBarCents < 0)
        {
            error = "El cargo transferido desde barra no puede ser negativo.";
            return false;
        }

        amountCents = (long)canonicalOrderCents + transferredBarCents;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Aplica un ajuste comercial al cobro sin modificar el precio histórico
    /// de la comanda. Se limita para impedir importes negativos o explosivos.
    /// </summary>
    public static bool TryApplyPaymentAdjustment(
        long baseAmountCents,
        int adjustmentBasisPoints,
        out long adjustedAmountCents,
        out string error)
    {
        adjustedAmountCents = 0L;

        if (baseAmountCents < 0L)
        {
            error = "El importe base del cobro no puede ser negativo.";
            return false;
        }

        if (adjustmentBasisPoints < -9000 ||
            adjustmentBasisPoints > 50000)
        {
            error = "El ajuste comercial del cobro queda fuera de rango.";
            return false;
        }

        long multiplier = 10000L + adjustmentBasisPoints;
        long numerator = checked(baseAmountCents * multiplier);
        adjustedAmountCents = (numerator + 5000L) / 10000L;
        error = string.Empty;
        return true;
    }

    public static bool TryBuildRequest(
        string canonicalOrderId,
        BistroBuilderServiceMode serviceMode,
        BistroBuilderMealServiceAvailability mealService,
        long amountCents,
        int dayIndex,
        int minuteOfDay,
        out BistroBuilderFinanceTransactionRequest request,
        out string error)
    {
        request = null;
        string orderId = BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);

        if (!BistroBuilderOrderIdUtility.IsValid(orderId))
        {
            error = "El CanonicalOrderId del cobro no es válido.";
            return false;
        }

        if (!TryGetChannelId(serviceMode, out string channelId))
        {
            error = "La modalidad no representa un cobro final contabilizable.";
            return false;
        }

        if (!TryGetMealServiceId(mealService, out string mealServiceId))
        {
            error = "El cobro no pertenece a un servicio concreto válido.";
            return false;
        }

        if (amountCents <= 0L)
        {
            error = "El importe del cobro debe ser positivo.";
            return false;
        }

        if (dayIndex < 1 || minuteOfDay < 0 || minuteOfDay > 1439)
        {
            error = "La fecha de juego del cobro no es válida.";
            return false;
        }

        request = new BistroBuilderFinanceTransactionRequest
        {
            operationId = "sale_" + channelId + "_" + orderId,
            sourceSystemId = SourceSystemId,
            sourceReferenceId = orderId,
            categoryId = "sales." + mealServiceId + "." + channelId,
            kind = BistroBuilderFinanceTransactionKind.Credit,
            amountCents = amountCents,
            dayIndex = dayIndex,
            minuteOfDay = minuteOfDay,
            description = channelId == "table"
                ? "Cobro de mesa " + orderId
                : "Cobro en barra " + orderId
        };

        error = string.Empty;
        return true;
    }

    private static bool TryGetChannelId(
        BistroBuilderServiceMode serviceMode,
        out string channelId)
    {
        switch (serviceMode)
        {
            case BistroBuilderServiceMode.TableService:
                channelId = "table";
                return true;
            case BistroBuilderServiceMode.BarService:
                channelId = "bar";
                return true;
            default:
                channelId = string.Empty;
                return false;
        }
    }

    private static bool TryGetMealServiceId(
        BistroBuilderMealServiceAvailability mealService,
        out string mealServiceId)
    {
        switch (mealService)
        {
            case BistroBuilderMealServiceAvailability.Breakfast:
                mealServiceId = "breakfast";
                return true;
            case BistroBuilderMealServiceAvailability.Lunch:
                mealServiceId = "lunch";
                return true;
            case BistroBuilderMealServiceAvailability.Dinner:
                mealServiceId = "dinner";
                return true;
            default:
                mealServiceId = string.Empty;
                return false;
        }
    }
}
