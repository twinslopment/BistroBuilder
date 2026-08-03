using System;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Contextos de cámara independientes. Cada contexto puede recordar su último encuadre sin
    /// convertir la cámara en un conjunto de modos rígidos.
    /// </summary>
    public enum BistroBuilderCameraContextMode
    {
        Service = 0,
        Edit = 1,
        Inspection = 2
    }

    [Serializable]
    public struct BistroBuilderCameraContextMemorySlot
    {
        [SerializeField] private bool hasState;
        [SerializeField] private BistroBuilderCameraNavigationState state;

        public bool HasState { get { return hasState && state.IsFinite; } }
        public BistroBuilderCameraNavigationState State { get { return state; } }

        public void Set(BistroBuilderCameraNavigationState value)
        {
            hasState = value.IsFinite;
            state = value;
        }

        public void Clear()
        {
            hasState = false;
            state = default;
        }
    }

    /// <summary>
    /// Snapshot neutral preparado para persistencia futura. 369C no se registra todavía como
    /// proveedor del guardado 366: expone captura/restauración para integrarlo sin acoplamientos.
    /// </summary>
    [Serializable]
    public sealed class BistroBuilderCameraContextSnapshot
    {
        public const int CurrentVersion = 1;

        [SerializeField] private int version = CurrentVersion;
        [SerializeField] private BistroBuilderCameraContextMode currentMode;
        [SerializeField] private BistroBuilderCameraContextMemorySlot service;
        [SerializeField] private BistroBuilderCameraContextMemorySlot edit;
        [SerializeField] private BistroBuilderCameraContextMemorySlot inspection;

        public int Version { get { return version; } }
        public BistroBuilderCameraContextMode CurrentMode { get { return currentMode; } }
        public BistroBuilderCameraContextMemorySlot Service { get { return service; } }
        public BistroBuilderCameraContextMemorySlot Edit { get { return edit; } }
        public BistroBuilderCameraContextMemorySlot Inspection { get { return inspection; } }

        public BistroBuilderCameraContextSnapshot(
            BistroBuilderCameraContextMode currentMode,
            BistroBuilderCameraContextMemorySlot service,
            BistroBuilderCameraContextMemorySlot edit,
            BistroBuilderCameraContextMemorySlot inspection)
        {
            version = CurrentVersion;
            this.currentMode = currentMode;
            this.service = service;
            this.edit = edit;
            this.inspection = inspection;
        }

        public bool IsCompatible
        {
            get { return version == CurrentVersion; }
        }
    }
}
