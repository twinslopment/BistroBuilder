using System;
using BistroBuilder.CameraSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Une la cámara profesional 369A/369C con la edición de mobiliario.
///
/// Principios:
/// - Nunca sustituye la autoridad de la cámara.
/// - No sigue el objeto durante el arrastre: evita realimentación cámara/puntero.
/// - Solo reencuadra al seleccionar o empezar una colocación cuando el objeto
///   queda demasiado pequeño o fuera de la zona cómoda del mundo.
/// - Mantiene yaw/pitch del jugador dentro de un rango de edición razonable.
/// - Usa el volumen visual real del objeto, ignorando geometría de debug.
/// </summary>
[DefaultExecutionOrder(120)]
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Restaurant/Edit Mode/Edit Camera Focus Coordinator")]
public sealed class BistroBuilderEditCameraFocusCoordinator : MonoBehaviour
{
    [Header("Dependencias")]
    [SerializeField] private RestaurantEditInteractionController interactionController;
    [SerializeField] private RestaurantEditModeService editModeService;
    [SerializeField] private BistroBuilderProfessionalCameraController cameraController;
    [SerializeField] private BistroBuilderCameraInspectionService inspectionService;
    [SerializeField] private Camera worldCamera;

    [Header("Encuadre contextual")]
    [SerializeField] private bool autoFrameSelection = true;
    [SerializeField] private bool autoFramePlacementStart = true;
    [SerializeField] private Vector2 comfortableViewportMin =
        new Vector2(0.28f, 0.18f);
    [SerializeField] private Vector2 comfortableViewportMax =
        new Vector2(0.72f, 0.80f);
    [SerializeField, Range(0.02f, 0.25f)]
    private float minimumVisibleViewportSpan = 0.085f;
    [SerializeField, Range(1f, 2f)]
    private float selectionFramingMargin = 1.24f;
    [SerializeField, Min(1f)]
    private float minimumSelectionDistance = 4.35f;
    [SerializeField, Range(20f, 70f)]
    private float minimumEditPitch = 34f;
    [SerializeField, Range(20f, 75f)]
    private float maximumEditPitch = 56f;
    [SerializeField, Min(0f)]
    private float repeatedFocusCooldown = 0.08f;

    private readonly Vector3[] boundsCorners = new Vector3[8];
    private RestaurantAreaMember lastFocusedMember;
    private float lastFocusTime = -100f;
    private bool subscribed;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(
        Scene scene,
        LoadSceneMode mode)
    {
        EnsureRuntimeInstallation();
    }

    private static void EnsureRuntimeInstallation()
    {
        RestaurantEditInteractionController interaction =
            FindFirstObjectByType<RestaurantEditInteractionController>();

        if (interaction == null ||
            interaction.GetComponent<
                BistroBuilderEditCameraFocusCoordinator>() != null)
        {
            return;
        }

        interaction.gameObject.AddComponent<
            BistroBuilderEditCameraFocusCoordinator>();
    }

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (editModeService != null &&
            editModeService.IsEditModeActive)
        {
            EnterEditCameraContext();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        lastFocusedMember = null;
        lastFocusTime = -100f;
    }

    public bool TryFocusSelected(bool force = true)
    {
        ResolveDependencies();

        RestaurantAreaMember selected =
            interactionController != null
                ? interactionController.SelectedMember
                : null;

        if (selected == null &&
            interactionController != null)
        {
            selected =
                interactionController.ActiveMember;
        }

        return TryFocusMember(selected, force);
    }

    public bool TryFocusMember(
        RestaurantAreaMember member,
        bool force)
    {
        ResolveDependencies();

        if (member == null ||
            editModeService == null ||
            !editModeService.IsEditModeActive ||
            cameraController == null ||
            worldCamera == null ||
            inspectionService == null ||
            inspectionService.InspectionSettings == null ||
            !cameraController.IsInitialized ||
            cameraController.IsDirectManipulationActive)
        {
            return false;
        }

        if (!force &&
            ReferenceEquals(lastFocusedMember, member) &&
            Time.unscaledTime - lastFocusTime <
                Mathf.Max(0f, repeatedFocusCooldown))
        {
            return false;
        }

        if (!TryResolveVisualBounds(
                member,
                out Bounds bounds))
        {
            return false;
        }

        if (!TryBuildFocusState(
                bounds,
                out BistroBuilderCameraNavigationState target))
        {
            return false;
        }

        if (!force &&
            IsCurrentFramingComfortable(
                bounds,
                target.Distance))
        {
            return false;
        }

        if (inspectionService.FramingService != null &&
            inspectionService.FramingService.ActiveView !=
                BistroBuilderCameraViewId.None)
        {
            inspectionService.FramingService.ExitPresetKeepingCurrentView();
        }

        cameraController.SetTargetState(
            target,
            false);

        lastFocusedMember = member;
        lastFocusTime = Time.unscaledTime;
        return true;
    }

