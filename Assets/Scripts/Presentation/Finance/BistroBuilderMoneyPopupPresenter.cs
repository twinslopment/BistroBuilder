using UnityEngine;

/// <summary>
/// Anima un único popup monetario y libera su GameObject al terminar.
/// </summary>
public sealed class BistroBuilderMoneyPopupPresenter : MonoBehaviour
{
    private BistroBuilderMoneyPopupService owner;
    private TextMesh textMesh;
    private Camera targetCamera;
    private Vector3 startPosition;
    private Color baseColor;
    private float duration;
    private float riseDistance;
    private float elapsed;
    private bool initialized;

    public void Initialize(
        BistroBuilderMoneyPopupService popupOwner,
        TextMesh text,
        Camera camera,
        float durationSeconds,
        float verticalDistance)
    {
        owner = popupOwner;
        textMesh = text;
        targetCamera = camera;
        startPosition = transform.position;
        baseColor = text != null ? text.color : Color.white;
        duration = Mathf.Max(0.1f, durationSeconds);
        riseDistance = Mathf.Max(0.05f, verticalDistance);
        elapsed = 0f;
        initialized = true;
        transform.localScale = Vector3.one * 0.72f;
        FaceCamera();
    }

    private void Update()
    {
        if (!initialized)
        {
            return;
        }

        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float easedRise = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = startPosition + Vector3.up * (riseDistance * easedRise);

        float pop = t < 0.18f
            ? Mathf.Lerp(0.72f, 1.08f, t / 0.18f)
            : Mathf.Lerp(1.08f, 1f, Mathf.InverseLerp(0.18f, 0.40f, t));
        transform.localScale = Vector3.one * pop;

        if (textMesh != null)
        {
            float alpha = t < 0.62f
                ? 1f
                : 1f - Mathf.InverseLerp(0.62f, 1f, t);
            Color color = baseColor;
            color.a = alpha;
            textMesh.color = color;
        }

        FaceCamera();
        if (t >= 1f)
        {
            Finish();
        }
    }

    private void FaceCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        Vector3 direction = transform.position - targetCamera.transform.position;
        if (direction.sqrMagnitude > 0.000001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    private void Finish()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        owner?.NotifyPopupFinished();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (!initialized)
        {
            return;
        }

        initialized = false;
        owner?.NotifyPopupFinished();
    }
}
