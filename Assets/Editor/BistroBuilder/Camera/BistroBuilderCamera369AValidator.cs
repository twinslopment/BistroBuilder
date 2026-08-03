#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369AValidator
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Validate 369A Professional Camera Navigation", false, 36901)]
        public static void ValidateMenu()
        {
            BistroBuilderCamera369AReport report = Validate();
            report.Log();
            report.ShowDialog();
        }

        internal static BistroBuilderCamera369AReport Validate()
        {
            BistroBuilderCamera369AReport report = new BistroBuilderCamera369AReport(
                "BISTRO BUILDER - CÁMARA PROFESIONAL Y NAVEGACIÓN 369A");

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                report.Pass("La escena activa está cargada y es válida.");
            }
            else
            {
                report.Fail("No existe una escena activa válida.");
                return report;
            }

            BistroBuilderCameraNavigationSettings settings = BistroBuilderCamera369AInstaller.LoadSettings();
            if (settings != null)
            {
                report.Pass("Existe la configuración canónica de cámara 369A.");
            }
            else
            {
                report.Fail("Falta la configuración canónica de cámara 369A.");
            }

            string reason = string.Empty;
            if (settings != null && settings.IsConfigurationValid(out reason))
            {
                report.Pass("Los límites, velocidades y tiempos de amortiguación son coherentes.");
            }
            else
            {
                report.Fail("La configuración es inválida: " + (settings != null ? reason : "asset ausente"));
            }

            if (settings != null &&
                settings.InteractionProfileVersion >=
                BistroBuilderCameraNavigationSettings.CurrentInteractionProfileVersion)
            {
                report.Pass("El perfil de interacción 369A12 está aplicado.");
            }
            else
            {
                report.Fail("Falta aplicar el perfil de interacción 369A12.");
            }

            int runtimeRevision = BistroBuilderProfessionalCameraController.RuntimeRevision;
            int diagnosticRevision = BistroBuilderCamera369AFunctionalTestWindow.DiagnosticRevision;
            if (runtimeRevision >= 12 && diagnosticRevision >= 12)
            {
                report.Pass("El runtime acumulativo 369A12 añade una banda R/F contextual con recorrido útil en ambas direcciones.");
            }
            else
            {
                report.Fail("Falta el runtime o diagnóstico acumulativo 369A12.");
            }

            if (settings != null &&
                settings.KeyboardElevationEnabled &&
                settings.KeyboardElevationSpeed > 0.0f)
            {
                report.Pass("R/F trasladan verticalmente la pose completa con pitch y distancia constantes.");
            }
            else
            {
                report.Fail("La elevación vertical mediante R/F no está habilitada o carece de velocidad.");
            }

            if (settings != null &&
                settings.ElevationAccelerationTime > 0.0f &&
                settings.ElevationDecelerationTime >= settings.ElevationAccelerationTime)
            {
                report.Pass("La elevación vertical dispone de arranque suave y frenada cinematográfica.");
            }
            else
            {
                report.Fail("La amortiguación vertical no garantiza una frenada progresiva.");
            }

            if (settings != null &&
                settings.MinimumElevatorHeight >= settings.MinimumCameraHeight &&
                settings.MaximumElevatorHeight <= settings.MaximumCameraHeight &&
                settings.MaximumElevatorHeight > settings.MinimumElevatorHeight &&
                settings.MinimumElevatorHeight <= 2.0f &&
                settings.MaximumElevatorHeight >= 12.0f &&
                settings.MaximumElevatorHeight <= 14.5f &&
                settings.ElevatorUpwardTravel >= 3.0f &&
                settings.ElevatorUpwardTravel <= 4.5f &&
                settings.ElevatorDownwardTravel >= 5.0f &&
                settings.ElevatorDownwardTravel <= 7.0f &&
                settings.ElevatorSoftLimitRange > 0.0f)
            {
                report.Pass("R/F reservan recorrido contextual útil por encima y por debajo de la vista actual, sin permitir acumulación indefinida.");
            }
            else
            {
                report.Fail("La banda contextual o la frenada suave de R/F no están configuradas correctamente.");
            }

            if (settings != null && settings.MousePitchEnabled && settings.OrbitAroundPointer &&
                settings.MinimumPitch <= 22.5f && settings.MouseYawDegreesPerPixel <= 0.13f)
            {
                report.Pass("El botón derecho orbita con sensibilidad contenida alrededor del punto elegido bajo el cursor.");
            }
            else
            {
                report.Fail("La órbita contextual del botón derecho no está configurada correctamente.");
            }

            if (settings != null && settings.KeyboardOrbitAroundPointer)
            {
                report.Pass("Q/E capturan un pivote contextual bajo el cursor y no fuerzan el centro geométrico del plano.");
            }
            else
            {
                report.Fail("Q/E no tienen habilitado el pivote contextual exigido por 369A12.");
            }

            if (settings != null &&
                settings.ZoomAroundPointer &&
                settings.ZoomSpeedNear > 0.0f &&
                settings.ZoomSpeedFar >= settings.ZoomSpeedNear &&
                Mathf.Abs(settings.ZoomSpeedNear - settings.PanSpeedNear) <= 0.01f &&
                Mathf.Abs(settings.ZoomSpeedFar - settings.PanSpeedFar) <= 0.01f &&
                Mathf.Abs(settings.ZoomAccelerationTime - settings.PanAccelerationTime) <= 0.01f &&
                Mathf.Abs(settings.ZoomDecelerationTime - settings.PanDecelerationTime) <= 0.01f &&
                Mathf.Abs(settings.ZoomDampingTime - settings.PositionDampingTime) <= 0.01f &&
                settings.ZoomIntentDurationPerNotch > 0.0f &&
                settings.MaximumZoomIntentDuration >= settings.ZoomIntentDurationPerNotch &&
                settings.MaximumScrollNotchesPerFrame <= 1.1f &&
                settings.MinimumOperationalDistance >= settings.MinimumDistance &&
                settings.MinimumOperationalDistance <= 6.0f &&
                settings.MaximumOperationalDistance <= settings.MaximumDistance &&
                settings.MaximumOperationalDistance > settings.MinimumOperationalDistance)
            {
                report.Pass("La rueda usa el perfil cinemático de WASD y conserva el punto interior bajo el cursor.");
            }
            else
            {
                report.Fail("El zoom no comparte todavía el perfil cinemático de desplazamiento exigido por 369A12.");
            }

            if (settings != null &&
                settings.MiddleMouseDragSensitivity > 0.0f &&
                settings.MiddleMouseDragDeadZonePixels >= 0.0f &&
                settings.MiddleMouseDragDeadZonePixels <= 1.0f)
            {
                report.Pass("El arrastre central dispone de escala estable y zona muerta anti-vibración.");
            }
            else
            {
                report.Fail("La estabilización del arrastre central no está configurada correctamente.");
            }

            UnityEngine.Camera camera = BistroBuilderCamera369AInstaller.FindBestSceneCamera(scene);
            if (camera != null)
            {
                report.Pass("Existe una cámara operativa en la escena.");
            }
            else
            {
                report.Fail("No existe ninguna cámara operativa en la escena.");
            }

            if (camera != null && camera.CompareTag("MainCamera"))
            {
                report.Pass("La cámara operativa conserva la etiqueta MainCamera.");
            }
            else
            {
                report.Warn("La cámara operativa no está identificada como MainCamera.");
            }

            BistroBuilderProfessionalCameraController[] controllers =
                BistroBuilderCamera369AInstaller.FindSceneControllers(scene);
            if (controllers.Length == 1)
            {
                report.Pass("Existe un único controlador profesional 369A en la escena.");
            }
            else if (controllers.Length == 0)
            {
                report.Fail("No existe el controlador profesional 369A en la escena.");
            }
            else
            {
                report.Fail("Existen " + controllers.Length + " controladores 369A; debe haber uno solo.");
            }

            BistroBuilderProfessionalCameraController controller =
                controllers.Length > 0 ? controllers[0] : null;

            if (controller != null && controller.ControlledCamera != null)
            {
                report.Pass("El controlador referencia explícitamente su cámara.");
            }
            else
            {
                report.Fail("El controlador no tiene una cámara asignada.");
            }

            if (controller != null && controller.ControlledCamera == camera)
            {
                report.Pass("La cámara principal y la cámara controlada son la misma instancia.");
            }
            else
            {
                report.Fail("El controlador no gobierna la cámara principal detectada.");
            }

            if (controller != null && controller.Settings == settings && settings != null)
            {
                report.Pass("El controlador usa la configuración canónica, sin duplicados de escena.");
            }
            else
            {
                report.Fail("El controlador no usa la configuración canónica de 369A.");
            }

            BistroBuilderCameraBounds bounds =
                controller != null ? controller.NavigationBounds :
                BistroBuilderCamera369AInstaller.FindSceneBounds(scene);
            if (bounds != null)
            {
                report.Pass("Existe una zona navegable explícita para la cámara.");
            }
            else
            {
                report.Fail("Falta la zona navegable de cámara.");
            }

            if (bounds != null && bounds.IsValid)
            {
                report.Pass("La zona navegable tiene dimensiones horizontales válidas.");
            }
            else
            {
                report.Fail("La zona navegable tiene dimensiones inválidas.");
            }

            if (bounds != null &&
                bounds.ConstraintMode == BistroBuilderCameraBoundsConstraintMode.FocusPointOnly &&
                bounds.CameraEnvelopePadding <= 0.001f)
            {
                report.Pass("Los límites actúan solo sobre el punto observado; la cámara física puede encuadrar desde fuera de la huella.");
            }
            else
            {
                report.Fail("La navegación normal sigue imponiendo una envolvente física a la cámara.");
            }

            if (BistroBuilderProfessionalCameraInput.HasSupportedBackend)
            {
                report.Pass("La entrada es compatible con " +
                            BistroBuilderProfessionalCameraInput.ActiveBackend + ".");
            }
            else
            {
                report.Fail("No existe un backend de entrada compatible habilitado.");
            }

            if (settings != null && settings.UseUnscaledTime)
            {
                report.Pass("La cámara seguirá respondiendo con el juego pausado o a velocidad reducida.");
            }
            else
            {
                report.Warn("La cámara está configurada con tiempo escalado; podría detenerse en pausa.");
            }

            if (settings != null && settings.BlockPointerInputOverUi)
            {
                report.Pass("La entrada de puntero se bloquea sobre la interfaz de usuario.");
            }
            else
            {
                report.Warn("La cámara puede reaccionar al puntero situado sobre la interfaz.");
            }

            if (settings != null && settings.BlockKeyboardWhileTyping)
            {
                report.Pass("WASD, Q/E y R/F no moverán la cámara mientras el usuario escribe.");
            }
            else
            {
                report.Warn("El teclado puede mover la cámara mientras un campo de texto está activo.");
            }

            if (settings != null &&
                settings.MinimumDistance > 0.0f &&
                settings.MaximumDistance > settings.MinimumDistance &&
                settings.MinimumCameraHeight > 0.0f &&
                settings.MaximumCameraHeight > settings.MinimumCameraHeight &&
                settings.MaximumPitch < 90.0f)
            {
                report.Pass("Los límites independientes de zoom, altura e inclinación son válidos.");
            }
            else
            {
                report.Fail("Los límites de distancia o inclinación permiten un estado degenerado.");
            }

            if (camera != null && camera.nearClipPlane > 0.0f &&
                camera.farClipPlane > camera.nearClipPlane)
            {
                report.Pass("Los planos de recorte de cámara son válidos.");
            }
            else
            {
                report.Fail("Los planos de recorte de cámara son inválidos.");
            }

            if (controller != null && controller.enabled && controller.gameObject.activeInHierarchy)
            {
                report.Pass("El controlador está activo y preparado para Play Mode.");
            }
            else
            {
                report.Warn("El controlador no está activo en la jerarquía actual.");
            }

            return report;
        }
    }
}
#endif
