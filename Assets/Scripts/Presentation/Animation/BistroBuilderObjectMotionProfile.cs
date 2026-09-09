using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BBObjectMotion",
    menuName = "Bistro Builder/Animation/Object Motion Profile")]
public sealed class BistroBuilderObjectMotionProfile : ScriptableObject
{
    [SerializeField] private string profileId = "object-motion.standard";
    [SerializeField] private BistroBuilderObjectMotionPrimitive primitive = BistroBuilderObjectMotionPrimitive.Hinge;
    [SerializeField] private Vector3 localAxis = Vector3.up;
    [SerializeField] private float amount = 90f;
    [SerializeField, Min(0.01f)] private float duration = 0.5f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public string ProfileId => BistroBuilderMotionProfile.NormalizeId(profileId);
    public BistroBuilderObjectMotionPrimitive Primitive => primitive;
    public Vector3 LocalAxis => localAxis.sqrMagnitude > 0.000001f ? localAxis.normalized : Vector3.up;
    public float Amount => amount;
    public float Duration => Mathf.Max(0.01f, duration);
    public AnimationCurve Curve => curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(ProfileId))
        {
            error = name + " necesita ProfileId.";
            return false;
        }
        if (localAxis.sqrMagnitude <= 0.000001f)
        {
            error = name + " necesita un eje local válido.";
            return false;
        }
        return true;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        string configuredId,
        BistroBuilderObjectMotionPrimitive configuredPrimitive,
        Vector3 configuredAxis,
        float configuredAmount,
        float configuredDuration)
    {
        profileId = configuredId;
        primitive = configuredPrimitive;
        localAxis = configuredAxis;
        amount = configuredAmount;
        duration = Mathf.Max(0.01f, configuredDuration);
    }
#endif
}
