using System;
using System.Globalization;

/// <summary>
/// Formato visible de 3J. No altera valores ni redondea la contabilidad:
/// únicamente transforma céntimos/basis points ya autoritativos a texto.
/// </summary>
public static class BistroBuilderFinanceUiFormat
{
    private static readonly CultureInfo Spanish =
        CultureInfo.GetCultureInfo("es-ES");

    public static string Money(long cents, bool signed = false)
    {
        decimal euros = cents / 100m;
        string value = Math.Abs(euros).ToString("N2", Spanish) + " €";
        if (signed)
        {
            if (cents > 0L) return "+" + value;
            if (cents < 0L) return "−" + value;
            return "0,00 €";
        }
        return cents < 0L ? "−" + value : value;
    }

    public static string Percent(int basisPoints)
    {
        decimal percent = basisPoints / 100m;
        return percent.ToString("N2", Spanish) + " %";
    }

    public static string Ratio(int basisPoints)
    {
        if (basisPoints == int.MaxValue)
        {
            return "Sin vencimientos";
        }
        return Percent(basisPoints);
    }

    public static string Liquidity(BistroBuilderLiquidityStatus status)
    {
        switch (status)
        {
            case BistroBuilderLiquidityStatus.Healthy: return "Sana";
            case BistroBuilderLiquidityStatus.Watch: return "Vigilancia";
            case BistroBuilderLiquidityStatus.Tight: return "Ajustada";
            case BistroBuilderLiquidityStatus.Critical: return "Crítica";
            case BistroBuilderLiquidityStatus.Insolvent: return "Insolvente";
            default: return "Información incompleta";
        }
    }

    public static string Risk(BistroBuilderFinancialRiskLevel risk)
    {
        switch (risk)
        {
            case BistroBuilderFinancialRiskLevel.Low: return "Bajo";
            case BistroBuilderFinancialRiskLevel.Moderate: return "Moderado";
            case BistroBuilderFinancialRiskLevel.High: return "Alto";
            case BistroBuilderFinancialRiskLevel.Severe: return "Severo";
            default: return "Desconocido";
        }
    }

    public static string Trend(BistroBuilderFinancialTrendDirection trend)
    {
        switch (trend)
        {
            case BistroBuilderFinancialTrendDirection.Up: return "Sube";
            case BistroBuilderFinancialTrendDirection.Down: return "Baja";
            case BistroBuilderFinancialTrendDirection.Flat: return "Estable";
            default: return "Sin comparación";
        }
    }

    public static string MealService(BistroBuilderMealServiceAvailability service)
    {
        switch (service)
        {
            case BistroBuilderMealServiceAvailability.Breakfast: return "Desayuno";
            case BistroBuilderMealServiceAvailability.Lunch: return "Comida";
            case BistroBuilderMealServiceAvailability.Dinner: return "Cena";
            default: return "Sin servicio";
        }
    }

    public static string CostQuality(BistroBuilderFinancialResultCostQuality quality)
    {
        switch (quality)
        {
            case BistroBuilderFinancialResultCostQuality.Actual: return "Coste real";
            case BistroBuilderFinancialResultCostQuality.Mixed: return "Coste mixto";
            case BistroBuilderFinancialResultCostQuality.Estimated: return "Coste estimado";
            default: return "Sin coste reconocido";
        }
    }

    public static string LoanStatus(BistroBuilderLoanStatus status)
    {
        switch (status)
        {
            case BistroBuilderLoanStatus.Active: return "Al día";
            case BistroBuilderLoanStatus.Delinquent: return "Con retraso";
            case BistroBuilderLoanStatus.Defaulted: return "Impagado";
            case BistroBuilderLoanStatus.PaidOff: return "Liquidado";
            default: return status.ToString();
        }
    }

    public static string InstallmentStatus(BistroBuilderLoanInstallmentStatus status)
    {
        switch (status)
        {
            case BistroBuilderLoanInstallmentStatus.Pending: return "Pendiente";
            case BistroBuilderLoanInstallmentStatus.Overdue: return "Vencida";
            case BistroBuilderLoanInstallmentStatus.Paid: return "Pagada";
            default: return status.ToString();
        }
    }

    public static string Period(BistroBuilderFinanceDashboardPeriod period)
    {
        switch (period)
        {
            case BistroBuilderFinanceDashboardPeriod.Last30Days: return "30 DÍAS";
            case BistroBuilderFinanceDashboardPeriod.Last90Days: return "90 DÍAS";
            case BistroBuilderFinanceDashboardPeriod.AllTime: return "TODO";
            default: return "7 DÍAS";
        }
    }

    public static string Clock(int minuteOfDay)
    {
        int safe = Math.Max(0, Math.Min(1439, minuteOfDay));
        return (safe / 60).ToString("D2") + ":" +
               (safe % 60).ToString("D2");
    }
}
