using System;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Estado neutral de navegación preparado para vistas 369B, inspección 369C y futura persistencia.
    /// 369A todavía no lo registra como proveedor de guardado para no acoplar la cámara al sistema 366.
    /// </summary>
    [Serializable]
    public struct BistroBuilderCameraNavigationState
    {
        public const int CurrentVersion = 1;

        [SerializeField] private int version;
        [SerializeField] private Vector3 focusPoint;
        [SerializeField] private float yaw;
        [SerializeField] private float pitch;
        [SerializeField] private float distance;

        public int Version { get { return version; } }
        public Vector3 FocusPoint { get { return focusPoint; } }
        public float Yaw { get { return yaw; } }
        public float Pitch { get { return pitch; } }
        public float Distance { get { return distance; } }

        public BistroBuilderCameraNavigationState(
            Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance)
        {
            version = CurrentVersion;
            this.focusPoint = focusPoint;
            this.yaw = yaw;
            this.pitch = pitch;
            this.distance = distance;
        }

        public bool IsFinite
        {
            get
            {
                return BistroBuilderProfessionalCameraMath.IsFinite(focusPoint) &&
                       BistroBuilderProfessionalCameraMath.IsFinite(yaw) &&
                       BistroBuilderProfessionalCameraMath.IsFinite(pitch) &&
                       BistroBuilderProfessionalCameraMath.IsFinite(distance);
            }
        }
    }
}