    private bool TryResolveVisualBounds(
        RestaurantAreaMember member,
        out Bounds bounds)
    {
        bounds = default;

        return member != null &&
            BistroBuilderCameraInspectionBounds.TryCalculate(
                member.gameObject,
                inspectionService.InspectionSettings,
                false,
                out bounds,
                out _);
    }

    private bool TryBuildFocusState(
        Bounds bounds,
        out BistroBuilderCameraNavigationState state)
    {
        state = default;

        BistroBuilderCameraNavigationState current =
            cameraController.TargetState.IsFinite
                ? cameraController.TargetState
                : cameraController.CurrentState;

        if (!current.IsFinite)
            return false;

        float minimumPitch =
            Mathf.Min(
                minimumEditPitch,
                maximumEditPitch);

        float maximumPitch =
            Mathf.Max(
                minimumEditPitch,
                maximumEditPitch);

        float pitch =
            Mathf.Clamp(
                current.Pitch,
                minimumPitch,
                maximumPitch);

        Vector3 focus =
            bounds.center;

        Bounds padded =
            bounds;

        float padding =
            Mathf.Max(
                0.08f,
                inspectionService
                    .InspectionSettings
                    .BoundsPadding *
                0.70f);

        padded.Expand(
            Vector3.one *
            padding *
            2f);

        BistroBuilderCameraInspectionBounds.GetCorners(
            padded,
            boundsCorners);

        float minimumDistance =
            Mathf.Max(
                cameraController.Settings.MinimumDistance,
                minimumSelectionDistance);

        float maximumDistance =
            cameraController.Settings.MaximumDistance;

        Quaternion rotation =
            Quaternion.Euler(
                pitch,
                current.Yaw,
                0f);

        if (!BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                worldCamera,
                focus,
                rotation,
                boundsCorners,
                Mathf.Max(
                    1f,
                    selectionFramingMargin),
                current.Distance,
                minimumDistance,
                maximumDistance,
                out float distance))
        {
            return false;
        }

        state =
            new BistroBuilderCameraNavigationState(
                focus,
                current.Yaw,
                pitch,
                distance);

