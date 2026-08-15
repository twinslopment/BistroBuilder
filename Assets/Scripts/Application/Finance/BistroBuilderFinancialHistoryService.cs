using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fachada de lectura de 3H para históricos, indicadores y comparativas.
///
/// No persiste resúmenes: reconstruye cada informe desde 3G, que a su vez lee
/// las autoridades canónicas 3A/3D. Las consultas por rango usan una única
/// captura de snapshots para evitar trabajo multiplicado por cada día.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Financial History Service")]
public sealed class BistroBuilderFinancialHistoryService : MonoBehaviour
{
    private const int MaximumDaysPerReport = 100000;

    [SerializeField] private BistroBuilderFinancialResultsService financialResultsService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;

    private readonly List<BistroBuilderDayFinancialResult> dayBuffer =
        new List<BistroBuilderDayFinancialResult>(64);

    public event Action HistoryChanged;

    public BistroBuilderFinancialResultsService FinancialResultsService =>
        financialResultsService;
    public BistroBuilderGeneralGameStateService GeneralGameStateService =>
        generalGameStateService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnEnable()
    {
        CacheDependenciesIfNeeded();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        dayBuffer.Clear();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (financialResultsService == null ||
            generalGameStateService == null)
        {
            error = "3H necesita Resultados 3G y el calendario canónico.";
            return false;
        }

        if (!financialResultsService.ValidateConfiguration(out error) ||
            !generalGameStateService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (!ReferenceEquals(
                financialResultsService.GeneralGameStateService,
                generalGameStateService))
        {
            error = "3H y 3G no comparten el mismo calendario canónico.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public bool TryGetPeriodReport(
        int startDayIndex,
        int endDayIndex,
        out BistroBuilderFinancialPeriodReport report,
        out string error)
    {
        report = null;

        if (!ValidateRange(startDayIndex, endDayIndex, out error))
        {
            return false;
        }

        dayBuffer.Clear();
        if (!financialResultsService.TryGetDayResults(
                startDayIndex,
                endDayIndex,
                dayBuffer,
                out error))
        {
            dayBuffer.Clear();
            return false;
        }

        bool built = BistroBuilderFinancialHistoryEngine.TryBuildPeriodReport(
            dayBuffer,
            startDayIndex,
            endDayIndex,
            out report,
            out error);
        dayBuffer.Clear();
        return built;
    }

    public bool TryGetCurrentRollingReport(
        int requestedDayCount,
        out BistroBuilderFinancialPeriodReport report,
        out string error)
    {
        report = null;

        if (generalGameStateService == null || requestedDayCount < 1)
        {
            error = "La ventana móvil debe contener al menos un día.";
            return false;
        }

        int endDay = generalGameStateService.DayIndex;
        long startLong = (long)endDay - requestedDayCount + 1L;
        int startDay = startLong < 1L ? 1 : (int)startLong;

        return TryGetPeriodReport(
            startDay,
            endDay,
            out report,
            out error);
    }

    /// <summary>
    /// Ventana de días ya completados. Excluye el día actual para que una
    /// jornada recién iniciada con resultado 0 no borre artificialmente una
    /// racha de pérdidas usada por 3I.
    /// </summary>
    public bool TryGetCompletedRollingReport(
        int requestedDayCount,
        out BistroBuilderFinancialPeriodReport report,
        out string error)
    {
        report = null;
        if (generalGameStateService == null || requestedDayCount < 1)
        {
            error = "La ventana completada debe contener al menos un día.";
            return false;
        }

        int endDay = generalGameStateService.DayIndex - 1;
        if (endDay < 1)
        {
            error = "Todavía no existe ningún día completado.";
            return false;
        }

        long startLong = (long)endDay - requestedDayCount + 1L;
        int startDay = startLong < 1L ? 1 : (int)startLong;
        return TryGetPeriodReport(startDay, endDay, out report, out error);
    }

    public bool TryComparePeriods(
        int previousStartDayIndex,
        int previousEndDayIndex,
        int currentStartDayIndex,
        int currentEndDayIndex,
        out BistroBuilderFinancialPeriodComparison comparison,
        out string error)
    {
        comparison = null;

        long previousLength =
            (long)previousEndDayIndex - previousStartDayIndex + 1L;
        long currentLength =
            (long)currentEndDayIndex - currentStartDayIndex + 1L;

        if (previousLength <= 0L || currentLength <= 0L ||
            previousLength != currentLength)
        {
            error = "3H solo compara periodos de igual duración.";
            return false;
        }

        if (!TryGetPeriodReport(
                previousStartDayIndex,
                previousEndDayIndex,
                out BistroBuilderFinancialPeriodReport previous,
                out error) ||
            !TryGetPeriodReport(
                currentStartDayIndex,
                currentEndDayIndex,
                out BistroBuilderFinancialPeriodReport current,
                out error))
        {
            return false;
        }

        return BistroBuilderFinancialHistoryEngine.TryBuildComparison(
            previous,
            current,
            out comparison,
            out error);
    }

    public bool TryCompareWithPreviousPeriod(
        int currentStartDayIndex,
        int currentEndDayIndex,
        out BistroBuilderFinancialPeriodComparison comparison,
        out string error)
    {
        comparison = null;

        if (currentStartDayIndex < 1 ||
            currentEndDayIndex < currentStartDayIndex)
        {
            error = "El periodo actual no es válido.";
            return false;
        }

        long length =
            (long)currentEndDayIndex - currentStartDayIndex + 1L;
        long previousEnd = (long)currentStartDayIndex - 1L;
        long previousStart = previousEnd - length + 1L;

        if (previousStart < 1L)
        {
            error = "No existe un periodo anterior completo de igual duración.";
            return false;
        }

        return TryComparePeriods(
            (int)previousStart,
            (int)previousEnd,
            currentStartDayIndex,
            currentEndDayIndex,
            out comparison,
            out error);
    }

    private bool ValidateRange(
        int startDayIndex,
        int endDayIndex,
        out string error)
    {
        if (financialResultsService == null || generalGameStateService == null)
        {
            error = "3H no tiene sus dependencias disponibles.";
            return false;
        }

        if (startDayIndex < 1 || endDayIndex < startDayIndex)
        {
            error = "El intervalo histórico no es válido.";
            return false;
        }

        if (endDayIndex > generalGameStateService.DayIndex)
        {
            error = "3H no puede informar sobre días futuros.";
            return false;
        }

        long length = (long)endDayIndex - startDayIndex + 1L;
        if (length > MaximumDaysPerReport)
        {
            error = "El informe solicitado supera el máximo de días por consulta.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void HandleResultsChanged()
    {
        HistoryChanged?.Invoke();
    }

    private void Subscribe()
    {
        if (financialResultsService != null)
        {
            financialResultsService.ResultsChanged -= HandleResultsChanged;
            financialResultsService.ResultsChanged += HandleResultsChanged;
        }
    }

    private void Unsubscribe()
    {
        if (financialResultsService != null)
        {
            financialResultsService.ResultsChanged -= HandleResultsChanged;
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (financialResultsService == null)
        {
            financialResultsService =
                FindFirstObjectByType<BistroBuilderFinancialResultsService>();
        }

        if (generalGameStateService == null)
        {
            generalGameStateService =
                FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
