using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Estado persistible de un cliente dentro de una comanda activa.
///
/// consumedLineIds representa la reclamación de consumo de ese cliente. Una
/// línea compartida solo pasa a Consumed cuando todos sus consumidores han
/// registrado su reclamación.
/// </summary>
[Serializable]
public sealed class BistroBuilderCustomerDiningCustomerRuntime
{
    [SerializeField]
    private string customerId;

    [SerializeField]
    private BistroBuilderCustomerDiningCustomerState state;

    [SerializeField]
    private int currentCourseIndex;

    [SerializeField]
    private float remainingEatingSeconds;

    [SerializeField]
    private List<string> consumedLineIds = new List<string>();

    [SerializeField]
    private int revision;

    public string CustomerId => customerId ?? string.Empty;
    public BistroBuilderCustomerDiningCustomerState State => state;
    public int CurrentCourseIndex => currentCourseIndex;
    public float RemainingEatingSeconds => remainingEatingSeconds;
    public IReadOnlyList<string> ConsumedLineIds => consumedLineIds;
    public int Revision => revision;
    public bool IsTerminal => BistroBuilderCustomerDiningPolicy.IsTerminal(state);

    internal BistroBuilderCustomerDiningCustomerRuntime(
        string customerId,
        int firstCourseIndex
    )
    {
        this.customerId = BistroBuilderOrderIdUtility.Normalize(customerId);
        state = BistroBuilderCustomerDiningCustomerState.WaitingForDish;
        currentCourseIndex = firstCourseIndex;
        remainingEatingSeconds = 0f;
        consumedLineIds = new List<string>();
        revision = 0;
    }

    private BistroBuilderCustomerDiningCustomerRuntime()
    {
    }

    public bool HasConsumedLine(string lineId)
    {
        return BistroBuilderCustomerDiningPolicy.ContainsNormalizedId(
            consumedLineIds,
            lineId
        );
    }

    internal bool TryStartCourse(
        int courseIndex,
        float durationSeconds,
        out string error
    )
    {
        if (IsTerminal)
        {
            error = "El cliente ya está en un estado terminal.";
            return false;
        }

        if (state == BistroBuilderCustomerDiningCustomerState.Eating)
        {
            error = "El cliente ya está comiendo.";
            return false;
        }

        if (courseIndex < 0 || courseIndex > 20)
        {
            error = "El pase del cliente no es válido.";
            return false;
        }

        if (float.IsNaN(durationSeconds) ||
            float.IsInfinity(durationSeconds) ||
            durationSeconds <= 0f)
        {
            error = "La duración de consumo no es válida.";
            return false;
        }

        currentCourseIndex = courseIndex;
        remainingEatingSeconds = durationSeconds;
        state = BistroBuilderCustomerDiningCustomerState.Eating;
        revision++;
        error = string.Empty;
        return true;
    }

    internal bool AdvanceTime(float deltaSeconds)
    {
        if (state != BistroBuilderCustomerDiningCustomerState.Eating ||
            deltaSeconds <= 0f)
        {
            return false;
        }

        remainingEatingSeconds = Mathf.Max(
            0f,
            remainingEatingSeconds - deltaSeconds
        );

        return remainingEatingSeconds <= 0f;
    }

    internal bool AddConsumedLineClaim(string lineId)
    {
        string normalized = BistroBuilderOrderIdUtility.Normalize(lineId);

        if (!BistroBuilderOrderIdUtility.IsValid(normalized) ||
            HasConsumedLine(normalized))
        {
            return false;
        }

        consumedLineIds.Add(normalized);
        consumedLineIds.Sort(StringComparer.Ordinal);
        revision++;
        return true;
    }

    internal void SetWaitingForCourse(int courseIndex)
    {
        currentCourseIndex = courseIndex;
        remainingEatingSeconds = 0f;
        state = BistroBuilderCustomerDiningCustomerState.WaitingForDish;
        revision++;
    }

    internal void SetCompleted()
    {
        remainingEatingSeconds = 0f;
        state = BistroBuilderCustomerDiningCustomerState.Completed;
        revision++;
    }

    internal void SetCancelled()
    {
        remainingEatingSeconds = 0f;
        state = BistroBuilderCustomerDiningCustomerState.Cancelled;
        revision++;
    }

    internal void SetFailed()
    {
        remainingEatingSeconds = 0f;
        state = BistroBuilderCustomerDiningCustomerState.Failed;
        revision++;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(CustomerId))
        {
            error = "El runtime de cliente contiene un CustomerId inválido.";
            return false;
        }

