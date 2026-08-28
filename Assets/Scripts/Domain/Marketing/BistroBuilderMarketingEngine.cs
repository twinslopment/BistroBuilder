using System;
using System.Collections.Generic;

/// <summary>
/// Núcleo puro de Marketing. Valida contenido/estado, crea campañas y agrega
/// efectos sin conocer GameObjects, Finanzas, clientes, reservas ni UI.
/// </summary>
public static class BistroBuilderMarketingEngine
{
    public static BistroBuilderMarketingSnapshot CreateEmptySnapshot() =>
        new BistroBuilderMarketingSnapshot();

    public static string NormalizeId(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    public static bool TryValidateDefinition(
        BistroBuilderMarketingCampaignDefinition definition,
        out string error)
    {
        if (definition == null)
        {
            error = "La definición de Marketing es nula.";
            return false;
        }

        string id = NormalizeId(definition.campaignId);
        if (id.Length < 3)
        {
            error = "CampaignId vacío o demasiado corto.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.displayName))
        {
            error = id + " no tiene nombre visible.";
            return false;
        }

        if (definition.baseCostCents <= 0L)
        {
            error = id + " debe tener un coste positivo.";
            return false;
        }

        if (definition.durationDays < 1 || definition.durationDays > 60)
        {
            error = id + " tiene una duración fuera de 1..60 días.";
            return false;
        }

        if (definition.minProgressionLevel < 1)
        {
            error = id + " tiene un nivel mínimo inválido.";
            return false;
        }

        if (definition.modifiers == null || definition.modifiers.Count == 0)
        {
            error = id + " no contiene modificadores.";
            return false;
        }

        for (int i = 0; i < definition.modifiers.Count; i++)
        {
            BistroBuilderMarketingModifier modifier = definition.modifiers[i];
            if (modifier == null)
            {
                error = id + " contiene un modificador nulo.";
                return false;
            }

            if (modifier.basisPoints < -9000 ||
                modifier.basisPoints > 50000)
            {
                error = id + " contiene puntos básicos fuera de rango.";
                return false;
            }

            if (modifier.kind == BistroBuilderMarketingModifierKind.Reputation)
            {
                if (modifier.flatPoints < -100 || modifier.flatPoints > 100)
                {
                    error = id + " contiene reputación fuera de rango.";
                    return false;
                }
            }
            else if (modifier.flatPoints != 0)
            {
                error = id + " usa puntos planos fuera de Reputación.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateCatalog(
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions,
        out string error)
    {
        if (definitions == null || definitions.Count == 0)
        {
            error = "El catálogo de Marketing está vacío.";
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            BistroBuilderMarketingCampaignDefinition definition =
                definitions[i];
            if (!TryValidateDefinition(definition, out error))
                return false;

            string id = NormalizeId(definition.campaignId);
            if (!ids.Add(id))
            {
                error = "CampaignId duplicado: " + id + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderMarketingSnapshot snapshot,
        out string error)
    {
        if (snapshot == null ||
            snapshot.schemaId != BistroBuilderMarketingSnapshot.CurrentSchemaId ||
            snapshot.schemaVersion !=
                BistroBuilderMarketingSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L ||
            snapshot.campaigns == null)
        {
            error = "marketing.state contiene una cabecera inválida.";
            return false;
        }

        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord record =
                snapshot.campaigns[i];
            if (record == null)
            {
                error = "marketing.state contiene una campaña nula.";
                return false;
            }

            string instanceId = NormalizeId(record.instanceId);
            string campaignId = NormalizeId(record.campaignId);
            string operationId = NormalizeId(record.financeOperationId);
            if (instanceId.Length == 0 || campaignId.Length == 0 ||
                operationId.Length == 0 || !instanceIds.Add(instanceId))
            {
                error = "marketing.state contiene identidades inválidas o duplicadas.";
                return false;
            }

            if (record.startDayIndex < 1 ||
                record.endDayExclusive <= record.startDayIndex ||
                record.paidCostCents <= 0L ||
                record.revision < 1L)
            {
                error = instanceId + " contiene planificación inválida.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static bool TryCreateCampaign(
        BistroBuilderMarketingSnapshot source,
        BistroBuilderMarketingCampaignDefinition definition,
        int currentDayIndex,
        string targetId,
        string instanceId,
        string financeOperationId,
        out BistroBuilderMarketingSnapshot candidate,
        out string error)
    {
        candidate = null;
        if (!TryValidateSnapshot(source, out error) ||
            !TryValidateDefinition(definition, out error))
            return false;

        if (currentDayIndex < 1)
        {
            error = "El día actual de Marketing es inválido.";
            return false;
        }

        string normalizedTarget = NormalizeId(targetId);
        if (definition.targetKind != BistroBuilderMarketingTargetKind.None &&
            normalizedTarget.Length == 0)
        {
            error = definition.displayName + " requiere un objetivo lógico.";
            return false;
        }

        if (definition.targetKind == BistroBuilderMarketingTargetKind.None)
            normalizedTarget = string.Empty;

        string normalizedInstance = NormalizeId(instanceId);
        string normalizedOperation = NormalizeId(financeOperationId);
        if (normalizedInstance.Length == 0 || normalizedOperation.Length == 0)
        {
            error = "La instancia de campaña no tiene identidades estables.";
            return false;
        }

        string campaignId = NormalizeId(definition.campaignId);
        for (int i = 0; i < source.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord existing =
                source.campaigns[i];
            if (existing == null)
                continue;

            if (NormalizeId(existing.instanceId) == normalizedInstance)
            {
                error = "InstanceId de Marketing duplicado.";
                return false;
            }

            if (NormalizeId(existing.campaignId) == campaignId &&
                NormalizeId(existing.targetId) == normalizedTarget &&
                existing.IsActiveOnDay(currentDayIndex))
            {
                error = "Esa campaña ya está activa para el mismo objetivo.";
                return false;
            }
        }

        candidate = source.DeepClone();
        candidate.revision = checked(source.revision + 1L);
        candidate.campaigns.Add(new BistroBuilderMarketingCampaignRecord
        {
            instanceId = normalizedInstance,
            campaignId = campaignId,
            targetId = normalizedTarget,
            startDayIndex = currentDayIndex,
            endDayExclusive = checked(
                currentDayIndex + definition.durationDays),
            paidCostCents = definition.baseCostCents,
            financeOperationId = normalizedOperation,
            revision = 1L
        });

        if (!TryValidateSnapshot(candidate, out error))
        {
            candidate = null;
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryPruneExpired(
        BistroBuilderMarketingSnapshot source,
        int currentDayIndex,
        out BistroBuilderMarketingSnapshot candidate,
        out bool changed,
        out string error)
    {
        candidate = null;
        changed = false;
        if (!TryValidateSnapshot(source, out error) || currentDayIndex < 1)
        {
            if (currentDayIndex < 1)
                error = "El día actual de Marketing es inválido.";
            return false;
        }

        candidate = source.DeepClone();
        for (int i = candidate.campaigns.Count - 1; i >= 0; i--)
        {
            if (candidate.campaigns[i].endDayExclusive <= currentDayIndex)
            {
                candidate.campaigns.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            candidate.revision = checked(source.revision + 1L);

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Evalúa únicamente el modificador de ticket medio. Las campañas con
    /// objetivo lógico solo contribuyen cuando ese objetivo pertenece al
    /// conjunto aplicable del cobro (carta activa o platos realmente pedidos).
    /// </summary>
    public static bool TryEvaluateAverageTicket(
        BistroBuilderMarketingSnapshot snapshot,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions,
        int dayIndex,
        BistroBuilderMarketingCustomerSegment segment,
        BistroBuilderMarketingDayPart dayPart,
        ISet<string> applicableTargetIds,
        out int basisPoints,
        out int contributingCampaigns,
        out string error)
    {
        basisPoints = 0;
        contributingCampaigns = 0;
        if (!TryValidateSnapshot(snapshot, out error) ||
            !TryValidateCatalog(definitions, out error))
            return false;

        if (dayIndex < 1)
        {
            error = "La consulta de ticket medio necesita un día válido.";
            return false;
        }

        var normalizedTargets = new HashSet<string>(StringComparer.Ordinal);
        if (applicableTargetIds != null)
        {
            foreach (string targetId in applicableTargetIds)
            {
                string normalized = NormalizeId(targetId);
                if (normalized.Length > 0)
                    normalizedTargets.Add(normalized);
            }
        }

        var byId =
            new Dictionary<string, BistroBuilderMarketingCampaignDefinition>(
                StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
            byId.Add(NormalizeId(definitions[i].campaignId), definitions[i]);

        var query = new BistroBuilderMarketingEffectQuery
        {
            dayIndex = dayIndex,
            segment = segment,
            dayPart = dayPart
        };
        var contributors = new HashSet<string>(StringComparer.Ordinal);
        long aggregate = 0L;

        for (int i = 0; i < snapshot.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord record = snapshot.campaigns[i];
            if (record == null || !record.IsActiveOnDay(dayIndex))
                continue;

            if (!byId.TryGetValue(
                    NormalizeId(record.campaignId),
                    out BistroBuilderMarketingCampaignDefinition definition))
            {
                error = "marketing.state referencia una campaña ausente del catálogo.";
                return false;
            }

            if (definition.targetKind != BistroBuilderMarketingTargetKind.None &&
                !normalizedTargets.Contains(NormalizeId(record.targetId)))
                continue;

            bool contributed = false;
            for (int j = 0; j < definition.modifiers.Count; j++)
            {
                BistroBuilderMarketingModifier modifier = definition.modifiers[j];
                if (modifier == null ||
                    modifier.kind != BistroBuilderMarketingModifierKind.AverageTicket ||
                    !MatchesContext(modifier, query))
                    continue;

                aggregate += modifier.basisPoints;
                contributed = true;
            }

            if (contributed)
                contributors.Add(record.instanceId);
        }

        if (aggregate < int.MinValue || aggregate > int.MaxValue)
        {
            error = "El ajuste agregado de ticket medio desborda el rango permitido.";
            return false;
        }

        basisPoints = (int)aggregate;
        contributingCampaigns = contributors.Count;
        error = string.Empty;
        return true;
    }

    public static bool TryEvaluate(
        BistroBuilderMarketingSnapshot snapshot,
        IReadOnlyList<BistroBuilderMarketingCampaignDefinition> definitions,
        BistroBuilderMarketingEffectQuery query,
        out BistroBuilderMarketingEffectSnapshot effects,
        out string error)
    {
        effects = null;
        if (!TryValidateSnapshot(snapshot, out error) ||
            !TryValidateCatalog(definitions, out error))
            return false;

        if (query == null || query.dayIndex < 1)
        {
            error = "La consulta de efectos de Marketing es inválida.";
            return false;
        }

        var byId =
            new Dictionary<string, BistroBuilderMarketingCampaignDefinition>(
                StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
            byId.Add(NormalizeId(definitions[i].campaignId), definitions[i]);

        effects = new BistroBuilderMarketingEffectSnapshot();
        var contributors = new HashSet<string>(StringComparer.Ordinal);
        string queryTarget = NormalizeId(query.targetId);

        for (int i = 0; i < snapshot.campaigns.Count; i++)
        {
            BistroBuilderMarketingCampaignRecord record =
                snapshot.campaigns[i];
            if (!record.IsActiveOnDay(query.dayIndex))
                continue;

            if (!byId.TryGetValue(
                    NormalizeId(record.campaignId),
                    out BistroBuilderMarketingCampaignDefinition definition))
            {
                error = "marketing.state referencia una campaña ausente del catálogo.";
                effects = null;
                return false;
            }

            bool contributed = false;
            for (int j = 0; j < definition.modifiers.Count; j++)
            {
                BistroBuilderMarketingModifier modifier =
                    definition.modifiers[j];
                if (!MatchesContext(modifier, query))
                    continue;

                if (modifier.kind ==
                    BistroBuilderMarketingModifierKind.TargetDemand)
                {
                    if (queryTarget.Length == 0 ||
                        NormalizeId(record.targetId) != queryTarget)
                        continue;
                }

                ApplyModifier(effects, modifier);
                contributed = true;
            }

            if (contributed)
                contributors.Add(record.instanceId);
        }

        effects.contributingCampaigns = contributors.Count;
        error = string.Empty;
        return true;
    }

    private static bool MatchesContext(
        BistroBuilderMarketingModifier modifier,
        BistroBuilderMarketingEffectQuery query)
    {
        // Any en el modificador significa universal. Any en la consulta no
        // absorbe campañas segmentadas: evita inflar la demanda global.
        bool segmentMatches =
            modifier.segment == BistroBuilderMarketingCustomerSegment.Any ||
            modifier.segment == query.segment;
        bool dayPartMatches =
            modifier.dayPart == BistroBuilderMarketingDayPart.Any ||
            modifier.dayPart == query.dayPart;
        return segmentMatches && dayPartMatches;
    }

    private static void ApplyModifier(
        BistroBuilderMarketingEffectSnapshot effects,
        BistroBuilderMarketingModifier modifier)
    {
        switch (modifier.kind)
        {
            case BistroBuilderMarketingModifierKind.OverallDemand:
                effects.overallDemandBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.ReservationDemand:
                effects.reservationDemandBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.WalkInDemand:
                effects.walkInDemandBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.Reputation:
                effects.reputationFlatPoints += modifier.flatPoints;
                break;
            case BistroBuilderMarketingModifierKind.AverageTicket:
                effects.averageTicketBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.RepeatVisit:
                effects.repeatVisitBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.OperationalPressure:
                effects.operationalPressureBasisPoints += modifier.basisPoints;
                break;
            case BistroBuilderMarketingModifierKind.TargetDemand:
                effects.targetDemandBasisPoints += modifier.basisPoints;
                break;
        }
    }
}
