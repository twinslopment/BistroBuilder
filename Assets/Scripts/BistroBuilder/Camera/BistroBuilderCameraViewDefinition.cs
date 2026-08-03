using System;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Definición serializable de una vista canónica. No guarda posiciones absolutas de escena:
    /// el estado final se calcula a partir de la huella del local, la cámara y el foco actual.
    /// </summary>
    [Serializable]
    public sealed class BistroBuilderCameraViewDefinition
    {
        [SerializeField] private BistroBuilderCameraViewId id;
        [SerializeField] private string displayName;
        [SerializeField] private BistroBuilderCameraViewFocusMode focusMode;
        [SerializeField] private BistroBuilderCameraViewYawMode yawMode;
        [SerializeField] private BistroBuilderCameraViewFramingMode framingMode;
        [SerializeField] private float fixedYaw;
        [SerializeField] private float pitch;
        [SerializeField] private float fixedDistance;
        [SerializeField] private float framingMargin;
        [SerializeField] private float framingContentHeight;
        [SerializeField] private float focusHeight;
        [SerializeField] private bool allowExtendedPitch;
        [SerializeField] private float extendedMinimumPitch;
        [SerializeField] private float extendedMaximumPitch;

        public BistroBuilderCameraViewId Id { get { return id; } }
        public string DisplayName { get { return displayName; } }
        public BistroBuilderCameraViewFocusMode FocusMode { get { return focusMode; } }
        public BistroBuilderCameraViewYawMode YawMode { get { return yawMode; } }
        public BistroBuilderCameraViewFramingMode FramingMode { get { return framingMode; } }
        public float FixedYaw { get { return fixedYaw; } }
        public float Pitch { get { return pitch; } }
        public float FixedDistance { get { return fixedDistance; } }
        public float FramingMargin { get { return framingMargin; } }
        public float FramingContentHeight { get { return framingContentHeight; } }
        public float FocusHeight { get { return focusHeight; } }
        public bool AllowExtendedPitch { get { return allowExtendedPitch; } }
        public float ExtendedMinimumPitch { get { return extendedMinimumPitch; } }
        public float ExtendedMaximumPitch { get { return extendedMaximumPitch; } }

        public BistroBuilderCameraViewDefinition(
            BistroBuilderCameraViewId id,
            string displayName,
            BistroBuilderCameraViewFocusMode focusMode,
            BistroBuilderCameraViewYawMode yawMode,
            BistroBuilderCameraViewFramingMode framingMode,
            float fixedYaw,
            float pitch,
            float fixedDistance,
            float framingMargin,
            float framingContentHeight,
            float focusHeight,
            bool allowExtendedPitch,
            float extendedMinimumPitch,
            float extendedMaximumPitch)
        {
            this.id = id;
            this.displayName = displayName;
            this.focusMode = focusMode;
            this.yawMode = yawMode;
            this.framingMode = framingMode;
            this.fixedYaw = fixedYaw;
            this.pitch = pitch;
            this.fixedDistance = fixedDistance;
            this.framingMargin = framingMargin;
            this.framingContentHeight = framingContentHeight;
            this.focusHeight = focusHeight;
            this.allowExtendedPitch = allowExtendedPitch;
            this.extendedMinimumPitch = extendedMinimumPitch;
            this.extendedMaximumPitch = extendedMaximumPitch;
        }

        public bool IsValid(out string reason)
        {
            if (id == BistroBuilderCameraViewId.None)
            {
                reason = "La vista no puede usar la identidad None.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                reason = "La vista " + id + " no tiene nombre visible.";
                return false;
            }

            if (!BistroBuilderProfessionalCameraMath.IsFinite(fixedYaw) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(pitch) ||
                pitch <= 0.0f || pitch >= 90.0f)
            {
                reason = "La orientación de " + id + " no es válida.";
                return false;
            }

            if (framingMode == BistroBuilderCameraViewFramingMode.FixedDistance &&
                (!BistroBuilderProfessionalCameraMath.IsFinite(fixedDistance) || fixedDistance <= 0.0f))
            {
                reason = "La distancia fija de " + id + " no es válida.";
                return false;
            }

            if (framingMargin < 1.0f || framingMargin > 2.0f ||
                framingContentHeight <= 0.0f || focusHeight < 0.0f)
            {
                reason = "El volumen o margen de encuadre de " + id + " no es válido.";
                return false;
            }

            if (allowExtendedPitch &&
                (extendedMinimumPitch <= 0.0f ||
                 extendedMaximumPitch <= extendedMinimumPitch ||
                 extendedMaximumPitch >= 90.0f ||
                 pitch < extendedMinimumPitch ||
                 pitch > extendedMaximumPitch))
            {
                reason = "El rango de pitch extendido de " + id + " no es válido.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
