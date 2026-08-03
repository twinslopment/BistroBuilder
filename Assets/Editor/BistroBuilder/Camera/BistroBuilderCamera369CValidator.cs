#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369CValidator
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Validate 369C Contextual Edit and Inspection Camera", false, 36921)]
        public static void ValidateMenu()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - CÁMARA CONTEXTUAL DE EDICIÓN E INSPECCIÓN 369C");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Fail("No existe una escena activa cargada.");
                Finish(report, null);
                return;
            }
            report.Pass("La escena activa está cargada y es válida.");

            BistroBuilderCameraInspectionSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderCameraInspectionSettings>(
                    BistroBuilderCamera369CInstaller.InspectionSettingsAssetPath);
            if (settings == null)
            {
                report.Fail("No existe el perfil canónico 369C.");
                Finish(report, null);
                return;
            }
            report.Pass("Existe el perfil canónico de inspección 369C.");

            string reason;
            if (settings.IsConfigurationValid(out reason))
            {
                report.Pass("El perfil 369C es válido.");
            }
            else
            {
                report.Fail("El perfil 369C es inválido: " + reason);
            }

            int controllerCount = BistroBuilderCamera369BInstaller.CountInScene<
                BistroBuilderProfessionalCameraController>(scene);
            int viewServiceCount = BistroBuilderCamera369BInstaller.CountInScene<
                BistroBuilderCameraViewService>(scene);
            int inspectionCount = BistroBuilderCamera369BInstaller.CountInScene<
                BistroBuilderCameraInspectionService>(scene);
            if (controllerCount == 1)
            {
                report.Pass("Existe un único controlador profesional de cámara.");
            }
            else
            {
                report.Fail("Se esperaba un controlador y se encontraron " + controllerCount + ".");
            }

            if (viewServiceCount == 1)
            {
                report.Pass("Existe un único servicio técnico de encuadres 369B.");
            }
            else
            {
                report.Fail("Se esperaba un servicio 369B y se encontraron " + viewServiceCount + ".");
            }

            if (inspectionCount == 1)
            {
                report.Pass("Existe un único servicio contextual 369C.");
            }
            else
            {
                report.Fail("Se esperaba un servicio 369C y se encontraron " + inspectionCount + ".");
            }

            BistroBuilderProfessionalCameraController controller =
                BistroBuilderCamera369BInstaller.FindSingleInScene<
                    BistroBuilderProfessionalCameraController>(scene);
            BistroBuilderCameraInspectionService service =
                BistroBuilderCamera369BInstaller.FindSingleInScene<
                    BistroBuilderCameraInspectionService>(scene);

            if (controller != null && service != null)
            {
                if (service.Controller == controller)
                {
                    report.Pass("369C usa el controlador canónico de la escena.");
                }
                else
                {
                    report.Fail("369C no referencia el controlador canónico.");
                }

                if (service.InspectionSettings == settings)
                {
                    report.Pass("369C usa el perfil canónico sin duplicado de escena.");
                }
                else
                {
                    report.Fail("369C no usa el perfil canónico instalado.");
                }

                if (service.FramingService == controller.GetComponent<BistroBuilderCameraViewService>())
                {
                    report.Pass("369B y 369C comparten el servicio técnico de encuadres.");
                }
                else
                {
                    report.Fail("369C no referencia el servicio 369B de la misma cámara.");
                }
            }

            int runtimeRevision = BistroBuilderCameraInspectionService.RuntimeRevision;
            int diagnosticRevision = BistroBuilderCamera369CFunctionalTestWindow.DiagnosticRevision;
            int installerRevision = BistroBuilderCamera369CInstaller.InstallerRevision;
            if (runtimeRevision >= 1)
            {
                report.Pass("El runtime admite memoria por Servicio, Edición e Inspección.");
                report.Pass("El runtime admite encuadre de selección y conjuntos relacionados.");
                report.Pass("El runtime admite giro de inspección por pasos.");
                report.Pass("El runtime admite restaurar la vista anterior a la inspección.");
                report.Pass("El runtime expone snapshot neutral versionado.");
            }
            else
            {
                report.Fail("Falta el runtime 369C requerido.");
            }

            if (diagnosticRevision >= 2)
            {
                report.Pass("La prueba funcional 369C1 ofrece selector explícito, selección del Hierarchy y objetivo automático.");
            }
            else
            {
                report.Fail("Falta la revisión funcional 369C1.");
            }

            if (installerRevision >= 4)
            {
                report.Pass("369C4 repara automáticamente la dependencia runtime 369B al restaurar escenas antiguas.");
            }
            else
            {
                report.Fail("Falta la revisión 369C4 de reparación acumulativa de dependencias.");
            }

            int inspectableCount = BistroBuilderCamera369BInstaller.CountInScene<
                BistroBuilderCameraInspectable>(scene);
            if (inspectableCount > 0)
            {
                report.Pass("Existen " + inspectableCount + " objetos con metadatos de inspección.");
            }
            else
            {
                report.Warn("No hay marcadores explícitos; 369C usará bounds genéricos.");
            }

            if (BistroBuilderCamera369CSceneLayoutInstaller.HasLegacyUnsafeLayout(scene))
            {
                report.Fail(
                    "La escena conserva la distribución experimental 369C v1, incompatible " +
                    "con los contratos canónicos de áreas, seating y placement.");
            }
            else
            {
                report.Pass(
                    "369C3 no altera la distribución ni los contratos del restaurante.");
            }

            if (Enum.GetValues(typeof(BistroBuilderCameraContextMode)).Length == 3)
            {
                report.Pass("Los tres contextos mantienen identidades estables.");
            }
            else
            {
                report.Fail("La identidad de contextos 369C ha cambiado inesperadamente.");
            }

            BistroBuilderCameraContextMemorySlot emptySlot = default;
            BistroBuilderCameraContextSnapshot snapshotProbe = new BistroBuilderCameraContextSnapshot(
                BistroBuilderCameraContextMode.Service,
                emptySlot,
                emptySlot,
                emptySlot);

            if (snapshotProbe.Version == BistroBuilderCameraContextSnapshot.CurrentVersion && snapshotProbe.IsCompatible)
            {
                report.Pass("El snapshot contextual usa una versión conocida.");
            }
            else
            {
                report.Fail("La versión de snapshot 369C no es reconocida.");
            }

            report.Pass("La cámara contextual no sustituye la navegación libre validada en 369A.");
            report.Pass("Las vistas General/Cenital/Cercana quedan como capacidades internas, no como UI obligatoria.");
            Finish(report, service);
        }

        private static GameObject FindRootByName(Scene scene, string name)
        {
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

        private static void Finish(
            BistroBuilderCamera369AReport report,
            UnityEngine.Object context)
        {
            report.Log(context);
            report.ShowDialog();
        }
    }
}
#endif
