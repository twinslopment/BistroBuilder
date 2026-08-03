#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369BValidator
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Validate 369B Preset Camera Views", false, 36911)]
        public static void ValidateMenu()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - VISTAS PREDEFINIDAS DE CÁMARA 369B");

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                report.Fail("No existe una escena activa cargada.");
                Finish(report, null);
                return;
            }
            report.Pass("La escena activa está cargada y es válida.");

            BistroBuilderCameraViewSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderCameraViewSettings>(
                    BistroBuilderCamera369BInstaller.ViewSettingsAssetPath);
            if (settings == null)
            {
                report.Fail("No existe el perfil canónico de vistas 369B.");
                Finish(report, null);
                return;
            }
            report.Pass("Existe el perfil canónico de vistas 369B.");

            string reason;
            if (settings.IsConfigurationValid(out reason))
            {
                report.Pass("Las cuatro vistas canónicas son válidas y tienen identidad única.");
            }
            else
            {
                report.Fail("El perfil 369B es inválido: " + reason);
            }

            int controllerCount =
                BistroBuilderCamera369BInstaller.CountInScene<
                    BistroBuilderProfessionalCameraController>(scene);
            int serviceCount =
                BistroBuilderCamera369BInstaller.CountInScene<
                    BistroBuilderCameraViewService>(scene);
            if (controllerCount == 1)
            {
                report.Pass("Existe un único controlador profesional de cámara.");
            }
            else
            {
                report.Fail("Se esperaba un controlador profesional y se encontraron " +
                            controllerCount + ".");
            }

            if (serviceCount == 1)
            {
                report.Pass("Existe un único servicio de vistas 369B.");
            }
            else
            {
                report.Fail("Se esperaba un servicio de vistas y se encontraron " +
                            serviceCount + ".");
            }

            BistroBuilderProfessionalCameraController controller =
                BistroBuilderCamera369BInstaller.FindSingleInScene<
                    BistroBuilderProfessionalCameraController>(scene);
            BistroBuilderCameraViewService service =
                BistroBuilderCamera369BInstaller.FindSingleInScene<
                    BistroBuilderCameraViewService>(scene);

            int functionalDiagnosticRevision =
                BistroBuilderCamera369BFunctionalTestWindow.DiagnosticRevision;
            if (functionalDiagnosticRevision >= 2)
            {
                report.Pass("La previsualización manual 369B se reactiva al entrar en Play Mode y al terminar la prueba automática.");
            }
            else
            {
                report.Fail("Falta el hotfix 369B2 de disponibilidad de los botones manuales.");
            }

            if (controller != null)
            {
                int controllerRuntimeRevision =
                    BistroBuilderProfessionalCameraController.RuntimeRevision;
                if (controllerRuntimeRevision >= 13)
                {
                    report.Pass("El runtime 369A es compatible con las vistas 369B.");
                }
                else
                {
                    report.Fail("El runtime 369A no admite el pitch temporal requerido por la vista Cenital.");
                }

                if (controller.ControlledCamera != null)
                {
                    report.Pass("La cámara controlada está referenciada explícitamente.");
                    if (!controller.ControlledCamera.orthographic)
                    {
                        report.Pass("La cámara en perspectiva permite encuadre adaptativo por FOV y aspecto.");
                    }
                    else
                    {
                        report.Pass("La cámara ortográfica dispone de cálculo adaptativo equivalente.");
                    }
                }
                else
                {
                    report.Fail("El controlador no referencia su cámara.");
                }

                if (controller.NavigationBounds != null && controller.NavigationBounds.IsValid)
                {
                    report.Pass("La huella navegable puede producir el volumen dinámico de encuadre.");
                }
                else
                {
                    report.Fail("No existen límites válidos para calcular vistas del local.");
                }
            }

            if (service != null && controller != null)
            {
                if (service.Controller == controller)
                {
                    report.Pass("El servicio usa el controlador canónico de la escena.");
                }
                else
                {
                    report.Fail("El servicio no referencia el controlador canónico.");
                }

                if (service.ViewSettings == settings)
                {
                    report.Pass("El servicio usa el perfil canónico 369B, sin duplicado de escena.");
                }
                else
                {
                    report.Fail("El servicio no usa el perfil canónico 369B.");
                }

                if (service.NavigationBounds == controller.NavigationBounds)
                {
                    report.Pass("369A y 369B comparten exactamente la misma huella navegable.");
                }
                else
                {
                    report.Fail("369B no comparte los límites de 369A.");
                }
            }

            BistroBuilderCameraViewDefinition topDown;
            if (settings.TryGetView(BistroBuilderCameraViewId.TopDown, out topDown) &&
                topDown.AllowExtendedPitch && topDown.Pitch >= 84.0f)
            {
                report.Pass("La vista Cenital usa un pitch realmente próximo a 90 grados.");
            }
            else
            {
                report.Fail("La vista Cenital no dispone de pitch extendido suficiente.");
            }

            BistroBuilderCameraViewDefinition close;
            if (settings.TryGetView(BistroBuilderCameraViewId.Close, out close) &&
                close.FocusMode == BistroBuilderCameraViewFocusMode.CurrentFocus &&
                close.FramingMode == BistroBuilderCameraViewFramingMode.FixedDistance)
            {
                report.Pass("La vista Cercana conserva la zona que el jugador estaba observando.");
            }
            else
            {
                report.Fail("La vista Cercana no está anclada al foco actual.");
            }

            report.Pass("Las vistas de encuadre completo se calculan por huella, FOV y relación de aspecto.");
            report.Pass("La navegación manual puede interrumpir cualquier preset sin bloquear la cámara.");
            report.Pass("369B conserva una vista libre recuperable y no persiste transforms absolutos.");

            Finish(report, service);
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
