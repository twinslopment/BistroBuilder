using System;
using UnityEngine;

/// <summary>
/// Volumen temporal que ocupa espacio de circulacion. Sirve para puertas,
/// sillas en movimiento y cualquier elemento con ocupacion transitoria.
/// </summary>
[DisallowMultipleComponent]
public class BistroBuilderDynamicCirculationEnvelope : MonoBehaviour
{
    [SerializeField] private Vector3 localCenter = Vector3.zero;
    [SerializeField] private Vector2 size = new Vector2(0.8f, 0.8f);
    [SerializeField] private BistroBuilderNavigationAgentMask blockedAgents =
        BistroBuilderNavigationAgentMask.All;
    [SerializeField] private BistroBuilderDynamicSpaceKind kind =
        BistroBuilderDynamicSpaceKind.TemporaryObstacle;

    private BistroBuilderNavigationService navigation;
    private float activeUntil;
    private string ownerId = string.Empty;
    private bool explicitlyActive;
    private BistroBuilderSpatialInteractionService spatialService;
    private string spatialLeaseId = string.Empty;
    private string spatialLeaseOwnerId = string.Empty;

    public bool IsActive => explicitlyActive || Time.unscaledTime < activeUntil;
    public BistroBuilderDynamicSpaceKind Kind => kind;
    public string OwnerId => ownerId;
    public BistroBuilderNavigationAgentMask BlockedAgents => blockedAgents;
    public bool HasSpatialLease => !string.IsNullOrEmpty(spatialLeaseId);
    public Vector3 WorldCenter => transform.TransformPoint(localCenter);
    public Vector2 WorldSize => new Vector2(
        Mathf.Max(0.05f, Mathf.Abs(size.x * transform.lossyScale.x)),
        Mathf.Max(0.05f, Mathf.Abs(size.y * transform.lossyScale.z)));

    protected virtual void Awake()
    {
        navigation = FindFirstObjectByType<BistroBuilderNavigationService>();
        spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }

    protected virtual void OnEnable()
    {
        if (navigation == null)
            navigation = FindFirstObjectByType<BistroBuilderNavigationService>();
        spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        navigation?.RegisterDynamicEnvelope(this);
    }

    protected virtual void OnDisable()
    {
        ReleaseSpatialLease();
        navigation?.UnregisterDynamicEnvelope(this);
    }

    public void BeginWindow(string owner, float durationSeconds)
    {
        ownerId = owner ?? string.Empty;
        activeUntil = Mathf.Max(
            activeUntil,
            Time.unscaledTime + Mathf.Max(0.01f, durationSeconds));
        SyncSpatialLease(ownerId, explicitlyActive ? 0f : Mathf.Max(0.01f, durationSeconds));
        navigation?.NotifyDynamicSpaceChanged();
    }
    public void SetActiveWindow(string owner, bool active)
    {
        ownerId = owner ?? string.Empty;
        explicitlyActive = active;
        if (active)
            SyncSpatialLease(ownerId, 0f);
        else
        {
            activeUntil = 0f;
            ReleaseSpatialLease();
        }
        navigation?.NotifyDynamicSpaceChanged();
    }

    public bool BlocksPoint(
        Vector3 point,
        float radius,
        BistroBuilderNavigationAgentMask agent,
        string requesterId)
    {
        if (!IsActive || (blockedAgents & agent) == 0 ||
            (!string.IsNullOrEmpty(ownerId) &&
             string.Equals(ownerId, requesterId, StringComparison.Ordinal)))
            return false;

        Vector3 local = transform.InverseTransformPoint(point);
        Vector2 half = size * 0.5f;
        float sx = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
        float sz = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.z));
        float localRadiusX = radius / sx;
        float localRadiusZ = radius / sz;
        return Mathf.Abs(local.x - localCenter.x) <= half.x + localRadiusX &&
               Mathf.Abs(local.z - localCenter.z) <= half.y + localRadiusZ;
    }
    private void SyncSpatialLease(string owner, float durationSeconds)
    {
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (spatialService == null || string.IsNullOrWhiteSpace(owner)) return;

        if (!string.IsNullOrEmpty(spatialLeaseId) &&
            !string.Equals(spatialLeaseOwnerId, owner, StringComparison.Ordinal))
            ReleaseSpatialLease();
        if (!string.IsNullOrEmpty(spatialLeaseId))
        {
            spatialService.RefreshLease(spatialLeaseId, durationSeconds);
            return;
        }

        BistroBuilderSpatialClaimKind claimKind =
            kind == BistroBuilderDynamicSpaceKind.AgentPresence
                ? BistroBuilderSpatialClaimKind.Mobility
                : BistroBuilderSpatialClaimKind.DynamicSweep;
        var request = new BistroBuilderSpatialClaimRequest
        {
            ownerId = owner,
            kind = claimKind,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Box(
                WorldCenter,
                transform.right,
                transform.forward,
                WorldSize * 0.5f),
            durationSeconds = durationSeconds
        };
        if (spatialService.TryAcquireLease(
                request,
                out BistroBuilderSpatialLease lease,
                out _))
        {
            spatialLeaseId = lease.leaseId;
            spatialLeaseOwnerId = owner;
        }
    }

    private void ReleaseSpatialLease()
    {
        if (string.IsNullOrEmpty(spatialLeaseId)) return;
        spatialService?.ReleaseLease(spatialLeaseId);
        spatialLeaseId = string.Empty;
        spatialLeaseOwnerId = string.Empty;
    }
    protected void ConfigureEnvelope(
        Vector3 center,
        Vector2 envelopeSize,
        BistroBuilderNavigationAgentMask blocked,
        BistroBuilderDynamicSpaceKind envelopeKind)
    {
        localCenter = center;
        size = new Vector2(
            Mathf.Max(0.05f, envelopeSize.x),
            Mathf.Max(0.05f, envelopeSize.y));
        blockedAgents = blocked;
        kind = envelopeKind;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Vector3 center,
        Vector2 envelopeSize,
        BistroBuilderNavigationAgentMask blocked,
        BistroBuilderDynamicSpaceKind envelopeKind) =>
        ConfigureEnvelope(center, envelopeSize, blocked, envelopeKind);
#endif
}