        return state.IsFinite;
    }

    private bool IsCurrentFramingComfortable(
        Bounds bounds,
        float desiredDistance)
    {
        if (worldCamera == null)
            return false;

        BistroBuilderCameraInspectionBounds.GetCorners(
            bounds,
            boundsCorners);

        if (!TryProjectViewportBounds(
                worldCamera,
                boundsCorners,
                out Vector2 min,
                out Vector2 max))
        {
            return false;
        }

        if (!IsViewportFramingComfortable(
                min,
                max,
                comfortableViewportMin,
                comfortableViewportMax,
                minimumVisibleViewportSpan))
        {
            return false;
        }

        BistroBuilderCameraNavigationState current =
            cameraController.CurrentState.IsFinite
                ? cameraController.CurrentState
                : cameraController.TargetState;

        if (!current.IsFinite)
            return false;

        return current.Distance <=
            desiredDistance * 1.22f;
    }

    internal static bool TryProjectViewportBounds(
        Camera camera,
        Vector3[] worldPoints,
        out Vector2 min,
        out Vector2 max)
    {
        min = new Vector2(
            float.PositiveInfinity,
            float.PositiveInfinity);

        max = new Vector2(
            float.NegativeInfinity,
            float.NegativeInfinity);

        if (camera == null ||
            worldPoints == null ||
            worldPoints.Length == 0)
        {
            return false;
        }

        bool any =
            false;

        for (int i = 0; i < worldPoints.Length; i++)
        {
            Vector3 viewport =
                camera.WorldToViewportPoint(
                    worldPoints[i]);

            if (!BistroBuilderProfessionalCameraMath.IsFinite(
                    viewport) ||
                viewport.z <= 0f)
            {
                continue;
            }

            any = true;

            min.x =
                Mathf.Min(
                    min.x,
                    viewport.x);

            min.y =
                Mathf.Min(
                    min.y,
                    viewport.y);

            max.x =
                Mathf.Max(
                    max.x,
                    viewport.x);

            max.y =
                Mathf.Max(
                    max.y,
                    viewport.y);
        }

        return any;
    }

    internal static bool IsViewportFramingComfortable(
        Vector2 projectedMin,
        Vector2 projectedMax,
        Vector2 comfortableMin,
        Vector2 comfortableMax,
        float minimumSpan)
    {
        if (!IsFinite(projectedMin) ||
            !IsFinite(projectedMax) ||
            projectedMax.x < projectedMin.x ||
            projectedMax.y < projectedMin.y)
        {
            return false;
        }

        Vector2 center =
            (projectedMin +
             projectedMax) *
            0.5f;

        Vector2 span =
            projectedMax -
            projectedMin;

        float requiredSpan =
            Mathf.Max(
                0.001f,
                minimumSpan);

        bool centerComfortable =
            center.x >= comfortableMin.x &&
            center.x <= comfortableMax.x &&
            center.y >= comfortableMin.y &&
            center.y <= comfortableMax.y;

        bool visibleEnough =
            Mathf.Max(
                span.x,
                span.y) >=
            requiredSpan;

        return centerComfortable &&
            visibleEnough;
    }

    private static bool IsFinite(
        Vector2 value)
    {
        return !float.IsNaN(value.x) &&
            !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) &&
            !float.IsInfinity(value.y);
    }

    private void HandleSelectedMemberChanged(
        RestaurantAreaMember member)
    {
        if (!autoFrameSelection ||
            member == null)
        {
            return;
        }

        TryFocusMember(
            member,
            false);
    }

    private void HandleActiveMemberChanged(
        RestaurantAreaMember member)
    {
        if (!autoFramePlacementStart ||
            member == null)
        {
            return;
        }

        TryFocusMember(
            member,
            false);
    }

    private void HandleEditModeEntered()
    {
        EnterEditCameraContext();
    }

    private void HandleEditModeExited()
    {
        ResolveDependencies();

        if (inspectionService != null)
        {
            inspectionService.TrySwitchMode(
                BistroBuilderCameraContextMode.Service,
                true,
                false);
        }

        lastFocusedMember = null;
    }

    private void EnterEditCameraContext()
    {
        ResolveDependencies();

        if (inspectionService == null)
            return;

        inspectionService.TrySwitchMode(
            BistroBuilderCameraContextMode.Edit,
            true,
            false);
    }

    private void ResolveDependencies()
    {
        if (interactionController == null)
            interactionController =
                GetComponent<
                    RestaurantEditInteractionController>();

        if (interactionController == null)
            interactionController =
                FindFirstObjectByType<
                    RestaurantEditInteractionController>();

        if (editModeService == null)
            editModeService =
                FindFirstObjectByType<
                    RestaurantEditModeService>(
                    FindObjectsInactive.Include);

        if (cameraController == null)
            cameraController =
                FindFirstObjectByType<
                    BistroBuilderProfessionalCameraController>(
                    FindObjectsInactive.Exclude);

        if (inspectionService == null)
            inspectionService =
                FindFirstObjectByType<
                    BistroBuilderCameraInspectionService>(
                    FindObjectsInactive.Exclude);

        if (worldCamera == null &&
            cameraController != null)
        {
            worldCamera =
                cameraController.ControlledCamera;
        }

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (interactionController != null)
        {
            interactionController.SelectedMemberChanged +=
                HandleSelectedMemberChanged;

            interactionController.ActiveMemberChanged +=
                HandleActiveMemberChanged;
        }

        if (editModeService != null)
        {
            editModeService.EditModeEntered +=
                HandleEditModeEntered;

            editModeService.EditModeExited +=
                HandleEditModeExited;
        }

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (interactionController != null)
        {
            interactionController.SelectedMemberChanged -=
                HandleSelectedMemberChanged;

            interactionController.ActiveMemberChanged -=
                HandleActiveMemberChanged;
        }

        if (editModeService != null)
        {
            editModeService.EditModeEntered -=
                HandleEditModeEntered;

            editModeService.EditModeExited -=
                HandleEditModeExited;
        }

        subscribed = false;
    }
}
