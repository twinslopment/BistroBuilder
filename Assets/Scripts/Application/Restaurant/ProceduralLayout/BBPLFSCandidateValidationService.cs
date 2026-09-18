using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct BBPLFSCandidateEvaluation
{
    public readonly bool IsValid;
    public readonly float ScoreAdjustment;
    public readonly string ReasonCode;
    public readonly string Message;

    public BBPLFSCandidateEvaluation(bool valid, float scoreAdjustment, string reasonCode, string message)
    {
        IsValid = valid;
        ScoreAdjustment = scoreAdjustment;
        ReasonCode = reasonCode ?? string.Empty;
        Message = message ?? string.Empty;
    }

    public static BBPLFSCandidateEvaluation Valid(float scoreAdjustment = 0f) =>
        new(true, scoreAdjustment, string.Empty, string.Empty);
    public static BBPLFSCandidateEvaluation Invalid(string code, string message) =>
        new(false, 0f, code, message);
}

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Candidate Validation Service")]
public sealed class BBPLFSCandidateValidationService : MonoBehaviour
{
    [SerializeField] private RestaurantPlacementValidationService placementValidationService;
    [SerializeField] private RestaurantAreaMemberRegistry memberRegistry;
    [SerializeField] private RestaurantPlacementRegistry placementRegistry;
    [SerializeField] private RestaurantPlaceableRegistry placeableRegistry;
    [SerializeField] private RestaurantTableRegistry tableRegistry;
    [SerializeField] private RestaurantSeatRegistry seatRegistry;
    [SerializeField] private BistroBuilderSpatialPlacementAssessmentService spatialAssessmentService;
    [SerializeField] private BistroBuilderNavigationService navigationService;

    private readonly List<TemporaryObject> temporary = new();
    private readonly List<Vector3> routeBuffer = new(64);
    private GameObject temporaryRoot;

    public BBPLFSCandidateEvaluation Evaluate(
        BBPLFSLayoutCandidate candidate,
        RestaurantArea area,
        BBPLFSAssetLayoutCatalog catalog)
    {
        ResolveDependencies();
        if (candidate == null || area == null || catalog == null)
            return BBPLFSCandidateEvaluation.Invalid("bbplfs.invalid_input", "Falta el candidato, el área o el catálogo.");
        if (placementValidationService == null || memberRegistry == null || placementRegistry == null || placeableRegistry == null)
            return BBPLFSCandidateEvaluation.Invalid("bbplfs.authority_unavailable", "No están disponibles las autoridades de colocación del Modo Edición.");

        try
        {
            if (!CreateTemporaryLayout(candidate, area, catalog, out string createError))
                return BBPLFSCandidateEvaluation.Invalid("bbplfs.preview_build_failed", createError);

            spatialAssessmentService?.RefreshProviderCache();
            for (int i = 0; i < temporary.Count; i++)
            {
                TemporaryObject item = temporary[i];
                RestaurantPlacementValidationResult result = placementValidationService.ValidatePlacement(
                    item.Member, item.Root.transform.position, item.Root.transform.rotation);
                if (!result.IsValid)
                {
                    string detail = ResolvePlacementMessage(result) +
                        " Object=" + item.Root.name +
                        " root=" + item.Root.transform.position.ToString("F3") +
                        " ref=" + item.Member.ReferencePosition.ToString("F3") +
                        " areaRoot=" + area.ContainsPosition(item.Root.transform.position) +
                        " areaRef=" + area.ContainsPosition(item.Member.ReferencePosition);
                    return BBPLFSCandidateEvaluation.Invalid(
                        ResolvePlacementReason(result), detail);
                }
            }

            float navigationAdjustment = 0f;
            if (navigationService != null)
            {
                navigationService.RebuildNavigationTopology();
                BBPLFSCandidateEvaluation nav = EvaluateNavigation(candidate);
                if (!nav.IsValid) return nav;
                navigationAdjustment = nav.ScoreAdjustment;
            }
            return BBPLFSCandidateEvaluation.Valid(navigationAdjustment);
        }
        finally
        {
            CleanupTemporaryLayout();
        }
    }

