using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad visual única para cualquier operación provisional del modo edición.
/// No valida, no hace snapping y no confirma gameplay: recibe esos resultados y
/// los traduce a un lenguaje visual común para mobiliario y construcción.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Universal Preview Service")]
public sealed class BistroBuilderUniversalPreviewService : MonoBehaviour
{
    public const string FurnitureOwner = "furniture";
    public const string ConstructionOwner = "construction";

    private readonly BistroBuilderUniversalPreviewState current =
        new BistroBuilderUniversalPreviewState();

    public BistroBuilderUniversalPreviewState Current => current;
    public event Action<BistroBuilderUniversalPreviewState> PreviewChanged;
    public event Action PreviewCleared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeService()
    {
        GetOrCreate();
    }

    public static BistroBuilderUniversalPreviewService GetOrCreate()
    {
        BistroBuilderUniversalPreviewService existing =
            FindFirstObjectByType<BistroBuilderUniversalPreviewService>();
        if (existing != null) return existing;

        RestaurantEditModeService editMode =
            FindFirstObjectByType<RestaurantEditModeService>();
        if (editMode == null) return null;

        return editMode.gameObject.AddComponent<BistroBuilderUniversalPreviewService>();
    }

    public void PublishSegments(
        string ownerId,
        BistroBuilderPreviewDomain domain,
        BistroBuilderPreviewValidity validity,
        BistroBuilderPreviewPhase phase,
        IReadOnlyList<Vector3> segments,
        string message = "",
        IReadOnlyList<BistroBuilderPreviewBox> volumes = null)
    {
        BeginWrite(ownerId, domain, validity, phase, message);
        CopySegments(segments, current.MutableCandidateSegments);
        CopyVolumes(volumes, current.MutableVolumes);
        Publish();
    }

    public void SetGhostSegments(string ownerId, IReadOnlyList<Vector3> segments)
    {
        if (!IsOwner(ownerId)) return;
        CopySegments(segments, current.MutableGhostSegments);
        Publish();
    }

    public void SetSnapPoint(string ownerId, Vector3 point, bool visible)
    {
        if (!IsOwner(ownerId)) return;
        current.HasSnapPoint = visible;
        current.SnapPoint = point;
        if (visible && current.Phase == BistroBuilderPreviewPhase.Previewing)
            current.Phase = BistroBuilderPreviewPhase.Snapped;
        Publish();
    }

    public void PublishFurniture(
        RestaurantAreaMember member,
        Vector3 originalPosition,
        Quaternion originalRotation,
        RestaurantPlacementValidationResult validation,
        RestaurantPlacementSnapResult snapResult,
        bool showOriginalGhost = true)
    {
        if (member == null)
        {
            ClearOwner(FurnitureOwner);
            return;
        }

        Vector3 candidatePosition = member.transform.position;
        Quaternion candidateRotation = member.transform.rotation;
        BistroBuilderPreviewValidity validity = validation.IsValid
            ? BistroBuilderPreviewValidity.Valid
            : BistroBuilderPreviewValidity.Invalid;
        BistroBuilderPreviewPhase phase = snapResult.IsSnapped
            ? BistroBuilderPreviewPhase.Snapped
            : (validation.IsValid
                ? BistroBuilderPreviewPhase.Ready
                : BistroBuilderPreviewPhase.Previewing);

        BeginWrite(
            FurnitureOwner,
            BistroBuilderPreviewDomain.Furniture,
            validity,
            phase,
            validation.UserMessage);

        current.HasCandidatePose = true;
        current.CandidatePosition = candidatePosition;
        current.CandidateRotation = candidateRotation;
        current.RelatedObject = snapResult.RelatedObject;
        current.HasSnapPoint = snapResult.IsSnapped;
        current.SnapPoint = snapResult.IsSnapped
            ? snapResult.RootPosition
            : candidatePosition;

        if (member.TryGetComponent(out RestaurantPlacementFootprint footprint))
        {
            AddShapeSegments(
                footprint.BuildShapeAtPose(candidatePosition, candidateRotation),
                current.MutableCandidateSegments,
                0.018f);

            if (showOriginalGhost)
            {
                AddShapeSegments(
                    footprint.BuildShapeAtPose(originalPosition, originalRotation),
                    current.MutableGhostSegments,
                    0.012f);
            }
        }

        UnityEngine.Object conflict = ResolveConflictObject(validation);
        current.ConflictObject = conflict;
        AddConflictSegments(conflict, current.MutableConflictSegments);
        Publish();
    }

    public void MarkConfirmed(string ownerId)
    {
        if (!IsOwner(ownerId)) return;
        current.Phase = BistroBuilderPreviewPhase.Confirmed;
        Publish();
    }

