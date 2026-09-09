using System;
using UnityEngine;

/// <summary>
/// Ajusta exclusivamente la jerarquía visual Humanoid respecto al root autoritativo.
/// No mueve el actor lógico: Navigation 17 conserva posición y BBSIS la semántica espacial.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderCharacterVisualGroundingAdapter : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0.01f)] private float maximumCorrectionMeters = 0.25f;
    [SerializeField, Range(-0.03f, 0.05f)] private float soleClearanceMeters = 0.005f;

    private Vector3 baseLocalPosition;
    private bool baseCaptured;

    public Transform VisualRoot => visualRoot;
    public bool IsCalibrated { get; private set; }
    public float AppliedVerticalOffsetMeters { get; private set; }

    public void Configure(Transform configuredVisualRoot)
    {
        visualRoot = configuredVisualRoot;
        CaptureBasePose();
    }

    public bool CalibrateFromCurrentPose(out string error)
    {
        error = string.Empty;
        if (visualRoot == null)
        {
            error = name + ": falta VisualRoot para grounding visual.";
            return false;
        }
        CaptureBasePose();
        visualRoot.localPosition = baseLocalPosition;

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0)
        {
            error = name + ": el VisualRoot no contiene renderers activos.";
            return false;
        }

        float minimumY = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;
            minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
        }
        if (float.IsInfinity(minimumY) || float.IsNaN(minimumY))
        {
            error = name + ": no pudo medirse el contacto visual con suelo.";
            return false;
        }

        float targetY = transform.position.y + soleClearanceMeters;
        float correction = targetY - minimumY;
        if (Mathf.Abs(correction) > Mathf.Max(0.01f, maximumCorrectionMeters))
        {
            error = name + ": corrección de grounding fuera de presupuesto: " +
                correction.ToString("0.000") + " m.";
            return false;
        }
        Vector3 local = baseLocalPosition;
        Vector3 worldUp = transform.up * correction;
        Vector3 localDelta = visualRoot.parent != null
            ? visualRoot.parent.InverseTransformVector(worldUp)
            : worldUp;
        local += localDelta;
        visualRoot.localPosition = local;

        AppliedVerticalOffsetMeters = correction;
        IsCalibrated = true;
        return true;
    }

    public void ResetCalibration()
    {
        if (visualRoot != null && baseCaptured)
            visualRoot.localPosition = baseLocalPosition;
        AppliedVerticalOffsetMeters = 0f;
        IsCalibrated = false;
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (visualRoot == null)
        {
            error = name + ": falta VisualRoot.";
            return false;
        }
        if (visualRoot == transform || !visualRoot.IsChildOf(transform))
        {
            error = name + ": VisualRoot debe ser hijo del root autoritativo.";
            return false;
        }
        return true;
    }

    private void CaptureBasePose()
    {
        if (visualRoot == null || baseCaptured) return;
        baseLocalPosition = visualRoot.localPosition;
        baseCaptured = true;
    }
}
