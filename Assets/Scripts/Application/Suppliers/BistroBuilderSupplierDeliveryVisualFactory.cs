using System;
using System.Collections.Generic;
using UnityEngine;

public static class BistroBuilderSupplierDeliveryVisualFactory
{
    public static GameObject CreateVehicle(
        BistroBuilderSupplierVehiclePreference vehicle,
        BistroBuilderSupplierDeliveryPresentationSettings settings,
        Transform parent)
    {
        GameObject prefab = vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero
            ? settings.LightTruckPrefab
            : settings.VanPrefab;
        GameObject result = prefab != null
            ? UnityEngine.Object.Instantiate(prefab, parent)
            : CreateFallbackVehicle(vehicle, parent);
        result.name = vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero
            ? "BB_Delivery_LightTruck"
            : "BB_Delivery_Van";
        EnsureVehicleView(result);
        EnsureBrandingView(result, settings);
        return result;
    }

    public static GameObject CreateDriver(BistroBuilderSupplierDeliveryPresentationSettings settings, Transform parent)
    {
        GameObject result = settings.DriverPrefab != null
            ? UnityEngine.Object.Instantiate(settings.DriverPrefab, parent)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        if (result.transform.parent != parent) result.transform.SetParent(parent, true);
        result.name = "BB_Delivery_Driver";
        if (settings.DriverPrefab == null)
        {
            result.transform.localScale = new Vector3(0.42f, 0.85f, 0.42f);
            SetRendererColor(result, new Color(0.22f, 0.24f, 0.26f, 1f));
        }
        return result;
    }

    public static GameObject CreateTrolley(BistroBuilderSupplierDeliveryPresentationSettings settings, Transform parent)
    {
        GameObject result = settings.TrolleyPrefab != null
            ? UnityEngine.Object.Instantiate(settings.TrolleyPrefab, parent)
            : CreateFallbackTrolley(parent);
        result.name = "BB_Delivery_Trolley";
        return result;
    }

    public static GameObject CreateBox(BistroBuilderSupplierDeliveryPresentationSettings settings, Transform parent, int index)
    {
        GameObject result = settings.BoxPrefab != null
            ? UnityEngine.Object.Instantiate(settings.BoxPrefab, parent)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (result.transform.parent != parent) result.transform.SetParent(parent, false);
        result.name = "BB_Delivery_Box_" + (index + 1).ToString("00");
        if (settings.BoxPrefab == null)
        {
            result.transform.localScale = new Vector3(0.42f, 0.28f, 0.34f);
            SetRendererColor(result, new Color(0.46f, 0.29f, 0.14f, 1f));
        }
        return result;
    }

    public static BistroBuilderSupplierDeliveryBrandingData ResolveBranding(BistroBuilderSupplierAuthoringRecord supplier)
    {
        if (supplier == null) return null;
        return new BistroBuilderSupplierDeliveryBrandingData
        {
            supplierId = supplier.SupplierId,
            displayName = string.IsNullOrWhiteSpace(supplier.displayName) ? supplier.shortName : supplier.displayName,
            logo = supplier.logo,
            primaryColor = supplier.primaryBrandColor,
            secondaryColor = supplier.secondaryBrandColor,
            textColor = supplier.textContrastColor
        };
    }

    public static bool ApplyBranding(
        GameObject vehicle,
        BistroBuilderSupplierDeliveryBrandingData branding,
        BistroBuilderSupplierDeliveryPresentationSettings settings,
        out string error)
    {
        error = null;
        if (vehicle == null)
        {
            error = "Vehículo nulo al aplicar branding.";
            return false;
        }
        if (branding == null || !branding.HasReadableIdentity)
        {
            error = "El proveedor no tiene nombre legible para el branding obligatorio 2.3H.";
            return false;
        }
        BistroBuilderSupplierDeliveryBrandingView view = vehicle.GetComponent<BistroBuilderSupplierDeliveryBrandingView>();
        if (view == null) view = EnsureBrandingView(vehicle, settings);
        if (settings.RequireBrandingOnBothSides && (view == null || !view.HasBothSides))
        {
            error = "El vehículo no dispone de branding en ambos laterales.";
            return false;
        }
        view.Apply(branding, settings.ShowSupplierNameWhenLogoExists);
        return true;
    }

