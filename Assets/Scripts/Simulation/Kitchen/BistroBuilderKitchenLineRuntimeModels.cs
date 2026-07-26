using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Evento inmutable emitido cuando una línea concreta termina su preparación.
/// </summary>
public readonly struct BistroBuilderOrderLineReadyEvent
{
    public KitchenSystem Kitchen { get; }
    public RestaurantOrder Order { get; }
    public string OrderLineId { get; }
    public string DishId { get; }

    public BistroBuilderOrderLineReadyEvent(
        KitchenSystem kitchen,
        RestaurantOrder order,
        string orderLineId,
        string dishId
    )
    {
        Kitchen = kitchen;
        Order = order;
        OrderLineId = BistroBuilderOrderIdUtility.Normalize(orderLineId);
        DishId = BistroBuilderOrderIdUtility.Normalize(dishId);
    }
}

/// <summary>
/// DTO persistible de una unidad de trabajo de cocina.
///
/// No contiene referencias a GameObject. El futuro service.runtime podrá
/// reconstruir las referencias mediante CanonicalOrderId y OrderLineId.
/// </summary>
[Serializable]
public sealed class BistroBuilderKitchenLineWorkSaveData
{
    public string canonicalOrderId = string.Empty;
    public string orderLineId = string.Empty;
    public string dishId = string.Empty;
    public int legacyOrderId;
    public long sequence;
    public float totalDurationSeconds;
    public float remainingDurationSeconds;
    public bool wasActive;

    public BistroBuilderKitchenLineWorkSaveData Clone()
    {
        return new BistroBuilderKitchenLineWorkSaveData
        {
            canonicalOrderId = canonicalOrderId,
            orderLineId = orderLineId,
            dishId = dishId,
            legacyOrderId = legacyOrderId,
            sequence = sequence,
            totalDurationSeconds = totalDurationSeconds,
            remainingDurationSeconds = remainingDurationSeconds,
            wasActive = wasActive
        };
    }

    public bool TryValidate(out string error)
    {
        canonicalOrderId =
            BistroBuilderOrderIdUtility.Normalize(canonicalOrderId);
        orderLineId =
            BistroBuilderOrderIdUtility.Normalize(orderLineId);
        dishId = BistroBuilderOrderIdUtility.Normalize(dishId);

        if (!BistroBuilderOrderIdUtility.IsValid(canonicalOrderId))
        {
            error = "El trabajo de cocina contiene un OrderId inválido.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(orderLineId))
        {
            error = "El trabajo de cocina contiene un LineId inválido.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(dishId))
        {
            error = "El trabajo de cocina contiene un DishId inválido.";
            return false;
        }

        if (legacyOrderId < 1)
        {
            error = "El trabajo de cocina contiene un OrderId legacy inválido.";
            return false;
        }

        if (sequence < 0)
        {
            error = "La secuencia del trabajo de cocina es inválida.";
            return false;
        }

        if (!IsFiniteNonNegative(totalDurationSeconds) ||
            totalDurationSeconds <= 0f ||
            !IsFiniteNonNegative(remainingDurationSeconds) ||
            remainingDurationSeconds > totalDurationSeconds + 0.001f)
        {
            error = "Los tiempos del trabajo de cocina son inválidos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value >= 0f;
    }
}

/// <summary>
/// Snapshot modular de una cocina concreta.
///
/// Captura la cola, la línea activa y el tiempo pendiente sin duplicar el
/// estado canónico de la línea. Ese estado continúa perteneciendo a 367B.
/// </summary>
[Serializable]
public sealed class BistroBuilderKitchenRuntimeSnapshot
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public string kitchenId = string.Empty;
    public long nextSequence;
    public List<BistroBuilderKitchenLineWorkSaveData> workItems =
        new List<BistroBuilderKitchenLineWorkSaveData>();

    public BistroBuilderKitchenRuntimeSnapshot Clone()
    {
        BistroBuilderKitchenRuntimeSnapshot clone =
            new BistroBuilderKitchenRuntimeSnapshot
            {
                version = version,
                kitchenId = kitchenId,
                nextSequence = nextSequence
            };

        if (workItems != null)
        {
            for (int index = 0; index < workItems.Count; index++)
            {
                clone.workItems.Add(workItems[index]?.Clone());
            }
        }

        return clone;
    }

    public bool TryValidate(out string error)
    {
        kitchenId = BistroBuilderOrderIdUtility.Normalize(kitchenId);

        if (version != CurrentVersion)
        {
            error = "La versión del snapshot de cocina no es compatible.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(kitchenId))
        {
            error = "El snapshot contiene un KitchenId inválido.";
            return false;
        }

        if (nextSequence < 0)
        {
            error = "La siguiente secuencia de cocina es inválida.";
            return false;
        }

        if (workItems == null)
        {
            error = "La colección de trabajos de cocina es nula.";
            return false;
        }

        HashSet<string> lineIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<long> sequences = new HashSet<long>();
        int activeCount = 0;
        long maximumSequence = -1;
        long activeSequence = -1;

        for (int index = 0; index < workItems.Count; index++)
        {
            BistroBuilderKitchenLineWorkSaveData item = workItems[index];

            if (item == null)
            {
                error = "El snapshot contiene un trabajo de cocina nulo.";
                return false;
            }

            if (!item.TryValidate(out error))
            {
                return false;
            }

            if (!lineIds.Add(item.orderLineId))
            {
                error = "El snapshot contiene un LineId de cocina duplicado.";
                return false;
            }

            if (!sequences.Add(item.sequence))
            {
                error = "El snapshot contiene una secuencia de cocina duplicada.";
                return false;
            }

            maximumSequence = Math.Max(maximumSequence, item.sequence);

            if (item.wasActive)
            {
                activeCount++;
                activeSequence = item.sequence;
            }
        }

        if (activeCount > 1)
        {
            error = "Una cocina no puede tener más de una línea activa.";
            return false;
        }

        if (workItems.Count > 0 && nextSequence <= maximumSequence)
        {
            error =
                "La siguiente secuencia de cocina debe ser posterior a " +
                "todos los trabajos capturados.";
            return false;
        }

        if (activeCount == 1)
        {
            long minimumSequence = long.MaxValue;

            for (int index = 0; index < workItems.Count; index++)
            {
                minimumSequence = Math.Min(
                    minimumSequence,
                    workItems[index].sequence
                );
            }

            if (activeSequence != minimumSequence)
            {
                error =
                    "La línea activa debe ser el trabajo más antiguo de " +
                    "la cola capturada.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
