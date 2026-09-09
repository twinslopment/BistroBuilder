using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Kernel de restricciones recíprocas inspirado en ORCA/HRVO.
/// Resuelve half-planes de velocidad de forma determinista y acotada.
/// </summary>
public sealed class BistroBuilderNavigationReciprocalConstraintSolver
{
    public struct Constraint
    {
        public Vector3 point;
        public Vector3 normal;
        public string otherOwnerId;
    }

    private readonly List<Constraint> constraints = new List<Constraint>(8);
    public int LastConstraintCount { get; private set; }
    public int ConstraintCount => constraints.Count;

    public void Clear() => constraints.Clear();

    public bool AddAgentConstraint(Vector3 selfPosition, Vector3 selfVelocity,
        float selfRadius, Vector3 otherPosition, Vector3 otherVelocity,
        float otherRadius, string otherOwnerId, float horizon, float responsibility)
    {
        Vector3 relative = Horizontal(selfPosition - otherPosition);
        Vector3 relativeVelocity = Horizontal(selfVelocity - otherVelocity);
        float distance = relative.magnitude;
        float combined = Mathf.Max(0.05f, selfRadius) +
                         Mathf.Max(0.05f, otherRadius) + 0.04f;
        float safeHorizon = Mathf.Max(0.15f, horizon);

        float relativeSpeedSq = relativeVelocity.sqrMagnitude;
        float timeToClosest = relativeSpeedSq > 0.0001f
            ? Mathf.Clamp(-Vector3.Dot(relative, relativeVelocity) / relativeSpeedSq, 0f, safeHorizon)
            : 0f;
        Vector3 closest = relative + relativeVelocity * timeToClosest;
        float closestDistance = closest.magnitude;
        if (distance >= combined && (relativeSpeedSq <= 0.0001f || closestDistance >= combined))
            return false;

        Vector3 normal = closestDistance > 0.0001f
            ? closest / closestDistance
            : (distance > 0.0001f ? relative / distance : Vector3.right);
        float penetration = Mathf.Max(0f, combined - Mathf.Min(distance, closestDistance));
        float responseTime = timeToClosest > 0.05f
            ? timeToClosest
            : Mathf.Max(0.05f, safeHorizon * 0.25f);
        float required = penetration / responseTime;
        float share = Mathf.Clamp(responsibility, 0.15f, 0.85f);
        constraints.Add(new Constraint
        {
            normal = normal,
            point = Horizontal(selfVelocity) + normal * required * share,
            otherOwnerId = otherOwnerId ?? string.Empty
        });
        return true;
    }
    public Vector3 Solve(Vector3 preferredVelocity, Vector3 currentVelocity,
        float maxSpeed, float maxAcceleration, float deltaTime)
    {
        LastConstraintCount = constraints.Count;
        float speedLimit = Mathf.Max(0f, maxSpeed);
        Vector3 preferred = Vector3.ClampMagnitude(Horizontal(preferredVelocity), speedLimit);
        Vector3 current = Horizontal(currentVelocity);
        float maxDelta = Mathf.Max(0.05f, maxAcceleration) * Mathf.Max(0.001f, deltaTime);
        Vector3 velocity = current + Vector3.ClampMagnitude(preferred - current, maxDelta);
        velocity = Vector3.ClampMagnitude(velocity, speedLimit);
        for (int pass = 0; pass < 2; pass++)
        {
            bool changed = false;
            for (int i = 0; i < constraints.Count; i++)
            {
                Constraint c = constraints[i];
                float violation = Vector3.Dot(velocity - c.point, c.normal);
                if (violation >= -0.0001f) continue;
                velocity += c.normal * (-violation);
                velocity = Vector3.ClampMagnitude(velocity, speedLimit);
                changed = true;
            }
            if (!changed) break;
        }
        return Horizontal(velocity);
    }

    public static bool IsConstraintSatisfied(Vector3 velocity, Constraint constraint, float tolerance = 0.001f)
    {
        return Vector3.Dot(Horizontal(velocity) - constraint.point, constraint.normal) >= -Mathf.Abs(tolerance);
    }

    private static Vector3 Horizontal(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
