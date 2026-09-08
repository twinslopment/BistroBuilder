using UnityEngine;

/// <summary>
/// Perfil data-driven del volumen de movilidad y carga de un objeto movil.
/// La geometria espacial permanece separada de la malla de render.
/// </summary>
[CreateAssetMenu(
    fileName = "BB_MobilitySpatialProfile",
    menuName = "Bistro Builder/Spatial/Mobility Profile")]
public sealed class BistroBuilderMobilitySpatialProfileDefinition :
    ScriptableObject
{
    [SerializeField] private string profileId = "mobility.generic";
    [SerializeField, Min(0.05f)] private float bodyWidth = 0.65f;
    [SerializeField, Min(0.05f)] private float bodyDepth = 0.9f;
    [SerializeField, Min(0f)] private float movementMargin = 0.12f;
    [SerializeField, Min(0f)] private float trailingClearance = 0.18f;
    [SerializeField, Min(0f)] private float carryWidthExpansion = 0.18f;
    [SerializeField, Min(0f)] private float carryDepthExpansion = 0.25f;
    [SerializeField, Min(1)] private int maximumLoadUnits = 6;
    [SerializeField, Min(0.02f)] private float movementThreshold = 0.04f;

    public string ProfileId => profileId;
    public float BodyWidth => bodyWidth;
    public float BodyDepth => bodyDepth;
    public float MovementMargin => movementMargin;
    public float TrailingClearance => trailingClearance;
    public float CarryWidthExpansion => carryWidthExpansion;
    public float CarryDepthExpansion => carryDepthExpansion;
    public int MaximumLoadUnits => maximumLoadUnits;
    public float MovementThreshold => movementThreshold;
    public bool ValidateDefinition(out string error)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            error = "Mobility Profile sin profileId estable.";
            return false;
        }
        if (bodyWidth < 0.05f || bodyDepth < 0.05f ||
            maximumLoadUnits < 1 || movementThreshold < 0.02f)
        {
            error = profileId + ": dimensiones o limites invalidos.";
            return false;
        }
        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string stableProfileId,
        float width,
        float depth,
        float margin,
        float trailing,
        float carryWidth,
        float carryDepth,
        int loadUnits)
    {
        profileId = stableProfileId ?? string.Empty;
        bodyWidth = Mathf.Max(0.05f, width);
        bodyDepth = Mathf.Max(0.05f, depth);
        movementMargin = Mathf.Max(0f, margin);
        trailingClearance = Mathf.Max(0f, trailing);
        carryWidthExpansion = Mathf.Max(0f, carryWidth);
        carryDepthExpansion = Mathf.Max(0f, carryDepth);
        maximumLoadUnits = Mathf.Max(1, loadUnits);
    }
#endif
}
