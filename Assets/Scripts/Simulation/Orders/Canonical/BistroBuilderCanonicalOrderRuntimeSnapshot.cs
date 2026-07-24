using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fotografía autocontenida del módulo de comandas.
///
/// No se registra aún como sección de guardado porque el restaurante abierto
/// todavía no es cargable. service.runtime reutilizará este contrato sin
/// cambiar el dominio ni los IDs.
/// </summary>
[Serializable]
public sealed class BistroBuilderCanonicalOrderRuntimeSnapshot
{
    public const int CurrentSchemaVersion = 1;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private long nextSequenceNumber = 1;

    [SerializeField]
    private List<BistroBuilderCanonicalOrder> orders =
        new List<BistroBuilderCanonicalOrder>();

    public int SchemaVersion => schemaVersion;
    public long NextSequenceNumber => nextSequenceNumber;
    public IReadOnlyList<BistroBuilderCanonicalOrder> Orders => orders;

    public BistroBuilderCanonicalOrderRuntimeSnapshot(
        long nextSequenceNumber,
        IList<BistroBuilderCanonicalOrder> source
    )
    {
        this.nextSequenceNumber = nextSequenceNumber;
        orders = new List<BistroBuilderCanonicalOrder>(
            source != null ? source.Count : 0
        );

        if (source != null)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] != null)
                {
                    orders.Add(source[index].Clone());
                }
            }
        }
    }

    private BistroBuilderCanonicalOrderRuntimeSnapshot()
    {
    }

    public BistroBuilderCanonicalOrderRuntimeSnapshot Clone()
    {
        return new BistroBuilderCanonicalOrderRuntimeSnapshot(
            nextSequenceNumber,
            orders
        );
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            error = "La versión del snapshot de comandas no es compatible.";
            return false;
        }

        if (nextSequenceNumber < 1)
        {
            error = "El siguiente número de comanda no es válido.";
            return false;
        }

        if (orders == null)
        {
            error = "La colección de comandas del snapshot es nula.";
            return false;
        }

        HashSet<string> orderIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> lineIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int orderIndex = 0;
             orderIndex < orders.Count;
             orderIndex++)
        {
            BistroBuilderCanonicalOrder order = orders[orderIndex];

            if (order == null)
            {
                error = "El snapshot contiene una comanda nula.";
                return false;
            }

            if (!order.TryValidate(out error))
            {
                return false;
            }

            if (!orderIds.Add(order.OrderId))
            {
                error = "El snapshot contiene un OrderId duplicado.";
                return false;
            }

            for (int lineIndex = 0;
                 lineIndex < order.Lines.Count;
                 lineIndex++)
            {
                if (!lineIds.Add(order.Lines[lineIndex].LineId))
                {
                    error = "El snapshot contiene un LineId duplicado.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }
}
