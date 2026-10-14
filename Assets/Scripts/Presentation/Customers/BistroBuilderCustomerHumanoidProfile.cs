using UnityEngine;

/// <summary>Visual authoring only. Does not grant a seat or change customer simulation.</summary>
[CreateAssetMenu(menuName = "Bistro Builder/Customers/Humanoid Presentation")]
public sealed class BistroBuilderCustomerHumanoidProfile : ScriptableObject
{
    [SerializeField] private GameObject modelPrefab;
    [SerializeField] private float modelScale = 1f;
    [SerializeField] private float pelvisAboveSeatMeters = 0.10f;
    [SerializeField] private string sitMotionId = "seat.sit.standard";
    [SerializeField] private string idleMotionId = "seat.idle.standard";
    [SerializeField] private string standMotionId = "seat.stand.standard";
    [SerializeField] private string baselineMotionId = "locomotion.idle";

    public GameObject ModelPrefab => modelPrefab;
    public float ModelScale => modelScale;
    public float PelvisAboveSeatMeters => pelvisAboveSeatMeters;
    public string SitMotionId => sitMotionId;
    public string IdleMotionId => idleMotionId;
    public string StandMotionId => standMotionId;
    public string BaselineMotionId => baselineMotionId;

    public bool ValidateConfiguration(out string error)
    {
        error = "Customer presentation requires a Humanoid model, positive finite scale and a finite pelvis offset.";
        Animator animator = modelPrefab != null ? modelPrefab.GetComponentInChildren<Animator>(true) : null;
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman ||
            float.IsNaN(modelScale) || float.IsInfinity(modelScale) || modelScale <= 0f ||
            float.IsNaN(pelvisAboveSeatMeters) || float.IsInfinity(pelvisAboveSeatMeters) ||
            pelvisAboveSeatMeters < 0f || pelvisAboveSeatMeters > 0.25f) return false;
        if (string.IsNullOrWhiteSpace(sitMotionId) || string.IsNullOrWhiteSpace(idleMotionId) ||
            string.IsNullOrWhiteSpace(standMotionId) || string.IsNullOrWhiteSpace(baselineMotionId)) return false;
        error = string.Empty; return true;
    }
#if UNITY_EDITOR
    public void ConfigureForEditor(GameObject model, float scale, float pelvisOffset)
    { modelPrefab = model; modelScale = scale; pelvisAboveSeatMeters = pelvisOffset; }
#endif
}
