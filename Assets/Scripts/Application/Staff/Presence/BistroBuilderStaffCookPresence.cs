using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-only chef presence derived from canonical staff and schedules. It
/// never creates an Employee, Waiter, kitchen task or parallel cook authority.
/// The authored cook model is mandatory: no placeholder or disguised waiter.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Kitchen Staff Presence")]
public sealed class BistroBuilderStaffCookPresence : MonoBehaviour
{
    [SerializeField] private BistroBuilderStaffService staffService;
    [SerializeField] private BistroBuilderStaffScheduleService scheduleService;
    [SerializeField] private BistroBuilderGeneralGameStateService generalGameStateService;
    [SerializeField] private BistroBuilderCanonicalOrderIntegrationService orderIntegration;
    [SerializeField] private RestaurantServiceStateService serviceStateService;
    [SerializeField] private GameObject cookVisualPrefab;
    [SerializeField] private Transform kitchenStaffAnchor;
    [SerializeField, Min(.2f)] private float staffSpacing = .65f;

    private readonly Dictionary<string, GameObject> live =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);
    private readonly List<BistroBuilderEmployeeRecord> employees =
        new List<BistroBuilderEmployeeRecord>(16);
    private readonly List<string> obsolete = new List<string>(16);
    private readonly HashSet<string> eligible = new HashSet<string>(StringComparer.Ordinal);

    private RestaurantArea kitchenArea;
    private bool dirty = true;
    private bool notifiedMissingVisual;
    private bool subscribed;

    public int VisibleCookCount => live.Count;

    private void Awake()
    {
        CacheDependencies();
        if (cookVisualPrefab == null)
            cookVisualPrefab = Resources.Load<GameObject>(
                "BistroBuilder/Characters/CookPresence");
    }

    private void OnEnable()
    {
        CacheDependencies();
        Subscribe();
        dirty = true;
    }

    private void OnDisable()
    {
        Unsubscribe();
        RetireAll();
    }

    private void Update()
    {
        // An event-driven dirty gate, not scene-wide polling each frame.
        if (!dirty || BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
            return;
        dirty = false;
        if (!TryReconcile(out string error) && !string.IsNullOrWhiteSpace(error) &&
            !notifiedMissingVisual)
        {
            Debug.LogWarning("Personal / presencia de cocina: " + error, this);
            notifiedMissingVisual = true;
        }
    }

    public bool TryReconcile(out string error)
    {
        error = string.Empty;
        CacheDependencies();
        if (staffService == null || scheduleService == null ||
            generalGameStateService == null || orderIntegration == null ||
            serviceStateService == null)
        {
            error = "No están conectados los servicios canónicos de Personal, Horarios y Cocina.";
            return false;
        }
        if (BistroBuilderActiveServiceRuntimeLoadScope.IsRestoring)
        {
            dirty = true;
            return true;
        }

        RestaurantServiceState state = serviceStateService.CurrentState;
        if (state == RestaurantServiceState.Closed)
        {
            RetireAll();
            return true;
        }

        if (orderIntegration.CurrentMealService ==
            BistroBuilderMealServiceAvailability.None)
        {
            RetireAll();
            return true;
        }

        // Use the operational adapter, not visual assumptions about role name.
        eligible.Clear();
        employees.Clear();
        staffService.CopyEmployees(employees, false);
        int dayIndex = Math.Max(1, generalGameStateService.DayIndex);
        for (int i = 0; i < employees.Count; i++)
        {
            BistroBuilderEmployeeRecord person = employees[i];
            if (person == null ||
                person.employmentStatus != BistroBuilderEmploymentStatus.Active ||
                person.availability != BistroBuilderEmployeeAvailability.Available ||
                !staffService.TryGetRoleDefinition(
                    person.roleId, out BistroBuilderStaffRoleDefinition definition) ||
                definition == null || !definition.active ||
                !string.Equals(definition.operationalAdapterId,
                    BistroBuilderStaffOperationalAdapterIds.CookAgent,
                    StringComparison.Ordinal) ||
                !scheduleService.IsScheduled(
                    person.employeeId, dayIndex, orderIntegration.CurrentMealService))
                continue;
            eligible.Add(person.employeeId);
        }

        obsolete.Clear();
        foreach (KeyValuePair<string, GameObject> entry in live)
            if (!eligible.Contains(entry.Key) || entry.Value == null)
                obsolete.Add(entry.Key);
        for (int i = 0; i < obsolete.Count; i++)
            Retire(obsolete[i]);

        if (eligible.Count == 0) return true;
        if (cookVisualPrefab == null)
        {
            error = "Falta el prefab de personaje Cocinero/a; no se crearán figuras provisionales.";
            return false;
        }
        if (cookVisualPrefab.GetComponentInChildren<Waiter>(true) != null ||
            cookVisualPrefab.GetComponentInChildren<CustomerGroup>(true) != null ||
            cookVisualPrefab.GetComponentInChildren<KitchenSystem>(true) != null)
        {
            error = "El prefab visual del cocinero contiene componentes de gameplay incompatibles.";
            return false;
        }
        if (!TryResolveKitchenArea(out error))
            return false;

        employees.Sort((a, b) => string.CompareOrdinal(a.employeeId, b.employeeId));
        int ordinal = 0;
        for (int i = 0; i < employees.Count; i++)
        {
            BistroBuilderEmployeeRecord person = employees[i];
            if (person == null || !eligible.Contains(person.employeeId))
                continue;
            if (live.ContainsKey(person.employeeId))
            {
                ordinal++;
                continue;
            }
            if (!TryFindKitchenPosition(ordinal, out Vector3 position))
            {
                error = "No existe una plaza física comprobable dentro de la zona Cocina.";
                return false;
            }
            GameObject avatar = Instantiate(cookVisualPrefab,
                position, kitchenArea.transform.rotation, kitchenArea.transform);
            avatar.name = "StaffCook_" + person.employeeId;
            // The avatar remains presentation only: Kitchen's AssignCook is
            // the authority for actual preparation and employee attribution.
            live.Add(person.employeeId, avatar);
            ordinal++;
        }
        notifiedMissingVisual = false;
        return true;
    }

    private bool TryResolveKitchenArea(out string error)
    {
        if (kitchenArea != null && kitchenArea.isActiveAndEnabled &&
            kitchenArea.IsOperational && kitchenArea.Definition != null &&
            string.Equals(kitchenArea.Definition.AreaTypeId, "kitchen",
                StringComparison.Ordinal))
        {
            error = string.Empty;
            return true;
        }

        kitchenArea = null;
        RestaurantArea[] areas = FindObjectsByType<RestaurantArea>(
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < areas.Length; i++)
        {
            RestaurantArea area = areas[i];
            if (area != null && area.IsOperational && area.Definition != null &&
                string.Equals(area.Definition.AreaTypeId, "kitchen",
                    StringComparison.Ordinal))
            {
                kitchenArea = area;
                break;
            }
        }
        error = kitchenArea == null
            ? "No existe una zona física Cocina operativa; no se mostrará al cocinero en Sala."
            : string.Empty;
        return kitchenArea != null;
    }

    private bool TryFindKitchenPosition(int index, out Vector3 result)
    {
        result = Vector3.zero;
        if (kitchenArea == null) return false;
        if (kitchenStaffAnchor != null &&
            kitchenArea.ContainsPosition(kitchenStaffAnchor.position))
        {
            result = kitchenStaffAnchor.position +
                kitchenStaffAnchor.right * (index * staffSpacing);
            if (kitchenArea.ContainsPosition(result)) return true;
        }

        IReadOnlyList<Collider> boundaries = kitchenArea.BoundaryColliders;
        if (boundaries == null) return false;
        for (int i = 0; i < boundaries.Count; i++)
        {
            Collider zone = boundaries[i];
            if (zone == null || !zone.enabled) continue;
            Bounds bounds = zone.bounds;
            Vector3 center = bounds.center;
            center.y = bounds.min.y + Mathf.Max(.025f,
                Mathf.Min(.10f, bounds.extents.y));
            float radius = Math.Min(bounds.extents.x, bounds.extents.z) * .30f;
            float angle = index * 2.399963f;
            Vector3 candidate = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f, Mathf.Sin(angle) * radius);
            if (!kitchenArea.ContainsPosition(candidate)) candidate = center;
            if (!kitchenArea.ContainsPosition(candidate)) continue;
            result = candidate;
            return true;
        }
        return false;
    }

    private void Retire(string employeeId)
    {
        if (!live.TryGetValue(employeeId, out GameObject avatar)) return;
        live.Remove(employeeId);
        if (avatar == null) return;
        avatar.SetActive(false);
        Destroy(avatar);
    }

    private void RetireAll()
    {
        obsolete.Clear();
        foreach (string id in live.Keys) obsolete.Add(id);
        for (int i = 0; i < obsolete.Count; i++) Retire(obsolete[i]);
        obsolete.Clear();
    }

    private void Invalidate() => dirty = true;
    private void OnStaffChanged(long _) => Invalidate();
    private void OnScheduleChanged(long _) => Invalidate();
    private void OnServiceStateChanged(
        RestaurantServiceState _, RestaurantServiceState __) => Invalidate();
    private void OnMealChanged(BistroBuilderMealServiceAvailability _) => Invalidate();

    private void Subscribe()
    {
        if (subscribed || staffService == null || scheduleService == null ||
            serviceStateService == null || orderIntegration == null) return;
        staffService.StaffChanged += OnStaffChanged;
        staffService.StateRestored += Invalidate;
        scheduleService.ScheduleChanged += OnScheduleChanged;
        scheduleService.ScheduleRestored += Invalidate;
        serviceStateService.StateChanged += OnServiceStateChanged;
        orderIntegration.CurrentMealServiceChanged += OnMealChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        if (staffService != null)
        {
            staffService.StaffChanged -= OnStaffChanged;
            staffService.StateRestored -= Invalidate;
        }
        if (scheduleService != null)
        {
            scheduleService.ScheduleChanged -= OnScheduleChanged;
            scheduleService.ScheduleRestored -= Invalidate;
        }
        if (serviceStateService != null)
            serviceStateService.StateChanged -= OnServiceStateChanged;
        if (orderIntegration != null)
            orderIntegration.CurrentMealServiceChanged -= OnMealChanged;
        subscribed = false;
    }

    private void CacheDependencies()
    {
        if (staffService == null) TryGetComponent(out staffService);
        if (scheduleService == null) TryGetComponent(out scheduleService);
        if (generalGameStateService == null) TryGetComponent(out generalGameStateService);
        if (orderIntegration == null) TryGetComponent(out orderIntegration);
        if (serviceStateService == null) TryGetComponent(out serviceStateService);
    }
}
