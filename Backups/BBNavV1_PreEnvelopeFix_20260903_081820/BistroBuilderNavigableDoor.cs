using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Puerta navegable: coordina apertura visual, espacio de barrido y bloqueo.
/// La reserva del arco empieza antes de mover la hoja y se libera al terminar.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BistroBuilderDoorCirculationEnvelope))]
[RequireComponent(typeof(NavMeshObstacle))]
public sealed class BistroBuilderNavigableDoor : MonoBehaviour
{
    [SerializeField] private Transform movingLeaf;
    [SerializeField] private bool startsOpen;
    [SerializeField, Range(-170f, 170f)] private float openAngle = 90f;
    [SerializeField, Min(0.05f)] private float motionDuration = 0.55f;
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    private BistroBuilderDoorCirculationEnvelope envelope;
    private Quaternion closedRotation;
    private Coroutine motionRoutine;
    public bool IsOpen { get; private set; }
    public bool IsMoving => motionRoutine != null;

    private void Awake()
    {
        envelope = GetComponent<BistroBuilderDoorCirculationEnvelope>();
        if (navMeshObstacle == null) navMeshObstacle = GetComponent<NavMeshObstacle>();
        envelope.ConfigureSweepForAngle(openAngle);
        if (movingLeaf == null) movingLeaf = transform;
        closedRotation = movingLeaf.localRotation;
        IsOpen = startsOpen;
        ApplyPoseImmediate(IsOpen);
    }

    public bool TrySetOpen(bool open)
    {
        if (IsMoving || open == IsOpen) return false;
        motionRoutine = StartCoroutine(MoveDoor(open));
        return true;
    }

    private IEnumerator MoveDoor(bool opening)
    {
        string owner = "door:" + GetInstanceID();
        envelope.SetActiveWindow(owner, true);
        if (opening) envelope.NotifyOpening(motionDuration);
        else envelope.NotifyClosing(motionDuration);

        if (navMeshObstacle != null)
        {
            navMeshObstacle.carving = true;
            navMeshObstacle.enabled = true;
        }

        Quaternion start = movingLeaf.localRotation;
        Quaternion target = closedRotation *
            Quaternion.Euler(0f, opening ? openAngle : 0f, 0f);
        float elapsed = 0f;
        while (elapsed < motionDuration)
        {
            elapsed += Time.deltaTime;
            movingLeaf.localRotation = Quaternion.Slerp(
                start, target, Mathf.Clamp01(elapsed / motionDuration));
            yield return null;
        }

        movingLeaf.localRotation = target;
        IsOpen = opening;
        motionRoutine = null;

        if (navMeshObstacle != null)
            navMeshObstacle.enabled = !IsOpen;
        envelope.SetActiveWindow(owner, false);
    }

    private void ApplyPoseImmediate(bool open)
    {
        movingLeaf.localRotation = closedRotation *
            Quaternion.Euler(0f, open ? openAngle : 0f, 0f);
        if (navMeshObstacle != null)
        {
            navMeshObstacle.carving = true;
            navMeshObstacle.enabled = !open;
        }
        envelope.SetActiveWindow("door:" + GetInstanceID(), false);
    }
}
