using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    public enum BistroBuilderCameraInspectableKind
    {
        Auto = 0,
        Generic = 1,
        Table = 2,
        Bar = 3,
        Equipment = 4
    }

    /// <summary>
    /// Metadatos opcionales para objetos que necesitan un encuadre específico. Si no existe este
    /// componente, 369C calcula un encuadre genérico a partir de renderers y colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BistroBuilderCameraInspectable : MonoBehaviour
    {
        [SerializeField] private BistroBuilderCameraInspectableKind kind =
            BistroBuilderCameraInspectableKind.Auto;
        [SerializeField] private bool includeChildren = true;
        [SerializeField] private bool includeRelatedSeating = true;
        [SerializeField] private bool trackWhileInspecting = true;
        [SerializeField] private Vector3 focusOffset;
        [SerializeField] private float framingMarginOverride;
        [SerializeField] private float pitchOverride = -1.0f;
        [SerializeField] private float rotationStepOverride;
        [SerializeField] private List<Transform> relatedRoots = new List<Transform>();

        public BistroBuilderCameraInspectableKind Kind { get { return kind; } }
        public bool IncludeChildren { get { return includeChildren; } }
        public bool IncludeRelatedSeating { get { return includeRelatedSeating; } }
        public bool TrackWhileInspecting { get { return trackWhileInspecting; } }
        public Vector3 FocusOffset { get { return focusOffset; } }
        public float FramingMarginOverride { get { return framingMarginOverride; } }
        public float PitchOverride { get { return pitchOverride; } }
        public float RotationStepOverride { get { return rotationStepOverride; } }
        public IReadOnlyList<Transform> RelatedRoots { get { return relatedRoots; } }
    }
}
