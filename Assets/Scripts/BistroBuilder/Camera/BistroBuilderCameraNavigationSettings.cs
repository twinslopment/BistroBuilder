using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Configuración canónica y reutilizable de la navegación de cámara 369A.
    /// Los valores se mantienen fuera de la escena para que las futuras vistas 369B/369C
    /// puedan compartir exactamente la misma respuesta de movimiento.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BistroBuilderCameraNavigationSettings",
        menuName = "Bistro Builder/Camera/369A Camera Navigation Settings")]
    public sealed class BistroBuilderCameraNavigationSettings : ScriptableObject
    {
        public const int CurrentInteractionProfileVersion = 7;

        [Header("Entrada")]
        [SerializeField] private bool keyboardPanEnabled = true;
        [SerializeField] private bool keyboardElevationEnabled = true;
        [SerializeField] private bool edgePanEnabled = true;
        [SerializeField] private bool middleMouseDragEnabled = true;
        [SerializeField] private bool rightMouseRotationEnabled = true;
        [SerializeField] private bool mousePitchEnabled = true;
        [SerializeField] private bool orbitAroundPointer = true;
        [SerializeField] private bool blockPointerInputOverUi = true;
        [SerializeField] private bool blockKeyboardWhileTyping = true;
        [SerializeField] private bool requireApplicationFocus = true;
        [Min(1.0f)]
        [SerializeField] private float maximumPointerDeltaPerFrame = 180.0f;

        [Header("Desplazamiento")]
        [Min(0.01f)]
        [SerializeField] private float panSpeedNear = 6.5f;
        [Min(0.01f)]
        [SerializeField] private float panSpeedFar = 26.0f;
        [Min(1.0f)]
        [SerializeField] private float fastPanMultiplier = 1.75f;
        [Min(0.01f)]
        [SerializeField] private float panAccelerationTime = 0.16f;
        [Min(0.01f)]
        [SerializeField] private float panDecelerationTime = 0.24f;
        [Min(0.01f)]
        [SerializeField] private float positionDampingTime = 0.13f;
        [Min(0.01f)]
        [SerializeField] private float dragPositionDampingTime = 0.055f;
        [Range(0.1f, 3.0f)]
        [SerializeField] private float middleMouseDragSensitivity = 1.0f;
        [Range(0.0f, 3.0f)]
        [SerializeField] private float middleMouseDragDeadZonePixels = 0.35f;

        [Header("Elevación vertical recta")]
        [Min(0.01f)]
        [SerializeField] private float keyboardElevationSpeed = 3.25f;
        [Min(0.01f)]
        [SerializeField] private float elevationAccelerationTime = 0.20f;
        [Min(0.01f)]
        [SerializeField] private float elevationDecelerationTime = 0.34f;
        [Min(0.1f)]
        [SerializeField] private float minimumElevatorHeight = 10.0f;
        [Min(0.2f)]
        [SerializeField] private float maximumElevatorHeight = 22.0f;
        [Min(0.0f)]
        [SerializeField] private float elevatorSoftLimitRange = 2.75f;

        [Header("Bordes de pantalla")]
        [Range(0.005f, 0.15f)]
        [SerializeField] private float edgeMarginNormalized = 0.025f;
        [Min(1)]
        [SerializeField] private int edgeMarginMinimumPixels = 14;
        [Min(1)]
        [SerializeField] private int edgeMarginMaximumPixels = 54;
        [Range(0.1f, 2.0f)]
        [SerializeField] private float edgePanStrength = 1.0f;

        [Header("Rotación")]
        [Min(0.01f)]
        [SerializeField] private float keyboardYawSpeed = 82.0f;
        [Min(0.001f)]
        [SerializeField] private float mouseYawDegreesPerPixel = 0.12f;
        [Min(0.001f)]
        [SerializeField] private float mousePitchDegreesPerPixel = 0.10f;
        [Min(0.01f)]
        [SerializeField] private float yawAccelerationTime = 0.12f;
        [Min(0.01f)]
        [SerializeField] private float yawDecelerationTime = 0.17f;
        [Min(0.01f)]
        [SerializeField] private float rotationDampingTime = 0.18f;
        [Range(10.0f, 85.0f)]
        [SerializeField] private float minimumPitch = 22.0f;
        [Range(10.0f, 89.0f)]
        [SerializeField] private float maximumPitch = 72.0f;
        [Range(10.0f, 89.0f)]
        [SerializeField] private float fallbackPitch = 48.0f;
        [Min(0.1f)]
        [SerializeField] private float minimumCameraHeight = 4.5f;
        [Min(0.2f)]
        [SerializeField] private float maximumCameraHeight = 46.0f;

        [Header("Zoom")]
        [Min(0.1f)]
        [SerializeField] private float minimumDistance = 7.0f;
        [Min(0.2f)]
        [SerializeField] private float maximumDistance = 58.0f;
        [Min(0.1f)]
        [SerializeField] private float fallbackDistance = 24.0f;
        [Range(0.01f, 0.5f)]
        [SerializeField] private float logarithmicZoomStep = 0.032f;
        [Min(0.01f)]
        [SerializeField] private float zoomDampingTime = 0.30f;
        [Range(0.1f, 8.0f)]
        [SerializeField] private float maximumScrollNotchesPerFrame = 1.0f;
        [Min(0.01f)]
        [SerializeField] private float zoomInputSmoothingTime = 0.16f;
        [Range(0.5f, 8.0f)]
        [SerializeField] private float maximumQueuedScrollNotches = 3.0f;
        [Min(0.1f)]
        [SerializeField] private float minimumOperationalDistance = 10.5f;
        [Min(0.2f)]
        [SerializeField] private float maximumOperationalDistance = 32.0f;

        [Header("Estabilidad")]
        [Min(0.01f)]
        [SerializeField] private float maximumSimulationDeltaTime = 0.05f;
        [Min(0.1f)]
        [SerializeField] private float maximumPanSpeedSafetyMultiplier = 2.25f;
        [SerializeField] private bool useUnscaledTime = true;

        [SerializeField, HideInInspector] private int interactionProfileVersion;

        public bool KeyboardPanEnabled { get { return keyboardPanEnabled; } }
        public bool KeyboardElevationEnabled { get { return keyboardElevationEnabled; } }
        public bool EdgePanEnabled { get { return edgePanEnabled; } }
        public bool MiddleMouseDragEnabled { get { return middleMouseDragEnabled; } }
        public bool RightMouseRotationEnabled { get { return rightMouseRotationEnabled; } }
        public bool MousePitchEnabled { get { return mousePitchEnabled; } }
        public bool OrbitAroundPointer { get { return orbitAroundPointer; } }
        public bool BlockPointerInputOverUi { get { return blockPointerInputOverUi; } }
        public bool BlockKeyboardWhileTyping { get { return blockKeyboardWhileTyping; } }
        public bool RequireApplicationFocus { get { return requireApplicationFocus; } }
        public float MaximumPointerDeltaPerFrame { get { return maximumPointerDeltaPerFrame; } }

        public float PanSpeedNear { get { return panSpeedNear; } }
        public float PanSpeedFar { get { return panSpeedFar; } }
        public float FastPanMultiplier { get { return fastPanMultiplier; } }
        public float PanAccelerationTime { get { return panAccelerationTime; } }
        public float PanDecelerationTime { get { return panDecelerationTime; } }
        public float PositionDampingTime { get { return positionDampingTime; } }
        public float DragPositionDampingTime { get { return dragPositionDampingTime; } }
        public float MiddleMouseDragSensitivity { get { return middleMouseDragSensitivity; } }
        public float MiddleMouseDragDeadZonePixels { get { return middleMouseDragDeadZonePixels; } }
        public float KeyboardElevationSpeed { get { return keyboardElevationSpeed; } }
        public float ElevationAccelerationTime { get { return elevationAccelerationTime; } }
        public float ElevationDecelerationTime { get { return elevationDecelerationTime; } }
        public float MinimumElevatorHeight { get { return minimumElevatorHeight; } }
        public float MaximumElevatorHeight { get { return maximumElevatorHeight; } }
        public float ElevatorSoftLimitRange { get { return elevatorSoftLimitRange; } }

        public float EdgeMarginNormalized { get { return edgeMarginNormalized; } }
        public int EdgeMarginMinimumPixels { get { return edgeMarginMinimumPixels; } }
        public int EdgeMarginMaximumPixels { get { return edgeMarginMaximumPixels; } }
        public float EdgePanStrength { get { return edgePanStrength; } }

        public float KeyboardYawSpeed { get { return keyboardYawSpeed; } }
        public float MouseYawDegreesPerPixel { get { return mouseYawDegreesPerPixel; } }
        public float MousePitchDegreesPerPixel { get { return mousePitchDegreesPerPixel; } }
        public float YawAccelerationTime { get { return yawAccelerationTime; } }
        public float YawDecelerationTime { get { return yawDecelerationTime; } }
        public float RotationDampingTime { get { return rotationDampingTime; } }
        public float MinimumPitch { get { return minimumPitch; } }
        public float MaximumPitch { get { return maximumPitch; } }
        public float FallbackPitch { get { return fallbackPitch; } }
        public float MinimumCameraHeight { get { return minimumCameraHeight; } }
        public float MaximumCameraHeight { get { return maximumCameraHeight; } }

        public float MinimumDistance { get { return minimumDistance; } }
        public float MaximumDistance { get { return maximumDistance; } }
        public float FallbackDistance { get { return fallbackDistance; } }
        public float LogarithmicZoomStep { get { return logarithmicZoomStep; } }
        public float ZoomDampingTime { get { return zoomDampingTime; } }
        public float MaximumScrollNotchesPerFrame { get { return maximumScrollNotchesPerFrame; } }
        public float ZoomInputSmoothingTime { get { return zoomInputSmoothingTime; } }
        public float MaximumQueuedScrollNotches { get { return maximumQueuedScrollNotches; } }
        public float MinimumOperationalDistance { get { return minimumOperationalDistance; } }
        public float MaximumOperationalDistance { get { return maximumOperationalDistance; } }

        public float MaximumSimulationDeltaTime { get { return maximumSimulationDeltaTime; } }
        public float MaximumPanSpeedSafetyMultiplier { get { return maximumPanSpeedSafetyMultiplier; } }
        public bool UseUnscaledTime { get { return useUnscaledTime; } }
        public int InteractionProfileVersion { get { return interactionProfileVersion; } }

        /// <summary>
        /// Verificación sin efectos laterales utilizada por instalador, validador y autotest.
        /// </summary>
        public bool IsConfigurationValid(out string reason)
        {
            if (panSpeedNear <= 0.0f || panSpeedFar <= 0.0f || panSpeedFar < panSpeedNear)
            {
                reason = "Las velocidades de desplazamiento no son válidas.";
                return false;
            }

            if (panAccelerationTime <= 0.0f || panDecelerationTime <= 0.0f ||
                positionDampingTime <= 0.0f || dragPositionDampingTime <= 0.0f)
            {
                reason = "Los tiempos de amortiguación de desplazamiento deben ser positivos.";
                return false;
            }

            if (middleMouseDragSensitivity <= 0.0f || middleMouseDragDeadZonePixels < 0.0f)
            {
                reason = "La sensibilidad o zona muerta del arrastre central no es válida.";
                return false;
            }

            if (keyboardElevationSpeed <= 0.0f ||
                elevationAccelerationTime <= 0.0f || elevationDecelerationTime <= 0.0f)
            {
                reason = "La velocidad o amortiguación de elevación vertical no es válida.";
                return false;
            }

            if (minimumElevatorHeight < minimumCameraHeight ||
                maximumElevatorHeight > maximumCameraHeight ||
                maximumElevatorHeight <= minimumElevatorHeight ||
                elevatorSoftLimitRange < 0.0f ||
                elevatorSoftLimitRange > (maximumElevatorHeight - minimumElevatorHeight) * 0.5f)
            {
                reason = "El rango operativo o la zona de frenada de R/F no son válidos.";
                return false;
            }

            if (keyboardYawSpeed <= 0.0f || mouseYawDegreesPerPixel <= 0.0f ||
                mousePitchDegreesPerPixel <= 0.0f || yawAccelerationTime <= 0.0f ||
                yawDecelerationTime <= 0.0f || rotationDampingTime <= 0.0f)
            {
                reason = "La sensibilidad o amortiguación de rotación no es válida.";
                return false;
            }

            if (minimumPitch < 0.0f || maximumPitch <= minimumPitch || maximumPitch >= 90.0f)
            {
                reason = "Los límites de inclinación no son válidos.";
                return false;
            }

            if (minimumCameraHeight <= 0.0f || maximumCameraHeight <= minimumCameraHeight)
            {
                reason = "Los límites de altura de cámara no son válidos.";
                return false;
            }

            if (minimumDistance <= 0.0f || maximumDistance <= minimumDistance)
            {
                reason = "Los límites de zoom no son válidos.";
                return false;
            }

            if (fallbackDistance < minimumDistance || fallbackDistance > maximumDistance)
            {
                reason = "La distancia inicial debe quedar dentro de los límites de zoom.";
                return false;
            }

            if (fallbackPitch < minimumPitch || fallbackPitch > maximumPitch)
            {
                reason = "La inclinación inicial debe quedar dentro de sus límites.";
                return false;
            }

            float fallbackHeight = fallbackDistance * Mathf.Sin(fallbackPitch * Mathf.Deg2Rad);
            if (fallbackHeight < minimumCameraHeight || fallbackHeight > maximumCameraHeight)
            {
                reason = "La combinación inicial de inclinación y distancia incumple los límites de altura.";
                return false;
            }

            if (fallbackHeight < minimumElevatorHeight || fallbackHeight > maximumElevatorHeight)
            {
                reason = "La altura inicial debe quedar dentro del rango operativo de R/F.";
                return false;
            }

            if (logarithmicZoomStep <= 0.0f || zoomDampingTime <= 0.0f ||
                maximumScrollNotchesPerFrame <= 0.0f || zoomInputSmoothingTime <= 0.0f ||
                maximumQueuedScrollNotches < maximumScrollNotchesPerFrame)
            {
                reason = "Los parámetros de zoom o su cola de suavizado no son válidos.";
                return false;
            }

            if (minimumOperationalDistance < minimumDistance ||
                maximumOperationalDistance > maximumDistance ||
                maximumOperationalDistance <= minimumOperationalDistance ||
                fallbackDistance < minimumOperationalDistance ||
                fallbackDistance > maximumOperationalDistance)
            {
                reason = "El rango operativo de la rueda debe quedar dentro del rango global y contener la vista inicial.";
                return false;
            }

            if (maximumSimulationDeltaTime <= 0.0f)
            {
                reason = "El paso máximo de simulación debe ser positivo.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumPointerDeltaPerFrame = Mathf.Max(1.0f, maximumPointerDeltaPerFrame);

            panSpeedNear = Mathf.Max(0.01f, panSpeedNear);
            panSpeedFar = Mathf.Max(panSpeedNear, panSpeedFar);
            fastPanMultiplier = Mathf.Max(1.0f, fastPanMultiplier);
            panAccelerationTime = Mathf.Max(0.01f, panAccelerationTime);
            panDecelerationTime = Mathf.Max(0.01f, panDecelerationTime);
            positionDampingTime = Mathf.Max(0.01f, positionDampingTime);
            dragPositionDampingTime = Mathf.Max(0.01f, dragPositionDampingTime);
            middleMouseDragSensitivity = Mathf.Clamp(middleMouseDragSensitivity, 0.1f, 3.0f);
            middleMouseDragDeadZonePixels = Mathf.Clamp(middleMouseDragDeadZonePixels, 0.0f, 3.0f);
            keyboardElevationSpeed = Mathf.Max(0.01f, keyboardElevationSpeed);
            elevationAccelerationTime = Mathf.Max(0.01f, elevationAccelerationTime);
            elevationDecelerationTime = Mathf.Max(0.01f, elevationDecelerationTime);

            edgeMarginNormalized = Mathf.Clamp(edgeMarginNormalized, 0.005f, 0.15f);
            edgeMarginMinimumPixels = Mathf.Max(1, edgeMarginMinimumPixels);
            edgeMarginMaximumPixels = Mathf.Max(edgeMarginMinimumPixels, edgeMarginMaximumPixels);
            edgePanStrength = Mathf.Clamp(edgePanStrength, 0.1f, 2.0f);

            keyboardYawSpeed = Mathf.Max(0.01f, keyboardYawSpeed);
            mouseYawDegreesPerPixel = Mathf.Max(0.001f, mouseYawDegreesPerPixel);
            mousePitchDegreesPerPixel = Mathf.Max(0.001f, mousePitchDegreesPerPixel);
            yawAccelerationTime = Mathf.Max(0.01f, yawAccelerationTime);
            yawDecelerationTime = Mathf.Max(0.01f, yawDecelerationTime);
            rotationDampingTime = Mathf.Max(0.01f, rotationDampingTime);
            minimumPitch = Mathf.Clamp(minimumPitch, 10.0f, 85.0f);
            maximumPitch = Mathf.Clamp(maximumPitch, minimumPitch + 0.5f, 89.0f);
            fallbackPitch = Mathf.Clamp(fallbackPitch, minimumPitch, maximumPitch);
            minimumCameraHeight = Mathf.Max(0.1f, minimumCameraHeight);
            maximumCameraHeight = Mathf.Max(minimumCameraHeight + 0.1f, maximumCameraHeight);
            minimumElevatorHeight = Mathf.Clamp(
                minimumElevatorHeight,
                minimumCameraHeight,
                maximumCameraHeight - 0.1f);
            maximumElevatorHeight = Mathf.Clamp(
                maximumElevatorHeight,
                minimumElevatorHeight + 0.1f,
                maximumCameraHeight);
            elevatorSoftLimitRange = Mathf.Clamp(
                elevatorSoftLimitRange,
                0.0f,
                Mathf.Max(0.0f, (maximumElevatorHeight - minimumElevatorHeight) * 0.5f));

            minimumDistance = Mathf.Max(0.1f, minimumDistance);
            maximumDistance = Mathf.Max(minimumDistance + 0.1f, maximumDistance);
            fallbackDistance = Mathf.Clamp(fallbackDistance, minimumDistance, maximumDistance);
            fallbackDistance = BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                fallbackDistance,
                fallbackPitch,
                minimumDistance,
                maximumDistance,
                minimumCameraHeight,
                maximumCameraHeight);
            logarithmicZoomStep = Mathf.Clamp(logarithmicZoomStep, 0.01f, 0.5f);
            zoomDampingTime = Mathf.Max(0.01f, zoomDampingTime);
            maximumScrollNotchesPerFrame = Mathf.Clamp(maximumScrollNotchesPerFrame, 0.1f, 8.0f);
            zoomInputSmoothingTime = Mathf.Max(0.01f, zoomInputSmoothingTime);
            maximumQueuedScrollNotches = Mathf.Clamp(
                maximumQueuedScrollNotches,
                maximumScrollNotchesPerFrame,
                8.0f);
            minimumOperationalDistance = Mathf.Clamp(
                minimumOperationalDistance,
                minimumDistance,
                maximumDistance - 0.1f);
            maximumOperationalDistance = Mathf.Clamp(
                maximumOperationalDistance,
                minimumOperationalDistance + 0.1f,
                maximumDistance);
            fallbackDistance = Mathf.Clamp(
                fallbackDistance,
                minimumOperationalDistance,
                maximumOperationalDistance);

            maximumSimulationDeltaTime = Mathf.Max(0.01f, maximumSimulationDeltaTime);
            maximumPanSpeedSafetyMultiplier = Mathf.Max(0.1f, maximumPanSpeedSafetyMultiplier);
        }
#endif
    }
}
