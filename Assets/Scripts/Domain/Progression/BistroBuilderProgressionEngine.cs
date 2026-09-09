using System;
using System.Collections.Generic;

/// <summary>
/// Motor puro del Bloque 9. Valida catálogo/estado y calcula disponibilidad
/// sin tocar Unity, Finanzas, Reputación ni la escena.
/// </summary>
public static class BistroBuilderProgressionEngine
{
    public const int MinimumReputationBasisPoints = 0;
    public const int MaximumReputationBasisPoints = 10000;

    public static BistroBuilderUpgradeSnapshot CreateInitialSnapshot()
    {
        return new BistroBuilderUpgradeSnapshot();
    }

    public static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsSafeStableId(string value)
    {
        string id = NormalizeId(value);
        if (id.Length < 2 || id.Length > 96) return false;
        for (int i = 0; i < id.Length; i++)
        {
            char c = id[i];
            if ((c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9') ||
                c == '.' || c == '_' || c == '-')
                continue;
            return false;
        }
        return true;
    }

    public static bool TryValidateDefinition(
        BistroBuilderUpgradeDefinition definition,
        out string error)
    {
        if (definition == null)
        {
            error = "La definición de mejora es nula.";
            return false;
        }
        string id = NormalizeId(definition.upgradeId);
        if (!IsSafeStableId(id))
        {
            error = "La mejora no tiene un ID estable válido.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.displayName) ||
            string.IsNullOrWhiteSpace(definition.description))
        {
            error = id + ": nombre o descripción vacíos.";
            return false;
        }
        if (!Enum.IsDefined(typeof(BistroBuilderUpgradeCategory), definition.category))
        {
            error = id + ": categoría desconocida.";
            return false;
        }
        if (definition.costCents <= 0L || definition.requiredProgressionLevel < 1)
        {
            error = id + ": coste o nivel requerido inválidos.";
            return false;
        }
        if (definition.requiredReputationBasisPoints < MinimumReputationBasisPoints ||
            definition.requiredReputationBasisPoints > MaximumReputationBasisPoints)
        {
            error = id + ": reputación requerida fuera de rango.";
            return false;
        }
        if (!TryValidateIdList(definition.prerequisiteUpgradeIds, id, false, out error) ||
            !TryValidateIdList(definition.requiredCapabilityIds, id, true, out error) ||
            !TryValidateIdList(definition.incompatibleCapabilityIds, id, true, out error))
            return false;        if (definition.effects == null)
        {
            error = id + ": la lista de efectos es nula.";
            return false;
        }
        for (int effectIndex = 0; effectIndex < definition.effects.Count; effectIndex++)
        {
            BistroBuilderUpgradeEffectDefinition effect = definition.effects[effectIndex];
            if (effect == null ||
                !Enum.IsDefined(typeof(BistroBuilderUpgradeEffectKind), effect.kind) ||
                effect.basisPoints < -5000 || effect.basisPoints > 5000)
            {
                error = id + ": contiene un efecto inválido o fuera de rango.";
                return false;
            }
        }


        var required = new HashSet<string>(StringComparer.Ordinal);
        if (definition.requiredCapabilityIds != null)
            for (int i = 0; i < definition.requiredCapabilityIds.Count; i++)
                required.Add(NormalizeId(definition.requiredCapabilityIds[i]));
        if (definition.incompatibleCapabilityIds != null)
            for (int i = 0; i < definition.incompatibleCapabilityIds.Count; i++)
                if (required.Contains(NormalizeId(definition.incompatibleCapabilityIds[i])))
                {
                    error = id + ": una capacidad no puede ser requerida e incompatible a la vez.";
                    return false;
                }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateCatalog(
        IReadOnlyList<BistroBuilderUpgradeDefinition> definitions,
        out string error)
    {
        if (definitions == null || definitions.Count == 0)
        {
            error = "El catálogo de mejoras está vacío.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (!TryValidateDefinition(definitions[i], out error)) return false;
            string id = NormalizeId(definitions[i].upgradeId);
            if (!ids.Add(id))
            {
                error = "ID de mejora duplicado: " + id + ".";
                return false;
            }
        }
        for (int i = 0; i < definitions.Count; i++)
        {
            var prerequisites = definitions[i].prerequisiteUpgradeIds;
            if (prerequisites == null) continue;
            for (int p = 0; p < prerequisites.Count; p++)
            {
                string prerequisiteId = NormalizeId(prerequisites[p]);
                if (!ids.Contains(prerequisiteId))
                {
                    error = definitions[i].upgradeId +
                        ": prerrequisito inexistente " + prerequisiteId + ".";
                    return false;
                }
            }
        }
        if (ContainsPrerequisiteCycle(definitions))
        {
            error = "El catálogo contiene un ciclo de prerrequisitos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public static bool TryValidateSnapshot(
        BistroBuilderUpgradeSnapshot snapshot,
        out string error)
    {
        if (snapshot == null || snapshot.schemaVersion != BistroBuilderUpgradeSnapshot.CurrentSchemaVersion ||
            snapshot.revision < 0L || snapshot.purchased == null)
        {
            error = "El snapshot de mejoras no es válido.";
            return false;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < snapshot.purchased.Count; i++)
        {
            BistroBuilderPurchasedUpgradeRecord record = snapshot.purchased[i];
            if (record == null || !IsSafeStableId(record.upgradeId) ||
                record.purchasedDayIndex < 1 || record.paidCents <= 0L)
            {
                error = "Existe una adquisición de mejora inválida.";
                return false;
            }
            if (!ids.Add(NormalizeId(record.upgradeId)))
            {
                error = "El snapshot contiene una mejora adquirida dos veces.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    public static BistroBuilderUpgradeAvailability EvaluateAvailability(
        BistroBuilderUpgradeDefinition definition,
        BistroBuilderUpgradeAvailabilityContext context)
    {
        var result = new BistroBuilderUpgradeAvailability();
        if (definition == null || context == null)
        {
            result.state = BistroBuilderUpgradeAvailabilityState.Locked;
            result.blockedReason = "Contexto de progresión incompleto.";
            return result;
        }
        string id = NormalizeId(definition.upgradeId);
        if (context.purchasedUpgradeIds.Contains(id))
        {
            result.state = BistroBuilderUpgradeAvailabilityState.Purchased;
            result.affordable = true;
            return result;
        }
        if (context.progressionLevel < definition.requiredProgressionLevel)
            return Locked(result, "Requiere nivel " + definition.requiredProgressionLevel + ".");
        if (context.reputationBasisPoints < definition.requiredReputationBasisPoints)
            return Locked(result, "Requiere reputación " +
                FormatBasisPoints(definition.requiredReputationBasisPoints) + ".");
        if (!ContainsAll(context.purchasedUpgradeIds, definition.prerequisiteUpgradeIds, out string missingUpgrade))
            return Locked(result, "Requiere la mejora " + missingUpgrade + ".");
        if (!ContainsAll(context.capabilityIds, definition.requiredCapabilityIds, out string missingCapability))
            return Locked(result, "Este local no dispone de " + missingCapability + ".");
        if (ContainsAny(context.capabilityIds, definition.incompatibleCapabilityIds, out string incompatible))
            return Locked(result, "Esta mejora es incompatible con " + incompatible + ".");

        result.state = BistroBuilderUpgradeAvailabilityState.Available;
        result.affordable = context.availableCashCents >= definition.costCents;
        result.blockedReason = result.affordable ? string.Empty : "Fondos insuficientes.";
        return result;
    }

    public static bool TryCreatePurchaseCandidate(
        BistroBuilderUpgradeSnapshot current,
        BistroBuilderUpgradeDefinition definition,
        int dayIndex,
        out BistroBuilderUpgradeSnapshot candidate,
        out string error)
    {
        candidate = null;
        if (!TryValidateSnapshot(current, out error) ||
            !TryValidateDefinition(definition, out error)) return false;
        if (dayIndex < 1)
        {
            error = "El día de compra no es válido.";
            return false;
        }
        string id = NormalizeId(definition.upgradeId);
        for (int i = 0; i < current.purchased.Count; i++)
            if (NormalizeId(current.purchased[i].upgradeId) == id)
            {
                error = "La mejora ya fue adquirida.";
                return false;
            }

        candidate = current.DeepClone();
        candidate.revision = checked(candidate.revision + 1L);
        candidate.purchased.Add(new BistroBuilderPurchasedUpgradeRecord
        {
            upgradeId = id,
            purchasedDayIndex = dayIndex,
            paidCents = definition.costCents
        });
        return TryValidateSnapshot(candidate, out error);
    }

    private static BistroBuilderUpgradeAvailability Locked(
        BistroBuilderUpgradeAvailability result,
        string reason)
    {
        result.state = BistroBuilderUpgradeAvailabilityState.Locked;
        result.affordable = false;
        result.blockedReason = reason;
        return result;
    }

    private static bool TryValidateIdList(
        IReadOnlyList<string> values,
        string ownerId,
        bool allowOwnerId,
        out string error)
    {
        if (values == null)
        {
            error = string.Empty;
            return true;
        }
        var unique = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
        {
            string id = NormalizeId(values[i]);
            if (!IsSafeStableId(id) || !unique.Add(id))
            {
                error = ownerId + ": lista de IDs inválida o duplicada.";
                return false;
            }
            if (!allowOwnerId && id == ownerId)
            {
                error = ownerId + ": una mejora no puede depender de sí misma.";
                return false;
            }
        }
        error = string.Empty;
        return true;
    }

    private static bool ContainsAll(
        HashSet<string> available,
        IReadOnlyList<string> required,
        out string missing)
    {
        missing = string.Empty;
        if (required == null) return true;
        for (int i = 0; i < required.Count; i++)
        {
            string id = NormalizeId(required[i]);
            if (!available.Contains(id))
            {
                missing = id;
                return false;
            }
        }
        return true;
    }

    private static bool ContainsAny(
        HashSet<string> available,
        IReadOnlyList<string> forbidden,
        out string match)
    {
        match = string.Empty;
        if (forbidden == null) return false;
        for (int i = 0; i < forbidden.Count; i++)
        {
            string id = NormalizeId(forbidden[i]);
            if (available.Contains(id))
            {
                match = id;
                return true;
            }
        }
        return false;
    }

    private static bool ContainsPrerequisiteCycle(
        IReadOnlyList<BistroBuilderUpgradeDefinition> definitions)
    {
        var byId = new Dictionary<string, BistroBuilderUpgradeDefinition>(StringComparer.Ordinal);
        for (int i = 0; i < definitions.Count; i++)
            byId[NormalizeId(definitions[i].upgradeId)] = definitions[i];
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in byId.Keys)
            if (HasCycle(id, byId, visiting, visited)) return true;
        return false;
    }

    private static bool HasCycle(
        string id,
        Dictionary<string, BistroBuilderUpgradeDefinition> byId,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(id)) return false;
        if (!visiting.Add(id)) return true;
        IReadOnlyList<string> dependencies = byId[id].prerequisiteUpgradeIds;
        if (dependencies != null)
            for (int i = 0; i < dependencies.Count; i++)
            {
                string next = NormalizeId(dependencies[i]);
                if (byId.ContainsKey(next) && HasCycle(next, byId, visiting, visited))
                    return true;
            }
        visiting.Remove(id);
        visited.Add(id);
        return false;
    }

    private static string FormatBasisPoints(int value)
    {
        return (value / 100d).ToString("0.##") + "%";
    }
}