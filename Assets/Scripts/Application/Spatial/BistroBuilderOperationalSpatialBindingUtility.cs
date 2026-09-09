using System;
using UnityEngine;

/// <summary>
/// Vinculación universal de cocina y barra basada en geometría funcional,
/// puntos reales y contratos, nunca en nombres de modelos concretos.
/// </summary>
public static class BistroBuilderOperationalSpatialBindingUtility
{
    public static bool BindKitchen(
        KitchenSystem kitchen,
        BistroBuilderSpatialContractDefinition contract,
        string subjectId)
    {
        if (kitchen == null || contract == null ||
            string.IsNullOrWhiteSpace(subjectId))
            return false;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(
                kitchen.gameObject);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(
                kitchen.gameObject);
        BistroBuilderKitchenSpatialAdapter adapter =
            GetOrAdd<BistroBuilderKitchenSpatialAdapter>(
                kitchen.gameObject);
        GetOrAdd<BistroBuilderSpatialPortAnchors>(
            kitchen.gameObject);

        ConfigureKitchenProxy(kitchen, proxy);
        subject.Configure(subjectId, contract, proxy);
        adapter.Configure(kitchen, subject);
        return true;
    }

    public static bool BindBarSpot(
        BistroBuilderBarServiceSpot spot,
        BistroBuilderSpatialContractDefinition contract,
        string subjectId)
    {
        if (spot == null || contract == null ||
            string.IsNullOrWhiteSpace(subjectId))
            return false;

        BistroBuilderAdaptiveSpatialProxy proxy =
            GetOrAdd<BistroBuilderAdaptiveSpatialProxy>(
                spot.gameObject);
        BistroBuilderSpatialSubject subject =
            GetOrAdd<BistroBuilderSpatialSubject>(
                spot.gameObject);
        BistroBuilderBarSpatialAdapter adapter =
            GetOrAdd<BistroBuilderBarSpatialAdapter>(
                spot.gameObject);
        GetOrAdd<BistroBuilderSpatialPortAnchors>(
            spot.gameObject);

        ConfigureBarProxy(spot, proxy);
        subject.Configure(subjectId, contract, proxy);
        adapter.Configure(spot, subject);
        return true;
    }

    private static void ConfigureKitchenProxy(
        KitchenSystem kitchen,
        BistroBuilderAdaptiveSpatialProxy proxy)
    {
        proxy.Configure(
            BistroBuilderAdaptiveSpatialProxyMode.Layered);
        proxy.ClearParts();

        ResolveLocalFootprint(
            kitchen.transform,
            kitchen.PickupPoint,
            new Vector2(2.4f, 1.4f),
            out Vector3 center,
            out Vector2 size);

        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "kitchen.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            shapeKind =
                BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = center,
            size = size
        });
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "kitchen.operational",
            layer = BistroBuilderSpatialProxyLayer.Operational,
            shapeKind =
                BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = center + Vector3.back * 0.2f,
            size = new Vector2(
                Mathf.Max(0.6f, size.x + 0.4f),
                Mathf.Max(0.6f, size.y + 0.8f))
        });
    }

    private static void ConfigureBarProxy(
        BistroBuilderBarServiceSpot spot,
        BistroBuilderAdaptiveSpatialProxy proxy)
    {
        proxy.Configure(
            BistroBuilderAdaptiveSpatialProxyMode.Layered);
        proxy.ClearParts();

        Vector3 customerLocal = spot.transform.InverseTransformPoint(
            spot.CustomerPoint.position);
        Vector3 waiterLocal = spot.transform.InverseTransformPoint(
            spot.WaiterServicePoint.position);
        Vector3 center = (customerLocal + waiterLocal) * 0.5f;
        float depth = Mathf.Max(
            0.35f,
            Mathf.Abs(customerLocal.z - waiterLocal.z));
        float width = Mathf.Max(
            0.45f,
            Mathf.Abs(customerLocal.x - waiterLocal.x) + 0.35f);

        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "bar.body",
            layer = BistroBuilderSpatialProxyLayer.Static,
            shapeKind =
                BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = center,
            size = new Vector2(width, depth)
        });
        proxy.AddPart(new BistroBuilderSpatialProxyPart
        {
            partId = "bar.operational",
            layer = BistroBuilderSpatialProxyLayer.Operational,
            shapeKind =
                BistroBuilderSpatialShapeKind.OrientedBox,
            localCenter = center,
            size = new Vector2(width + 0.4f, depth + 0.55f)
        });
    }

    private static void ResolveLocalFootprint(
        Transform root,
        Transform reference,
        Vector2 fallback,
        out Vector3 localCenter,
        out Vector2 localSize)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(
            true);
        bool found = false;
        Bounds bounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null ||
                renderer is ParticleSystemRenderer)
                continue;
            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (found)
        {
            localCenter = root.InverseTransformPoint(bounds.center);
            Vector3 scale = root.lossyScale;
            localSize = new Vector2(
                bounds.size.x /
                    Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
                bounds.size.z /
                    Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
            localSize.x = Mathf.Max(0.4f, localSize.x);
            localSize.y = Mathf.Max(0.4f, localSize.y);
            return;
        }

        localCenter = reference != null
            ? root.InverseTransformPoint(reference.position) +
              Vector3.forward * fallback.y * 0.5f
            : Vector3.zero;
        localSize = fallback;
    }

    private static T GetOrAdd<T>(
        GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null
            ? component
            : gameObject.AddComponent<T>();
    }
}
