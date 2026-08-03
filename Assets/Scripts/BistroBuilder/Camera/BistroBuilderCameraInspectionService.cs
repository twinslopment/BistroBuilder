using System;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Cámara contextual 369C: memoria por modo, encuadre de selección, conjuntos mesa+sillas,
    /// giro por pasos y seguimiento opcional. Reutiliza íntegramente el controlador 369A.
    /// </summary>
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class BistroBuilderCameraInspectionService : MonoBehaviour
    {
        public const int RuntimeRevision = 1;

        [SerializeField] private BistroBuilderProfessionalCameraController controller;
        [SerializeField] private BistroBuilderCameraInspectionSettings inspectionSettings;
        [SerializeField] private BistroBuilderCameraViewService framingService;
        [SerializeField] private BistroBuilderCameraContextMode currentMode =
            BistroBuilderCameraContextMode.Service;

        [SerializeField] private BistroBuilderCameraContextMemorySlot serviceMemory;
        [SerializeField] private BistroBuilderCameraContextMemorySlot editMemory;
        [SerializeField] private BistroBuilderCameraContextMemorySlot inspectionMemory;

        private GameObject inspectionTarget;
        private GameObject inspectionSemanticRoot;
        private BistroBuilderCameraInspectable activeInspectable;
        private Bounds lastInspectionBounds;
        private bool hasInspectionBounds;
        private bool manualInspectionOverride;

        private BistroBuilderCameraContextMode modeBeforeInspection;
        private BistroBuilderCameraNavigationState stateBeforeInspection;
        private bool hasStateBeforeInspection;
        private readonly Vector3[] framingCorners = new Vector3[8];

        public BistroBuilderProfessionalCameraController Controller { get { return controller; } }
        public BistroBuilderCameraInspectionSettings InspectionSettings { get { return inspectionSettings; } }
        public BistroBuilderCameraViewService FramingService { get { return framingService; } }
        public BistroBuilderCameraContextMode CurrentMode { get { return currentMode; } }
        public GameObject InspectionTarget { get { return inspectionTarget; } }
        public GameObject InspectionSemanticRoot { get { return inspectionSemanticRoot; } }
        public bool HasInspectionTarget { get { return inspectionTarget != null; } }
        public bool HasStateBeforeInspection { get { return hasStateBeforeInspection; } }

        public event Action<BistroBuilderCameraContextMode> ModeChanged;
        public event Action<GameObject> InspectionTargetChanged;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (controller == null || inspectionSettings == null ||
                currentMode != BistroBuilderCameraContextMode.Inspection ||
                inspectionTarget == null)
            {
                return;
            }

            if (controller.HadNavigationInputThisFrame || controller.IsDirectManipulationActive)
            {
                manualInspectionOverride = true;
                return;
            }

            if (manualInspectionOverride || !ShouldTrackTarget())
            {
                return;
            }

            Bounds bounds;
            GameObject semanticRoot;
            if (!BistroBuilderCameraInspectionBounds.TryCalculate(
                    inspectionTarget,
                    inspectionSettings,
                    true,
                    out bounds,
                    out semanticRoot))
            {
                return;
            }

            if (!hasInspectionBounds ||
                Vector3.Distance(bounds.center, lastInspectionBounds.center) >=
                inspectionSettings.TrackingPositionEpsilon)
            {
                Vector3 focus = bounds.center + GetFocusOffset();
                controller.SetFocusPoint(focus, false);
                lastInspectionBounds = bounds;
                hasInspectionBounds = true;
                inspectionSemanticRoot = semanticRoot;
            }
        }

        public bool TrySwitchMode(
            BistroBuilderCameraContextMode mode,
            bool restoreRememberedView = true,
            bool immediate = false)
        {
            ResolveReferences();
            if (controller == null || !controller.IsInitialized)
            {
                return false;
            }

            RememberCurrentModeState();
            if (mode != BistroBuilderCameraContextMode.Inspection)
            {
                ClearInspectionTarget(false);
            }

            currentMode = mode;
            BistroBuilderCameraContextMemorySlot slot = GetSlot(mode);
            if (restoreRememberedView && slot.HasState)
            {
                controller.SetTargetState(slot.State, immediate);
            }

            ModeChanged?.Invoke(currentMode);
            return true;
        }

        public void RememberCurrentModeState()
        {
            ResolveReferences();
            if (controller == null || !controller.IsInitialized)
            {
                return;
            }

            BistroBuilderCameraNavigationState state = controller.CurrentState;
            if (!state.IsFinite)
            {
                state = controller.TargetState;
            }
            if (!state.IsFinite)
            {
                return;
            }

            SetSlot(currentMode, state);
        }

        public bool TryInspect(
            GameObject target,
            bool includeRelated = true,
            bool immediate = false)
        {
            ResolveReferences();
            if (target == null || controller == null || inspectionSettings == null ||
                !controller.IsInitialized)
            {
                return false;
            }

            BistroBuilderCameraNavigationState state;
            Bounds bounds;
            GameObject semanticRoot;
            if (!TryCalculateInspectionState(
                    target,
                    includeRelated,
                    out state,
                    out bounds,
                    out semanticRoot))
            {
                return false;
            }

            if (framingService != null &&
                framingService.ActiveView != BistroBuilderCameraViewId.None)
            {
                framingService.ExitPresetKeepingCurrentView();
            }

            if (currentMode != BistroBuilderCameraContextMode.Inspection)
            {
                RememberCurrentModeState();
                modeBeforeInspection = currentMode;
                stateBeforeInspection = controller.CurrentState;
                hasStateBeforeInspection = stateBeforeInspection.IsFinite;
            }

            currentMode = BistroBuilderCameraContextMode.Inspection;
            inspectionTarget = target;
            inspectionSemanticRoot = semanticRoot;
            activeInspectable = target.GetComponentInParent<BistroBuilderCameraInspectable>();
            lastInspectionBounds = bounds;
            hasInspectionBounds = true;
            manualInspectionOverride = false;

            controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            controller.SetTargetState(state, immediate);
            inspectionMemory.Set(state);

            ModeChanged?.Invoke(currentMode);
            InspectionTargetChanged?.Invoke(inspectionTarget);
            return true;
        }

        public bool TryFrameCurrentTarget(bool immediate = false)
        {
            return inspectionTarget != null && TryInspect(inspectionTarget, true, immediate);
        }

        public bool TryRestoreBeforeInspection(bool immediate = false)
        {
            ResolveReferences();
            if (controller == null || !hasStateBeforeInspection || !stateBeforeInspection.IsFinite)
            {
                return false;
            }

            controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            controller.SetTargetState(stateBeforeInspection, immediate);
            currentMode = modeBeforeInspection;
            SetSlot(currentMode, stateBeforeInspection);
            hasStateBeforeInspection = false;
            ClearInspectionTarget(true);
            ModeChanged?.Invoke(currentMode);
            return true;
        }

        public bool RotateInspectionByStep(int direction, bool immediate = false)
        {
            ResolveReferences();
            if (direction == 0 || controller == null || !controller.IsInitialized ||
                currentMode != BistroBuilderCameraContextMode.Inspection)
            {
                return false;
            }

            BistroBuilderCameraNavigationState current = controller.TargetState;
            if (!current.IsFinite)
            {
                current = controller.CurrentState;
            }
            if (!current.IsFinite)
            {
                return false;
            }

            float step = activeInspectable != null &&
                         activeInspectable.RotationStepOverride > 0.0f
                ? activeInspectable.RotationStepOverride
                : inspectionSettings.RotationStep;
            float yaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(
                current.Yaw + Mathf.Sign(direction) * step);
            BistroBuilderCameraNavigationState target = new BistroBuilderCameraNavigationState(
                current.FocusPoint,
                yaw,
                current.Pitch,
                current.Distance);
            controller.SetTargetState(target, immediate);
            inspectionMemory.Set(target);
            manualInspectionOverride = false;
            return true;
        }

        public bool TryCalculateInspectionState(
            GameObject target,
            bool includeRelated,
            out BistroBuilderCameraNavigationState state,
            out Bounds bounds,
            out GameObject semanticRoot)
        {
            state = default;
            bounds = default;
            semanticRoot = null;
            ResolveReferences();
            if (target == null || controller == null || controller.ControlledCamera == null ||
                controller.Settings == null || inspectionSettings == null)
            {
                return false;
            }

            if (!BistroBuilderCameraInspectionBounds.TryCalculate(
                    target,
                    inspectionSettings,
                    includeRelated,
                    out bounds,
                    out semanticRoot))
            {
                return false;
            }

            BistroBuilderCameraInspectable inspectable =
                target.GetComponentInParent<BistroBuilderCameraInspectable>();
            float margin = inspectable != null && inspectable.FramingMarginOverride >= 1.0f
                ? inspectable.FramingMarginOverride
                : inspectionSettings.FramingMargin;
            float pitch = inspectable != null && inspectable.PitchOverride > 0.0f
                ? inspectable.PitchOverride
                : inspectionSettings.DefaultInspectionPitch;
            pitch = Mathf.Clamp(
                pitch,
                controller.Settings.MinimumPitch,
                controller.Settings.MaximumPitch);

            BistroBuilderCameraNavigationState current = controller.CurrentState;
            if (!current.IsFinite)
            {
                current = controller.TargetState;
            }
            if (!current.IsFinite)
            {
                return false;
            }

            Bounds padded = bounds;
            padded.Expand(Vector3.one * inspectionSettings.BoundsPadding * 2.0f);
            BistroBuilderCameraInspectionBounds.GetCorners(padded, framingCorners);
            Vector3 focus = bounds.center +
                (inspectable != null ? inspectable.FocusOffset : Vector3.zero);
            Quaternion rotation = Quaternion.Euler(pitch, current.Yaw, 0.0f);
            float minimumDistance = Mathf.Max(
                controller.Settings.MinimumDistance,
                inspectionSettings.MinimumInspectionDistance);
            float maximumDistance = Mathf.Min(
                controller.Settings.MaximumDistance,
                inspectionSettings.MaximumInspectionDistance);
            float distance;
            if (!BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                    controller.ControlledCamera,
                    focus,
                    rotation,
                    framingCorners,
                    margin,
                    current.Distance,
                    minimumDistance,
                    maximumDistance,
                    out distance))
            {
                return false;
            }

            state = new BistroBuilderCameraNavigationState(
                focus,
                current.Yaw,
                pitch,
                distance);
            return state.IsFinite;
        }

        public BistroBuilderCameraContextSnapshot CaptureSnapshot()
        {
            RememberCurrentModeState();
            return new BistroBuilderCameraContextSnapshot(
                currentMode,
                serviceMemory,
                editMemory,
                inspectionMemory);
        }

        public bool RestoreSnapshot(
            BistroBuilderCameraContextSnapshot snapshot,
            bool restoreCurrentModeView,
            bool immediate = false)
        {
            ResolveReferences();
            if (snapshot == null || !snapshot.IsCompatible || controller == null)
            {
                return false;
            }

            serviceMemory = snapshot.Service;
            editMemory = snapshot.Edit;
            inspectionMemory = snapshot.Inspection;
            currentMode = snapshot.CurrentMode;
            ClearInspectionTarget(false);

            if (restoreCurrentModeView)
            {
                BistroBuilderCameraContextMemorySlot slot = GetSlot(currentMode);
                if (slot.HasState)
                {
                    controller.SetTargetState(slot.State, immediate);
                }
            }

            ModeChanged?.Invoke(currentMode);
            return true;
        }

        private bool ShouldTrackTarget()
        {
            if (!inspectionSettings.TrackTargetWhileInspecting)
            {
                return false;
            }
            return activeInspectable == null || activeInspectable.TrackWhileInspecting;
        }

        private Vector3 GetFocusOffset()
        {
            return activeInspectable != null ? activeInspectable.FocusOffset : Vector3.zero;
        }

        private void ClearInspectionTarget(bool notify)
        {
            inspectionTarget = null;
            inspectionSemanticRoot = null;
            activeInspectable = null;
            hasInspectionBounds = false;
            manualInspectionOverride = false;
            if (notify)
            {
                InspectionTargetChanged?.Invoke(null);
            }
        }

        private BistroBuilderCameraContextMemorySlot GetSlot(BistroBuilderCameraContextMode mode)
        {
            switch (mode)
            {
                case BistroBuilderCameraContextMode.Edit:
                    return editMemory;
                case BistroBuilderCameraContextMode.Inspection:
                    return inspectionMemory;
                default:
                    return serviceMemory;
            }
        }

        private void SetSlot(
            BistroBuilderCameraContextMode mode,
            BistroBuilderCameraNavigationState state)
        {
            switch (mode)
            {
                case BistroBuilderCameraContextMode.Edit:
                    editMemory.Set(state);
                    break;
                case BistroBuilderCameraContextMode.Inspection:
                    inspectionMemory.Set(state);
                    break;
                default:
                    serviceMemory.Set(state);
                    break;
            }
        }

        private void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponent<BistroBuilderProfessionalCameraController>();
            }
            if (framingService == null)
            {
                framingService = GetComponent<BistroBuilderCameraViewService>();
            }
        }
    }
}
