#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369CSelfTest
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Run 369C Contextual Camera Self-Test", false, 36922)]
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

            RunProfileTests(check, highlights);
            RunStateTests(check, highlights);
            RunBoundsTests(check, highlights);
            RunRuntimeContractTests(check, highlights);

            StringBuilder full = new StringBuilder(2400);
            full.AppendLine("BISTRO BUILDER - AUTOTEST 369C");
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

        private static void RunProfileTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            BistroBuilderCameraInspectionSettings settings =
                ScriptableObject.CreateInstance<BistroBuilderCameraInspectionSettings>();
            try
            {
                settings.ApplyCanonicalProfile();
                string reason;
                check(settings.IsConfigurationValid(out reason), "Perfil canónico válido: " + reason);
                check(settings.ProfileVersion == BistroBuilderCameraInspectionSettings.CurrentProfileVersion,
                    "Versión de perfil actual");
                check(settings.FramingMargin >= 1.0f, "Margen de encuadre seguro");
                check(settings.MinimumInspectionDistance > 0.0f, "Distancia mínima positiva");
                check(settings.MaximumInspectionDistance > settings.MinimumInspectionDistance,
                    "Rango de inspección ordenado");
                check(settings.RotationStep > 0.0f && settings.RotationStep <= 180.0f,
                    "Paso de giro válido");
                check(settings.MaximumRelatedSeats <= 32, "Límite de asientos relacionado acotado");
                check(settings.TrackingPositionEpsilon >= 0.0f, "Tolerancia de seguimiento válida");
                highlights.Add("El perfil de inspección define encuadre, giro por pasos y seguimiento acotado.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void RunStateTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            BistroBuilderCameraNavigationState serviceState =
                new BistroBuilderCameraNavigationState(new Vector3(1.0f, 4.0f, 2.0f), 25.0f, 40.0f, 12.0f);
            BistroBuilderCameraNavigationState editState =
                new BistroBuilderCameraNavigationState(new Vector3(-3.0f, 3.0f, 5.0f), -40.0f, 35.0f, 8.0f);
            BistroBuilderCameraNavigationState inspectionState =
                new BistroBuilderCameraNavigationState(new Vector3(4.0f, 2.0f, -2.0f), 80.0f, 32.0f, 5.0f);

            BistroBuilderCameraContextMemorySlot service = default;
            BistroBuilderCameraContextMemorySlot edit = default;
            BistroBuilderCameraContextMemorySlot inspection = default;
            service.Set(serviceState);
            edit.Set(editState);
            inspection.Set(inspectionState);

            check(service.HasState && service.State.IsFinite, "Memoria de servicio válida");
            check(edit.HasState && edit.State.IsFinite, "Memoria de edición válida");
            check(inspection.HasState && inspection.State.IsFinite, "Memoria de inspección válida");

            BistroBuilderCameraContextSnapshot snapshot = new BistroBuilderCameraContextSnapshot(
                BistroBuilderCameraContextMode.Edit,
                service,
                edit,
                inspection);
            check(snapshot.IsCompatible, "Snapshot compatible");
            check(snapshot.CurrentMode == BistroBuilderCameraContextMode.Edit,
                "Snapshot conserva modo actual");
            check(snapshot.Service.State.FocusPoint == serviceState.FocusPoint,
                "Snapshot conserva servicio");
            check(snapshot.Edit.State.Distance == editState.Distance,
                "Snapshot conserva edición");
            check(snapshot.Inspection.State.Yaw == inspectionState.Yaw,
                "Snapshot conserva inspección");

            service.Clear();
            check(!service.HasState, "La memoria puede limpiarse");
            check(Enum.GetValues(typeof(BistroBuilderCameraContextMode)).Length == 3,
                "Existen tres contextos estables");
            highlights.Add("Servicio, Edición e Inspección conservan estados independientes y versionables.");
        }

        private static void RunBoundsTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Mesa_369C_SelfTest";
            root.transform.position = new Vector3(2.0f, 1.0f, -3.0f);
            GameObject chair = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chair.name = "Silla_369C_SelfTest";
            chair.transform.position = new Vector3(2.0f, 0.5f, -4.3f);
            BistroBuilderCameraInspectionSettings settings =
                ScriptableObject.CreateInstance<BistroBuilderCameraInspectionSettings>();
            settings.ApplyCanonicalProfile();

            try
            {
                Bounds bounds;
                GameObject semanticRoot;
                bool calculated = BistroBuilderCameraInspectionBounds.TryCalculate(
                    root,
                    settings,
                    true,
                    out bounds,
                    out semanticRoot);
                check(calculated, "Se calcula volumen de selección");
                check(semanticRoot == root, "Se conserva raíz semántica de mesa");
                check(bounds.size.x > 0.0f && bounds.size.y > 0.0f && bounds.size.z > 0.0f,
                    "El volumen tiene dimensiones positivas");
                check(bounds.Contains(root.GetComponent<Renderer>().bounds.center),
                    "El volumen contiene la mesa");
                check(bounds.Contains(chair.GetComponent<Renderer>().bounds.center),
                    "El volumen relacionado contiene la silla próxima");

                Vector3[] corners = new Vector3[8];
                BistroBuilderCameraInspectionBounds.GetCorners(bounds, corners);
                for (int index = 0; index < corners.Length; index++)
                {
                    check(BistroBuilderProfessionalCameraMath.IsFinite(corners[index]),
                        "Esquina finita " + index);
                }

                check(BistroBuilderCameraInspectionBounds.IsSeatName("Silla_A"),
                    "Reconoce nombre español de silla");
                check(BistroBuilderCameraInspectionBounds.IsSeatName("Chair_A"),
                    "Reconoce nombre inglés de silla");
                check(BistroBuilderCameraInspectionBounds.IsSeatName("Taburete_A"),
                    "Reconoce taburete");
                check(!BistroBuilderCameraInspectionBounds.IsSeatName("Mesa_A"),
                    "No confunde mesa con asiento");
                highlights.Add("El encuadre genérico incluye automáticamente asientos próximos a una mesa.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(chair);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        private static void RunRuntimeContractTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            Type serviceType = typeof(BistroBuilderCameraInspectionService);
            check(serviceType.GetMethod("TrySwitchMode") != null, "Existe API de cambio de contexto");
            check(serviceType.GetMethod("RememberCurrentModeState") != null, "Existe API de memoria");
            check(serviceType.GetMethod("TryInspect") != null, "Existe API de inspección");
            check(serviceType.GetMethod("TryFrameCurrentTarget") != null, "Existe API de reencuadre");
            check(serviceType.GetMethod("TryRestoreBeforeInspection") != null, "Existe API de restauración");
            check(serviceType.GetMethod("RotateInspectionByStep") != null, "Existe API de giro por pasos");
            check(serviceType.GetMethod("TryCalculateInspectionState") != null,
                "Existe API de cálculo sin activar");
            check(serviceType.GetMethod("CaptureSnapshot") != null, "Existe captura neutral de snapshot");
            check(serviceType.GetMethod("RestoreSnapshot") != null, "Existe restauración neutral de snapshot");

            MethodInfo targetSetter = typeof(BistroBuilderProfessionalCameraController).GetMethod("SetTargetState");
            MethodInfo focusSetter = typeof(BistroBuilderProfessionalCameraController).GetMethod("SetFocusPoint");
            check(targetSetter != null, "369A expone estado objetivo");
            check(focusSetter != null, "369A expone seguimiento de foco");
            check(typeof(BistroBuilderCameraViewMath).GetMethod("TryCalculateDistanceToFit") != null,
                "369B expone cálculo adaptativo de distancia");
            check(BistroBuilderCameraInspectionService.RuntimeRevision >= 1,
                "Revisión runtime 369C conocida");
            check(BistroBuilderCameraContextSnapshot.CurrentVersion == 1,
                "Versión de snapshot 369C conocida");
            check(BistroBuilderCamera369CSceneLayoutInstaller.LayoutRevision >= 2,
                "Revisión 369C3 de seguridad no destructiva conocida");
            check(BistroBuilderCamera369CFunctionalTestWindow.DiagnosticRevision >= 2,
                "Revisión 369C1 de selección manual conocida");
            check(BistroBuilderCamera369CInstaller.InstallerRevision >= 4,
                "Revisión 369C4 de reparación automática de 369B conocida");

            highlights.Add("369C reutiliza 369A/369B y no crea un segundo controlador de cámara.");
            highlights.Add("369C1 permite elegir objetivo sin depender de que exista una selección previa en el Hierarchy.");
            highlights.Add("369C3 separa la cámara contextual de cualquier modificación automática de la escena.");
            highlights.Add("369C4 restaura automáticamente el servicio técnico 369B si una escena antigua no lo contiene.");
            highlights.Add("El snapshot queda listo para integrar persistencia cuando se defina el contrato de guardado de cámara.");
        }

        private static string BuildDialogText(
            int passed,
            List<string> failures,
            List<string> highlights)
        {
            StringBuilder builder = new StringBuilder(1000);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 369C");
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
                builder.AppendLine("(Consulta el log completo del Editor para el resto.)");
            }
            return builder.ToString().TrimEnd();
        }
    }
}
#endif