    public void ClearOwner(string ownerId)
    {
        if (!IsOwner(ownerId)) return;
        current.Reset();
        current.Revision++;
        PreviewCleared?.Invoke();
        PreviewChanged?.Invoke(current);
    }

    private void BeginWrite(
        string ownerId,
        BistroBuilderPreviewDomain domain,
        BistroBuilderPreviewValidity validity,
        BistroBuilderPreviewPhase phase,
        string message)
    {
        current.OwnerId = ownerId ?? string.Empty;
        current.Domain = domain;
        current.Validity = validity;
        current.Phase = phase;
        current.Message = message ?? string.Empty;
        current.HasCandidatePose = false;
        current.CandidatePosition = Vector3.zero;
        current.CandidateRotation = Quaternion.identity;
        current.HasSnapPoint = false;
        current.SnapPoint = Vector3.zero;
        current.RelatedObject = null;
        current.ConflictObject = null;
        current.MutableCandidateSegments.Clear();
        current.MutableGhostSegments.Clear();
        current.MutableConflictSegments.Clear();
        current.MutableVolumes.Clear();
    }

    private void Publish()
    {
        current.Revision++;
        PreviewChanged?.Invoke(current);
    }

    private bool IsOwner(string ownerId)
    {
        return !string.IsNullOrEmpty(ownerId) &&
               string.Equals(current.OwnerId, ownerId, StringComparison.Ordinal);
    }

    private static void CopySegments(
        IReadOnlyList<Vector3> source,
        List<Vector3> destination)
    {
        destination.Clear();
        if (source == null) return;
        int evenCount = source.Count - (source.Count % 2);
        for (int i = 0; i < evenCount; i++)
            destination.Add(source[i]);
    }

    private static void CopyVolumes(
        IReadOnlyList<BistroBuilderPreviewBox> source,
        List<BistroBuilderPreviewBox> destination)
    {
        destination.Clear();
        if (source == null) return;

        for (int i = 0; i < source.Count; i++)
            destination.Add(source[i]);
    }

    private static void AddShapeSegments(
        RestaurantPlacementShape shape,
        List<Vector3> destination,
        float heightOffset)
    {
        Vector3 a = shape.GetCorner(-1f, -1f);
        Vector3 b = shape.GetCorner(1f, -1f);
        Vector3 c = shape.GetCorner(1f, 1f);
        Vector3 d = shape.GetCorner(-1f, 1f);
        a.y += heightOffset;
        b.y += heightOffset;
        c.y += heightOffset;
        d.y += heightOffset;
        AddSegment(destination, a, b);
        AddSegment(destination, b, c);
        AddSegment(destination, c, d);
        AddSegment(destination, d, a);
    }

    private static UnityEngine.Object ResolveConflictObject(
        RestaurantPlacementValidationResult validation)
    {
        if (validation.ConflictingFootprint != null)
            return validation.ConflictingFootprint;
        if (validation.ConflictingObstacle != null)
            return validation.ConflictingObstacle;
        if (validation.RelatedObject != null)
            return validation.RelatedObject;
        return null;
    }

    private static void AddConflictSegments(
        UnityEngine.Object conflict,
        List<Vector3> destination)
    {
        destination.Clear();
        if (conflict == null) return;

        GameObject go = null;
        if (conflict is Component component) go = component.gameObject;
        else if (conflict is GameObject gameObject) go = gameObject;
        if (go == null) return;

        RestaurantPlacementFootprint footprint =
            go.GetComponentInParent<RestaurantPlacementFootprint>();
        if (footprint != null)
        {
            AddShapeSegments(
                footprint.BuildCurrentShape(),
                destination,
                0.035f);
            return;
        }

        Renderer renderer = go.GetComponentInChildren<Renderer>();
        if (renderer == null) return;
        Bounds bounds = renderer.bounds;
        float y = bounds.min.y + 0.04f;
        Vector3 a = new Vector3(bounds.min.x, y, bounds.min.z);
        Vector3 b = new Vector3(bounds.max.x, y, bounds.min.z);
        Vector3 c = new Vector3(bounds.max.x, y, bounds.max.z);
        Vector3 d = new Vector3(bounds.min.x, y, bounds.max.z);
        AddSegment(destination, a, b);
        AddSegment(destination, b, c);
        AddSegment(destination, c, d);
        AddSegment(destination, d, a);
    }

    private static void AddSegment(List<Vector3> destination, Vector3 a, Vector3 b)
    {
        destination.Add(a);
        destination.Add(b);
    }
}
