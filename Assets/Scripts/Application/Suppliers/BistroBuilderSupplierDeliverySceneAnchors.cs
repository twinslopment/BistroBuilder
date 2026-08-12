using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anclajes de escena para 2.3H. Solo describen la ruta visual; no contienen
/// estado económico ni de inventario.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplierDeliverySceneAnchors : MonoBehaviour
{
    [Header("Vehículo")]
    public Transform vehicleEntry;
    public List<Transform> vehicleEntryWaypoints = new List<Transform>();
    public Transform vehicleParking;
    public List<Transform> vehicleExitWaypoints = new List<Transform>();
    public Transform vehicleExit;

    [Header("Repartidor / almacén")]
    public Transform driverExitPoint;
    public List<Transform> driverToWarehouseWaypoints = new List<Transform>();
    public Transform warehouseDoor;
    public Transform warehouseDropoff;

    public bool IsComplete =>
        vehicleEntry != null &&
        vehicleParking != null &&
        vehicleExit != null &&
        driverExitPoint != null &&
        warehouseDoor != null &&
        warehouseDropoff != null;

    public void BuildVehicleEntryPath(List<Vector3> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        Add(buffer, vehicleEntry);
        AddList(buffer, vehicleEntryWaypoints);
        Add(buffer, vehicleParking);
    }

    public void BuildVehicleExitPath(List<Vector3> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        Add(buffer, vehicleParking);
        AddList(buffer, vehicleExitWaypoints);
        Add(buffer, vehicleExit);
    }

    public void BuildDriverWarehousePath(List<Vector3> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        Add(buffer, driverExitPoint);
        AddList(buffer, driverToWarehouseWaypoints);
        Add(buffer, warehouseDoor);
        Add(buffer, warehouseDropoff);
    }

    public void BuildDriverReturnPath(List<Vector3> buffer)
    {
        BuildDriverWarehousePath(buffer);
        buffer.Reverse();
    }

    private static void AddList(List<Vector3> buffer, List<Transform> source)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++) Add(buffer, source[i]);
    }

    private static void Add(List<Vector3> buffer, Transform point)
    {
        if (point == null) return;
        if (buffer.Count > 0 && Vector3.SqrMagnitude(buffer[buffer.Count - 1] - point.position) < 0.0001f) return;
        buffer.Add(point.position);
    }
}
