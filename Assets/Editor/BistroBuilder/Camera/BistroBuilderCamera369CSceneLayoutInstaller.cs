#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    /// <summary>
    /// Guardia de seguridad de escena introducida en 369C3.
    ///
    /// La primera distribución experimental 369C movía objetos visualmente sin pasar por los
    /// contratos canónicos de áreas, seating, grupos enlazados y validación de placement. Esa
    /// estrategia queda retirada. La cámara contextual debe ser completamente no destructiva.
    ///
    /// La futura preparación de comedores se hará desde el modo edición real, usando sus servicios
    /// de validación y confirmación, nunca desde el instalador de cámara.
    /// </summary>
    public static class BistroBuilderCamera369CSceneLayoutInstaller
    {
        public const int LayoutRevision = 2;
        public const string LegacyUnsafeLayoutMarkerName =
            "BB_369C_ProfessionalDiningLayout_v1";

        // Se conserva como alias para no romper referencias editoriales antiguas, pero identifica
        // únicamente el marcador experimental que debe eliminarse restaurando la escena válida.
        public const string LayoutMarkerName = LegacyUnsafeLayoutMarkerName;

        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Audit 369C Scene Layout Safety", false, 36924)]
        public static void AuditLayoutSafetyMenu()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - SEGURIDAD DE ESCENA 369C3");

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                report.Fail("La auditoría de escena debe ejecutarse fuera de Play Mode.");
                report.Log();
                report.ShowDialog();
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Fail("No existe una escena activa cargada.");
            }
            else if (HasLegacyUnsafeLayout(scene))
            {
                report.Fail(
                    "Se detectó el marcador de la distribución experimental 369C v1. " +
                    "Restaura el archivo de escena desde el último commit válido. No intentes " +
                    "reparar áreas o seating moviendo objetos manualmente.");
            }
            else
            {
                report.Pass("No existe la distribución experimental 369C v1 en la escena.");
                report.Pass("La instalación 369C3 es no destructiva para mesas, sillas y barra.");
            }

            report.Log();
            report.ShowDialog();
        }

        internal static bool HasLegacyUnsafeLayout(Scene scene)
        {
            return FindRootByName(scene, LegacyUnsafeLayoutMarkerName) != null;
        }

        [Obsolete(
            "La redistribución automática 369C fue retirada. Use el modo edición canónico.",
            false)]
        internal static bool ApplyLayoutInternal(BistroBuilderCamera369AReport report)
        {
            if (report != null)
            {
                report.Fail(
                    "La redistribución automática 369C está retirada por seguridad. " +
                    "Use el modo edición canónico para colocar mobiliario.");
            }
            return false;
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (string.Equals(roots[index].name, name, StringComparison.Ordinal))
                {
                    return roots[index];
                }
            }
            return null;
        }
    }
}
#endif
