using UnityEngine;

/// <summary>
/// Identidad estable de una silla visual instalada por 368A.
/// Permite que el instalador sea idempotente y que el validador compruebe
/// exactamente una silla por plaza de mesa.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Inventory/368A Installed Chair"
)]
public sealed class BistroBuilder368AInstalledChair : MonoBehaviour
{
    [SerializeField]
    private int tableId;

    [SerializeField]
    private int slotIndex = -1;

    public int TableId => tableId;

    public int SlotIndex => slotIndex;

    public bool ValidateConfiguration(out string error)
    {
        if (tableId <= 0)
        {
            error = name + " no tiene un TableId válido.";
            return false;
        }

        if (slotIndex < 0)
        {
            error = name + " no tiene un SlotIndex válido.";
            return false;
        }

        if (!TryGetComponent(out RestaurantSeat seat))
        {
            error = name + " no contiene RestaurantSeat.";
            return false;
        }

        if (!seat.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public void EditorAssign(int targetTableId, int targetSlotIndex)
    {
        tableId = targetTableId;
        slotIndex = targetSlotIndex;
    }
#endif
}
