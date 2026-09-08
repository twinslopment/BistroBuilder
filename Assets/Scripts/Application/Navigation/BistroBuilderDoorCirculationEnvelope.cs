using UnityEngine;


/// <summary>
/// Reserva exactamente el barrido de una hoja de puerta mientras se mueve.
/// El volumen se calcula para el angulo y sentido reales de apertura.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderDoorCirculationEnvelope :
    BistroBuilderDynamicCirculationEnvelope
{
    [SerializeField, Min(0.2f)] private float doorLeafWidth = 0.9f;
    [SerializeField, Min(0.05f)] private float doorLeafDepth = 0.12f;
    [SerializeField, Range(-180f, 180f)] private float openingAngle = 90f;

    protected override void Awake()
    {
        base.Awake();
        ConfigureSweepForAngle(openingAngle);
    }

    public void ConfigureSweepForAngle(float signedOpeningAngle)
    {
        openingAngle = Mathf.Clamp(signedOpeningAngle, -180f, 180f);
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity;
        float maxZ = float.NegativeInfinity;
        const int samples = 24;

        for (int i = 0; i <= samples; i++)
        {
            float angle = openingAngle * (i / (float)samples) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            AccumulateCorner(0f, -doorLeafDepth * 0.5f, cos, sin,
                ref minX, ref maxX, ref minZ, ref maxZ);
            AccumulateCorner(0f, doorLeafDepth * 0.5f, cos, sin,
                ref minX, ref maxX, ref minZ, ref maxZ);
            AccumulateCorner(doorLeafWidth, -doorLeafDepth * 0.5f, cos, sin,
                ref minX, ref maxX, ref minZ, ref maxZ);
            AccumulateCorner(doorLeafWidth, doorLeafDepth * 0.5f, cos, sin,
                ref minX, ref maxX, ref minZ, ref maxZ);
        }

        const float margin = 0.08f;
        Vector3 center = new Vector3(
            (minX + maxX) * 0.5f,
            0f,
            (minZ + maxZ) * 0.5f);
        Vector2 sweepSize = new Vector2(
            maxX - minX + margin * 2f,
            maxZ - minZ + margin * 2f);
        ConfigureEnvelope(
            center,
            sweepSize,
            BistroBuilderNavigationAgentMask.All,
            BistroBuilderDynamicSpaceKind.DoorSwing);
    }

    private static void AccumulateCorner(
        float x0,
        float z0,
        float cos,
        float sin,
        ref float minX,
        ref float maxX,
        ref float minZ,
        ref float maxZ)
    {
        float x = x0 * cos - z0 * sin;
        float z = x0 * sin + z0 * cos;
        minX = Mathf.Min(minX, x);
        maxX = Mathf.Max(maxX, x);
        minZ = Mathf.Min(minZ, z);
        maxZ = Mathf.Max(maxZ, z);
    }

    public void NotifyOpening(float durationSeconds) =>
        BeginWindow("door:" + GetInstanceID(), durationSeconds);
    public void NotifyClosing(float durationSeconds) =>
        BeginWindow("door:" + GetInstanceID(), durationSeconds);
    public void NotifyMotion(float durationSeconds) =>
        BeginWindow("door:" + GetInstanceID(), durationSeconds);
}
