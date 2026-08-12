using UnityEngine;

/// <summary>
/// Vista de branding del vehículo. Puede existir ya en un prefab personalizado;
/// si no existe, 2.3H genera una implementación fallback en ambos laterales.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplierDeliveryBrandingView : MonoBehaviour
{
    [Header("Panel izquierdo")]
    public MeshRenderer leftPanel;
    public SpriteRenderer leftLogo;
    public TextMesh leftName;

    [Header("Panel derecho")]
    public MeshRenderer rightPanel;
    public SpriteRenderer rightLogo;
    public TextMesh rightName;

    public bool HasBothSides =>
        (leftPanel != null || leftLogo != null || leftName != null) &&
        (rightPanel != null || rightLogo != null || rightName != null);

    public void Apply(BistroBuilderSupplierDeliveryBrandingData data, bool showNameWhenLogoExists)
    {
        if (data == null) return;
        ApplyPanel(leftPanel, data.primaryColor);
        ApplyPanel(rightPanel, data.primaryColor);

        ApplyLogo(leftLogo, data.logo);
        ApplyLogo(rightLogo, data.logo);

        bool showName = data.logo == null || showNameWhenLogoExists;
        ApplyName(leftName, data.displayName, data.textColor, showName);
        ApplyName(rightName, data.displayName, data.textColor, showName);
    }

    private static void ApplyPanel(MeshRenderer renderer, Color color)
    {
        if (renderer == null) return;
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private static void ApplyLogo(SpriteRenderer renderer, Sprite logo)
    {
        if (renderer == null) return;
        renderer.sprite = logo;
        renderer.enabled = logo != null;
    }

    private static void ApplyName(TextMesh text, string value, Color color, bool visible)
    {
        if (text == null) return;
        text.text = string.IsNullOrWhiteSpace(value) ? "PROVEEDOR" : value.Trim();
        text.color = color;
        text.gameObject.SetActive(visible);
    }
}

/// <summary>Vista opcional para animación de puertas traseras.</summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSupplierDeliveryVehicleView : MonoBehaviour
{
    public Animator animator;
    public Transform leftRearDoor;
    public Transform rightRearDoor;
    [Range(20f, 140f)] public float fallbackDoorOpenAngle = 78f;

    private Quaternion leftClosed;
    private Quaternion rightClosed;
    private bool cached;

    private void Cache()
    {
        if (cached) return;
        cached = true;
        if (leftRearDoor != null) leftClosed = leftRearDoor.localRotation;
        if (rightRearDoor != null) rightClosed = rightRearDoor.localRotation;
    }

    public void SetRearDoors01(float t)
    {
        Cache();
        t = Mathf.Clamp01(t);
        if (animator != null)
        {
            animator.SetFloat("RearDoorsOpen", t);
            return;
        }
        if (leftRearDoor != null)
            leftRearDoor.localRotation = leftClosed * Quaternion.Euler(0f, -fallbackDoorOpenAngle * t, 0f);
        if (rightRearDoor != null)
            rightRearDoor.localRotation = rightClosed * Quaternion.Euler(0f, fallbackDoorOpenAngle * t, 0f);
    }
}
