using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Perfil canónico de inspección y memoria contextual 369C.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BistroBuilderCameraInspectionSettings",
        menuName = "Bistro Builder/Camera/Inspection Settings")]
    public sealed class BistroBuilderCameraInspectionSettings : ScriptableObject
    {
        public const int CurrentProfileVersion = 1;

        [SerializeField] private int profileVersion;

        [Header("Encuadre de selección")]
        [SerializeField] private float framingMargin = 1.18f;
        [SerializeField] private float boundsPadding = 0.35f;
        [SerializeField] private float defaultInspectionPitch = 36.0f;
        [SerializeField] private float minimumInspectionDistance = 3.5f;
        [SerializeField] private float maximumInspectionDistance = 22.0f;
        [SerializeField] private float fallbackBoundsSize = 1.0f;

        [Header("Conjuntos relacionados")]
        [SerializeField] private bool includeRelatedSeatingByDefault = true;
        [SerializeField] private float relatedSeatSearchRadius = 2.6f;
        [SerializeField] private int maximumRelatedSeats = 8;
        [SerializeField] private bool includeInactiveGeometry;

        [Header("Inspección")]
        [SerializeField] private float rotationStep = 45.0f;
        [SerializeField] private bool trackTargetWhileInspecting = true;
        [SerializeField] private float trackingPositionEpsilon = 0.025f;
        [SerializeField] private bool restorePreviousModeOnExit = true;

        public int ProfileVersion { get { return profileVersion; } }
        public float FramingMargin { get { return framingMargin; } }
        public float BoundsPadding { get { return boundsPadding; } }
        public float DefaultInspectionPitch { get { return defaultInspectionPitch; } }
        public float MinimumInspectionDistance { get { return minimumInspectionDistance; } }
        public float MaximumInspectionDistance { get { return maximumInspectionDistance; } }
        public float FallbackBoundsSize { get { return fallbackBoundsSize; } }
        public bool IncludeRelatedSeatingByDefault { get { return includeRelatedSeatingByDefault; } }
        public float RelatedSeatSearchRadius { get { return relatedSeatSearchRadius; } }
        public int MaximumRelatedSeats { get { return maximumRelatedSeats; } }
        public bool IncludeInactiveGeometry { get { return includeInactiveGeometry; } }
        public float RotationStep { get { return rotationStep; } }
        public bool TrackTargetWhileInspecting { get { return trackTargetWhileInspecting; } }
        public float TrackingPositionEpsilon { get { return trackingPositionEpsilon; } }
        public bool RestorePreviousModeOnExit { get { return restorePreviousModeOnExit; } }

        public void ApplyCanonicalProfile()
        {
            framingMargin = 1.18f;
            boundsPadding = 0.35f;
            defaultInspectionPitch = 36.0f;
            minimumInspectionDistance = 3.5f;
            maximumInspectionDistance = 22.0f;
            fallbackBoundsSize = 1.0f;
            includeRelatedSeatingByDefault = true;
            relatedSeatSearchRadius = 2.6f;
            maximumRelatedSeats = 8;
            includeInactiveGeometry = false;
            rotationStep = 45.0f;
            trackTargetWhileInspecting = true;
            trackingPositionEpsilon = 0.025f;
            restorePreviousModeOnExit = true;
            profileVersion = CurrentProfileVersion;
        }

        public bool IsConfigurationValid(out string reason)
        {
            if (profileVersion != CurrentProfileVersion)
            {
                reason = "El perfil 369C no está actualizado.";
                return false;
            }

            if (framingMargin < 1.0f || framingMargin > 2.0f ||
                boundsPadding < 0.0f || boundsPadding > 5.0f)
            {
                reason = "El margen o padding de encuadre no es válido.";
                return false;
            }

            if (defaultInspectionPitch <= 0.0f || defaultInspectionPitch >= 89.0f ||
                minimumInspectionDistance <= 0.0f ||
                maximumInspectionDistance <= minimumInspectionDistance)
            {
                reason = "El pitch o el rango de distancia de inspección no es válido.";
                return false;
            }

            if (fallbackBoundsSize <= 0.01f || relatedSeatSearchRadius < 0.0f ||
                maximumRelatedSeats < 0 || maximumRelatedSeats > 32)
            {
                reason = "La geometría fallback o la búsqueda de asientos no es válida.";
                return false;
            }

            if (rotationStep <= 0.0f || rotationStep > 180.0f ||
                trackingPositionEpsilon < 0.0f)
            {
                reason = "El paso de giro o la tolerancia de seguimiento no es válido.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
