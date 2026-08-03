#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public sealed class BistroBuilderCamera369CFunctionalTestWindow : EditorWindow
    {
        public const int DiagnosticRevision = 2;

        private enum TestPhase
        {
            Idle = 0,
            FrameSelection = 1,
            RotateSelection = 2,
            RestoreOriginal = 3,
            Complete = 4
        }

        private BistroBuilderCameraInspectionService service;
        private BistroBuilderProfessionalCameraController controller;
        private BistroBuilderCameraNavigationState originalState;
        private BistroBuilderCameraContextMode originalMode;
        private bool originalInputEnabled;
        private GameObject testTarget;
        private GameObject manualTarget;
        private TestPhase phase;
        private double phaseStartTime;
        private float settledTime;
        private float yawBeforeRotation;
        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();
        private readonly Vector3[] corners = new Vector3[8];
        private Vector2 scroll;

        [MenuItem("Bistro Builder/Camera/369C Contextual Edit and Inspection Functional Test", false, 36923)]
        public static void OpenWindow()
        {
            GetWindow<BistroBuilderCamera369CFunctionalTestWindow>("BB 369C Inspect");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            Selection.selectionChanged += OnEditorSelectionChanged;
            FindRuntimeReferences();
            EnsureManualTarget();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Selection.selectionChanged -= OnEditorSelectionChanged;
            AbortAndRestore();
        }

        private void OnGUI()
        {
            RefreshRuntimeReferences();
            EnsureManualTarget();

            EditorGUILayout.LabelField(
                "369C — Cámara contextual de edición e inspección",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "En Play Mode elige un objetivo de la escena. Puedes seleccionarlo en el " +
                "Hierarchy, arrastrarlo al campo o usar el objetivo automático. 369C encuadra " +
                "su volumen, incluye asientos relacionados, recuerda la cámara de cada contexto " +
                "y permite girar la inspección por pasos.",
                MessageType.Info);

            DrawTargetPicker();
            GameObject selected = GetPreferredManualTarget();

            bool runtimeReady =
                EditorApplication.isPlaying &&
                service != null &&
                controller != null &&
                controller.IsInitialized &&
                !IsAutomatedTestRunning;
            bool hasInspectionTarget = service != null && service.HasInspectionTarget;
            bool hasStateBeforeInspection = service != null && service.HasStateBeforeInspection;

            using (new EditorGUI.DisabledScope(!runtimeReady))
            {
                if (GUILayout.Button("Ejecutar prueba funcional 369C", GUILayout.Height(32.0f)))
                {
                    StartTest();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Inspección manual", EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(selected == null))
                {
                    if (GUILayout.Button("Encuadrar selección"))
                    {
                        if (service.TryInspect(selected, true, false))
                        {
                            manualTarget = selected;
                            Repaint();
                        }
                    }
                }

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(!hasInspectionTarget))
                {
                    if (GUILayout.Button("Girar - paso"))
                    {
                        service.RotateInspectionByStep(-1, false);
                    }
                    if (GUILayout.Button("Girar + paso"))
                    {
                        service.RotateInspectionByStep(1, false);
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Contexto Servicio"))
                {
                    service.TrySwitchMode(BistroBuilderCameraContextMode.Service, true, false);
                }
                if (GUILayout.Button("Contexto Edición"))
                {
                    service.TrySwitchMode(BistroBuilderCameraContextMode.Edit, true, false);
                }
                EditorGUILayout.EndHorizontal();

                using (new EditorGUI.DisabledScope(!hasStateBeforeInspection))
                {
                    if (GUILayout.Button("Volver a la vista anterior a la inspección"))
                    {
                        service.TryRestoreBeforeInspection(false);
                    }
                }
            }

            DrawManualInteractionHint(runtimeReady, selected);

            EditorGUILayout.Space();
            DrawStatus();
        }

        private void DrawTargetPicker()
        {
            GameObject current = GetPreferredManualTarget();
            EditorGUI.BeginChangeCheck();
            GameObject picked = (GameObject)EditorGUILayout.ObjectField(
                "Objetivo de inspección",
                current,
                typeof(GameObject),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                manualTarget = IsValidInspectionTarget(picked) ? picked : null;
                Repaint();
            }

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(GetValidEditorSelection() == null))
            {
                if (GUILayout.Button("Usar selección del Hierarchy"))
                {
                    manualTarget = GetValidEditorSelection();
                    Repaint();
                }
            }

            if (GUILayout.Button("Elegir objetivo automático"))
            {
                manualTarget = FindFallbackTarget();
                if (manualTarget != null)
                {
                    Selection.activeGameObject = manualTarget;
                }
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Objetivo activo:",
                current != null ? current.name : "No se encontró un objeto inspeccionable");
        }

        private void DrawManualInteractionHint(bool runtimeReady, GameObject selected)
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Entra en Play Mode para usar la inspección manual.",
                    MessageType.None);
                return;
            }

            if (!runtimeReady)
            {
                if (IsAutomatedTestRunning)
                {
                    EditorGUILayout.HelpBox(
                        "La prueba automática está usando temporalmente la cámara.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "El servicio 369C todavía no está inicializado.",
                        MessageType.Warning);
                }
                return;
            }

            if (selected == null)
            {
                EditorGUILayout.HelpBox(
                    "Selecciona un objeto del Hierarchy, arrástralo al campo o pulsa " +
                    "«Elegir objetivo automático».",
                    MessageType.Warning);
                return;
            }

            if (!service.HasInspectionTarget)
            {
                EditorGUILayout.HelpBox(
                    "Pulsa «Encuadrar selección». Después se habilitarán el giro por pasos " +
                    "y la restauración de la vista anterior.",
                    MessageType.None);
            }
            else if (!service.HasStateBeforeInspection)
            {
                EditorGUILayout.HelpBox(
                    "Hay un objetivo de inspección activo, pero no existe una vista anterior " +
                    "recuperable para este contexto.",
                    MessageType.None);
            }
        }

        private bool IsAutomatedTestRunning
        {
            get { return phase != TestPhase.Idle && phase != TestPhase.Complete; }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                FindRuntimeReferences();
                EnsureManualTarget();
                Repaint();
                return;
            }

            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                manualTarget = null;
                FindRuntimeReferences();
                Repaint();
            }
        }

        private void OnEditorSelectionChanged()
        {
            GameObject selected = GetValidEditorSelection();
            if (selected != null)
            {
                manualTarget = selected;
            }
            Repaint();
        }

        private void RefreshRuntimeReferences()
        {
            if (!EditorApplication.isPlaying)
            {
                service = null;
                controller = null;
                return;
            }

            if (service == null || controller == null || !controller.IsInitialized)
            {
                FindRuntimeReferences();
            }
        }

        private void FindRuntimeReferences()
        {
            service = null;
            controller = null;
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            service = UnityEngine.Object.FindFirstObjectByType<BistroBuilderCameraInspectionService>();
            if (service != null)
            {
                controller = service.Controller;
            }
        }

        private void StartTest()
        {
            passed.Clear();
            failed.Clear();
            FindRuntimeReferences();
            if (service == null || controller == null || !controller.IsInitialized)
            {
                failed.Add("No existe un servicio 369C inicializado en la escena.");
                Repaint();
                return;
            }

            testTarget = GetPreferredManualTarget();
            if (testTarget == null)
            {
                testTarget = FindFallbackTarget();
            }
            if (testTarget == null)
            {
                failed.Add("No se encontró un objeto con geometría para la prueba.");
                Repaint();
                return;
            }

            originalState = controller.CurrentState;
            originalMode = service.CurrentMode;
            originalInputEnabled = controller.InputEnabled;
            controller.SetInputEnabled(false);
            phase = TestPhase.FrameSelection;
            settledTime = 0.0f;
            phaseStartTime = EditorApplication.timeSinceStartup;
            if (!service.TryInspect(testTarget, true, false))
            {
                failed.Add("No se pudo encuadrar la selección " + testTarget.name + ".");
                CompleteTest();
            }
        }

        private void OnEditorUpdate()
        {
            if (!IsAutomatedTestRunning)
            {
                return;
            }

            if (!EditorApplication.isPlaying || service == null || controller == null)
            {
                failed.Add("La prueba se interrumpió porque Play Mode o sus referencias dejaron de estar disponibles.");
                CompleteTest();
                return;
            }

            double elapsed = EditorApplication.timeSinceStartup - phaseStartTime;
            float deltaTime = Mathf.Max(0.0f, Time.unscaledDeltaTime);
            if (controller.IsMotionSettled(0.08f, 0.15f, 0.08f))
            {
                settledTime += deltaTime;
            }
            else
            {
                settledTime = 0.0f;
            }

            if (elapsed > 7.0)
            {
                failed.Add("La fase " + phase + " no se asentó dentro del tiempo previsto.");
                CompleteTest();
                return;
            }

            if (settledTime < 0.18f)
            {
                return;
            }

            switch (phase)
            {
                case TestPhase.FrameSelection:
                    ValidateFramedSelection();
                    BeginRotationPhase();
                    break;
                case TestPhase.RotateSelection:
                    ValidateRotation();
                    BeginRestorePhase();
                    break;
                case TestPhase.RestoreOriginal:
                    ValidateRestore();
                    CompleteTest();
                    break;
            }
        }

        private void ValidateFramedSelection()
        {
            Bounds bounds;
            GameObject semanticRoot;
            BistroBuilderCameraNavigationState state;
            bool calculated = service.TryCalculateInspectionState(
                testTarget,
                true,
                out state,
                out bounds,
                out semanticRoot);
            if (calculated && state.IsFinite)
            {
                passed.Add("La selección produjo un estado de cámara finito.");
            }
            else
            {
                failed.Add("No se pudo recalcular el estado de inspección.");
                return;
            }

            BistroBuilderCameraInspectionBounds.GetCorners(bounds, corners);
            if (BistroBuilderCameraViewMath.ArePointsInsideViewport(
                    controller.ControlledCamera,
                    corners,
                    0.005f))
            {
                passed.Add("El volumen seleccionado quedó dentro del encuadre.");
            }
            else
            {
                failed.Add("El volumen seleccionado no quedó completamente visible.");
            }

            if (service.CurrentMode == BistroBuilderCameraContextMode.Inspection &&
                service.HasInspectionTarget)
            {
                passed.Add("El contexto cambió a Inspección y conserva el objetivo.");
            }
            else
            {
                failed.Add("El contexto de inspección no quedó activo.");
            }
        }

        private void BeginRotationPhase()
        {
            yawBeforeRotation = controller.CurrentState.Yaw;
            settledTime = 0.0f;
            phaseStartTime = EditorApplication.timeSinceStartup;
            phase = TestPhase.RotateSelection;
            if (!service.RotateInspectionByStep(1, false))
            {
                failed.Add("No se pudo iniciar el giro de inspección por pasos.");
                CompleteTest();
            }
        }

        private void ValidateRotation()
        {
            float difference = Mathf.Abs(Mathf.DeltaAngle(
                yawBeforeRotation,
                controller.CurrentState.Yaw));
            float expected = service.InspectionSettings.RotationStep;
            if (Mathf.Abs(difference - expected) <= 1.0f)
            {
                passed.Add("El giro por pasos alcanzó aproximadamente " + expected.ToString("0.#") + "°.");
            }
            else
            {
                failed.Add("El giro por pasos alcanzó " + difference.ToString("0.##") + "°.");
            }
        }

        private void BeginRestorePhase()
        {
            settledTime = 0.0f;
            phaseStartTime = EditorApplication.timeSinceStartup;
            phase = TestPhase.RestoreOriginal;
            if (!service.TryRestoreBeforeInspection(false))
            {
                failed.Add("No se pudo restaurar la vista previa a la inspección.");
                CompleteTest();
            }
        }

        private void ValidateRestore()
        {
            BistroBuilderCameraNavigationState restored = controller.CurrentState;
            float focusError = Vector3.Distance(restored.FocusPoint, originalState.FocusPoint);
            float yawError = Mathf.Abs(Mathf.DeltaAngle(restored.Yaw, originalState.Yaw));
            float pitchError = Mathf.Abs(restored.Pitch - originalState.Pitch);
            float distanceError = Mathf.Abs(restored.Distance - originalState.Distance);
            if (focusError <= 0.08f && yawError <= 0.25f &&
                pitchError <= 0.25f && distanceError <= 0.08f)
            {
                passed.Add("La vista anterior se restauró sin deriva significativa.");
            }
            else
            {
                failed.Add(
                    "La restauración dejó deriva: foco " + focusError.ToString("0.###") +
                    ", yaw " + yawError.ToString("0.###") +
                    ", pitch " + pitchError.ToString("0.###") +
                    ", distancia " + distanceError.ToString("0.###") + ".");
            }

            if (service.CurrentMode == originalMode)
            {
                passed.Add("La inspección devolvió el contexto de cámara original.");
            }
            else
            {
                failed.Add("No se recuperó el contexto original.");
            }
        }

        private void CompleteTest()
        {
            if (controller != null)
            {
                controller.SetInputEnabled(originalInputEnabled);
            }
            phase = TestPhase.Complete;
            Repaint();
            LogResult();
        }

        private void AbortAndRestore()
        {
            if (controller != null && originalState.IsFinite && IsAutomatedTestRunning)
            {
                controller.SetTargetState(originalState, true);
                controller.SetInputEnabled(originalInputEnabled);
            }
            phase = TestPhase.Idle;
        }

        private void DrawStatus()
        {
            string status;
            MessageType messageType;
            if (IsAutomatedTestRunning)
            {
                status = "Estado: EJECUTANDO — " + phase;
                messageType = MessageType.Info;
            }
            else if (failed.Count > 0)
            {
                status = "Estado: FALLIDA";
                messageType = MessageType.Error;
            }
            else if (passed.Count > 0)
            {
                status = "Estado: SUPERADA";
                messageType = MessageType.Info;
            }
            else
            {
                status = "Estado: PENDIENTE";
                messageType = MessageType.None;
            }

            EditorGUILayout.HelpBox(status, messageType);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int index = 0; index < passed.Count; index++)
            {
                EditorGUILayout.LabelField("✓ " + passed[index], EditorStyles.wordWrappedLabel);
            }
            for (int index = 0; index < failed.Count; index++)
            {
                EditorGUILayout.LabelField("✗ " + failed[index], EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void LogResult()
        {
            string title = failed.Count == 0
                ? "BISTRO BUILDER — PRUEBA FUNCIONAL 369C SUPERADA"
                : "BISTRO BUILDER — PRUEBA FUNCIONAL 369C FALLIDA";
            System.Text.StringBuilder builder = new System.Text.StringBuilder(1200);
            builder.AppendLine(title);
            builder.AppendLine("Comprobaciones correctas: " + passed.Count);
            builder.AppendLine("Comprobaciones fallidas: " + failed.Count);
            for (int index = 0; index < passed.Count; index++)
            {
                builder.AppendLine("- OK: " + passed[index]);
            }
            for (int index = 0; index < failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + failed[index]);
            }

            if (failed.Count == 0)
            {
                Debug.Log(builder.ToString().TrimEnd(), service);
            }
            else
            {
                Debug.LogError(builder.ToString().TrimEnd(), service);
            }
        }

        private void EnsureManualTarget()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (IsValidInspectionTarget(manualTarget))
            {
                return;
            }

            GameObject selected = GetValidEditorSelection();
            if (selected != null)
            {
                manualTarget = selected;
                return;
            }

            if (service != null &&
                service.HasInspectionTarget &&
                IsValidInspectionTarget(service.InspectionTarget))
            {
                manualTarget = service.InspectionTarget;
                return;
            }

            manualTarget = FindFallbackTarget();
        }

        private GameObject GetPreferredManualTarget()
        {
            if (IsValidInspectionTarget(manualTarget))
            {
                return manualTarget;
            }

            GameObject selected = GetValidEditorSelection();
            if (selected != null)
            {
                return selected;
            }

            if (service != null &&
                service.HasInspectionTarget &&
                IsValidInspectionTarget(service.InspectionTarget))
            {
                return service.InspectionTarget;
            }

            return null;
        }

        private GameObject GetValidEditorSelection()
        {
            GameObject selected = Selection.activeGameObject;
            return IsValidInspectionTarget(selected) ? selected : null;
        }

        private static bool IsValidInspectionTarget(GameObject candidate)
        {
            if (candidate == null ||
                !candidate.scene.IsValid() ||
                candidate.GetComponentInParent<BistroBuilderProfessionalCameraController>() != null)
            {
                return false;
            }

            return candidate.GetComponentInChildren<Renderer>(true) != null ||
                   candidate.GetComponentInChildren<Collider>(true) != null;
        }

        private GameObject FindFallbackTarget()
        {
            BistroBuilderCameraInspectable[] inspectables =
                UnityEngine.Object.FindObjectsByType<BistroBuilderCameraInspectable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int index = 0; index < inspectables.Length; index++)
            {
                if (IsValidInspectionTarget(inspectables[index].gameObject))
                {
                    return inspectables[index].gameObject;
                }
            }

            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Renderer[] renderers = roots[rootIndex].GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    string name = renderer.gameObject.name;
                    if (name.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("suelo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    BistroBuilderCameraInspectable inspectable =
                        renderer.GetComponentInParent<BistroBuilderCameraInspectable>();
                    GameObject semanticRoot = inspectable != null
                        ? inspectable.gameObject
                        : renderer.gameObject;
                    if (IsValidInspectionTarget(semanticRoot))
                    {
                        return semanticRoot;
                    }
                }
            }
            return null;
        }
    }
}
#endif
