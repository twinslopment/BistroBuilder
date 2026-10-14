using UnityEngine;

/// <summary>Inspector requests use the existing creation/placement authority.
/// Catalog settings are transient; instances persist their existing Transform.</summary>
public sealed partial class RestaurantEditInteractionController
{
    private RestaurantPlaceableItemDefinition inspectorDefinition;
    private Vector3 inspectorScale = Vector3.one;
    private Vector3 inspectorEuler;
    public string InspectorTransformMessage { get; private set; } = "";

    public void PrepareInspectorDefinition(RestaurantPlaceableItemDefinition item)
    {
        if (inspectorDefinition == item) return;
        inspectorDefinition = item;
        inspectorScale = Vector3.one;
        inspectorEuler = Vector3.zero;
        InspectorTransformMessage = "";
    }

    public bool CanConfigureInspectorDimensions(RestaurantPlaceableItemDefinition item) =>
        item != null && item.Prefab != null && item.EditableDefinition != null &&
        item.EditableDefinition.CanResize && item.Category != RestaurantPlaceableItemCategory.Structural &&
        PositiveFinite(item.DimensionsCentimeters);

    public Vector3 GetInspectorDimensions(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject instance = null)
    {
        if (item == null) return Vector3.zero;
        var target = ResolveInspectorInstance(item, instance);
        if (target == null) return Vector3.Scale(item.DimensionsCentimeters,
            inspectorDefinition == item ? inspectorScale : Vector3.one);
        var baseline = item.Prefab.transform.lossyScale;
        return Vector3.Scale(item.DimensionsCentimeters, Divide(target.transform.lossyScale, baseline));
    }

    public Vector3 GetInspectorRotation(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject instance = null)
    {
        var target = ResolveInspectorInstance(item, instance);
        return target != null ? target.transform.eulerAngles : inspectorDefinition == item ? inspectorEuler : Vector3.zero;
    }

    public bool TrySetInspectorDimensions(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject instance, Vector3 dimensions)
    {
        if (!CanConfigureInspectorDimensions(item) || !PositiveFinite(dimensions))
            return InspectorRejected("Introduce medidas mayores que cero.");
        Vector3 multiplier = Divide(dimensions, item.DimensionsCentimeters);
        var policy = item.EditableDefinition;
        if (Mathf.Min(multiplier.x, multiplier.y, multiplier.z) < policy.MinimumInspectorScale ||
            Mathf.Max(multiplier.x, multiplier.y, multiplier.z) > policy.MaximumInspectorScale)
            return InspectorRejected("Medidas admitidas: " + policy.MinimumInspectorScale.ToString("0.##") +
                "–" + policy.MaximumInspectorScale.ToString("0.##") + " veces el tamaño original.");
        var target = ResolveInspectorInstance(item, instance);
        if (target == null)
        {
            if (HasActivePlacement) return InspectorRejected("Termina la colocación activa antes de configurar otro artículo.");
            PrepareInspectorDefinition(item); inspectorScale = multiplier;
            return InspectorAccepted("Medidas preparadas para la colocación.");
        }
        Vector3 parentScale = target.transform.parent != null ? target.transform.parent.lossyScale : Vector3.one;
        Vector3 scale = Divide(Vector3.Scale(item.Prefab.transform.lossyScale, multiplier), parentScale);
        return TryApplyInspectorTransform(target, scale, target.transform.rotation, "Dimensiones actualizadas.");
    }

    public bool TrySetInspectorRotation(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject instance, Vector3 euler)
    {
        if (item == null || item.EditableDefinition == null || !item.EditableDefinition.CanRotate || !Finite(euler))
            return InspectorRejected("Este artículo no permite esa rotación.");
        euler = new Vector3(Mathf.Repeat(euler.x, 360), Mathf.Repeat(euler.y, 360), Mathf.Repeat(euler.z, 360));
        var target = ResolveInspectorInstance(item, instance);
        if (target == null)
        {
            if (HasActivePlacement) return InspectorRejected("Termina la colocación activa antes de configurar otro artículo.");
            PrepareInspectorDefinition(item); inspectorEuler = euler;
            return InspectorAccepted("Rotación preparada para la colocación.");
        }
        return TryApplyInspectorTransform(target, target.transform.localScale, Quaternion.Euler(euler), "Rotación actualizada.");
    }

    private RestaurantPlaceableObject ResolveInspectorInstance(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject instance)
    {
        if (instance != null && instance.ItemDefinition == item) return instance;
        var provisional = creationService != null ? creationService.ActiveProvisionalPlaceable : null;
        return provisional != null && provisional.ItemDefinition == item ? provisional : null;
    }

    private bool TryApplyInspectorTransform(RestaurantPlaceableObject target, Vector3 scale, Quaternion rotation, string message)
    {
        if (editModeService == null || !editModeService.IsEditModeActive || transactionService == null ||
            !target.TryGetComponent(out RestaurantAreaMember member) ||
            !target.TryGetComponent(out RestaurantEditableObject editable) || !editable.EditingEnabled)
            return InspectorRejected("El artículo no está disponible para editar.");
        if (transactionService.HasActiveTransaction)
        {
            if (activeMember != member) return InspectorRejected("Hay otra operación de colocación activa.");
            target.transform.localScale = scale;
            candidateRotation = rotation; hasPublishedPreviewPose = false;
            placementSnapService?.ReleaseCurrentCapture();
            Physics.SyncTransforms();
            TryPreviewActivePlacementAtWorldPose(candidatePosition, rotation, out _, out _);
            return InspectorAccepted("Cambio provisional. Clic para colocar; Escape para cancelar.");
        }
        var selection = FindFirstObjectByType<BistroBuilderEditorV2SelectionCoordinator>();
        if (selectedMember != member || (selection != null && selection.SelectionCount > 1))
            return InspectorRejected("Selecciona un único artículo para editar sus medidas.");
        if ((target.transform.localScale - scale).sqrMagnitude < .0000001f &&
            Quaternion.Angle(target.transform.rotation, rotation) < .001f) return true;
        if (!transactionService.TryBeginPlacement(member, out _)) return InspectorRejected("No se pudo iniciar el cambio.");
        target.transform.localScale = scale;
        Physics.SyncTransforms();
        bool previewed = transactionService.TryPreviewPlacement(target.transform.position, rotation, out var result, out _);
        bool committed = previewed && transactionService.TryCommitPlacement(out result, out _);
        if (!committed)
        {
            transactionService.CancelPlacement();
            Physics.SyncTransforms();
            return InspectorRejected("Cambio rechazado: comprueba espacio, límites y separaciones.");
        }
        LastRotationFeedbackTime = Time.unscaledTime;
        return InspectorAccepted(message);
    }

    private void ApplyPreparedInspectorTransform(RestaurantPlaceableItemDefinition item, RestaurantPlaceableObject target)
    {
        if (inspectorDefinition != item || target == null) return;
        target.transform.localScale = Vector3.Scale(target.transform.localScale, inspectorScale);
        target.transform.rotation = Quaternion.Euler(inspectorEuler);
        Physics.SyncTransforms();
    }

    private bool InspectorRejected(string message) { InspectorTransformMessage = message; PublishMessage(message); return false; }
    private bool InspectorAccepted(string message) { InspectorTransformMessage = message; PublishMessage(message); return true; }
    private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    private static bool PositiveFinite(Vector3 v) => Finite(v) && v.x > .001f && v.y > .001f && v.z > .001f;
    private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) &&
        !float.IsNaN(v.y) && !float.IsInfinity(v.y) && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
}