    private bool CreateTemporaryLayout(BBPLFSLayoutCandidate candidate, RestaurantArea area,
        BBPLFSAssetLayoutCatalog catalog, out string error)
    {
        CleanupTemporaryLayout();
        temporaryRoot = new GameObject("BBPLFS_CANDIDATE_VALIDATION");
        temporaryRoot.hideFlags = HideFlags.HideAndDontSave;
        temporaryRoot.SetActive(false);

        for (int i = 0; i < candidate.Placements.Count; i++)
        {
            BBPLFSLayoutPlacement placement = candidate.Placements[i];
            if (!TryGetDefinition(catalog, placement.ItemId, out RestaurantPlaceableItemDefinition definition) || definition.Prefab == null)
            { error = "No existe un prefab válido para " + placement.ItemId + "."; return false; }

            GameObject clone = Instantiate(definition.Prefab.gameObject, temporaryRoot.transform);
            clone.name = "BBPLFS_Temp_" + i + "_" + definition.ItemId;
            clone.hideFlags = HideFlags.HideAndDontSave;
            clone.transform.SetPositionAndRotation(placement.WorldPosition, placement.WorldRotation);
            if (!clone.TryGetComponent(out RestaurantPlaceableObject placeable) ||
                !clone.TryGetComponent(out RestaurantAreaMember member))
            { error = definition.ItemId + " no contiene los contratos mínimos de colocación."; return false; }

            placeable.SetItemDefinition(definition);
            placeable.AssignInstanceId("bbplfs_temp_" + candidate.CandidateId + "_" + i);
            member.SetArea(area);
            BistroBuilderSpatialSubject[] subjects = clone.GetComponentsInChildren<BistroBuilderSpatialSubject>(true);
            for (int s = 0; s < subjects.Length; s++)
            {
                BistroBuilderSpatialSubject subject = subjects[s];
                subject.Configure("bbplfs_temp_" + candidate.CandidateId + "_" + i + "_" + s,
                    subject.Contract, subject.Proxy);
            }
            temporary.Add(new TemporaryObject(clone, placeable, member));
        }

        temporaryRoot.SetActive(true);
        for (int i = 0; i < temporary.Count; i++)
        {
            TemporaryObject item = temporary[i];
            memberRegistry.RegisterMember(item.Member);
            placeableRegistry.RegisterPlaceable(item.Placeable);
            if (item.Root.TryGetComponent(out RestaurantTable table)) tableRegistry?.RegisterTable(table);
            if (item.Root.TryGetComponent(out RestaurantSeat seat)) seatRegistry?.RegisterSeat(seat);
            if (item.Root.TryGetComponent(out RestaurantPlacementFootprint footprint)) placementRegistry.RegisterFootprint(footprint);
        }
        error = string.Empty;
        return true;
    }

