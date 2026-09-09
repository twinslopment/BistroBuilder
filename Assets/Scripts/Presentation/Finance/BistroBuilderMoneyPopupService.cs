using System;
using UnityEngine;

/// <summary>
/// Presentación diegética de un movimiento monetario confirmado.
/// No conoce reglas económicas: recibe un importe firmado y una posición.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Finance/Money Popup Service")]
public sealed class BistroBuilderMoneyPopupService : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.1f)] private float durationSeconds = 1.05f;
    [SerializeField, Min(0.05f)] private float riseDistance = 0.72f;
    [SerializeField, Min(0.001f)] private float characterSize = 0.045f;
    [SerializeField] private Color positiveColor =
        new Color(0.34f, 0.72f, 0.48f, 1f);
    [SerializeField] private Color negativeColor =
        new Color(0.92f, 0.38f, 0.32f, 1f);

    private int activePopupCount;

    public event Action<long, Vector3> PopupShown;
    public int ActivePopupCount => activePopupCount;

    public bool ValidateConfiguration(out string error)
    {
        if (durationSeconds <= 0f || riseDistance <= 0f || characterSize <= 0f)
        {
            error = "La animación monetaria necesita duración, recorrido y tamaño positivos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void Show(long signedCents, Vector3 worldPosition)
    {
        if (signedCents == 0L)
        {
            return;
        }

        Camera camera = targetCamera != null ? targetCamera : Camera.main;
        GameObject popupObject = new GameObject("BB_MoneyPopup");
        popupObject.transform.position = worldPosition;

        int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycast >= 0)
        {
            popupObject.layer = ignoreRaycast;
        }

        TextMesh text = popupObject.AddComponent<TextMesh>();
        text.text = FormatSignedMoney(signedCents);
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 64;
        text.characterSize = characterSize;
        text.color = signedCents > 0L ? positiveColor : negativeColor;

        MeshRenderer renderer = text.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 32700;
        }

        BistroBuilderMoneyPopupPresenter presenter =
            popupObject.AddComponent<BistroBuilderMoneyPopupPresenter>();
        presenter.Initialize(this, text, camera, durationSeconds, riseDistance);

        activePopupCount++;
        PopupShown?.Invoke(signedCents, worldPosition);
    }

    internal void NotifyPopupFinished()
    {
        activePopupCount = Mathf.Max(0, activePopupCount - 1);
    }

    private static string FormatSignedMoney(long signedCents)
    {
        decimal euros = Math.Abs(signedCents) / 100m;
        string amount = signedCents % 100L == 0L
            ? euros.ToString("N0")
            : euros.ToString("N2");
        return (signedCents > 0L ? "+" : "-") + amount + " €";
    }
}
