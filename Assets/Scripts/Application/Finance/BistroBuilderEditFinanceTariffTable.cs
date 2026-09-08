using System;
using System.Collections.Generic;
using UnityEngine;

public enum BistroBuilderEditFinanceRateUnit
{
    // Compatibility for pre-18M runtime fixtures. Published tariffs require an explicit unit.
    ProposalMeasure = 0, Quantity = 1, Length = 2, Area = 3
}

[Flags]
public enum BistroBuilderEditFinancePricedChanges
{
    None = 0, Added = 1, Modified = 2, Removed = 4, All = Added | Modified | Removed
}

/// <summary>
/// Finance-authored renovation prices. Empty data is deliberately not a free-build policy.
/// Placeable acquisition prices are not substituted for architectural work measurements.
/// </summary>
[CreateAssetMenu(fileName = "BistroBuilderEditFinanceTariffs",
    menuName = "Bistro Builder/Finance/Edit Mode Renovation Tariffs")]
public sealed class BistroBuilderEditFinanceTariffTable : ScriptableObject
{
    [SerializeField] private string sourceReference = string.Empty;
    [SerializeField] private string tariffRevision = string.Empty;
    [SerializeField] private List<BistroBuilderEditFinanceRateDefinition> rates =
        new List<BistroBuilderEditFinanceRateDefinition>();

    public string SourceReference => sourceReference;
    public string TariffRevision => tariffRevision;
    public IReadOnlyList<BistroBuilderEditFinanceRateDefinition> Rates => rates;

    public bool ValidateConfiguration(out string error)
    {
        if (string.IsNullOrWhiteSpace(sourceReference) || string.IsNullOrWhiteSpace(tariffRevision))
        {
            error = "La tabla financiera de reformas necesita procedencia y revisión de Finanzas.";
            return false;
        }
        if (rates == null || rates.Count == 0)
        {
            error = "Finanzas todavía no ha publicado importes de reforma.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < rates.Count; i++)
        {
            var rate = rates[i];
            string id = rate == null || string.IsNullOrWhiteSpace(rate.definitionId)
                ? string.Empty : rate.definitionId.Trim().ToLowerInvariant();
            if (id.Length == 0 || id == "*" || !ids.Add(id))
            {
                error = "Las tarifas publicadas necesitan DefinitionId explícitos, únicos y no vacíos.";
                return false;
            }
            if (rate.unit < BistroBuilderEditFinanceRateUnit.Quantity ||
                rate.unit > BistroBuilderEditFinanceRateUnit.Area ||
                rate.pricedChanges == BistroBuilderEditFinancePricedChanges.None ||
                (rate.pricedChanges & ~BistroBuilderEditFinancePricedChanges.All) != 0)
            {
                error = "La tarifa " + id + " necesita unidad y operaciones económicas explícitas.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }
}
