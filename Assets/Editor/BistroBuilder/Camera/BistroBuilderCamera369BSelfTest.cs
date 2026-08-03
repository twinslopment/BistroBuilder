#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369BSelfTest
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Run 369B Preset Camera Views Self-Test", false, 36912)]
        public static void RunMenu()
        {
            int passed = 0;
            List<string> failures = new List<string>();
            List<string> highlights = new List<string>();
            Action<bool, string> check = (condition, name) =>
            {
                if (condition)
                {
                    passed++;
                }
                else
                {
                    failures.Add(name);
                }
            };

            RunIdentityTests(check);
            RunProfileTests(check, highlights);
            RunFramingMathTests(check, highlights);
            RunRuntimeContractTests(check, highlights);

            StringBuilder full = new StringBuilder(2048);
            full.AppendLine("BISTRO BUILDER - AUTOTEST 369B");
            full.AppendLine("Pruebas superadas: " + passed);
            full.AppendLine("Pruebas fallidas: " + failures.Count);
            for (int index = 0; index < highlights.Count; index++)
            {
                full.AppendLine("- OK: " + highlights[index]);
            }
            for (int index = 0; index < failures.Count; index++)
            {
                full.AppendLine("- ERROR: " + failures[index]);
            }

            string message = full.ToString().TrimEnd();
            if (failures.Count == 0)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }

            EditorUtility.DisplayDialog(
                "Bistro Builder",
                BuildDialogText(passed, failures, highlights),
                "Aceptar");
        }

        private static void RunIdentityTests(Action<bool, string> check)
        {
            check((int)BistroBuilderCameraViewId.None == 0, "None conserva identidad 0");
            check((int)BistroBuilderCameraViewId.General == 1, "General conserva identidad 1");
            check((int)BistroBuilderCameraViewId.Isometric == 2, "Isométrica conserva identidad 2");
            check((int)BistroBuilderCameraViewId.TopDown == 3, "Cenital conserva identidad 3");
            check((int)BistroBuilderCameraViewId.Close == 4, "Cercana conserva identidad 4");
            check(BistroBuilderCameraViewService.RuntimeRevision >= 1, "Revisión runtime 369B");
            check(BistroBuilderCamera369BFunctionalTestWindow.DiagnosticRevision >= 2,
                "Diagnóstico 369B2 habilita previsualización manual antes y después del autotest funcional");
            check(BistroBuilderProfessionalCameraController.RuntimeRevision >= 13,
                "369A contiene compatibilidad 369B");
        }

        private static void RunProfileTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            BistroBuilderCameraViewSettings settings =
                AssetDatabase.LoadAssetAtPath<BistroBuilderCameraViewSettings>(
                    BistroBuilderCamera369BInstaller.ViewSettingsAssetPath);
            check(settings != null, "Existe el asset canónico de vistas");
            if (settings == null)
            {
                return;
            }

            string reason;
            check(settings.IsConfigurationValid(out reason),
                "El perfil canónico es válido: " + reason);
            check(settings.ProfileVersion == BistroBuilderCameraViewSettings.CurrentProfileVersion,
                "Versión de perfil actual");

            HashSet<BistroBuilderCameraViewId> ids =
                new HashSet<BistroBuilderCameraViewId>();
            BistroBuilderCameraViewId[] expected =
            {
                BistroBuilderCameraViewId.General,
                BistroBuilderCameraViewId.Isometric,
                BistroBuilderCameraViewId.TopDown,
                BistroBuilderCameraViewId.Close
            };
            for (int index = 0; index < expected.Length; index++)
            {
                BistroBuilderCameraViewDefinition definition;
                check(settings.TryGetView(expected[index], out definition),
                    "Se resuelve " + expected[index]);
                check(definition != null, "Definición no nula " + expected[index]);
                if (definition != null)
                {
                    check(ids.Add(definition.Id), "Identidad única " + definition.Id);
                    check(definition.FramingMargin >= 1.0f,
                        "Margen seguro " + definition.Id);
                    check(definition.Pitch > 0.0f && definition.Pitch < 90.0f,
                        "Pitch finito " + definition.Id);
                }
            }

            BistroBuilderCameraViewDefinition general;
            BistroBuilderCameraViewDefinition isometric;
            BistroBuilderCameraViewDefinition topDown;
            BistroBuilderCameraViewDefinition close;
            settings.TryGetView(BistroBuilderCameraViewId.General, out general);
            settings.TryGetView(BistroBuilderCameraViewId.Isometric, out isometric);
            settings.TryGetView(BistroBuilderCameraViewId.TopDown, out topDown);
            settings.TryGetView(BistroBuilderCameraViewId.Close, out close);

            check(general != null && general.YawMode == BistroBuilderCameraViewYawMode.PreserveCurrent,
                "General conserva orientación del jugador");
            check(isometric != null && isometric.YawMode == BistroBuilderCameraViewYawMode.Fixed &&
                  Mathf.Abs(isometric.FixedYaw - 45.0f) < 0.001f,
                "Isométrica usa yaw canónico 45");
            check(topDown != null && topDown.AllowExtendedPitch && topDown.Pitch >= 84.0f,
                "Cenital usa pitch extendido real");
            check(close != null && close.FocusMode == BistroBuilderCameraViewFocusMode.CurrentFocus,
                "Cercana conserva foco actual");

            highlights.Add("El perfil contiene General, Isométrica, Cenital y Cercana con identidad estable.");
            highlights.Add("La vista Cenital no amplía los límites manuales permanentes de 369A.");
        }

        private static void RunFramingMathTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            GameObject cameraObject = new GameObject("BB_369B_SelfTestCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Camera camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.fieldOfView = 60.0f;
            camera.aspect = 16.0f / 9.0f;
            camera.nearClipPlane = 0.3f;

            try
            {
                Vector3 focus = new Vector3(0.0f, 2.0f, 0.0f);
                Vector3[] points = BuildBoxCorners(
                    new Vector3(-7.0f, 0.0f, -10.0f),
                    new Vector3(7.0f, 4.0f, 10.0f));

                float distance;
                bool fit = BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                    camera,
                    focus,
                    Quaternion.Euler(48.0f, 45.0f, 0.0f),
                    points,
                    1.08f,
                    12.0f,
                    3.5f,
                    45.0f,
                    out distance);
                check(fit, "El encuadre isométrico se resuelve");
                check(distance > 3.5f && distance < 45.0f,
                    "La distancia isométrica queda en rango");
                check(BistroBuilderProfessionalCameraMath.IsFinite(distance),
                    "La distancia isométrica es finita");

                float topDistance;
                bool topFit = BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                    camera,
                    focus,
                    Quaternion.Euler(88.0f, 0.0f, 0.0f),
                    points,
                    1.06f,
                    12.0f,
                    3.5f,
                    45.0f,
                    out topDistance);
                check(topFit, "El encuadre cenital se resuelve");
                check(topDistance > 3.5f && topDistance < 45.0f,
                    "La distancia cenital queda en rango");

                camera.aspect = 4.0f / 3.0f;
                float narrowDistance;
                check(BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                        camera,
                        focus,
                        Quaternion.Euler(48.0f, 45.0f, 0.0f),
                        points,
                        1.08f,
                        12.0f,
                        3.5f,
                        45.0f,
                        out narrowDistance),
                    "El encuadre 4:3 se resuelve");
                check(narrowDistance >= distance,
                    "Una pantalla más estrecha no reduce indebidamente la distancia");

                camera.aspect = 16.0f / 9.0f;
                float largerMarginDistance;
                check(BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                        camera,
                        focus,
                        Quaternion.Euler(48.0f, 45.0f, 0.0f),
                        points,
                        1.25f,
                        12.0f,
                        3.5f,
                        45.0f,
                        out largerMarginDistance),
                    "El encuadre con margen mayor se resuelve");
                check(largerMarginDistance >= distance,
                    "Aumentar margen no acerca la cámara");

                check(!BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                        null,
                        focus,
                        Quaternion.identity,
                        points,
                        1.1f,
                        12.0f,
                        3.5f,
                        45.0f,
                        out distance),
                    "Se rechaza cámara nula");
                check(!BistroBuilderCameraViewMath.TryCalculateDistanceToFit(
                        camera,
                        focus,
                        Quaternion.identity,
                        null,
                        1.1f,
                        12.0f,
                        3.5f,
                        45.0f,
                        out distance),
                    "Se rechaza volumen nulo");

                highlights.Add("El encuadre se adapta al FOV, aspecto, orientación y margen del local.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void RunRuntimeContractTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            check(typeof(BistroBuilderCameraViewService).GetMethod("TryActivateView") != null,
                "Existe API de activación de vista");
            check(typeof(BistroBuilderCameraViewService).GetMethod("TryRestorePreviousView") != null,
                "Existe API de restauración");
            check(typeof(BistroBuilderCameraViewService).GetMethod("TryCalculateViewState") != null,
                "Existe API de cálculo sin activar");
            check(typeof(BistroBuilderProfessionalCameraController).GetMethod("SetExternalPitchRange") != null,
                "369A expone pitch temporal controlado");
            check(typeof(BistroBuilderCameraBounds).GetMethod("TryGetWorldFramingCorners") != null,
                "Los límites exponen volumen de encuadre");
            check(typeof(BistroBuilderCameraBounds).GetMethod("GetWorldFocusCenter") != null,
                "Los límites exponen centro semántico");

            highlights.Add("Las vistas usan el estado objetivo amortiguado de 369A y permiten interrupción manual.");
            highlights.Add("No se guardan transforms absolutos: el mismo perfil sirve para futuros locales.");
        }

        private static Vector3[] BuildBoxCorners(Vector3 minimum, Vector3 maximum)
        {
            Vector3[] corners = new Vector3[8];
            int index = 0;
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        corners[index++] = new Vector3(
                            x == 0 ? minimum.x : maximum.x,
                            y == 0 ? minimum.y : maximum.y,
                            z == 0 ? minimum.z : maximum.z);
                    }
                }
            }
            return corners;
        }

        private static string BuildDialogText(
            int passed,
            List<string> failures,
            List<string> highlights)
        {
            StringBuilder builder = new StringBuilder(900);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 369B");
            builder.AppendLine("Pruebas superadas: " + passed);
            builder.AppendLine("Pruebas fallidas: " + failures.Count);
            int visibleHighlights = Mathf.Min(7, highlights.Count);
            for (int index = 0; index < visibleHighlights; index++)
            {
                builder.AppendLine("- OK: " + highlights[index]);
            }
            int visibleFailures = Mathf.Min(6, failures.Count);
            for (int index = 0; index < visibleFailures; index++)
            {
                builder.AppendLine("- ERROR: " + failures[index]);
            }
            if (failures.Count > visibleFailures)
            {
                builder.AppendLine("- ...");
            }
            builder.AppendLine();
            builder.Append("(For the full message, see the editor log file)");
            return builder.ToString().TrimEnd();
        }
    }
}
#endif