    private BBPLFSCandidateEvaluation EvaluateNavigation(BBPLFSLayoutCandidate candidate)
    {
        bool containsDining = false;
        for (int i = 0; i < candidate.Placements.Count; i++)
            if (string.Equals(candidate.Placements[i].Role, "table", StringComparison.OrdinalIgnoreCase))
            { containsDining = true; break; }
        if (!containsDining) return BBPLFSCandidateEvaluation.Valid();

        GameObject entranceObject = GameObject.Find("RestaurantEntrancePoint");
        Transform entrance = entranceObject != null ? entranceObject.transform : null;
        if (entrance == null)
            return BBPLFSCandidateEvaluation.Invalid("navigation.entrance_missing", "No existe un acceso de entrada navegable.");

        KitchenSystem[] kitchens = FindObjectsByType<KitchenSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.InstanceID);
        float qualitySum = 0f;
        int qualityCount = 0;
        for (int i = 0; i < temporary.Count; i++)
        {
            if (!temporary[i].Root.TryGetComponent(out RestaurantTable table)) continue;
            if (table.CustomerApproachPoint != null)
            {
                if (!TryRoute("bbplfs_customer_" + i, BistroBuilderNavigationAgentMask.Customer,
                        entrance.position, table.CustomerApproachPoint.position, out float customerQuality))
                    return BBPLFSCandidateEvaluation.Invalid("navigation.customer_unreachable", "Una mesa generada no es alcanzable desde la entrada.");
                qualitySum += customerQuality; qualityCount++;
            }
            if (table.WaiterServicePoint != null && kitchens.Length > 0)
            {
                float bestQuality = -1f;
                bool reachable = false;
                for (int k = 0; k < kitchens.Length; k++)
                {
                    if (kitchens[k] == null || kitchens[k].PickupPoint == null) continue;
                    if (TryRoute("bbplfs_waiter_" + i + "_" + k, BistroBuilderNavigationAgentMask.Waiter,
                            kitchens[k].PickupPoint.position, table.WaiterServicePoint.position, out float q))
                    { reachable = true; bestQuality = Mathf.Max(bestQuality, q); }
                }
                if (!reachable)
                    return BBPLFSCandidateEvaluation.Invalid("navigation.service_unreachable", "Una mesa generada no tiene ruta de servicio desde cocina.");
                qualitySum += bestQuality; qualityCount++;
            }
        }
        float quality = qualityCount > 0 ? qualitySum / qualityCount : 0.5f;
        return BBPLFSCandidateEvaluation.Valid((quality - 0.5f) * 0.30f);
    }

    private bool TryRoute(string requester, BistroBuilderNavigationAgentMask agent,
        Vector3 from, Vector3 to, out float quality)
    {
        routeBuffer.Clear();
        quality = 0f;
        if (!navigationService.TryBuildRoute(requester, agent, from, to, routeBuffer, out float meters, out _)) return false;
        float direct = Mathf.Max(0.1f, Vector3.Distance(from, to));
        quality = Mathf.Clamp01(direct / Mathf.Max(direct, meters));
        return true;
    }

    private void CleanupTemporaryLayout()
    {
        if (temporary.Count > 0)
        {
            for (int i = temporary.Count - 1; i >= 0; i--)
            {
                TemporaryObject item = temporary[i];
                if (item.Root == null) continue;
                if (item.Root.TryGetComponent(out RestaurantPlacementFootprint footprint)) placementRegistry?.UnregisterFootprint(footprint);
                if (item.Root.TryGetComponent(out RestaurantSeat seat)) seatRegistry?.UnregisterSeat(seat);
                if (item.Root.TryGetComponent(out RestaurantTable table)) tableRegistry?.UnregisterTable(table);
                placeableRegistry?.UnregisterPlaceable(item.Placeable);
                memberRegistry?.UnregisterMember(item.Member);
                item.Root.SetActive(false);
            }
        }
        temporary.Clear();
        if (temporaryRoot != null)
        {
            temporaryRoot.SetActive(false);
            if (Application.isPlaying) Destroy(temporaryRoot); else DestroyImmediate(temporaryRoot);
            temporaryRoot = null;
        }
        spatialAssessmentService?.RefreshProviderCache();
        navigationService?.RebuildNavigationTopology();
    }

    private static bool TryGetDefinition(BBPLFSAssetLayoutCatalog catalog, string itemId,
        out RestaurantPlaceableItemDefinition definition)
    {
        for (int i = 0; i < catalog.Profiles.Count; i++)
        {
            BBPLFSAssetLayoutProfile profile = catalog.Profiles[i];
            if (profile != null && profile.ItemDefinition != null &&
                string.Equals(profile.ItemDefinition.ItemId, itemId, StringComparison.Ordinal))
            { definition = profile.ItemDefinition; return true; }
        }
        definition = null; return false;
    }

    private static string ResolvePlacementReason(RestaurantPlacementValidationResult result)
    {
        if (result.Status == RestaurantPlacementValidationStatus.PlacementConstraintViolation &&
            !string.IsNullOrWhiteSpace(result.ConstraintEvaluation.RuleId)) return result.ConstraintEvaluation.RuleId;
        return "placement." + result.Status.ToString().ToLowerInvariant();
    }

    private static string ResolvePlacementMessage(RestaurantPlacementValidationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.UserMessage)) return result.UserMessage;
        return result.Status switch
        {
            RestaurantPlacementValidationStatus.FootprintOutsideCandidateArea => "Parte de un elemento generado queda fuera del área seleccionada.",
            RestaurantPlacementValidationStatus.PhysicalOverlap => "Dos elementos o un obstáculo ocupan el mismo espacio.",
            RestaurantPlacementValidationStatus.MinimumClearanceViolation => "No se cumple la separación mínima necesaria.",
            RestaurantPlacementValidationStatus.MissingRequiredCapability => "El área no dispone de una capacidad requerida por el elemento.",
            RestaurantPlacementValidationStatus.SystemUnavailable => "La autoridad de colocación no está disponible.",
            _ => "La autoridad de colocación ha rechazado una pose generada."
        };
    }

    private void ResolveDependencies()
    {
        if (placementValidationService == null) placementValidationService = FindFirstObjectByType<RestaurantPlacementValidationService>();
        if (memberRegistry == null) memberRegistry = FindFirstObjectByType<RestaurantAreaMemberRegistry>();
        if (placementRegistry == null) placementRegistry = FindFirstObjectByType<RestaurantPlacementRegistry>();
        if (placeableRegistry == null) placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (tableRegistry == null) tableRegistry = FindFirstObjectByType<RestaurantTableRegistry>();
        if (seatRegistry == null) seatRegistry = FindFirstObjectByType<RestaurantSeatRegistry>();
        if (spatialAssessmentService == null) spatialAssessmentService = FindFirstObjectByType<BistroBuilderSpatialPlacementAssessmentService>();
        if (navigationService == null) navigationService = FindFirstObjectByType<BistroBuilderNavigationService>();
    }

    private sealed class TemporaryObject
    {
        public readonly GameObject Root;
        public readonly RestaurantPlaceableObject Placeable;
        public readonly RestaurantAreaMember Member;
        public TemporaryObject(GameObject root, RestaurantPlaceableObject placeable, RestaurantAreaMember member)
        { Root = root; Placeable = placeable; Member = member; }
    }
}
