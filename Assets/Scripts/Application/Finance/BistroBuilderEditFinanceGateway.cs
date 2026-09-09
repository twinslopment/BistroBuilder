using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderEditFinanceRateDefinition
{
    public string definitionId = "*";
    public long addedSignedCentsPerUnit;
    public long modifiedSignedCentsPerUnit;
    public long removedSignedCentsPerUnit;
    public BistroBuilderEditFinanceRateUnit unit = BistroBuilderEditFinanceRateUnit.ProposalMeasure;
    public BistroBuilderEditFinancePricedChanges pricedChanges = BistroBuilderEditFinancePricedChanges.All;

    public bool Supports(BistroBuilderEditEconomicChangeKind kind)
    {
        int value = (int)kind;
        return value >= 0 && value <= 2 && ((int)pricedChanges & (1 << value)) != 0;
    }

    public long ResolveSignedRate(BistroBuilderEditEconomicChangeKind kind)
    {
        switch (kind)
        {
            case BistroBuilderEditEconomicChangeKind.Added:
                return addedSignedCentsPerUnit;
            case BistroBuilderEditEconomicChangeKind.Modified:
                return modifiedSignedCentsPerUnit;
            case BistroBuilderEditEconomicChangeKind.Removed:
                return removedSignedCentsPerUnit;
            default:
                return 0L;
        }
    }
}

public readonly struct BistroBuilderPricedEditEconomicLine
{
    public readonly BistroBuilderEditEconomicLine source;
    public readonly long signedCents;

    public BistroBuilderPricedEditEconomicLine(
        BistroBuilderEditEconomicLine source,
        long signedCents)
    {
        this.source = source == null ? null : new BistroBuilderEditEconomicLine
        {
            kind = source.kind, entityId = source.entityId, definitionId = source.definitionId,
            quantity = source.quantity, length = source.length, area = source.area
        };
        this.signedCents = signedCents;
    }
}

public static class BistroBuilderEditFinancePricingPolicy
{
    public static bool TryPrice(
        BistroBuilderEditEconomicProposal proposal,
        IReadOnlyList<BistroBuilderEditFinanceRateDefinition> rates,
        List<BistroBuilderPricedEditEconomicLine> priced,
        out long creditCents,
        out long debitCents,
        out string error)
    {
        creditCents = 0L;
        debitCents = 0L;
        error = string.Empty;
        if (priced == null) throw new ArgumentNullException(nameof(priced));
        priced.Clear();
        if (proposal == null || proposal.lines == null)
        {
            error = "La propuesta de reforma es nula.";
            return false;
        }
        if (proposal.lines.Count == 0) return true;
        if (rates == null || rates.Count == 0)
        {
            error = "Finanzas no tiene tarifas de reforma configuradas.";
            return false;
        }

        try
        {
            for (int i = 0; i < proposal.lines.Count; i++)
            {
                BistroBuilderEditEconomicLine line = proposal.lines[i];
                if (line == null || !line.entityId.IsValid ||
                    string.IsNullOrWhiteSpace(line.definitionId) ||
                    !IsValidMeasure(line.quantity) || !IsValidMeasure(line.length) || !IsValidMeasure(line.area))
                {
                    error = "La propuesta contiene una línea económica inválida.";
                    priced.Clear(); creditCents = 0L; debitCents = 0L;
                    return false;
                }
                if (!TryResolveRate(line.definitionId, rates, out var rate))
                {
                    error = "No existe tarifa financiera para " +
                            (line.definitionId ?? "<sin definitionId>") + ".";
                    priced.Clear(); creditCents = 0L; debitCents = 0L;
                    return false;
                }
                if (!rate.Supports(line.kind) || !TryResolveUnits(line, rate.unit, out decimal units))
                {
                    error = "La tarifa financiera no cubre la operación o unidad de " + line.definitionId + ".";
                    priced.Clear(); creditCents = 0L; debitCents = 0L;
                    return false;
                }

                long unitRate = rate.ResolveSignedRate(line.kind);
                long signed = checked((long)Math.Round(
                    units * unitRate,
                    MidpointRounding.AwayFromZero));
                priced.Add(new BistroBuilderPricedEditEconomicLine(line, signed));
                if (signed < 0L)
                    creditCents = checked(creditCents + -signed);
                else if (signed > 0L)
                    debitCents = checked(debitCents + signed);
            }
        }
        catch (OverflowException)
        {
            priced.Clear();
            creditCents = 0L;
            debitCents = 0L;
            error = "El coste de la reforma queda fuera de rango.";
            return false;
        }

        return true;
    }