    public static void ApplyDriverBrandColor(GameObject driver, BistroBuilderSupplierDeliveryBrandingData branding)
    {
        if (driver == null || branding == null) return;
        Renderer[] renderers = driver.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", branding.primaryColor);
            block.SetColor("_Color", branding.primaryColor);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private static GameObject CreateFallbackVehicle(BistroBuilderSupplierVehiclePreference vehicle, Transform parent)
    {
        GameObject root = new GameObject(vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero ? "Fallback_LightTruck" : "Fallback_Van");
        root.transform.SetParent(parent, false);
        float length = vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero ? 6.2f : 4.8f;
        float width = vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero ? 2.25f : 2.0f;
        float bodyHeight = vehicle == BistroBuilderSupplierVehiclePreference.CamionLigero ? 2.35f : 2.05f;

        GameObject body = CreateCube("Body", root.transform, new Vector3(width, bodyHeight, length), new Vector3(0f, bodyHeight * 0.5f, 0f), new Color(0.83f, 0.84f, 0.85f, 1f));
        body.transform.localPosition = new Vector3(0f, bodyHeight * 0.5f, 0f);
        GameObject cabin = CreateCube("Cabin", root.transform, new Vector3(width * 0.96f, bodyHeight * 0.78f, length * 0.28f), new Vector3(0f, bodyHeight * 0.39f, length * 0.36f), new Color(0.72f, 0.74f, 0.76f, 1f));
        cabin.transform.localPosition = new Vector3(0f, bodyHeight * 0.39f, length * 0.36f);

        BistroBuilderSupplierDeliveryVehicleView view = root.AddComponent<BistroBuilderSupplierDeliveryVehicleView>();
        GameObject leftDoor = CreateCube("RearDoor_L", root.transform, new Vector3(width * 0.48f, bodyHeight * 0.78f, 0.06f), Vector3.zero, new Color(0.78f, 0.79f, 0.80f, 1f));
        GameObject rightDoor = CreateCube("RearDoor_R", root.transform, new Vector3(width * 0.48f, bodyHeight * 0.78f, 0.06f), Vector3.zero, new Color(0.78f, 0.79f, 0.80f, 1f));
        leftDoor.transform.localPosition = new Vector3(-width * 0.24f, bodyHeight * 0.42f, -length * 0.505f);
        rightDoor.transform.localPosition = new Vector3(width * 0.24f, bodyHeight * 0.42f, -length * 0.505f);
        view.leftRearDoor = leftDoor.transform;
        view.rightRearDoor = rightDoor.transform;

        AddWheel(root.transform, new Vector3(-width * 0.52f, 0.35f, length * 0.30f));
        AddWheel(root.transform, new Vector3(width * 0.52f, 0.35f, length * 0.30f));
        AddWheel(root.transform, new Vector3(-width * 0.52f, 0.35f, -length * 0.30f));
        AddWheel(root.transform, new Vector3(width * 0.52f, 0.35f, -length * 0.30f));
        return root;
    }

    private static GameObject CreateFallbackTrolley(Transform parent)
    {
        GameObject root = new GameObject("Fallback_Trolley");
        root.transform.SetParent(parent, false);
        GameObject basePlate = CreateCube("Base", root.transform, new Vector3(0.75f, 0.10f, 1.05f), new Vector3(0f, 0.18f, 0f), new Color(0.20f, 0.22f, 0.24f, 1f));
        basePlate.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        GameObject handle = CreateCube("Handle", root.transform, new Vector3(0.06f, 1.0f, 0.06f), Vector3.zero, new Color(0.18f, 0.18f, 0.18f, 1f));
        handle.transform.localPosition = new Vector3(0f, 0.72f, -0.48f);
        return root;
    }

    private static BistroBuilderSupplierDeliveryVehicleView EnsureVehicleView(GameObject root)
    {
        BistroBuilderSupplierDeliveryVehicleView view = root.GetComponent<BistroBuilderSupplierDeliveryVehicleView>();
        if (view == null) view = root.AddComponent<BistroBuilderSupplierDeliveryVehicleView>();
        if (view.animator == null) view.animator = root.GetComponentInChildren<Animator>();
        return view;
    }

    private static BistroBuilderSupplierDeliveryBrandingView EnsureBrandingView(
        GameObject root,
        BistroBuilderSupplierDeliveryPresentationSettings settings)
    {
        BistroBuilderSupplierDeliveryBrandingView existing = root.GetComponent<BistroBuilderSupplierDeliveryBrandingView>();
        if (existing == null) existing = root.AddComponent<BistroBuilderSupplierDeliveryBrandingView>();

        Bounds bounds = CalculateBounds(root);
        Vector3 centerLocal = root.transform.InverseTransformPoint(bounds.center);
        float halfWidthLocal = Mathf.Max(0.7f, bounds.extents.x / Mathf.Max(0.01f, Mathf.Abs(root.transform.lossyScale.x)));
        float halfHeightLocal = Mathf.Max(0.8f, bounds.extents.y / Mathf.Max(0.01f, Mathf.Abs(root.transform.lossyScale.y)));
        float halfLengthLocal = Mathf.Max(1.6f, bounds.extents.z / Mathf.Max(0.01f, Mathf.Abs(root.transform.lossyScale.z)));

        if (existing.leftPanel == null || existing.leftName == null || existing.leftLogo == null)
        {
            CreateBrandSide(root.transform, "Brand_Left", -1f, centerLocal, halfWidthLocal, halfHeightLocal, halfLengthLocal, settings,
                out existing.leftPanel, out existing.leftLogo, out existing.leftName);
        }
        if (existing.rightPanel == null || existing.rightName == null || existing.rightLogo == null)
        {
            CreateBrandSide(root.transform, "Brand_Right", 1f, centerLocal, halfWidthLocal, halfHeightLocal, halfLengthLocal, settings,
                out existing.rightPanel, out existing.rightLogo, out existing.rightName);
        }
        return existing;
    }

    private static void CreateBrandSide(
        Transform root,
        string name,
        float side,
        Vector3 centerLocal,
        float halfWidthLocal,
        float halfHeightLocal,
        float halfLengthLocal,
        BistroBuilderSupplierDeliveryPresentationSettings settings,
        out MeshRenderer panelRenderer,
        out SpriteRenderer logoRenderer,
        out TextMesh textMesh)
    {
        GameObject sideRoot = new GameObject(name);
        sideRoot.transform.SetParent(root, false);
        sideRoot.transform.localPosition = new Vector3(side * (halfWidthLocal + 0.045f), Mathf.Max(0.75f, centerLocal.y), centerLocal.z - halfLengthLocal * 0.05f);
        sideRoot.transform.localRotation = Quaternion.Euler(0f, side > 0f ? -90f : 90f, 0f);

        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = "Panel";
        panel.transform.SetParent(sideRoot.transform, false);
        panel.transform.localScale = new Vector3(
            Mathf.Min(settings.BrandingPanelLength, Mathf.Max(1.0f, halfLengthLocal * 1.3f)),
            settings.BrandingPanelHeight,
            settings.BrandingPanelThickness);
        panelRenderer = panel.GetComponent<MeshRenderer>();
        Collider collider = panel.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(collider);
            else UnityEngine.Object.DestroyImmediate(collider);
        }

        GameObject logo = new GameObject("Logo");
        logo.transform.SetParent(sideRoot.transform, false);
        logo.transform.localPosition = new Vector3(-0.55f, 0f, -(settings.BrandingPanelThickness * 0.55f + 0.006f));
        logo.transform.localScale = Vector3.one * 0.42f;
        logoRenderer = logo.AddComponent<SpriteRenderer>();
        logoRenderer.sortingOrder = 2;

        GameObject text = new GameObject("SupplierName");
        text.transform.SetParent(sideRoot.transform, false);
        text.transform.localPosition = new Vector3(0.20f, -0.02f, -(settings.BrandingPanelThickness * 0.55f + 0.009f));
        textMesh = text.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.10f;
        textMesh.fontSize = 44;
        textMesh.text = "PROVEEDOR";
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position + Vector3.up, new Vector3(2f, 2f, 4f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 scale, Vector3 position, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = scale;
        go.transform.localPosition = position;
        SetRendererColor(go, color);
        return go;
    }

    private static void AddWheel(Transform parent, Vector3 localPosition)
    {
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = "Wheel";
        wheel.transform.SetParent(parent, false);
        wheel.transform.localScale = new Vector3(0.38f, 0.13f, 0.38f);
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.transform.localPosition = localPosition;
        SetRendererColor(wheel, new Color(0.05f, 0.05f, 0.05f, 1f));
    }

    private static void SetRendererColor(GameObject go, Color color)
    {
        Renderer renderer = go != null ? go.GetComponent<Renderer>() : null;
        if (renderer == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }
}