        if (currentCourseIndex < 0 || currentCourseIndex > 20)
        {
            error = "El runtime de cliente contiene un pase inválido.";
            return false;
        }

        if (float.IsNaN(remainingEatingSeconds) ||
            float.IsInfinity(remainingEatingSeconds) ||
            remainingEatingSeconds < 0f)
        {
            error = "El runtime de cliente contiene un tiempo inválido.";
            return false;
        }

        if (state == BistroBuilderCustomerDiningCustomerState.Eating &&
            remainingEatingSeconds <= 0f)
        {
            error = "Un cliente Eating debe conservar tiempo pendiente.";
            return false;
        }

        if (state != BistroBuilderCustomerDiningCustomerState.Eating &&
            remainingEatingSeconds > 0f)
        {
            error = "Solo un cliente Eating puede conservar tiempo pendiente.";
            return false;
        }

        if (consumedLineIds == null)
        {
            error = "La colección de líneas consumidas es nula.";
            return false;
        }

        HashSet<string> unique =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < consumedLineIds.Count; index++)
        {
            string normalized = BistroBuilderOrderIdUtility.Normalize(
                consumedLineIds[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(normalized))
            {
                error = "El cliente contiene un LineId consumido inválido.";
                return false;
            }

            if (!unique.Add(normalized))
            {
                error = "El cliente contiene un LineId consumido duplicado.";
                return false;
            }

            consumedLineIds[index] = normalized;
        }

        if (revision < 0)
        {
            error = "La revisión del cliente no es válida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal BistroBuilderCustomerDiningCustomerRuntime Clone()
    {
        return new BistroBuilderCustomerDiningCustomerRuntime
        {
            customerId = CustomerId,
            state = state,
            currentCourseIndex = currentCourseIndex,
            remainingEatingSeconds = remainingEatingSeconds,
            consumedLineIds = new List<string>(consumedLineIds),
            revision = revision
        };
    }
}

/// <summary>
/// Estado persistible del consumo individual de una comanda activa.
/// </summary>
[Serializable]
public sealed class BistroBuilderCustomerDiningOrderRuntime
{
    [SerializeField]
    private string orderId;

    [SerializeField]
    private int legacyOrderId;

    [SerializeField]
    private string customerGroupReferenceId;

    [SerializeField]
    private string tableReferenceId;

    [SerializeField]
    private bool billRequested;

    [SerializeField]
    private int revision;

    [SerializeField]
    private List<BistroBuilderCustomerDiningCustomerRuntime> customers =
        new List<BistroBuilderCustomerDiningCustomerRuntime>();

    public string OrderId => orderId ?? string.Empty;
    public int LegacyOrderId => legacyOrderId;
    public string CustomerGroupReferenceId =>
        customerGroupReferenceId ?? string.Empty;
    public string TableReferenceId => tableReferenceId ?? string.Empty;
    public bool BillRequested => billRequested;
    public int Revision => revision;
    public IReadOnlyList<BistroBuilderCustomerDiningCustomerRuntime> Customers =>
        customers;

    public bool AllCustomersCompleted
    {
        get
        {
            if (customers == null || customers.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < customers.Count; index++)
            {
                BistroBuilderCustomerDiningCustomerState state =
                    customers[index].State;

                if (state != BistroBuilderCustomerDiningCustomerState.Completed &&
                    state != BistroBuilderCustomerDiningCustomerState.Cancelled)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal BistroBuilderCustomerDiningOrderRuntime(
        string orderId,
        int legacyOrderId,
        string customerGroupReferenceId,
        string tableReferenceId,
        IList<BistroBuilderCustomerDiningCustomerRuntime> sourceCustomers
    )
    {
        this.orderId = BistroBuilderOrderIdUtility.Normalize(orderId);
        this.legacyOrderId = legacyOrderId;
        this.customerGroupReferenceId =
            BistroBuilderOrderIdUtility.Normalize(customerGroupReferenceId);
        this.tableReferenceId =
            BistroBuilderOrderIdUtility.Normalize(tableReferenceId);
        billRequested = false;
        revision = 0;
        customers = new List<BistroBuilderCustomerDiningCustomerRuntime>(
            sourceCustomers != null ? sourceCustomers.Count : 0
        );

        if (sourceCustomers != null)
        {
            for (int index = 0; index < sourceCustomers.Count; index++)
            {
                if (sourceCustomers[index] != null)
                {
                    customers.Add(sourceCustomers[index].Clone());
                }
            }
        }
    }

    private BistroBuilderCustomerDiningOrderRuntime()
    {
    }

    public bool TryGetCustomer(
        string customerId,
        out BistroBuilderCustomerDiningCustomerRuntime customer
    )
    {
        customer = null;
        string normalized = BistroBuilderOrderIdUtility.Normalize(customerId);

        if (customers == null)
        {
            return false;
        }

        for (int index = 0; index < customers.Count; index++)
        {
            BistroBuilderCustomerDiningCustomerRuntime candidate =
                customers[index];

            if (candidate != null &&
                string.Equals(
                    candidate.CustomerId,
                    normalized,
                    StringComparison.Ordinal
                ))
            {
                customer = candidate;
                return true;
            }
        }

        return false;
    }

    internal void IncrementRevision()
    {
        revision++;
    }

    internal void MarkBillRequested()
    {
        if (billRequested)
        {
            return;
        }

        billRequested = true;
        revision++;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderOrderIdUtility.IsValid(OrderId))
        {
            error = "El runtime de consumo contiene un OrderId inválido.";
            return false;
        }

        if (legacyOrderId < 1)
        {
            error = "El runtime de consumo contiene un OrderId legacy inválido.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(CustomerGroupReferenceId) ||
            !BistroBuilderOrderIdUtility.IsValid(TableReferenceId))
        {
            error = "El runtime de consumo contiene referencias inválidas.";
            return false;
        }

        if (customers == null || customers.Count == 0)
        {
            error = "El runtime de consumo no contiene clientes.";
            return false;
        }

        HashSet<string> unique =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < customers.Count; index++)
        {
            BistroBuilderCustomerDiningCustomerRuntime customer =
                customers[index];

            if (customer == null)
            {
                error = "El runtime de consumo contiene un cliente nulo.";
                return false;
            }

            if (!customer.TryValidate(out error))
            {
                return false;
            }

            if (!unique.Add(customer.CustomerId))
            {
                error = "El runtime contiene un CustomerId duplicado.";
                return false;
            }
        }

        if (billRequested && !AllCustomersCompleted)
        {
            error = "No puede solicitarse la cuenta con clientes pendientes.";
            return false;
        }

        if (revision < 0)
        {
            error = "La revisión del runtime de consumo no es válida.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal BistroBuilderCustomerDiningOrderRuntime Clone()
    {
        BistroBuilderCustomerDiningOrderRuntime clone =
            new BistroBuilderCustomerDiningOrderRuntime(
                OrderId,
                legacyOrderId,
                CustomerGroupReferenceId,
                TableReferenceId,
                customers
            )
            {
                billRequested = billRequested,
                revision = revision
            };

        return clone;
    }
}

/// <summary>
/// Fotografía versionada del consumo individual preparada para service.runtime.
/// </summary>
[Serializable]
public sealed class BistroBuilderCustomerDiningRuntimeSnapshot
{
    public const int CurrentSchemaVersion = 1;

    [SerializeField]
    private int schemaVersion = CurrentSchemaVersion;

    [SerializeField]
    private List<BistroBuilderCustomerDiningOrderRuntime> orders =
        new List<BistroBuilderCustomerDiningOrderRuntime>();

    public int SchemaVersion => schemaVersion;
    public IReadOnlyList<BistroBuilderCustomerDiningOrderRuntime> Orders =>
        orders;

    public BistroBuilderCustomerDiningRuntimeSnapshot(
        IList<BistroBuilderCustomerDiningOrderRuntime> source
    )
    {
        orders = new List<BistroBuilderCustomerDiningOrderRuntime>(
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

    private BistroBuilderCustomerDiningRuntimeSnapshot()
    {
    }

    public bool TryValidate(out string error)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            error = "La versión del snapshot de consumo no es compatible.";
            return false;
        }

        if (orders == null)
        {
            error = "La colección del snapshot de consumo es nula.";
            return false;
        }

        HashSet<string> orderIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<int> legacyOrderIds = new HashSet<int>();

        for (int index = 0; index < orders.Count; index++)
        {
            BistroBuilderCustomerDiningOrderRuntime order = orders[index];

            if (order == null)
            {
                error = "El snapshot de consumo contiene una comanda nula.";
                return false;
            }

            if (!order.TryValidate(out error))
            {
                return false;
            }

            if (!orderIds.Add(order.OrderId) ||
                !legacyOrderIds.Add(order.LegacyOrderId))
            {
                error = "El snapshot de consumo contiene identidades duplicadas.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public BistroBuilderCustomerDiningRuntimeSnapshot Clone()
    {
        return new BistroBuilderCustomerDiningRuntimeSnapshot(orders);
    }
}
