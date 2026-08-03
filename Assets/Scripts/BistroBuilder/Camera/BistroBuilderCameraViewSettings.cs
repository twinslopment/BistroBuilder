using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Perfil canónico 369B. Las vistas se definen semánticamente para adaptarse a locales de
    /// distinto tamaño; no almacenan transforms absolutos ni dependen de Prototype_Restaurant.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BistroBuilderCameraViewSettings",
        menuName = "Bistro Builder/Camera/369B Preset View Settings")]
    public sealed class BistroBuilderCameraViewSettings : ScriptableObject
    {
        public const int CurrentProfileVersion = 1;

        [SerializeField] private int profileVersion;
        [SerializeField] private BistroBuilderCameraViewDefinition generalView;
        [SerializeField] private BistroBuilderCameraViewDefinition isometricView;
        [SerializeField] private BistroBuilderCameraViewDefinition topDownView;
        [SerializeField] private BistroBuilderCameraViewDefinition closeView;

        public int ProfileVersion { get { return profileVersion; } }
        public BistroBuilderCameraViewDefinition GeneralView { get { return generalView; } }
        public BistroBuilderCameraViewDefinition IsometricView { get { return isometricView; } }
        public BistroBuilderCameraViewDefinition TopDownView { get { return topDownView; } }
        public BistroBuilderCameraViewDefinition CloseView { get { return closeView; } }

        public void ApplyCanonicalProfile()
        {
            generalView = new BistroBuilderCameraViewDefinition(
                BistroBuilderCameraViewId.General,
                "General",
                BistroBuilderCameraViewFocusMode.BoundsCenter,
                BistroBuilderCameraViewYawMode.PreserveCurrent,
                BistroBuilderCameraViewFramingMode.FitRestaurantBounds,
                45.0f,
                50.0f,
                18.0f,
                1.14f,
                4.5f,
                2.25f,
                false,
                0.0f,
                0.0f);

            isometricView = new BistroBuilderCameraViewDefinition(
                BistroBuilderCameraViewId.Isometric,
                "Isométrica",
                BistroBuilderCameraViewFocusMode.BoundsCenter,
                BistroBuilderCameraViewYawMode.Fixed,
                BistroBuilderCameraViewFramingMode.FitRestaurantBounds,
                45.0f,
                48.0f,
                18.0f,
                1.08f,
                4.5f,
                2.25f,
                false,
                0.0f,
                0.0f);

            topDownView = new BistroBuilderCameraViewDefinition(
                BistroBuilderCameraViewId.TopDown,
                "Cenital",
                BistroBuilderCameraViewFocusMode.BoundsCenter,
                BistroBuilderCameraViewYawMode.Fixed,
                BistroBuilderCameraViewFramingMode.FitRestaurantBounds,
                0.0f,
                88.0f,
                18.0f,
                1.06f,
                4.5f,
                2.25f,
                true,
                84.0f,
                89.0f);

            closeView = new BistroBuilderCameraViewDefinition(
                BistroBuilderCameraViewId.Close,
                "Cercana",
                BistroBuilderCameraViewFocusMode.CurrentFocus,
                BistroBuilderCameraViewYawMode.PreserveCurrent,
                BistroBuilderCameraViewFramingMode.FixedDistance,
                45.0f,
                34.0f,
                7.25f,
                1.05f,
                4.5f,
                1.4f,
                false,
                0.0f,
                0.0f);

            profileVersion = CurrentProfileVersion;
        }

        public bool TryGetView(
            BistroBuilderCameraViewId id,
            out BistroBuilderCameraViewDefinition definition)
        {
            switch (id)
            {
                case BistroBuilderCameraViewId.General:
                    definition = generalView;
                    return definition != null;
                case BistroBuilderCameraViewId.Isometric:
                    definition = isometricView;
                    return definition != null;
                case BistroBuilderCameraViewId.TopDown:
                    definition = topDownView;
                    return definition != null;
                case BistroBuilderCameraViewId.Close:
                    definition = closeView;
                    return definition != null;
                default:
                    definition = null;
                    return false;
            }
        }

        public bool IsConfigurationValid(out string reason)
        {
            if (profileVersion != CurrentProfileVersion)
            {
                reason = "El perfil de vistas 369B no está actualizado.";
                return false;
            }

            BistroBuilderCameraViewDefinition[] definitions =
            {
                generalView,
                isometricView,
                topDownView,
                closeView
            };
            HashSet<BistroBuilderCameraViewId> identities =
                new HashSet<BistroBuilderCameraViewId>();

            for (int index = 0; index < definitions.Length; index++)
            {
                BistroBuilderCameraViewDefinition definition = definitions[index];
                if (definition == null)
                {
                    reason = "Falta una de las cuatro vistas canónicas 369B.";
                    return false;
                }

                string definitionReason;
                if (!definition.IsValid(out definitionReason))
                {
                    reason = definitionReason;
                    return false;
                }

                if (!identities.Add(definition.Id))
                {
                    reason = "La identidad " + definition.Id + " está duplicada.";
                    return false;
                }
            }

            if (!identities.Contains(BistroBuilderCameraViewId.General) ||
                !identities.Contains(BistroBuilderCameraViewId.Isometric) ||
                !identities.Contains(BistroBuilderCameraViewId.TopDown) ||
                !identities.Contains(BistroBuilderCameraViewId.Close))
            {
                reason = "El perfil no contiene General, Isométrica, Cenital y Cercana.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }
}