    private static bool IsValidMeasure(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool TryResolveUnits(BistroBuilderEditEconomicLine line,
        BistroBuilderEditFinanceRateUnit unit, out decimal units)
    {
        float measure;
        switch (unit)
        {
            case BistroBuilderEditFinanceRateUnit.Quantity: measure = line.quantity; break;
            case BistroBuilderEditFinanceRateUnit.Length: measure = line.length; break;
            case BistroBuilderEditFinanceRateUnit.Area: measure = line.area; break;
            case BistroBuilderEditFinanceRateUnit.ProposalMeasure:
                measure = line.area > 0.0001f ? line.area : line.length > 0.0001f ? line.length : line.quantity;
                break;
            default: units = 0m; return false;
        }
        units = (decimal)measure;
        return units > 0m;
    }

    private static bool TryResolveRate(
        string definitionId,
        IReadOnlyList<BistroBuilderEditFinanceRateDefinition> rates,
        out BistroBuilderEditFinanceRateDefinition rate)
    {
        rate = null;
        string target = Normalize(definitionId);
        BistroBuilderEditFinanceRateDefinition wildcard = null;
        for (int i = 0; i < rates.Count; i++)
        {
            var candidate = rates[i];
            if (candidate == null) continue;
            string id = Normalize(candidate.definitionId);
            if (id == "*")
            {
                wildcard = candidate;
                continue;
            }
            if (!string.Equals(id, target, StringComparison.Ordinal)) continue;
            rate = candidate;
            return true;
        }
        rate = wildcard;
        return rate != null;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Edit Mode Renovation Finance Gateway")]
public sealed class BistroBuilderEditFinanceGateway :
    MonoBehaviour,
    IBistroBuilderEditEconomicGateway
{
    public const string SourceSystemId = "edit_mode_renovation";

    [SerializeField] private BistroBuilderFinanceService financeService;
    [SerializeField] private BistroBuilderDiscretionaryFinanceService discretionaryFinanceService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private GameClock gameClock;
    [SerializeField] private BistroBuilderEditFinanceTariffTable tariffTable;

    public BistroBuilderEditFinanceTariffTable TariffTable => tariffTable;

    public void ConfigureTariffTable(BistroBuilderEditFinanceTariffTable configuredTable)
    {
        tariffTable = configuredTable;
    }

    public bool ValidateTariffAuthority(out string error)
    {
        if (tariffTable != null) return tariffTable.ValidateConfiguration(out error);
        error = "Finanzas no ha publicado una tabla de tarifas de reforma con procedencia y revisión.";
        return false;
    }

    private IReadOnlyList<BistroBuilderEditFinanceRateDefinition> EffectiveRates =>
        tariffTable != null ? tariffTable.Rates : rates;
    [SerializeField] private List<BistroBuilderEditFinanceRateDefinition> rates =
        new List<BistroBuilderEditFinanceRateDefinition>();

    private sealed class PendingAuthorization
    {
        public BistroBuilderEditEconomicAuthorization authorization;
        public BistroBuilderEditEconomicProposal proposal;
        public readonly List<BistroBuilderPricedEditEconomicLine> priced =
            new List<BistroBuilderPricedEditEconomicLine>();
    }

    private readonly Dictionary<string, PendingAuthorization> pending =
        new Dictionary<string, PendingAuthorization>(StringComparer.Ordinal);
    private readonly HashSet<string> finalized =
        new HashSet<string>(StringComparer.Ordinal);

    private bool ValidateDependencies(out string error)
    {
        CacheDependencies();
        if (financeService == null || discretionaryFinanceService == null ||
            generalGameStateService == null || gameClock == null)
        {
            error = "La pasarela financiera de reformas tiene dependencias incompletas.";
            return false;
        }
        if (!financeService.ValidateConfiguration(out error) ||
            !discretionaryFinanceService.ValidateConfiguration(out error))
            return false;
        error = string.Empty;
        return true;
    }

    public bool ValidateConfiguration(out string error)
    {
        if (!ValidateDependencies(out error)) return false;
        if (tariffTable != null && !tariffTable.ValidateConfiguration(out error)) return false;
        if (EffectiveRates == null || EffectiveRates.Count == 0)
        {
            error = "Finanzas no tiene tarifas de reforma configuradas.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryPrepareAuthorization(
        BistroBuilderEditEconomicProposal proposal,
        out BistroBuilderEditEconomicAuthorization authorization,
        out string error)
    {
        authorization = default;
        if (!ValidateDependencies(out error)) return false;
        if (proposal != null && proposal.lines != null && proposal.lines.Count > 0 &&
            !ValidateConfiguration(out error)) return false;

        var priced = new List<BistroBuilderPricedEditEconomicLine>();
        if (!BistroBuilderEditFinancePricingPolicy.TryPrice(
                proposal,
                EffectiveRates,
                priced,
                out long creditCents,
                out long debitCents,
                out error))
            return false;

        if (!discretionaryFinanceService.TryAuthorizeNetCashEffect(
                creditCents,
                debitCents,
                out error))
            return false;

        string id = "editfinance:" + Guid.NewGuid().ToString("N");
        long totalSigned = checked(debitCents - creditCents);
        authorization = new BistroBuilderEditEconomicAuthorization(
            id,
            proposal.draftRevision,
            totalSigned);
        var record = new PendingAuthorization
        {
            authorization = authorization,
            proposal = proposal
        };
        record.priced.AddRange(priced);
        pending[id] = record;
        error = string.Empty;
        return true;
    }

    public bool TryFinalizeAuthorization(
        BistroBuilderEditEconomicAuthorization authorization,
        string commitOperationId,
        out string error)
    {
        error = string.Empty;
        if (!authorization.IsValid || string.IsNullOrWhiteSpace(commitOperationId))
        {
            error = "La autorización económica de reforma no es válida.";
            return false;
        }
        if (finalized.Contains(authorization.authorizationId))
            return true;
        if (!pending.TryGetValue(
                authorization.authorizationId,
                out PendingAuthorization record))
        {
            error = "La autorización económica de reforma ya no está preparada.";
            return false;
        }
        if (record.authorization.draftRevision != authorization.draftRevision)
        {
            error = "La revisión económica preparada no coincide con el Draft.";
            return false;
        }

        List<BistroBuilderFinanceTransactionRequest> requests =
            BuildRequests(record, commitOperationId);
        if (requests.Count > 0 &&
            !financeService.TryPostTransactions(requests, out _, out error))
            return false;

        pending.Remove(authorization.authorizationId);
        finalized.Add(authorization.authorizationId);
        return true;
    }

    public bool TryAbortAuthorization(
        BistroBuilderEditEconomicAuthorization authorization,
        out string error)
    {
        error = string.Empty;
        if (!authorization.IsValid) return true;
        if (finalized.Contains(authorization.authorizationId))
        {
            error = "La autorización económica ya fue finalizada.";
            return false;
        }
        pending.Remove(authorization.authorizationId);
        return true;
    }

    public void ReplaceRatesRuntime(
        IEnumerable<BistroBuilderEditFinanceRateDefinition> configuredRates)
    {
        if (rates == null) rates = new List<BistroBuilderEditFinanceRateDefinition>();
        rates.Clear();
        if (configuredRates == null) return;
        foreach (var rate in configuredRates)
            if (rate != null) rates.Add(rate);
    }

    public void ConfigureRuntimeDependencies(
        BistroBuilderFinanceService finance,
        BistroBuilderDiscretionaryFinanceService discretionary,
        BistroBuilderGeneralGameStateService generalState,
        GameClock clock)
    {
        financeService = finance;
        discretionaryFinanceService = discretionary;
        generalGameStateService = generalState;
        gameClock = clock;
    }

    private List<BistroBuilderFinanceTransactionRequest> BuildRequests(
        PendingAuthorization record,
        string commitOperationId)
    {
        var requests = new List<BistroBuilderFinanceTransactionRequest>();
        for (int i = 0; i < record.priced.Count; i++)
        {
            BistroBuilderPricedEditEconomicLine priced = record.priced[i];
            if (priced.signedCents == 0L) continue;
            BistroBuilderEditEconomicLine line = priced.source;
            bool debit = priced.signedCents > 0L;
            long amount = debit ? priced.signedCents : -priced.signedCents;
            requests.Add(new BistroBuilderFinanceTransactionRequest
            {
                operationId = commitOperationId + ".renovation." + i.ToString("D4"),
                sourceSystemId = SourceSystemId,
                sourceReferenceId = line.entityId.Value,
                categoryId = debit
                    ? "investment.renovation"
                    : "income.asset_resale",
                kind = debit
                    ? BistroBuilderFinanceTransactionKind.Debit
                    : BistroBuilderFinanceTransactionKind.Credit,
                amountCents = amount,
                dayIndex = generalGameStateService.DayIndex,
                minuteOfDay = gameClock.Hour * 60 + gameClock.Minute,
                description = BuildDescription(line, debit)
            });
        }
        return requests;
    }

    private static string BuildDescription(
        BistroBuilderEditEconomicLine line,
        bool debit)
    {
        string verb = debit ? "Reforma" : "Recuperación de valor";
        return verb + " de " + (line.definitionId ?? "elemento") + ".";
    }

    private void CacheDependencies()
    {
        if (financeService == null)
            financeService = FindFirstObjectByType<BistroBuilderFinanceService>();
        if (discretionaryFinanceService == null)
            discretionaryFinanceService =
                FindFirstObjectByType<BistroBuilderDiscretionaryFinanceService>();
        if (generalGameStateService == null)
            generalGameStateService =
                FindFirstObjectByType<BistroBuilderGeneralGameStateService>();
        if (gameClock == null)
            gameClock = FindFirstObjectByType<GameClock>();
    }
}
