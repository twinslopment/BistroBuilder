using System;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Servicio runtime de vistas 369B. Calcula cada encuadre contra la huella del local y utiliza
    /// el mismo estado objetivo y la misma amortiguación profesional de 369A.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class BistroBuilderCameraViewService : MonoBehaviour
    {
        public const int RuntimeRevision = 1;

        [SerializeField] private BistroBuilderProfessionalCameraController controller;
        [SerializeField] private BistroBuilderCameraViewSettings viewSettings;
        [SerializeField] private BistroBuilderCameraBounds navigationBounds;

        private BistroBuilderCameraViewId activeView;
        private bool hasPreviousFreeState;
        private BistroBuilderCameraNavigationState previousFreeState;
        private int activationFrame = -1;
        private readonly Vector3[] framingCorners = new Vector3[8];

        public BistroBuilderProfessionalCameraController Controller { get { return controller; } }
        public BistroBuilderCameraViewSettings ViewSettings { get { return viewSettings; } }
        public BistroBuilderCameraBounds NavigationBounds { get { return navigationBounds; } }
        public BistroBuilderCameraViewId ActiveView { get { return activeView; } }
        public bool HasPreviousFreeState { get { return hasPreviousFreeState; } }

        public event Action<BistroBuilderCameraViewId> ViewChanged;

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

        private void OnDisable()
        {
            if (controller != null)
            {
                controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            }
        }

        private void LateUpdate()
        {
            if (activeView == BistroBuilderCameraViewId.None || controller == null)
            {
                return;
            }

            // Una vista es un acceso rápido, no un modo que secuestra la cámara. En cuanto el
            // jugador vuelve a navegar, el preset se abandona y 369A recupera sus límites manuales.
            if (Time.frameCount > activationFrame + 2 &&
                controller.HadNavigationInputThisFrame)
            {
                ExitPresetKeepingCurrentView();
            }
        }

        public bool TryActivateView(BistroBuilderCameraViewId viewId, bool immediate = false)
        {
            ResolveReferences();
            if (controller == null || viewSettings == null || navigationBounds == null)
            {
                return false;
            }

            BistroBuilderCameraNavigationState targetState;
            BistroBuilderCameraViewDefinition definition;
            if (!TryCalculateViewState(viewId, out targetState, out definition))
            {
                return false;
            }

            if (activeView == BistroBuilderCameraViewId.None)
            {
                previousFreeState = controller.CurrentState;
                hasPreviousFreeState = previousFreeState.IsFinite;
            }

            ApplyPitchPolicy(definition);
            controller.SetTargetState(targetState, immediate);
            activeView = viewId;
            activationFrame = Time.frameCount;
            ViewChanged?.Invoke(activeView);
            return true;
        }

        public bool TryRestorePreviousView(bool immediate = false)
        {
            ResolveReferences();
            if (controller == null || !hasPreviousFreeState || !previousFreeState.IsFinite)
            {
                return false;
            }

            controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            controller.SetTargetState(previousFreeState, immediate);
            activeView = BistroBuilderCameraViewId.None;
            hasPreviousFreeState = false;
            activationFrame = Time.frameCount;
            ViewChanged?.Invoke(activeView);
            return true;
        }

        public void ExitPresetKeepingCurrentView()
        {
            ResolveReferences();
            if (controller == null)
            {
                activeView = BistroBuilderCameraViewId.None;
                return;
            }

            BistroBuilderCameraNavigationState visibleState = controller.CurrentState;
            controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            controller.SetTargetState(visibleState, false);
            activeView = BistroBuilderCameraViewId.None;
            activationFrame = Time.frameCount;
            ViewChanged?.Invoke(activeView);
        }

        public bool TryCalculateViewState(
            BistroBuilderCameraViewId viewId,
            out BistroBuilderCameraNavigationState state,
            out BistroBuilderCameraViewDefinition definition)
        {
            state = default;
            definition = null;
            ResolveReferences();

            if (controller == null || controller.ControlledCamera == null ||
                controller.Settings == null || viewSettings == null ||
                navigationBounds == null || !navigationBounds.IsValid ||
                !viewSettings.TryGetView(viewId, out definition) || definition == null)
            {
                return false;
            }

            BistroBuilderCameraNavigationState current = controller.CurrentState;
            if (!current.IsFinite)
            {
                current = controller.TargetState;
            }
            if (!current.IsFinite)
            {
                return false;
            }

            float yaw = definition.YawMode == BistroBuilderCameraViewYawMode.PreserveCurrent
                ? current.Yaw
                : definition.FixedYaw;
            yaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(yaw);
            float pitch = definition.Pitch;

            Vector3 focusPoint = definition.FocusMode == BistroBuilderCameraViewFocusMode.CurrentFocus
                ? current.FocusPoint
                : navigationBounds.GetWorldFocusCenter(definition.FocusHeight);

            float distance = definition.FixedDistance;
            if (definition.FramingMode == BistroBuilderCameraViewFramingMode.FitRestaurantBounds)
            {
                if (!navigationBounds.TryGetWorldFramingCorners(
                        definition.FramingContentHeight,
                        framingCorners))
                {
                    return false;
                }

                Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
                if (!BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                        controller.ControlledCamera,
                        focusPoint,
                        rotation,
                        framingCorners,
                        definition.FramingMargin,
                        current.Distance,
                        controller.Settings.MinimumDistance,
                        controller.Settings.MaximumDistance,
                        out distance))
                {
                    return false;
                }
            }

            distance = Mathf.Clamp(
                distance,
                controller.Settings.MinimumDistance,
                controller.Settings.MaximumDistance);
            state = new BistroBuilderCameraNavigationState(
                focusPoint,
                yaw,
                pitch,
                distance);
            return state.IsFinite;
        }

        private void ApplyPitchPolicy(BistroBuilderCameraViewDefinition definition)
        {
            if (definition != null && definition.AllowExtendedPitch)
            {
                controller.SetExternalPitchRange(
                    true,
                    definition.ExtendedMinimumPitch,
                    definition.ExtendedMaximumPitch);
            }
            else
            {
                controller.SetExternalPitchRange(false, 0.0f, 0.0f);
            }
        }

        private void ResolveReferences()
        {
            if (controller == null)
            {
                controller = GetComponent<BistroBuilderProfessionalCameraController>();
            }

            if (navigationBounds == null && controller != null)
            {
                navigationBounds = controller.NavigationBounds;
            }
        }
    }
}
