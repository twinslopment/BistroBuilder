using System;
using System.Collections.Generic;

namespace BistroBuilder.ConstructionAuthoring
{
    /// <summary>Composition helper reading the project's existing Finance-authored
    /// definitions. No prices, room-purpose aliases, or second persistent catalog.</summary>
    public static class ConstructionCatalogAdapter
    {
        public static ConstructionDefinitionCatalog FromTariffs(BistroBuilderEditFinanceTariffTable table)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!table.ValidateConfiguration(out string error)) throw new ArgumentException(error, nameof(table));
            var definitions = new List<BistroBuilderEditCatalogDefinition>();
            foreach (var rate in table.Rates)
                if (rate.definitionId.StartsWith("zone.", StringComparison.Ordinal) &&
                    (rate.pricedChanges & BistroBuilderEditFinancePricedChanges.Added) != 0)
                    definitions.Add(new BistroBuilderEditCatalogDefinition { definitionId = rate.definitionId, semanticKind = "zone" });
            return new ConstructionDefinitionCatalog(definitions);
        }
    }
}
