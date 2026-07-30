using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de plazas operativas de barra.
///
/// Una plaza representa uno o varios puestos físicos según Capacity. Un grupo
/// puede ocupar varias plazas; la reserva y la liberación se ejecutan de forma
/// atómica para que nunca exista ocupación parcial.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Service/Bar Service Registry")]
public sealed class BistroBuilderBarServiceRegistry : MonoBehaviour
{
    [SerializeField]
    private bool discoverSceneSpotsOnAwake = true;

    [SerializeField]
    private bool logStartupSummary = true;

    private readonly HashSet<BistroBuilderBarServiceSpot> registeredSpots =
        new HashSet<BistroBuilderBarServiceSpot>();

    private readonly Dictionary<string, BistroBuilderBarServiceSpot> byId =
        new Dictionary<string, BistroBuilderBarServiceSpot>(
            StringComparer.Ordinal
        );

    private readonly List<BistroBuilderBarServiceSpot> candidateBuffer =
        new List<BistroBuilderBarServiceSpot>(16);

    private readonly List<BistroBuilderBarServiceSpot> reservationBuffer =
        new List<BistroBuilderBarServiceSpot>(16);

    public event Action<BistroBuilderBarServiceSpot> SpotRegistered;
    public event Action<BistroBuilderBarServiceSpot> SpotUnregistered;

    public IReadOnlyCollection<BistroBuilderBarServiceSpot> RegisteredSpots =>
        registeredSpots;

    public int RegisteredSpotCount => registeredSpots.Count;

