using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Construye la representación BBSIS de assets funcionales reales sin
/// depender de su malla de render ni de reglas por prefab concreto.
/// </summary>
public static class BistroBuilderSpatialBindingUtility
{
    public static bool BindSeat(
        RestaurantSeat seat,
        BistroBuilderSpatialContractDefinition contract,
        string subjectId)
    {
        if (seat == null || contract == null || string.IsNullOrWhiteSpace(subjectId))
            return false;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(seat.gameObject);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(seat.gameObject);
        BistroBuilderSpatialPortAnchors anchors =
            GetOrAdd<BistroBuilderSpatialPortAnchors>(seat.gameObject);
        BistroBuilderSeatSpatialAdapter adapter =
            GetOrAdd<BistroBuilderSeatSpatialAdapter>(seat.gameObject);

        ConfigureSeatProxy(seat, proxy);
        subject.Configure(subjectId, contract, proxy);
        anchors.ClearBindings();
        anchors.AddBinding("seat", seat.SeatPoint);
        anchors.AddBinding("approach", seat.CustomerApproachPoint);
        adapter.Configure(seat, subject);
        return true;
    }

    public static bool BindTable(
        RestaurantTableSeatingConfiguration table,
        BistroBuilderSpatialContractDefinition contract,
        string subjectId)
    {
        if (table == null || contract == null || string.IsNullOrWhiteSpace(subjectId))
            return false;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(table.gameObject);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(table.gameObject);
        BistroBuilderTableSpatialAdapter adapter =
            GetOrAdd<BistroBuilderTableSpatialAdapter>(table.gameObject);

        ConfigureTableProxy(table, proxy);
        subject.Configure(subjectId, contract, proxy);
        adapter.Configure(table, subject);
        return true;
    }

    public static bool BindDoor(
        BistroBuilderNavigableDoor door,
        BistroBuilderSpatialContractDefinition contract,
        string subjectId)
    {
        if (door == null || contract == null || string.IsNullOrWhiteSpace(subjectId))
            return false;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(door.gameObject);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(door.gameObject);
        BistroBuilderDoorSpatialAdapter adapter =
            GetOrAdd<BistroBuilderDoorSpatialAdapter>(door.gameObject);

        ConfigureDoorProxy(door, proxy);
        subject.Configure(subjectId, contract, proxy);
        adapter.Configure(door, subject);
        return true;
    }

    private static void ConfigureSeatProxy(
        RestaurantSeat seat,
        BistroBuilderAdaptiveSpatialProxy proxy)
    {
        proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Articulated);
        proxy.ClearParts();
        RestaurantPlacementFootprint footprint =
            seat.GetComponent<RestaurantPlacementFootprint>();
        Transform motionRoot = seat.OperationalMotionRoot != null
            ? seat.OperationalMotionRoot
            : seat.transform;

        Vector3 localCenter = footprint != null
            ? motionRoot.InverseTransformPoint(footprint.BuildCurrentShape().Center)
            : Vector3.zero;
        Vector2 baseSize = footprint != null
            ? footprint.Size
            : new Vector2(0.5f, 0.5f);
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "chair.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            anchor = motionRoot,
            localCenter = localCenter,
            size = baseSize
        });

        RestaurantSeatUseProfileDefinition profile = seat.UseProfile;
        float pullOut = profile != null ? profile.PullOutDistance : 0.35f;
        float approachRadius = profile != null ? profile.CustomerApproachRadius : 0.25f;
        Vector3 facing = seat.CalculateFacingDirectionAtPose(seat.transform.rotation);
        Vector3 away = -facing;
        Vector3 sweepCenterWorld = seat.transform.position + away * (pullOut * 0.5f);
        Vector3 sweepLocalCenter = seat.transform.InverseTransformPoint(sweepCenterWorld);
        Vector2 sweepSize = ResolveSeatSweepLocalSize(seat, footprint, pullOut, approachRadius);

        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "chair.operational",
            layer = BistroBuilderSpatialProxyLayer.Operational,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = sweepLocalCenter,
            size = sweepSize
        });
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "chair.sweep",
            layer = BistroBuilderSpatialProxyLayer.Dynamic,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = sweepLocalCenter,
            size = sweepSize
        });
    }

    private static Vector2 ResolveSeatSweepLocalSize(
        RestaurantSeat seat,
        RestaurantPlacementFootprint footprint,
        float pullOut,
        float approachRadius)
    {
        Vector3 scale = seat.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(scale.z));
        RestaurantPlacementShape shape = footprint != null
            ? footprint.BuildCurrentShape()
            : default;
        float worldWidth = footprint != null
            ? shape.HalfWidth * 2f
            : 0.5f;
        float worldDepth = footprint != null
            ? shape.HalfDepth * 2f
            : 0.5f;
        worldWidth = Mathf.Max(worldWidth, approachRadius * 2f);
        worldDepth = Mathf.Max(0.35f, worldDepth + Mathf.Max(0f, pullOut));
        return new Vector2(worldWidth / sx, worldDepth / sz);
    }

    private static void ConfigureTableProxy(
        RestaurantTableSeatingConfiguration table,
        BistroBuilderAdaptiveSpatialProxy proxy)
    {
        proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Layered);
        proxy.ClearParts();
        RestaurantPlacementFootprint footprint = table.PlacementFootprint;
        Vector3 localCenter = footprint != null ? footprint.LocalCenter : Vector3.zero;
        Vector2 baseSize = footprint != null ? footprint.Size : Vector2.one;
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "table.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = localCenter,
            size = baseSize
        });

        Vector3 scale = table.transform.lossyScale;
        float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x));
        float sz = Mathf.Max(0.0001f, Mathf.Abs(scale.z));
        const float worldMargin = 0.12f;
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "table.operational",
            layer = BistroBuilderSpatialProxyLayer.Operational,
            shapeKind = BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = localCenter,
            size = new Vector2(
                baseSize.x + worldMargin * 2f / sx,
                baseSize.y + worldMargin * 2f / sz)
        });
    }

    private static void ConfigureDoorProxy(
        BistroBuilderNavigableDoor door,
        BistroBuilderAdaptiveSpatialProxy proxy)
    {
        proxy.Configure(BistroBuilderAdaptiveSpatialProxyMode.Simple);
        proxy.ClearParts();
        NavMeshObstacle obstacle = door.GetComponent<NavMeshObstacle>();
        BistroBuilderSpatialProxyPart part = new BistroBuilderSpatialProxyPart
        {
            partId = "door.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            anchor = door.transform
        };
        if (obstacle != null && obstacle.shape == NavMeshObstacleShape.Capsule)
        {
            part.shapeKind = BistroBuilderSpatialShapeKind.Circle;
            part.localCenter = obstacle.center;
            part.radius = Mathf.Max(0.05f, obstacle.radius);
        }
        else
        {
            part.shapeKind = BistroBuilderSpatialShapeKind.OrientedBox;
            part.localCenter = obstacle != null ? obstacle.center : Vector3.zero;
            Vector3 size = obstacle != null ? obstacle.size : new Vector3(0.9f, 2f, 0.12f);
            part.size = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.z));
        }
        proxy.AddPart(part);
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }
}
