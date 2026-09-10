using System.Collections;
using UnityEngine;

/// <summary>Feedback visual efímero en escena. Nunca modifica estado de gameplay.</summary>
public static class BistroBuilderWorldUiFeedback
{
    public static void PulseReservedTable(int tableId)
    {
        if (tableId < 1) return;
        RestaurantTableRegistry registry = Object.FindFirstObjectByType<RestaurantTableRegistry>(
            FindObjectsInactive.Include);
        if (registry == null || !registry.TryGetTableById(tableId, out RestaurantTable table) ||
            table == null) return;

        GameObject pulse = new GameObject("BB_UIUX_ReservationPulse_" + tableId);
        pulse.transform.SetPositionAndRotation(table.transform.position, Quaternion.identity);
        BistroBuilderWorldPulse effect = pulse.AddComponent<BistroBuilderWorldPulse>();
        effect.Configure(table.transform);
    }
}

[DisallowMultipleComponent]
public sealed class BistroBuilderWorldPulse : MonoBehaviour
{
    private const int Segments = 64;
    private LineRenderer line;
    private Material material;
    private float radius = 0.75f;
    private float yOffset = 0.08f;

    public void Configure(Transform target)
    {
        ResolveBounds(target);
        line = gameObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = Segments;
        line.widthMultiplier = 0.035f;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            material = new Material(shader) { name = "BB_UIUX_ReservationPulse_Mat" };
            line.material = material;
        }
        Color accent = BistroBuilderUiTokens.WarmAccent;
        line.startColor = accent;
        line.endColor = accent;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, yOffset,
                Mathf.Sin(angle) * radius));
        }
        StartCoroutine(PulseRoutine());
    }

    private void ResolveBounds(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + 0.18f, 0.45f, 1.6f);
        transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private IEnumerator PulseRoutine()
    {
        float duration = 0.62f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float scale = Mathf.Lerp(0.90f, 1.10f, t);
            transform.localScale = new Vector3(scale, 1f, scale);
            Color c = BistroBuilderUiTokens.WarmAccent;
            c.a = envelope * 0.92f;
            if (line != null)
            {
                line.startColor = c;
                line.endColor = c;
                line.widthMultiplier = Mathf.Lerp(0.02f, 0.055f, envelope);
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
