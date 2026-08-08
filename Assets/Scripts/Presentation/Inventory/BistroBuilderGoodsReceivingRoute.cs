using UnityEngine;

/// <summary>
/// Dos anclajes de escena para la representación visual 2.2B:
/// acceso de suministros y punto de descarga del único almacén genérico.
///
/// No representa una red logística ni una colección de almacenes.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Inventory/Goods Receiving Route")]
public sealed class BistroBuilderGoodsReceivingRoute : MonoBehaviour
{
    [SerializeField]
    private Transform supplyAccessPoint;

    [SerializeField]
    private Transform warehouseDropPoint;

    public Transform SupplyAccessPoint => supplyAccessPoint;

    public Transform WarehouseDropPoint => warehouseDropPoint;

    public string WarehouseId => BistroBuilderGoodsReceivingIds.PrimaryWarehouse;

    public string SupplyAccessId =>
        BistroBuilderGoodsReceivingIds.PrimarySupplyAccess;

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (supplyAccessPoint == null)
        {
            error = "Falta el punto del acceso de suministros.";
            return false;
        }

        if (warehouseDropPoint == null)
        {
            error = "Falta el punto de descarga del almacén genérico.";
            return false;
        }

        if (supplyAccessPoint == warehouseDropPoint)
        {
            error = "El acceso de suministros y el almacén no pueden usar " +
                    "el mismo Transform.";
            return false;
        }

        Vector3 delta = warehouseDropPoint.position - supplyAccessPoint.position;
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.25f)
        {
            error = "El acceso de suministros y el almacén están demasiado " +
                    "próximos para representar el reparto.";
            return false;
        }

        return true;
    }

    public Vector3 GetExteriorSpawnPosition(float exteriorDistance)
    {
        Vector3 outward = supplyAccessPoint != null && warehouseDropPoint != null
            ? supplyAccessPoint.position - warehouseDropPoint.position
            : -transform.forward;
        outward.y = 0f;

        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = -transform.forward;
            outward.y = 0f;
        }

        if (outward.sqrMagnitude < 0.0001f)
        {
            outward = Vector3.back;
        }

        outward.Normalize();
        return supplyAccessPoint.position +
               outward * Mathf.Max(0.25f, exteriorDistance);
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (supplyAccessPoint != null)
        {
            Gizmos.color = new Color(0.20f, 0.75f, 1f, 0.95f);
            Gizmos.DrawWireSphere(supplyAccessPoint.position, 0.22f);
        }

        if (warehouseDropPoint != null)
        {
            Gizmos.color = new Color(1f, 0.72f, 0.18f, 0.95f);
            Gizmos.DrawWireCube(
                warehouseDropPoint.position + Vector3.up * 0.15f,
                new Vector3(0.42f, 0.30f, 0.42f)
            );
        }

        if (supplyAccessPoint != null && warehouseDropPoint != null)
        {
            Gizmos.color = new Color(0.50f, 0.90f, 0.50f, 0.80f);
            Gizmos.DrawLine(
                supplyAccessPoint.position,
                warehouseDropPoint.position
            );
        }
    }
#endif

}
