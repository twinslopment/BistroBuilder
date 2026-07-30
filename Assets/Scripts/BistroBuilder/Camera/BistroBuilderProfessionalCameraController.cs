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
    /// - El zoom es logarítmico, usa un rango operativo separado y una transición más lenta para
    ///   evitar extremos o saltos bruscos al girar la rueda.
    /// - R/F ejecutan una elevación vertical recta: la cámara cambia su Y sin variar la inclinación.
    ///   El rango operativo es corto, la velocidad es contenida y frena antes de los topes.
    /// - El arrastre central convierte el delta del puntero a unidades del encuadre visible, evitando
    ///   realimentación y vibraciones mientras conserva una respuesta proporcional al zoom.
    /// - Se utiliza tiempo no escalado por defecto: la cámara continúa operativa con el juego en pausa.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class BistroBuilderProfessionalCameraController : MonoBehaviour
    {
        public const int RuntimeRevision = 8;

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

        private bool middleDragActive;
        private bool rightDragActive;
        private Vector3 rightOrbitPivot;
        private bool rightOrbitPivotValid;
        private float pendingZoomLogAmount;

        private Vector3 previousAppliedPosition;
        private Quaternion previousAppliedRotation;
        private float previousAppliedDistance;
        private float orthographicSizePerDistance;

        public UnityEngine.Camera ControlledCamera { get { return controlledCamera; } }
        public BistroBuilderCameraNavigationSettings Settings { get { return settings; } }
        public BistroBuilderCameraBounds NavigationBounds { get { return navigationBounds; } }
        public bool InputEnabled { get { return inputEnabled; } }
        public bool IsInitialized { get { return initialized; } }
        public bool IsDirectManipulationActive { get { return middleDragActive || rightDragActive; } }
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
                pendingZoomLogAmount = 0.0f;
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

        public void SetTargetState(BistroBuilderCameraNavigationState state, bool immediate)
        {
            if (!state.IsFinite || !InitializeIfPossible())
            {
                return;
            }

            targetFocusPoint = state.FocusPoint;
            targetYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(state.Yaw);
            targetPitch = Mathf.Clamp(state.Pitch, settings.MinimumPitch, settings.MaximumPitch);
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
                settings.MinimumPitch,
                settings.MaximumPitch);

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
            }

            SmoothKeyboardVelocity(panInput, yawInput, elevationInput, input.FastModifier, deltaTime);

            if (!middleDragActive)
            {
                targetFocusPoint += currentPanVelocity * deltaTime;
            }

            if (!rightDragActive)
            {
                targetYaw += currentYawVelocity * deltaTime;
            }

            ApplyVerticalElevatorMovement(currentElevationVelocity * deltaTime);

            if (!pointerBlocked && input.PointerAvailable)
            {
                float normalizedScroll = BistroBuilderProfessionalCameraMath.NormalizeScroll(
                    input.RawScroll,
                    settings.MaximumScrollNotchesPerFrame);
                if (Mathf.Abs(normalizedScroll) > 0.0001f)
                {
                    float maximumQueuedLog =
                        settings.MaximumQueuedScrollNotches * settings.LogarithmicZoomStep;
                    pendingZoomLogAmount = Mathf.Clamp(
                        pendingZoomLogAmount - normalizedScroll * settings.LogarithmicZoomStep,
                        -maximumQueuedLog,
                        maximumQueuedLog);
                }
            }

            float consumedZoomLog =
                BistroBuilderProfessionalCameraMath.ConsumeSmoothedZoomLogAmount(
                    ref pendingZoomLogAmount,
                    settings.ZoomInputSmoothingTime,
                    deltaTime);
            if (Mathf.Abs(consumedZoomLog) > 0.000001f)
            {
                targetDistance = Mathf.Clamp(
                    targetDistance * Mathf.Exp(consumedZoomLog),
                    settings.MinimumOperationalDistance,
                    settings.MaximumOperationalDistance);
            }

            targetYaw = BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(targetYaw);
            targetPitch = Mathf.Clamp(targetPitch, settings.MinimumPitch, settings.MaximumPitch);
            targetDistance = Mathf.Clamp(
                targetDistance,
                settings.MinimumDistance,
                settings.MaximumDistance);
            ConstrainTargetState();
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

        private void ApplyVerticalElevatorMovement(float requestedHeightDelta)
        {
            if (Mathf.Abs(requestedHeightDelta) <= 0.000001f)
            {
                return;
            }

            float groundHeight = GetGroundHeight();
            float targetHeight =
                BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                    targetFocusPoint,
                    targetYaw,
                    targetPitch,
                    targetDistance).y - groundHeight;
            float currentHeight =
                BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                    currentFocusPoint,
                    currentYaw,
                    currentPitch,
                    currentDistance).y - groundHeight;

            float targetLimitedDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    targetHeight,
                    requestedHeightDelta,
                    settings.MinimumElevatorHeight,
                    settings.MaximumElevatorHeight,
                    settings.ElevatorSoftLimitRange);
            float currentLimitedDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    currentHeight,
                    requestedHeightDelta,
                    settings.MinimumElevatorHeight,
                    settings.MaximumElevatorHeight,
                    settings.ElevatorSoftLimitRange);

            // Objetivo y estado visible reciben exactamente el mismo delta. Elegimos el más restrictivo
            // para que ninguno atraviese el rango operativo durante una transición amortiguada.
            float effectiveHeightDelta = Mathf.Sign(requestedHeightDelta) * Mathf.Min(
                Mathf.Abs(targetLimitedDelta),
                Mathf.Abs(currentLimitedDelta));
            if (Mathf.Abs(effectiveHeightDelta) <= 0.000001f)
            {
                // Al tocar un límite eliminamos la presión residual. Así, invertir R/F responde
                // inmediatamente en lugar de tener que vencer una velocidad acumulada contra el tope.
                currentElevationVelocity = 0.0f;
                elevationVelocitySmoothReference = 0.0f;
                return;
            }

            Vector3 nextTargetFocusPoint;
            float nextTargetDistance;
            if (BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                targetFocusPoint,
                targetYaw,
                targetPitch,
                targetDistance,
                effectiveHeightDelta,
                groundHeight,
                settings.MinimumDistance,
                settings.MaximumDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight,
                out nextTargetFocusPoint,
                out nextTargetDistance))
            {
                targetFocusPoint = nextTargetFocusPoint;
                targetDistance = nextTargetDistance;
            }

            // La velocidad vertical ya está amortiguada. Aplicar el mismo desplazamiento al estado
            // visible y al objetivo evita que dos SmoothDamp independientes dibujen un arco lateral.
            Vector3 nextCurrentFocusPoint;
            float nextCurrentDistance;
            if (BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                currentFocusPoint,
                currentYaw,
                currentPitch,
                currentDistance,
                effectiveHeightDelta,
                groundHeight,
                settings.MinimumDistance,
                settings.MaximumDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight,
                out nextCurrentFocusPoint,
                out nextCurrentDistance))
            {
                currentFocusPoint = nextCurrentFocusPoint;
                currentDistance = nextCurrentDistance;
            }

            // El pitch no cambia: R/F desplazan la cámara únicamente en Y.
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
                pendingZoomLogAmount = 0.0f;

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
                        settings.MinimumPitch,
                        settings.MaximumPitch);
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
                pivot = navigationBounds.ClampFocusPoint(pivot);
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
            currentFocusPoint.y = GetGroundHeight();

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
            currentDistance = BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                currentDistance,
                currentPitch,
                settings.MinimumDistance,
                settings.MaximumDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight);
            ConstrainCurrentState();

            ApplyCurrentTransform();
            UpdateMotionMetrics(deltaTime);
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

        private void ConstrainTargetState()
        {
            targetPitch = Mathf.Clamp(targetPitch, settings.MinimumPitch, settings.MaximumPitch);
            targetDistance = BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                targetDistance,
                targetPitch,
                settings.MinimumDistance,
                settings.MaximumDistance,
                settings.MinimumCameraHeight,
                settings.MaximumCameraHeight);
            targetFocusPoint.y = GetGroundHeight();
            if (navigationBounds == null || !navigationBounds.IsValid)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.Euler(targetPitch, targetYaw, 0.0f);
            float boundsMaximumDistance = navigationBounds.CalculateMaximumDistanceForCameraAndFocus(
                targetRotation,
                settings.MaximumDistance);
            if (BistroBuilderProfessionalCameraMath.IsFinite(boundsMaximumDistance))
            {
                targetDistance = Mathf.Min(
                    targetDistance,
                    Mathf.Max(settings.MinimumDistance, boundsMaximumDistance));
            }

            targetFocusPoint = navigationBounds.Constrain(
                targetFocusPoint,
                targetRotation,
                targetDistance);
            targetFocusPoint.y = GetGroundHeight();
        }

        private void ConstrainCurrentState()
        {
            currentPitch = Mathf.Clamp(currentPitch, settings.MinimumPitch, settings.MaximumPitch);
            currentFocusPoint.y = GetGroundHeight();
            if (navigationBounds == null || !navigationBounds.IsValid)
            {
                return;
            }

            Vector3 unconstrainedFocus = currentFocusPoint;
            Quaternion currentRotation = Quaternion.Euler(currentPitch, currentYaw, 0.0f);
            float boundsMaximumDistance = navigationBounds.CalculateMaximumDistanceForCameraAndFocus(
                currentRotation,
                settings.MaximumDistance);
            if (BistroBuilderProfessionalCameraMath.IsFinite(boundsMaximumDistance))
            {
                currentDistance = Mathf.Min(
                    currentDistance,
                    Mathf.Max(settings.MinimumDistance, boundsMaximumDistance));
            }

            currentFocusPoint = navigationBounds.Constrain(
                currentFocusPoint,
                currentRotation,
                currentDistance);
            currentFocusPoint.y = GetGroundHeight();

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
            pendingZoomLogAmount = 0.0f;
        }

        private void CancelDirectManipulation()
        {
            middleDragActive = false;
            rightDragActive = false;
            rightOrbitPivotValid = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveReferences();
        }
#endif
    }
}
