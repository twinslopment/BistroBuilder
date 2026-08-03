using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Controlador principal 369A de la cámara de Bistro Builder.
    ///
    /// Diseño:
    /// - El jugador modifica un estado objetivo (punto de interés, giro, inclinación y distancia).
    /// - El estado visible persigue ese objetivo con amortiguación crítica, sin interpolaciones lineales.
    /// - El teclado utiliza aceleración/deceleración separadas para evitar arranques y frenadas mecánicas.
    /// - La rueda alimenta una velocidad de dolly continua y anclada al cursor: el punto del suelo
    ///   elegido permanece estable en pantalla mientras la cámara se acerca o se aleja.
    /// - R/F trasladan verticalmente la pose completa (cámara y punto de mirada) sin recalcular la
    ///   distancia orbital ni curvar la trayectoria.
    /// - El arrastre central convierte el delta del puntero a unidades del encuadre visible, evitando
    ///   realimentación y vibraciones mientras conserva una respuesta proporcional al zoom.
    /// - Se utiliza tiempo no escalado por defecto: la cámara continúa operativa con el juego en pausa.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class BistroBuilderProfessionalCameraController : MonoBehaviour
    {
        public const int RuntimeRevision = 13;

        [Header("Referencias")]
        [SerializeField] private UnityEngine.Camera controlledCamera;
        [SerializeField] private BistroBuilderCameraNavigationSettings settings;
        [SerializeField] private BistroBuilderCameraBounds navigationBounds;

        [Header("Estado")]
        [SerializeField] private bool inputEnabled = true;
        [SerializeField] private bool initializeFromCurrentCamera = true;
        [SerializeField] private float fallbackGroundHeight = 0.0f;

        private bool initialized;
        private Vector3 targetFocusPoint;
        private Vector3 currentFocusPoint;
        private Vector3 focusSmoothVelocity;

        private float targetYaw;
        private float currentYaw;
        private float yawSmoothVelocity;

        private float targetPitch;
        private float currentPitch;
        private float pitchSmoothVelocity;

        private float targetDistance;
        private float currentDistance;
        private float distanceSmoothVelocity;

        private Vector3 currentPanVelocity;
        private Vector3 panVelocitySmoothReference;
        private float currentYawVelocity;
        private float yawVelocitySmoothReference;
        private float currentElevationVelocity;
        private float elevationVelocitySmoothReference;
        private bool verticalElevationGestureActive;
        private bool elevatorReferenceValid;
        private float elevatorReferenceHeight;
        private float effectiveElevatorMinimumHeight;
        private float effectiveElevatorMaximumHeight;

        private bool middleDragActive;
        private bool rightDragActive;
        private Vector3 rightOrbitPivot;
        private bool rightOrbitPivotValid;

        private bool keyboardOrbitActive;
        private Vector3 keyboardOrbitPivot;
        private bool keyboardOrbitPivotValid;

        private float currentZoomVelocity;
        private float zoomVelocitySmoothReference;
        private float zoomIntentTimeRemaining;
        private float zoomIntentDirection;
        private bool zoomAnchorActive;
        private Vector3 zoomAnchorWorldPoint;
        private Vector2 zoomAnchorScreenPoint;

        [Header("Diagnóstico")]
        [SerializeField] private bool showRuntimeDiagnostics;

        // 369B puede solicitar temporalmente un pitch fuera de los límites de navegación manual
        // (por ejemplo, la vista cenital). El override se desactiva en cuanto el jugador retoma
        // el control o el servicio de vistas lo libera.
        private bool externalPitchRangeActive;
        private float externalMinimumPitch;
        private float externalMaximumPitch;

        private Vector3 previousAppliedPosition;
        private Quaternion previousAppliedRotation;
        private float previousAppliedDistance;
        private float orthographicSizePerDistance;

        public UnityEngine.Camera ControlledCamera { get { return controlledCamera; } }
        public BistroBuilderCameraNavigationSettings Settings { get { return settings; } }
        public BistroBuilderCameraBounds NavigationBounds { get { return navigationBounds; } }
        public bool InputEnabled { get { return inputEnabled; } }
        public bool IsInitialized { get { return initialized; } }
        public bool IsDirectManipulationActive { get { return middleDragActive || rightDragActive || keyboardOrbitActive; } }
        public bool HadNavigationInputThisFrame { get; private set; }
        public bool ExternalPitchRangeActive { get { return externalPitchRangeActive; } }
        public float LastFrameLinearSpeed { get; private set; }
        public float LastFrameAngularSpeed { get; private set; }
        public float LastFrameZoomSpeed { get; private set; }

        public BistroBuilderCameraNavigationState CurrentState
        {
            get
            {
                return new BistroBuilderCameraNavigationState(
                    currentFocusPoint,
                    currentYaw,
                    currentPitch,
                    currentDistance);
            }
        }

        public BistroBuilderCameraNavigationState TargetState
        {
            get
            {
                return new BistroBuilderCameraNavigationState(
                    targetFocusPoint,
                    targetYaw,
                    targetPitch,
                    targetDistance);
            }
        }

        private void Reset()
        {
            controlledCamera = GetComponent<UnityEngine.Camera>();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            InitializeIfPossible();
        }

        private void LateUpdate()
        {
            HadNavigationInputThisFrame = false;

            if (!InitializeIfPossible())
            {
                return;
            }

            float rawDeltaTime = settings.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float deltaTime = Mathf.Clamp(rawDeltaTime, 0.0f, settings.MaximumSimulationDeltaTime);
            if (deltaTime <= 0.0f)
            {
                return;
            }

            UpdateTargets(deltaTime);
            SmoothAndApply(deltaTime);
        }

        private void OnDisable()
        {
            CancelDirectManipulation();
        }

        public void SetInputEnabled(bool enabled)
        {
            inputEnabled = enabled;
            if (!enabled)
            {
                CancelDirectManipulation();
                currentPanVelocity = Vector3.zero;
                panVelocitySmoothReference = Vector3.zero;
                currentYawVelocity = 0.0f;
                yawVelocitySmoothReference = 0.0f;
                currentElevationVelocity = 0.0f;
                elevationVelocitySmoothReference = 0.0f;
                verticalElevationGestureActive = false;
                InvalidateElevatorReference();
                StopZoomImmediately();
            }
        }

        public void SetNavigationBounds(BistroBuilderCameraBounds bounds)
        {
            navigationBounds = bounds;
            if (initialized)
            {
                ConstrainTargetState();
            }
        }

        /// <summary>
        /// Permite a 369B usar una inclinación especial durante una vista predefinida sin ampliar
        /// los límites de navegación manual aceptados en 369A. Al desactivarlo, el objetivo vuelve
        /// suavemente al intervalo normal si estaba fuera de él.
        /// </summary>
        public void SetExternalPitchRange(bool enabled, float minimumPitch, float maximumPitch)
        {
            if (enabled &&
                BistroBuilderProfessionalCameraMath.IsFinite(minimumPitch) &&
                BistroBuilderProfessionalCameraMath.IsFinite(maximumPitch) &&
                maximumPitch > minimumPitch)
            {
                externalPitchRangeActive = true;
                externalMinimumPitch = Mathf.Clamp(minimumPitch, 0.1f, 89.0f);
                externalMaximumPitch = Mathf.Clamp(maximumPitch, externalMinimumPitch + 0.1f, 89.5f);
            }
            else
            {
                externalPitchRangeActive = false;
                externalMinimumPitch = 0.0f;
                externalMaximumPitch = 0.0f;
            }

            if (initialized)
            {
                targetPitch = Mathf.Clamp(targetPitch, GetActiveMinimumPitch(), GetActiveMaximumPitch());
                ConstrainTargetState();
            }
        }

        public void SetTargetState(BistroBuilderCameraNavigationState state, bool immediate)
        {
            if (!state.IsFinite || !InitializeIfPossible())
            {
                return;
            }

            InvalidateElevatorReference();
            targetFocusPoint = state.FocusPoint;
            targetYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(state.Yaw);
            targetPitch = Mathf.Clamp(state.Pitch, GetActiveMinimumPitch(), GetActiveMaximumPitch());
            targetDistance = Mathf.Clamp(
                state.Distance,
                settings.MinimumDistance,
                settings.MaximumDistance);
            ConstrainTargetState();

            if (immediate)
            {
                SnapCurrentToTarget();
                ApplyCurrentTransform();
                ResetMotionMetricsReference();
            }
        }

        public void SetFocusPoint(Vector3 worldFocusPoint, bool immediate)
        {
            if (!InitializeIfPossible())
            {
                return;
            }

            InvalidateElevatorReference();
            targetFocusPoint = worldFocusPoint;
            ConstrainTargetState();
            if (immediate)
            {
                SnapCurrentToTarget();
                ApplyCurrentTransform();
                ResetMotionMetricsReference();
            }
        }

        public bool IsMotionSettled(
            float linearSpeedThreshold,
            float angularSpeedThreshold,
            float zoomSpeedThreshold)
        {
            if (linearSpeedThreshold < 0.0f ||
                angularSpeedThreshold < 0.0f ||
                zoomSpeedThreshold < 0.0f)
            {
                return false;
            }

            return BistroBuilderProfessionalCameraMath.IsFinite(LastFrameLinearSpeed) &&
                   BistroBuilderProfessionalCameraMath.IsFinite(LastFrameAngularSpeed) &&
                   BistroBuilderProfessionalCameraMath.IsFinite(LastFrameZoomSpeed) &&
                   LastFrameLinearSpeed <= linearSpeedThreshold &&
                   LastFrameAngularSpeed <= angularSpeedThreshold &&
                   LastFrameZoomSpeed <= zoomSpeedThreshold;
        }

        public void ReinitializeFromCurrentCamera()
        {
            initialized = false;
            InitializeIfPossible();
        }

        private void ResolveReferences()
        {
            if (controlledCamera == null)
            {
                controlledCamera = GetComponent<UnityEngine.Camera>();
            }
        }

        private bool InitializeIfPossible()
        {
            if (initialized)
            {
                return true;
            }

            ResolveReferences();
            if (controlledCamera == null || settings == null)
            {
                return false;
            }

            string reason;
            if (!settings.IsConfigurationValid(out reason))
            {
                Debug.LogError("Bistro Builder 369A: configuración de cámara inválida. " + reason, this);
                return false;
            }

            float groundHeight = GetGroundHeight();
            Transform cameraTransform = controlledCamera.transform;
            float inferredYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(
                cameraTransform.eulerAngles.y);
            float inferredPitch = BistroBuilderProfessionalCameraMath.ClampPitch(
                cameraTransform.eulerAngles.x,
                GetActiveMinimumPitch(),
                GetActiveMaximumPitch());

            // Inicializamos siempre ambos valores. El operador && puede omitir la llamada con
            // parámetro out cuando initializeFromCurrentCamera es false; una inicialización
            // explícita mantiene la asignación definida para todos los compiladores usados por Unity.
            Vector3 inferredFocus = Vector3.zero;
            float inferredDistance = settings.FallbackDistance;
            bool currentTransformUsable = initializeFromCurrentCamera &&
                BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                    new Ray(cameraTransform.position, cameraTransform.forward),
                    groundHeight,
                    out inferredFocus);

            if (currentTransformUsable)
            {
                inferredDistance = Vector3.Distance(cameraTransform.position, inferredFocus);
                if (!BistroBuilderProfessionalCameraMath.IsFinite(inferredDistance) ||
                    inferredDistance < settings.MinimumDistance * 0.25f)
                {
                    currentTransformUsable = false;
                }
            }

            if (!currentTransformUsable)
            {
                inferredPitch = settings.FallbackPitch;
                inferredDistance = settings.FallbackDistance;
                Quaternion fallbackRotation = Quaternion.Euler(inferredPitch, inferredYaw, 0.0f);
                inferredFocus = cameraTransform.position +
                                fallbackRotation * Vector3.forward * inferredDistance;
                inferredFocus.y = groundHeight;
            }

            orthographicSizePerDistance = controlledCamera.orthographic
                ? controlledCamera.orthographicSize / Mathf.Max(0.1f, inferredDistance)
                : 0.0f;

            targetFocusPoint = inferredFocus;
            targetYaw = inferredYaw;
            targetPitch = inferredPitch;
            targetDistance = Mathf.Clamp(
                inferredDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);
            ConstrainTargetState();
            SnapCurrentToTarget();
            ApplyCurrentTransform();
            ResetMotionMetricsReference();
            initialized = true;
            return true;
        }

        private void UpdateTargets(float deltaTime)
        {
            BistroBuilderCameraInputFrame input = BistroBuilderProfessionalCameraInput.Read();
            bool applicationBlocked = settings.RequireApplicationFocus && !input.ApplicationFocused;

            if (!inputEnabled || applicationBlocked)
            {
                CancelDirectManipulation();
                SmoothKeyboardVelocity(Vector2.zero, 0.0f, 0.0f, false, deltaTime);
                StopZoomImmediately();
                ConstrainTargetState();
                return;
            }

            bool pointerBlocked = settings.BlockPointerInputOverUi && input.PointerOverUi;
            bool keyboardBlocked = settings.BlockKeyboardWhileTyping && input.TextInputFocused;

            Vector2 panInput = Vector2.zero;
            float yawInput = 0.0f;
            float elevationInput = 0.0f;

            if (!keyboardBlocked)
            {
                if (settings.KeyboardPanEnabled)
                {
                    panInput += input.Pan;
                }

                yawInput += input.Yaw;

                if (settings.KeyboardElevationEnabled)
                {
                    elevationInput += input.Elevation;
                }
            }

            if (settings.EdgePanEnabled && input.PointerAvailable && !pointerBlocked &&
                !middleDragActive && !rightDragActive)
            {
                Vector2 edgePan = BistroBuilderProfessionalCameraMath.CalculateEdgePan(
                    input.PointerPosition,
                    Screen.width,
                    Screen.height,
                    settings.EdgeMarginNormalized,
                    settings.EdgeMarginMinimumPixels,
                    settings.EdgeMarginMaximumPixels);
                panInput += edgePan * settings.EdgePanStrength;
            }

            if (panInput.sqrMagnitude > 1.0f)
            {
                panInput.Normalize();
            }

            bool nonElevationNavigationRequested =
                panInput.sqrMagnitude > 0.0001f ||
                Mathf.Abs(yawInput) > 0.0001f ||
                input.MiddlePressed || input.MiddleHeld ||
                input.RightPressed || input.RightHeld ||
                Mathf.Abs(input.RawScroll) > 0.0001f;
            if (nonElevationNavigationRequested && !verticalElevationGestureActive)
            {
                InvalidateElevatorReference();
            }

            bool elevationRequested = Mathf.Abs(elevationInput) > 0.0001f;
            HadNavigationInputThisFrame = nonElevationNavigationRequested || elevationRequested;

            if (elevationRequested && !verticalElevationGestureActive)
            {
                BeginVerticalElevationGesture();
            }

            HandleMiddleMouseDrag(input, pointerBlocked);
            HandleRightMouseRotation(input, pointerBlocked);

            if (middleDragActive)
            {
                panInput = Vector2.zero;
                currentPanVelocity = Vector3.zero;
                panVelocitySmoothReference = Vector3.zero;
            }

            if (rightDragActive)
            {
                yawInput = 0.0f;
                currentYawVelocity = 0.0f;
                yawVelocitySmoothReference = 0.0f;
                CancelKeyboardOrbit();
            }
            else
            {
                UpdateKeyboardOrbitGesture(yawInput, input, pointerBlocked);
            }

            SmoothKeyboardVelocity(panInput, yawInput, elevationInput, input.FastModifier, deltaTime);

            if (!middleDragActive)
            {
                targetFocusPoint += currentPanVelocity * deltaTime;
            }

            if (!rightDragActive)
            {
                ApplyKeyboardYawMovement(currentYawVelocity * deltaTime);
            }

            ApplyVerticalElevatorMovement(currentElevationVelocity * deltaTime);

            // R/F es un canal de altura explícito. Mientras el gesto está activo no mezclamos una
            // segunda transformación de distancia que pudiera inclinar perceptualmente la trayectoria.
            if (!verticalElevationGestureActive && !pointerBlocked && input.PointerAvailable)
            {
                float normalizedScroll = BistroBuilderProfessionalCameraMath.NormalizeScroll(
                    input.RawScroll,
                    settings.MaximumScrollNotchesPerFrame);
                RegisterZoomIntent(normalizedScroll, input.PointerPosition);
            }

            UpdateZoomVelocity(deltaTime);
            if (!elevationRequested && Mathf.Abs(currentElevationVelocity) <= 0.01f)
            {
                verticalElevationGestureActive = false;
            }

            targetYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(targetYaw);
            targetPitch = Mathf.Clamp(targetPitch, GetActiveMinimumPitch(), GetActiveMaximumPitch());
            targetDistance = Mathf.Clamp(
                targetDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);

            float distanceBeforeConstraint = targetDistance;
            ConstrainTargetState();
            if (zoomAnchorActive)
            {
                PreserveZoomAnchor(
                    ref targetFocusPoint,
                    targetYaw,
                    targetPitch,
                    targetDistance);
            }
            bool zoomBlockedByConstraint =
                (distanceBeforeConstraint > targetDistance + 0.0001f &&
                 currentZoomVelocity > 0.0f) ||
                (distanceBeforeConstraint < targetDistance - 0.0001f &&
                 currentZoomVelocity < 0.0f);
            if (zoomBlockedByConstraint)
            {
                StopZoomImmediately();
            }
        }

        private void SmoothKeyboardVelocity(
            Vector2 panInput,
            float yawInput,
            float elevationInput,
            bool fastModifier,
            float deltaTime)
        {
            float zoomRatio = BistroBuilderProfessionalCameraMath.DistanceRatio(
                targetDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);
            float panSpeed = Mathf.Lerp(settings.PanSpeedNear, settings.PanSpeedFar, zoomRatio);
            if (fastModifier)
            {
                panSpeed *= settings.FastPanMultiplier;
            }

            Vector3 right;
            Vector3 forward;
            BistroBuilderProfessionalCameraMath.GetGroundAlignedAxes(targetYaw, out right, out forward);
            Vector3 desiredPanVelocity = (right * panInput.x + forward * panInput.y) * panSpeed;
            float panSmoothTime = desiredPanVelocity.sqrMagnitude > 0.0001f
                ? settings.PanAccelerationTime
                : settings.PanDecelerationTime;
            float maximumPanSpeed = settings.PanSpeedFar *
                                    settings.FastPanMultiplier *
                                    settings.MaximumPanSpeedSafetyMultiplier;

            currentPanVelocity = Vector3.SmoothDamp(
                currentPanVelocity,
                desiredPanVelocity,
                ref panVelocitySmoothReference,
                panSmoothTime,
                maximumPanSpeed,
                deltaTime);
            currentPanVelocity.y = 0.0f;

            float desiredYawVelocity = yawInput * settings.KeyboardYawSpeed;
            float yawSmoothTime = Mathf.Abs(desiredYawVelocity) > 0.0001f
                ? settings.YawAccelerationTime
                : settings.YawDecelerationTime;
            currentYawVelocity = Mathf.SmoothDamp(
                currentYawVelocity,
                desiredYawVelocity,
                ref yawVelocitySmoothReference,
                yawSmoothTime,
                settings.KeyboardYawSpeed * 2.0f,
                deltaTime);

            float elevationSpeed = settings.KeyboardElevationSpeed;
            if (fastModifier)
            {
                elevationSpeed *= settings.FastPanMultiplier;
            }

            float desiredElevationVelocity = elevationInput * elevationSpeed;
            float elevationSmoothTime = Mathf.Abs(desiredElevationVelocity) > 0.0001f
                ? settings.ElevationAccelerationTime
                : settings.ElevationDecelerationTime;
            currentElevationVelocity = Mathf.SmoothDamp(
                currentElevationVelocity,
                desiredElevationVelocity,
                ref elevationVelocitySmoothReference,
                elevationSmoothTime,
                elevationSpeed * settings.MaximumPanSpeedSafetyMultiplier,
                deltaTime);
        }

        private void BeginVerticalElevationGesture()
        {
            // Partimos exactamente de la pose visible. Esto elimina cualquier diferencia Y que
            // estuviera todavía amortiguándose por una acción anterior y garantiza que R/F sea
            // una traslación vertical pura desde el primer fotograma hasta la frenada final.
            targetFocusPoint.y = currentFocusPoint.y;
            focusSmoothVelocity.y = 0.0f;
            verticalElevationGestureActive = true;
            StopZoomImmediately();

            // 369A12 usa una banda contextual persistente. La vista canónica de 369A11 quedaba a
            // unos 8,9 m dentro de un techo absoluto de 9,5 m, por lo que R apenas tenía recorrido.
            // Capturamos una referencia local y reservamos recorrido útil en ambos sentidos. La
            // referencia solo se reinicia cuando el jugador cambia encuadre mediante pan, órbita o zoom;
            // soltar y volver a pulsar R/F no permite acumular altura indefinidamente.
            if (!elevatorReferenceValid)
            {
                CaptureElevatorReference();
            }
        }

        private void CaptureElevatorReference()
        {
            float groundHeight = GetGroundHeight();
            float currentHeight = CalculateCameraHeightAboveGround(
                currentFocusPoint,
                currentYaw,
                currentPitch,
                currentDistance,
                groundHeight);

            elevatorReferenceHeight = Mathf.Clamp(
                currentHeight,
                settings.MinimumElevatorHeight,
                settings.MaximumElevatorHeight);
            effectiveElevatorMinimumHeight = Mathf.Max(
                settings.MinimumElevatorHeight,
                elevatorReferenceHeight - settings.ElevatorDownwardTravel);
            effectiveElevatorMaximumHeight = Mathf.Min(
                settings.MaximumElevatorHeight,
                elevatorReferenceHeight + settings.ElevatorUpwardTravel);

            if (effectiveElevatorMaximumHeight <= effectiveElevatorMinimumHeight + 0.01f)
            {
                effectiveElevatorMinimumHeight = settings.MinimumElevatorHeight;
                effectiveElevatorMaximumHeight = settings.MaximumElevatorHeight;
            }

            elevatorReferenceValid = true;
        }

        private void InvalidateElevatorReference()
        {
            elevatorReferenceValid = false;
            elevatorReferenceHeight = 0.0f;
            effectiveElevatorMinimumHeight = 0.0f;
            effectiveElevatorMaximumHeight = 0.0f;
        }

        private void ApplyVerticalElevatorMovement(float requestedHeightDelta)
        {
            if (Mathf.Abs(requestedHeightDelta) <= 0.000001f)
            {
                return;
            }

            if (!elevatorReferenceValid)
            {
                CaptureElevatorReference();
            }

            float groundHeight = GetGroundHeight();
            float targetHeight = CalculateCameraHeightAboveGround(
                targetFocusPoint,
                targetYaw,
                targetPitch,
                targetDistance,
                groundHeight);
            float currentHeight = CalculateCameraHeightAboveGround(
                currentFocusPoint,
                currentYaw,
                currentPitch,
                currentDistance,
                groundHeight);

            float contextualSpan = effectiveElevatorMaximumHeight - effectiveElevatorMinimumHeight;
            float softLimitRange = Mathf.Min(
                settings.ElevatorSoftLimitRange,
                Mathf.Max(0.0f, contextualSpan * 0.5f));

            float targetLimitedDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    targetHeight,
                    requestedHeightDelta,
                    effectiveElevatorMinimumHeight,
                    effectiveElevatorMaximumHeight,
                    softLimitRange);
            float currentLimitedDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    currentHeight,
                    requestedHeightDelta,
                    effectiveElevatorMinimumHeight,
                    effectiveElevatorMaximumHeight,
                    softLimitRange);

            float effectiveHeightDelta = Mathf.Sign(requestedHeightDelta) * Mathf.Min(
                Mathf.Abs(targetLimitedDelta),
                Mathf.Abs(currentLimitedDelta));
            if (Mathf.Abs(effectiveHeightDelta) <= 0.000001f)
            {
                currentElevationVelocity = 0.0f;
                elevationVelocitySmoothReference = 0.0f;
                return;
            }

            // Trasladamos cámara y punto de mirada por el mismo vector vertical. Como la pose se
            // reconstruye desde foco + rotación + distancia, esto produce una trayectoria Y recta,
            // mantiene yaw/pitch y no obliga al foco a deslizarse por el suelo ni por los bounds.
            targetFocusPoint.y += effectiveHeightDelta;
            currentFocusPoint.y += effectiveHeightDelta;
        }

        private void UpdateKeyboardOrbitGesture(
            float yawInput,
            BistroBuilderCameraInputFrame input,
            bool pointerBlocked)
        {
            bool yawRequested = Mathf.Abs(yawInput) > 0.0001f;
            if (yawRequested && !keyboardOrbitActive)
            {
                // Q/E toma un pivote contextual en el momento de iniciar el gesto. Se prioriza
                // el punto bajo el cursor y, si no es válido, el centro visible de la pantalla.
                // Nunca se fuerza el centro geométrico del plano.
                targetFocusPoint = currentFocusPoint;
                targetYaw = currentYaw;
                targetPitch = currentPitch;
                targetDistance = currentDistance;
                focusSmoothVelocity = Vector3.zero;
                yawSmoothVelocity = 0.0f;
                pitchSmoothVelocity = 0.0f;
                distanceSmoothVelocity = 0.0f;

                keyboardOrbitActive = true;
                keyboardOrbitPivotValid = false;
                if (settings.KeyboardOrbitAroundPointer &&
                    !pointerBlocked &&
                    input.PointerAvailable)
                {
                    keyboardOrbitPivotValid =
                        TryResolvePointerOrbitPivot(input.PointerPosition, out keyboardOrbitPivot);
                }

                if (!keyboardOrbitPivotValid)
                {
                    keyboardOrbitPivotValid = TryResolveViewportCenterOrbitPivot(
                        out keyboardOrbitPivot);
                }

                if (!keyboardOrbitPivotValid)
                {
                    keyboardOrbitPivot = targetFocusPoint;
                    keyboardOrbitPivotValid = true;
                }
            }

            // Conservamos el pivote durante la desaceleración para que el final de la órbita
            // no cambie de centro. Se libera cuando la tecla está suelta y la velocidad es mínima.
            if (!yawRequested && Mathf.Abs(currentYawVelocity) <= 0.05f)
            {
                CancelKeyboardOrbit();
            }
        }

        private void ApplyKeyboardYawMovement(float yawDelta)
        {
            if (Mathf.Abs(yawDelta) <= 0.000001f)
            {
                return;
            }

            float nextYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(
                targetYaw + yawDelta);
            Vector3 orbitFocus;
            float orbitDistance;
            if (keyboardOrbitActive &&
                keyboardOrbitPivotValid &&
                BistroBuilderProfessionalCameraMath.TryOrbitStateAroundPivot(
                    targetFocusPoint,
                    targetYaw,
                    targetPitch,
                    targetDistance,
                    keyboardOrbitPivot,
                    nextYaw,
                    targetPitch,
                    GetGroundHeight(),
                    settings.MinimumDistance,
                    settings.MaximumDistance,
                    out orbitFocus,
                    out orbitDistance))
            {
                targetFocusPoint = orbitFocus;
                targetDistance = orbitDistance;
                targetYaw = nextYaw;
                return;
            }

            targetYaw = nextYaw;
        }

        private bool TryResolveViewportCenterOrbitPivot(out Vector3 pivot)
        {
            pivot = targetFocusPoint;
            if (controlledCamera == null)
            {
                return false;
            }

            Vector2 viewportCenter = controlledCamera.pixelRect.center;
            return TryResolvePointerOrbitPivot(viewportCenter, out pivot);
        }

        private void CancelKeyboardOrbit()
        {
            keyboardOrbitActive = false;
            keyboardOrbitPivotValid = false;
        }

        private void RegisterZoomIntent(float normalizedScroll, Vector2 pointerPosition)
        {
            if (Mathf.Abs(normalizedScroll) <= 0.0001f)
            {
                return;
            }

            float direction = normalizedScroll > 0.0f ? -1.0f : 1.0f;
            float addedDuration =
                Mathf.Abs(normalizedScroll) * settings.ZoomIntentDurationPerNotch;

            if (zoomIntentTimeRemaining <= 0.0f ||
                Mathf.Sign(zoomIntentDirection) != Mathf.Sign(direction))
            {
                zoomIntentDirection = direction;
                zoomIntentTimeRemaining = addedDuration;
            }
            else
            {
                zoomIntentTimeRemaining += addedDuration;
            }

            zoomIntentTimeRemaining = Mathf.Clamp(
                zoomIntentTimeRemaining,
                0.0f,
                settings.MaximumZoomIntentDuration);

            Vector3 anchor;
            if (settings.ZoomAroundPointer &&
                TryResolvePointerGroundPoint(pointerPosition, out anchor))
            {
                zoomAnchorActive = true;
                zoomAnchorWorldPoint = anchor;
                zoomAnchorScreenPoint = pointerPosition;
            }
        }

        private void UpdateZoomVelocity(float deltaTime)
        {
            bool intentActive = zoomIntentTimeRemaining > 0.0001f;
            float desiredZoomVelocity = 0.0f;
            if (intentActive)
            {
                float zoomSpeed =
                    BistroBuilderProfessionalCameraMath.CalculateContinuousZoomSpeed(
                        targetDistance,
                        settings.MinimumOperationalDistance,
                        settings.MaximumOperationalDistance,
                        settings.ZoomSpeedNear,
                        settings.ZoomSpeedFar);
                desiredZoomVelocity = zoomIntentDirection * zoomSpeed;
                zoomIntentTimeRemaining = Mathf.Max(
                    0.0f,
                    zoomIntentTimeRemaining - deltaTime);
            }
            else
            {
                zoomIntentDirection = 0.0f;
            }

            float smoothTime = intentActive
                ? settings.ZoomAccelerationTime
                : settings.ZoomDecelerationTime;
            currentZoomVelocity = Mathf.SmoothDamp(
                currentZoomVelocity,
                desiredZoomVelocity,
                ref zoomVelocitySmoothReference,
                smoothTime,
                settings.ZoomSpeedFar * settings.MaximumPanSpeedSafetyMultiplier,
                deltaTime);

            if (Mathf.Abs(currentZoomVelocity) <= 0.0001f)
            {
                currentZoomVelocity = 0.0f;
            }

            float previousTargetDistance = targetDistance;
            targetDistance = Mathf.Clamp(
                targetDistance + currentZoomVelocity * deltaTime,
                settings.MinimumOperationalDistance,
                settings.MaximumOperationalDistance);

            if (zoomAnchorActive && !Mathf.Approximately(previousTargetDistance, targetDistance))
            {
                PreserveZoomAnchor(
                    ref targetFocusPoint,
                    targetYaw,
                    targetPitch,
                    targetDistance);
            }

            bool blockedAtNearLimit =
                targetDistance <= settings.MinimumOperationalDistance + 0.0001f &&
                currentZoomVelocity < 0.0f;
            bool blockedAtFarLimit =
                targetDistance >= settings.MaximumOperationalDistance - 0.0001f &&
                currentZoomVelocity > 0.0f;
            if (blockedAtNearLimit || blockedAtFarLimit ||
                (Mathf.Approximately(previousTargetDistance, targetDistance) &&
                 Mathf.Abs(currentZoomVelocity) > 0.0001f))
            {
                currentZoomVelocity = 0.0f;
                zoomVelocitySmoothReference = 0.0f;
                zoomIntentTimeRemaining = 0.0f;
                zoomIntentDirection = 0.0f;
            }
        }

        private void StopZoomImmediately()
        {
            currentZoomVelocity = 0.0f;
            zoomVelocitySmoothReference = 0.0f;
            zoomIntentTimeRemaining = 0.0f;
            zoomIntentDirection = 0.0f;
            zoomAnchorActive = false;
        }

        private void HandleMiddleMouseDrag(
            BistroBuilderCameraInputFrame input,
            bool pointerBlocked)
        {
            if (!settings.MiddleMouseDragEnabled)
            {
                middleDragActive = false;
                return;
            }

            if (input.MiddlePressed && !pointerBlocked && input.PointerAvailable)
            {
                middleDragActive = true;
            }

            // Se ignora el delta del mismo fotograma en que se pulsa la rueda. Algunos backends
            // entregan en ese instante el movimiento acumulado anterior y provocarían un salto inicial.
            if (middleDragActive && input.MiddleHeld && !input.MiddlePressed && input.PointerAvailable)
            {
                Vector2 safePointerDelta = Vector2.ClampMagnitude(
                    input.PointerDelta,
                    settings.MaximumPointerDeltaPerFrame);
                float verticalSpan = GetVisibleVerticalSpan();
                Vector3 dragDelta = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                    safePointerDelta,
                    controlledCamera.transform.rotation,
                    verticalSpan,
                    controlledCamera.aspect,
                    Mathf.Max(1, controlledCamera.pixelWidth),
                    Mathf.Max(1, controlledCamera.pixelHeight),
                    settings.MiddleMouseDragSensitivity,
                    settings.MiddleMouseDragDeadZonePixels);
                if (BistroBuilderProfessionalCameraMath.IsFinite(dragDelta))
                {
                    targetFocusPoint += dragDelta;
                }
            }

            if (input.MiddleReleased || !input.MiddleHeld)
            {
                middleDragActive = false;
            }
        }

        private void HandleRightMouseRotation(
            BistroBuilderCameraInputFrame input,
            bool pointerBlocked)
        {
            if (!settings.RightMouseRotationEnabled)
            {
                rightDragActive = false;
                rightOrbitPivotValid = false;
                return;
            }

            if (input.RightPressed && !pointerBlocked && input.PointerAvailable)
            {
                // La manipulación directa toma como origen el encuadre que el jugador ve en ese
                // instante, no un objetivo todavía en tránsito. Esto evita un tirón al iniciar la órbita.
                targetFocusPoint = currentFocusPoint;
                targetYaw = currentYaw;
                targetPitch = currentPitch;
                targetDistance = currentDistance;
                focusSmoothVelocity = Vector3.zero;
                yawSmoothVelocity = 0.0f;
                pitchSmoothVelocity = 0.0f;
                distanceSmoothVelocity = 0.0f;
                StopZoomImmediately();
                CancelKeyboardOrbit();

                rightDragActive = true;
                rightOrbitPivotValid = settings.OrbitAroundPointer &&
                    TryResolvePointerOrbitPivot(input.PointerPosition, out rightOrbitPivot);
                if (!rightOrbitPivotValid)
                {
                    rightOrbitPivot = targetFocusPoint;
                    rightOrbitPivotValid = true;
                }
            }

            if (rightDragActive && input.RightHeld)
            {
                Vector2 safePointerDelta = Vector2.ClampMagnitude(
                    input.PointerDelta,
                    settings.MaximumPointerDeltaPerFrame);
                float nextYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(
                    targetYaw + safePointerDelta.x * settings.MouseYawDegreesPerPixel);
                float nextPitch = targetPitch;
                if (settings.MousePitchEnabled)
                {
                    nextPitch = Mathf.Clamp(
                        targetPitch - safePointerDelta.y * settings.MousePitchDegreesPerPixel,
                        GetActiveMinimumPitch(),
                        GetActiveMaximumPitch());
                }

                Vector3 orbitFocus;
                float orbitDistance;
                if (rightOrbitPivotValid &&
                    BistroBuilderProfessionalCameraMath.TryOrbitStateAroundPivot(
                        targetFocusPoint,
                        targetYaw,
                        targetPitch,
                        targetDistance,
                        rightOrbitPivot,
                        nextYaw,
                        nextPitch,
                        GetGroundHeight(),
                        settings.MinimumDistance,
                        settings.MaximumDistance,
                        out orbitFocus,
                        out orbitDistance))
                {
                    targetFocusPoint = orbitFocus;
                    targetDistance = orbitDistance;
                    targetYaw = nextYaw;
                    targetPitch = nextPitch;
                }
                else
                {
                    targetYaw = nextYaw;
                    targetPitch = nextPitch;
                }
            }

            if (input.RightReleased || !input.RightHeld)
            {
                rightDragActive = false;
                rightOrbitPivotValid = false;
            }
        }

        private bool TryResolvePointerOrbitPivot(Vector2 pointerPosition, out Vector3 pivot)
        {
            pivot = targetFocusPoint;
            if (controlledCamera == null || !BistroBuilderProfessionalCameraMath.IsFinite(pointerPosition.x) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(pointerPosition.y))
            {
                return false;
            }

            Ray pointerRay = controlledCamera.ScreenPointToRay(pointerPosition);
            if (!BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                pointerRay,
                GetGroundHeight(),
                out pivot))
            {
                return false;
            }

            if (navigationBounds != null && navigationBounds.IsValid)
            {
                pivot = navigationBounds.ClampGroundPoint(pivot);
            }

            pivot.y = GetGroundHeight();
            return BistroBuilderProfessionalCameraMath.IsFinite(pivot);
        }

        private void SmoothAndApply(float deltaTime)
        {
            float positionDamping = middleDragActive
                ? settings.DragPositionDampingTime
                : settings.PositionDampingTime;

            currentFocusPoint = Vector3.SmoothDamp(
                currentFocusPoint,
                targetFocusPoint,
                ref focusSmoothVelocity,
                positionDamping,
                Mathf.Infinity,
                deltaTime);

            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawSmoothVelocity,
                settings.RotationDampingTime,
                Mathf.Infinity,
                deltaTime);
            currentPitch = Mathf.SmoothDampAngle(
                currentPitch,
                targetPitch,
                ref pitchSmoothVelocity,
                settings.RotationDampingTime,
                Mathf.Infinity,
                deltaTime);
            currentDistance = Mathf.SmoothDamp(
                currentDistance,
                targetDistance,
                ref distanceSmoothVelocity,
                settings.ZoomDampingTime,
                Mathf.Infinity,
                deltaTime);
            currentDistance = Mathf.Clamp(
                currentDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);

            ConstrainCurrentState();

            // El anclaje se aplica al final, una vez resueltas altura global y huella X/Z. Así ni el
            // clamp de seguridad ni la amortiguación pueden desplazar el punto elegido en pantalla.
            if (zoomAnchorActive)
            {
                PreserveZoomAnchor(
                    ref currentFocusPoint,
                    currentYaw,
                    currentPitch,
                    currentDistance);
            }

            ApplyCurrentTransform();
            UpdateMotionMetrics(deltaTime);

            if (zoomAnchorActive &&
                zoomIntentTimeRemaining <= 0.0001f &&
                Mathf.Abs(currentZoomVelocity) <= 0.02f &&
                Mathf.Abs(distanceSmoothVelocity) <= 0.02f &&
                Mathf.Abs(targetDistance - currentDistance) <= 0.02f)
            {
                zoomAnchorActive = false;
            }
        }

        private void ApplyCurrentTransform()
        {
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);
            Vector3 position = currentFocusPoint - rotation * Vector3.forward * currentDistance;
            controlledCamera.transform.SetPositionAndRotation(position, rotation);
            if (controlledCamera.orthographic && orthographicSizePerDistance > 0.0f)
            {
                controlledCamera.orthographicSize = Mathf.Max(0.01f, currentDistance * orthographicSizePerDistance);
            }
        }

        private void UpdateMotionMetrics(float deltaTime)
        {
            if (deltaTime <= 0.0f)
            {
                LastFrameLinearSpeed = 0.0f;
                LastFrameAngularSpeed = 0.0f;
                LastFrameZoomSpeed = 0.0f;
                return;
            }

            Transform cameraTransform = controlledCamera.transform;
            Vector3 currentPosition = cameraTransform.position;
            Quaternion currentRotation = cameraTransform.rotation;
            LastFrameLinearSpeed = Vector3.Distance(previousAppliedPosition, currentPosition) / deltaTime;
            LastFrameAngularSpeed = Quaternion.Angle(previousAppliedRotation, currentRotation) / deltaTime;
            LastFrameZoomSpeed = Mathf.Abs(currentDistance - previousAppliedDistance) / deltaTime;

            previousAppliedPosition = currentPosition;
            previousAppliedRotation = currentRotation;
            previousAppliedDistance = currentDistance;
        }

        private void ResetMotionMetricsReference()
        {
            if (controlledCamera == null)
            {
                previousAppliedPosition = Vector3.zero;
                previousAppliedRotation = Quaternion.identity;
                previousAppliedDistance = currentDistance;
            }
            else
            {
                previousAppliedPosition = controlledCamera.transform.position;
                previousAppliedRotation = controlledCamera.transform.rotation;
                previousAppliedDistance = currentDistance;
            }

            LastFrameLinearSpeed = 0.0f;
            LastFrameAngularSpeed = 0.0f;
            LastFrameZoomSpeed = 0.0f;
        }

        private float GetActiveMinimumPitch()
        {
            return externalPitchRangeActive
                ? externalMinimumPitch
                : settings.MinimumPitch;
        }

        private float GetActiveMaximumPitch()
        {
            return externalPitchRangeActive
                ? externalMaximumPitch
                : settings.MaximumPitch;
        }

        private void ConstrainTargetState()
        {
            targetPitch = Mathf.Clamp(targetPitch, GetActiveMinimumPitch(), GetActiveMaximumPitch());
            targetDistance = Mathf.Clamp(
                targetDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);

            if (navigationBounds != null && navigationBounds.IsValid)
            {
                targetFocusPoint = navigationBounds.ClampFocusPoint(targetFocusPoint);
            }

            ClampCameraHeightByTranslatingFocus(
                ref targetFocusPoint,
                targetYaw,
                targetPitch,
                targetDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight);
        }

        private void ConstrainCurrentState()
        {
            currentPitch = Mathf.Clamp(currentPitch, GetActiveMinimumPitch(), GetActiveMaximumPitch());
            Vector3 unconstrainedFocus = currentFocusPoint;

            if (navigationBounds != null && navigationBounds.IsValid)
            {
                currentFocusPoint = navigationBounds.ClampFocusPoint(currentFocusPoint);
            }

            ClampCameraHeightByTranslatingFocus(
                ref currentFocusPoint,
                currentYaw,
                currentPitch,
                currentDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight);

            Vector3 correction = currentFocusPoint - unconstrainedFocus;
            if (Mathf.Abs(correction.x) > 0.0001f)
            {
                focusSmoothVelocity.x = 0.0f;
                currentPanVelocity.x = 0.0f;
                panVelocitySmoothReference.x = 0.0f;
            }

            if (Mathf.Abs(correction.z) > 0.0001f)
            {
                focusSmoothVelocity.z = 0.0f;
                currentPanVelocity.z = 0.0f;
                panVelocitySmoothReference.z = 0.0f;
            }
        }

        private float CalculateCameraHeightAboveGround(
            Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance,
            float groundHeight)
        {
            Vector3 cameraPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                focusPoint,
                yaw,
                pitch,
                distance);
            return cameraPosition.y - groundHeight;
        }

        private void ClampCameraHeightByTranslatingFocus(
            ref Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance,
            float minimumHeight,
            float maximumHeight)
        {
            float height = CalculateCameraHeightAboveGround(
                focusPoint,
                yaw,
                pitch,
                distance,
                GetGroundHeight());
            float clampedHeight = Mathf.Clamp(height, minimumHeight, maximumHeight);
            focusPoint.y += clampedHeight - height;
        }

        private bool TryResolvePointerGroundPoint(Vector2 pointerPosition, out Vector3 worldPoint)
        {
            worldPoint = default(Vector3);
            if (controlledCamera == null ||
                !BistroBuilderProfessionalCameraMath.IsFinite(pointerPosition.x) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(pointerPosition.y))
            {
                return false;
            }

            Ray ray = controlledCamera.ScreenPointToRay(pointerPosition);
            if (!BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                ray,
                GetGroundHeight(),
                out worldPoint))
            {
                return false;
            }

            if (navigationBounds != null && navigationBounds.IsValid)
            {
                worldPoint = navigationBounds.ClampGroundPoint(worldPoint);
            }

            return BistroBuilderProfessionalCameraMath.IsFinite(worldPoint);
        }

        private void PreserveZoomAnchor(
            ref Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance)
        {
            if (!zoomAnchorActive || controlledCamera == null)
            {
                return;
            }

            Ray ray;
            if (!TryBuildScreenRayForPose(
                zoomAnchorScreenPoint,
                focusPoint,
                yaw,
                pitch,
                distance,
                out ray))
            {
                return;
            }

            Vector3 projectedGroundPoint;
            if (!BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                ray,
                GetGroundHeight(),
                out projectedGroundPoint))
            {
                return;
            }

            Vector3 correction = zoomAnchorWorldPoint - projectedGroundPoint;
            correction.y = 0.0f;
            if (!BistroBuilderProfessionalCameraMath.IsFinite(correction))
            {
                return;
            }

            focusPoint += correction;
            if (navigationBounds != null && navigationBounds.IsValid)
            {
                focusPoint = navigationBounds.ClampFocusPoint(focusPoint);
            }
        }

        private bool TryBuildScreenRayForPose(
            Vector2 screenPoint,
            Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance,
            out Ray ray)
        {
            ray = default(Ray);
            if (controlledCamera == null || controlledCamera.pixelRect.width <= 0.0f ||
                controlledCamera.pixelRect.height <= 0.0f)
            {
                return false;
            }

            Rect pixelRect = controlledCamera.pixelRect;
            float viewportX = (screenPoint.x - pixelRect.xMin) / pixelRect.width;
            float viewportY = (screenPoint.y - pixelRect.yMin) / pixelRect.height;
            if (!BistroBuilderProfessionalCameraMath.IsFinite(viewportX) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(viewportY))
            {
                return false;
            }

            // Extraemos el rayo en espacio local de la cámara real. Esto respeta perspectiva,
            // ortográfica, lens shift y cualquier matriz de proyección configurada por el proyecto.
            Ray referenceRay = controlledCamera.ViewportPointToRay(
                new Vector3(viewportX, viewportY, 0.0f));
            Transform cameraTransform = controlledCamera.transform;
            Vector3 localOrigin = Quaternion.Inverse(cameraTransform.rotation) *
                                  (referenceRay.origin - cameraTransform.position);
            Vector3 localDirection = Quaternion.Inverse(cameraTransform.rotation) *
                                     referenceRay.direction;

            Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0.0f);
            Vector3 targetCameraPosition =
                focusPoint - targetRotation * Vector3.forward * distance;
            Vector3 targetOrigin = targetCameraPosition + targetRotation * localOrigin;
            Vector3 targetDirection = targetRotation * localDirection;
            if (!BistroBuilderProfessionalCameraMath.IsFinite(targetOrigin) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(targetDirection) ||
                targetDirection.sqrMagnitude <= 0.000001f)
            {
                return false;
            }

            ray = new Ray(targetOrigin, targetDirection.normalized);
            return true;
        }

        private float GetVisibleVerticalSpan()
        {
            if (controlledCamera.orthographic)
            {
                return Mathf.Max(0.01f, controlledCamera.orthographicSize * 2.0f);
            }

            float halfFieldOfViewRadians = controlledCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            return Mathf.Max(0.01f, 2.0f * currentDistance * Mathf.Tan(halfFieldOfViewRadians));
        }

        private float GetGroundHeight()
        {
            return navigationBounds != null && navigationBounds.IsValid
                ? navigationBounds.GroundHeight
                : fallbackGroundHeight;
        }

        private void SnapCurrentToTarget()
        {
            currentFocusPoint = targetFocusPoint;
            currentYaw = targetYaw;
            currentPitch = targetPitch;
            currentDistance = targetDistance;

            focusSmoothVelocity = Vector3.zero;
            yawSmoothVelocity = 0.0f;
            pitchSmoothVelocity = 0.0f;
            distanceSmoothVelocity = 0.0f;
            currentPanVelocity = Vector3.zero;
            panVelocitySmoothReference = Vector3.zero;
            currentYawVelocity = 0.0f;
            yawVelocitySmoothReference = 0.0f;
            currentElevationVelocity = 0.0f;
            elevationVelocitySmoothReference = 0.0f;
            verticalElevationGestureActive = false;
            InvalidateElevatorReference();
            StopZoomImmediately();
            CancelKeyboardOrbit();
        }

        private void CancelDirectManipulation()
        {
            middleDragActive = false;
            rightDragActive = false;
            rightOrbitPivotValid = false;
            verticalElevationGestureActive = false;
            CancelKeyboardOrbit();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!showRuntimeDiagnostics || controlledCamera == null || settings == null)
            {
                return;
            }

            float height = controlledCamera.transform.position.y - GetGroundHeight();
            string text =
                "369A12 CAMERA\n" +
                "Pos: " + controlledCamera.transform.position.ToString("F2") + "\n" +
                "Foco: " + currentFocusPoint.ToString("F2") + "\n" +
                "Altura: " + height.ToString("F2") + " m\n" +
                "Distancia: " + currentDistance.ToString("F2") + " m\n" +
                "Yaw/Pitch: " + currentYaw.ToString("F1") + " / " + currentPitch.ToString("F1") + "\n" +
                "Zoom anclado: " + (zoomAnchorActive ? "sí" : "no") + "\n" +
                "R/F: " + (elevatorReferenceValid
                    ? effectiveElevatorMinimumHeight.ToString("F1") + "–" +
                      effectiveElevatorMaximumHeight.ToString("F1") + " m"
                    : "pendiente de referencia");

            GUI.Box(new Rect(12.0f, 12.0f, 275.0f, 138.0f), text);
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif
    }
}