    public int FreeSpotCount
    {
        get
        {
            int count = 0;

            foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
            {
                if (spot != null && spot.IsFree)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int FreeCapacity
    {
        get
        {
            int capacity = 0;

            foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
            {
                if (spot != null && spot.IsFree)
                {
                    capacity += spot.Capacity;
                }
            }

            return capacity;
        }
    }

    private void Awake()
    {
        if (discoverSceneSpotsOnAwake)
        {
            DiscoverSceneSpots();
        }
    }

    private void Start()
    {
        if (logStartupSummary)
        {
            Debug.Log(
                "BistroBuilderBarServiceRegistry ha registrado " +
                RegisteredSpotCount + " plaza(s) y " + FreeCapacity +
                " puesto(s) libres de barra.",
                this
            );
        }
    }

    public void RebuildRegistryFromScene()
    {
        registeredSpots.Clear();
        byId.Clear();
        DiscoverSceneSpots();
    }

    public bool RegisterSpot(BistroBuilderBarServiceSpot spot)
    {
        if (spot == null || registeredSpots.Contains(spot))
        {
            return false;
        }

        if (!spot.ValidateConfiguration(out string error))
        {
            Debug.LogError(error, spot);
            return false;
        }

        if (byId.ContainsKey(spot.BarSpotId))
        {
            Debug.LogError(
                "BarSpotId duplicado: " + spot.BarSpotId + ".",
                spot
            );
            return false;
        }

        registeredSpots.Add(spot);
        byId.Add(spot.BarSpotId, spot);
        SpotRegistered?.Invoke(spot);
        return true;
    }

    public bool UnregisterSpot(BistroBuilderBarServiceSpot spot)
    {
        if (spot == null || !registeredSpots.Remove(spot))
        {
            return false;
        }

        if (byId.TryGetValue(
                spot.BarSpotId,
                out BistroBuilderBarServiceSpot indexed
            ) && ReferenceEquals(indexed, spot))
        {
            byId.Remove(spot.BarSpotId);
        }

        SpotUnregistered?.Invoke(spot);
        return true;
    }

    public bool TryGetSpot(
        string barSpotId,
        out BistroBuilderBarServiceSpot spot
    )
    {
        return byId.TryGetValue(
            BistroBuilderOrderIdUtility.Normalize(barSpotId),
            out spot
        );
    }

    public bool TryAllocateSpot(
        CustomerGroup group,
        out BistroBuilderBarServiceSpot spot
    )
    {
        BistroBuilderServiceMode mode = group != null &&
            BistroBuilderServiceModeUtility.IsBarMode(
                group.RequestedServiceMode
            )
                ? group.RequestedServiceMode
                : BistroBuilderServiceMode.WaitingAtBar;

        return TryAllocateSpot(group, mode, out spot);
    }

    /// <summary>
    /// Reserva tantas plazas como sean necesarias para cubrir GroupSize y
    /// devuelve como ancla la más próxima. Si falla cualquier ocupación, toda
    /// la operación se revierte.
    /// </summary>
    public bool TryAllocateSpot(
        CustomerGroup group,
        BistroBuilderServiceMode serviceMode,
        out BistroBuilderBarServiceSpot spot
    )
    {
        spot = null;

        if (group == null || group.GroupSize < 1 ||
            !BistroBuilderServiceModeUtility.IsBarMode(serviceMode) ||
            group.HasAssignedBarSpot)
        {
            return false;
        }

        candidateBuffer.Clear();

        foreach (BistroBuilderBarServiceSpot candidate in registeredSpots)
        {
            if (candidate != null && candidate.CanHost(group))
            {
                candidateBuffer.Add(candidate);
            }
        }

        candidateBuffer.Sort((left, right) =>
        {
            float leftDistance = (
                group.transform.position - left.CustomerPoint.position
            ).sqrMagnitude;
            float rightDistance = (
                group.transform.position - right.CustomerPoint.position
            ).sqrMagnitude;

            int distanceComparison = leftDistance.CompareTo(rightDistance);
            return distanceComparison != 0
                ? distanceComparison
                : string.CompareOrdinal(left.BarSpotId, right.BarSpotId);
        });

        reservationBuffer.Clear();
        int reservedCapacity = 0;

        for (int index = 0;
             index < candidateBuffer.Count &&
             reservedCapacity < group.GroupSize;
             index++)
        {
            BistroBuilderBarServiceSpot candidate = candidateBuffer[index];
            reservationBuffer.Add(candidate);
            reservedCapacity += candidate.Capacity;
        }

        if (reservedCapacity < group.GroupSize || reservationBuffer.Count == 0)
        {
            reservationBuffer.Clear();
            return false;
        }

        int occupiedCount = 0;

        for (int index = 0; index < reservationBuffer.Count; index++)
        {
            if (!reservationBuffer[index].TryOccupy(group))
            {
                for (int rollback = occupiedCount - 1;
                     rollback >= 0;
                     rollback--)
                {
                    reservationBuffer[rollback].TryRelease(group);
                }

                reservationBuffer.Clear();
                return false;
            }

            occupiedCount++;
        }

        spot = reservationBuffer[0];

        if (!group.TryAssignBarSpot(spot, serviceMode))
        {
            for (int rollback = reservationBuffer.Count - 1;
                 rollback >= 0;
                 rollback--)
            {
                reservationBuffer[rollback].TryRelease(group);
            }

            spot = null;
            reservationBuffer.Clear();
            return false;
        }

        reservationBuffer.Clear();
        return true;
    }


    public bool TryRestoreGroupAllocation(
        CustomerGroup group,
        string anchorBarSpotId,
        IList<string> occupiedSpotIds,
        BistroBuilderServiceMode serviceMode,
        out string error
    )
    {
        error = string.Empty;

        if (group == null || occupiedSpotIds == null ||
            occupiedSpotIds.Count == 0 || group.HasAssignedBarSpot ||
            !BistroBuilderServiceModeUtility.IsBarMode(serviceMode))
        {
            error = "La ocupación persistente de barra no es válida.";
            return false;
        }

        reservationBuffer.Clear();
        int capacity = 0;

        for (int index = 0; index < occupiedSpotIds.Count; index++)
        {
            if (!TryGetSpot(occupiedSpotIds[index], out var spot) ||
                spot == null || !spot.IsFree || reservationBuffer.Contains(spot))
            {
                error = "No se pudo reconstruir una plaza de barra.";
                reservationBuffer.Clear();
                return false;
            }

            reservationBuffer.Add(spot);
            capacity += spot.Capacity;
        }

        if (capacity < group.GroupSize ||
            !TryGetSpot(anchorBarSpotId, out var anchor) ||
            anchor == null || !reservationBuffer.Contains(anchor))
        {
            error = "La ocupación persistente no cubre el grupo o su ancla.";
            reservationBuffer.Clear();
            return false;
        }

        int occupied = 0;

        for (int index = 0; index < reservationBuffer.Count; index++)
        {
            if (!reservationBuffer[index].TryOccupy(group))
            {
                for (int rollback = occupied - 1; rollback >= 0; rollback--)
                {
                    reservationBuffer[rollback].TryRelease(group);
                }
                error = "La ocupación de barra no pudo aplicarse atómicamente.";
                reservationBuffer.Clear();
                return false;
            }
            occupied++;
        }

        if (!group.TryAssignBarSpot(anchor, serviceMode))
        {
            for (int rollback = reservationBuffer.Count - 1; rollback >= 0; rollback--)
            {
                reservationBuffer[rollback].TryRelease(group);
            }
            error = "No se pudo restaurar la plaza ancla del grupo.";
            reservationBuffer.Clear();
            return false;
        }

        reservationBuffer.Clear();
        return true;
    }

    public int GetReservedCapacity(CustomerGroup group)
    {
        if (group == null)
        {
            return 0;
        }

        int capacity = 0;

        foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
        {
            if (spot != null &&
                ReferenceEquals(spot.AssignedCustomerGroup, group))
            {
                capacity += spot.Capacity;
            }
        }

        return capacity;
    }

    public int GetOccupiedSpots(
        CustomerGroup group,
        List<BistroBuilderBarServiceSpot> destination
    )
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();

        if (group == null)
        {
            return 0;
        }

        foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
        {
            if (spot != null &&
                ReferenceEquals(spot.AssignedCustomerGroup, group))
            {
                destination.Add(spot);
            }
        }

        destination.Sort((left, right) =>
            string.CompareOrdinal(left.BarSpotId, right.BarSpotId));
        return destination.Count;
    }

    public bool ReleaseGroup(CustomerGroup group)
    {
        if (group == null)
        {
            return false;
        }

        bool releasedAny = false;

        foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
        {
            if (spot != null &&
                ReferenceEquals(spot.AssignedCustomerGroup, group))
            {
                releasedAny |= spot.TryRelease(group);
            }
        }

        if (group.HasAssignedBarSpot)
        {
            group.TryClearBarSpotAssignmentAfterRegistryRelease();
        }

        return releasedAny;
    }

    public bool ValidateConfiguration(out string error)
    {
        // El validador debe asignar siempre el parámetro out, incluso si
        // encuentra una referencia nula antes de delegar otra validación.
        error = string.Empty;

        if (registeredSpots.Count == 0)
        {
            RebuildRegistryFromScene();
        }

        if (registeredSpots.Count == 0)
        {
            error = "No existe ninguna plaza de barra registrada.";
            return false;
        }

        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<CustomerGroup, int> capacityByGroup =
            new Dictionary<CustomerGroup, int>();
        Dictionary<CustomerGroup, bool> anchorFoundByGroup =
            new Dictionary<CustomerGroup, bool>();

        foreach (BistroBuilderBarServiceSpot spot in registeredSpots)
        {
            if (spot == null)
            {
                error = "El registro de barra contiene una plaza nula.";
                return false;
            }

            if (!spot.ValidateConfiguration(out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = "Una plaza de barra registrada no es válida.";
                }
                return false;
            }

            if (!unique.Add(spot.BarSpotId))
            {
                error = "BarSpotId duplicado: " + spot.BarSpotId + ".";
                return false;
            }

            CustomerGroup group = spot.AssignedCustomerGroup;

            if (group == null)
            {
                continue;
            }

            if (!capacityByGroup.ContainsKey(group))
            {
                capacityByGroup.Add(group, 0);
                anchorFoundByGroup.Add(group, false);
            }

            capacityByGroup[group] += spot.Capacity;

            if (ReferenceEquals(group.AssignedBarSpot, spot))
            {
                anchorFoundByGroup[group] = true;
            }
        }

        foreach (KeyValuePair<CustomerGroup, int> pair in capacityByGroup)
        {
            CustomerGroup group = pair.Key;

            if (group == null || !group.HasAssignedBarSpot ||
                !anchorFoundByGroup[group])
            {
                error = "La ocupación de barra no conserva una plaza ancla.";
                return false;
            }

            if (pair.Value < group.GroupSize)
            {
                error = "La barra no cubre la capacidad del grupo " +
                    group.GroupId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private void DiscoverSceneSpots()
    {
        BistroBuilderBarServiceSpot[] spots =
            FindObjectsByType<BistroBuilderBarServiceSpot>(
                FindObjectsSortMode.InstanceID
            );

        for (int index = 0; index < spots.Length; index++)
        {
            RegisterSpot(spots[index]);
        }
    }
}
