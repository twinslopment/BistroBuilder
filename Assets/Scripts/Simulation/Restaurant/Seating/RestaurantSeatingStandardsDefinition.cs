using System;
using UnityEngine;

/// <summary>
/// Medida estándar documentada para una mesa redonda.
/// </summary>
[Serializable]
public struct RestaurantRoundTableStandard
{
    [SerializeField]
    [Min(1)]
    private int capacity;

    [SerializeField]
    [Min(0f)]
    private float diameterMetres;

    [SerializeField]
    private bool diameterIsApproved;

    public int Capacity => Mathf.Max(1, capacity);

    public float DiameterMetres => Mathf.Max(0f, diameterMetres);

    public bool DiameterIsApproved =>
        diameterIsApproved &&
        DiameterMetres > 0f;

    public RestaurantRoundTableStandard(
        int capacity,
        float diameterMetres,
        bool diameterIsApproved
    )
    {
        this.capacity = Mathf.Max(1, capacity);
        this.diameterMetres = Mathf.Max(0f, diameterMetres);
        this.diameterIsApproved =
            diameterIsApproved &&
            this.diameterMetres > 0f;
    }
}

/// <summary>
/// Estándares de diseño aprobados para futuras familias de mesas.
/// </summary>
[CreateAssetMenu(
    fileName = "RestaurantSeatingStandards",
    menuName = "Bistro Builder/Restaurant/Seating Standards"
)]
public sealed class RestaurantSeatingStandardsDefinition :
    ScriptableObject
{
    [SerializeField]
    private int[] rectangularCapacities =
    {
        2,
        4,
        6
    };

    [SerializeField]
    private RestaurantRoundTableStandard[] roundTableStandards =
    {
        new RestaurantRoundTableStandard(4, 1.00f, true),
        new RestaurantRoundTableStandard(6, 1.20f, true),
        new RestaurantRoundTableStandard(8, 1.50f, true),
        new RestaurantRoundTableStandard(10, 0f, false)
    };

    public bool SupportsRectangularCapacity(int capacity)
    {
        if (rectangularCapacities == null)
        {
            return false;
        }

        for (int index = 0;
             index < rectangularCapacities.Length;
             index++)
        {
            if (rectangularCapacities[index] == capacity)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetRoundTableStandard(
        int capacity,
        out RestaurantRoundTableStandard standard
    )
    {
        standard = default;

        if (roundTableStandards == null)
        {
            return false;
        }

        for (int index = 0;
             index < roundTableStandards.Length;
             index++)
        {
            if (roundTableStandards[index].Capacity != capacity)
            {
                continue;
            }

            standard = roundTableStandards[index];
            return true;
        }

        return false;
    }
}
