using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class BistroBuilderLocomotionAnimationPresenter : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string accelerationParameter = "Acceleration";
    [SerializeField] private string turnRateParameter = "TurnRate";
    [SerializeField, Min(0.01f)] private float dampingSeconds = 0.12f;

    private Vector3 previousPosition;
    private Vector3 previousForward;
    private float previousSpeed;
    private int speedHash;
    private int accelerationHash;
    private int turnRateHash;
    private bool hasSpeed;
    private bool hasAcceleration;
    private bool hasTurnRate;

    public float ActualSpeedMetersPerSecond { get; private set; }
    public float ActualAcceleration { get; private set; }
    public float ActualTurnRateDegreesPerSecond { get; private set; }

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        CacheParameters();
        previousPosition = transform.position;
        previousForward = transform.forward;
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        previousForward = transform.forward;
        previousSpeed = 0f;
    }

    private void Update()
    {
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 delta = transform.position - previousPosition;
        delta.y = 0f;
        ActualSpeedMetersPerSecond = delta.magnitude / dt;
        ActualAcceleration = (ActualSpeedMetersPerSecond - previousSpeed) / dt;
        ActualTurnRateDegreesPerSecond = Vector3.SignedAngle(previousForward, transform.forward, Vector3.up) / dt;

        if (animator != null)
        {
            if (hasSpeed) animator.SetFloat(speedHash, ActualSpeedMetersPerSecond, dampingSeconds, dt);
            if (hasAcceleration) animator.SetFloat(accelerationHash, ActualAcceleration, dampingSeconds, dt);
            if (hasTurnRate) animator.SetFloat(turnRateHash, ActualTurnRateDegreesPerSecond, dampingSeconds, dt);
        }

        previousPosition = transform.position;
        previousForward = transform.forward;
        previousSpeed = ActualSpeedMetersPerSecond;
    }

    private void CacheParameters()
    {
        speedHash = Animator.StringToHash(speedParameter ?? string.Empty);
        accelerationHash = Animator.StringToHash(accelerationParameter ?? string.Empty);
        turnRateHash = Animator.StringToHash(turnRateParameter ?? string.Empty);
        hasSpeed = HasFloatParameter(speedHash);
        hasAcceleration = HasFloatParameter(accelerationHash);
        hasTurnRate = HasFloatParameter(turnRateHash);
    }

    private bool HasFloatParameter(int hash)
    {
        if (animator == null) return false;
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].nameHash == hash && parameters[i].type == AnimatorControllerParameterType.Float)
                return true;
        }
        return false;
    }
}
