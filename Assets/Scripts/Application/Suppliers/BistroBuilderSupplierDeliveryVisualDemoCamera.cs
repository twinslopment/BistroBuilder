#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Cámara temporal exclusiva del demo visual 2.3H5.
/// Solo existe en Editor/Play Mode y no forma parte del gameplay canónico.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplierDeliveryVisualDemoCamera : MonoBehaviour
{
    private BistroBuilderSupplierDeliveryPresentationController controller;
    private Vector3 worldOffset = new Vector3(11.5f, 8.5f, -11.5f);
    private float smooth = 4.5f;
    private bool initialized;

    public void Initialize(BistroBuilderSupplierDeliveryPresentationController sourceController)
    {
        controller = sourceController;
        initialized = controller != null;
        if (initialized)
        {
            Transform target = ResolveTarget();
            if (target != null)
            {
                transform.position = target.position + worldOffset;
                transform.LookAt(target.position + Vector3.up * 1.15f);
            }
        }
    }

    private void LateUpdate()
    {
        if (!initialized || controller == null) return;
        Transform target = ResolveTarget();
        if (target == null) return;

        Vector3 desired = target.position + worldOffset;
        float t = 1f - Mathf.Exp(-smooth * Mathf.Max(0.0001f, Time.unscaledDeltaTime));
        transform.position = Vector3.Lerp(transform.position, desired, t);

        Vector3 lookPoint = target.position + Vector3.up * 1.15f;
        Vector3 direction = lookPoint - transform.position;
        if (direction.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }
    }

    private Transform ResolveTarget()
    {
        BistroBuilderSupplierDeliveryPresentationRecord record = controller.Record;
        if (record == null) return controller.VehicleObject != null ? controller.VehicleObject.transform : null;

        bool driverPhase =
            record.state == BistroBuilderSupplierDeliveryPresentationState.DriverExiting ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.OpeningRearDoors ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.PreparingTrolley ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.GoingToWarehouse ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.Unloading ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.ReturningToVehicle ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.StowingTrolley ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.ClosingRearDoors ||
            record.state == BistroBuilderSupplierDeliveryPresentationState.DriverEnteringVehicle;

        if (driverPhase && controller.DriverObject != null && controller.DriverObject.activeInHierarchy)
            return controller.DriverObject.transform;

        return controller.VehicleObject != null ? controller.VehicleObject.transform : null;
    }
}
#endif
