#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.CameraSystem.Editor
{
    public sealed class BistroBuilderCamera369BFunctionalTestWindow : EditorWindow
    {
        public const int DiagnosticRevision = 2;

        private enum TestPhase
        {
            Idle = 0,
            General = 1,
            Isometric = 2,
            TopDown = 3,
            Close = 4,
            Restore = 5,
            Complete = 6
        }

        private BistroBuilderCameraViewService service;
        private BistroBuilderProfessionalCameraController controller;
        private BistroBuilderCameraNavigationState originalState;
        private bool originalInputEnabled;
        private TestPhase phase;
        private double phaseStartTime;
        private float settledTime;
        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();
        private Vector3[] framingCorners = new Vector3[8];
        private Vector2 scroll;

        [MenuItem("Bistro Builder/Camera/369B Preset Camera Views Functional Test", false, 36913)]
        public static void OpenWindow()
        {
            GetWindow<BistroBuilderCamera369BFunctionalTestWindow>(
                "BB 369B Views");
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            FindRuntimeReferences();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AbortAndRestore();
        }

        private void OnGUI()
        {
            RefreshRuntimeReferencesForManualPreview();

            EditorGUILayout.LabelField(
                "369B — Prueba funcional de vistas predefinidas",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "En Play Mode recorre General, Isométrica, Cenital y Cercana usando el mismo " +
                "estado objetivo amortiguado de 369A. Comprueba convergencia, encuadre, pitch " +
                "extendido temporal y restauración de la vista libre original.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || IsAutomatedTestRunning))
            {
                if (GUILayout.Button("Ejecutar prueba funcional 369B", GUILayout.Height(32.0f)))
                {
                    StartTest();
                }
            }

            EditorGUILayout.Space();
            DrawManualButtons();
            EditorGUILayout.Space();
            DrawStatus();
        }

        private void DrawManualButtons()
        {
            EditorGUILayout.LabelField("Previsualización manual", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(
                !EditorApplication.isPlaying ||
                service == null ||
                controller == null ||
                !controller.IsInitialized ||
                IsAutomatedTestRunning))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("General"))
                {
                    ActivateManualView(BistroBuilderCameraViewId.General);
                }
                if (GUILayout.Button("Isométrica"))
                {
                    ActivateManualView(BistroBuilderCameraViewId.Isometric);
                }
                if (GUILayout.Button("Cenital"))
                {
                    ActivateManualView(BistroBuilderCameraViewId.TopDown);
                }
                if (GUILayout.Button("Cercana"))
                {
                    ActivateManualView(BistroBuilderCameraViewId.Close);
                }
                EditorGUILayout.EndHorizontal();

                bool canRestorePreviousView =
                    service != null && service.HasPreviousFreeState;
                using (new EditorGUI.DisabledScope(!canRestorePreviousView))
                {
                    if (GUILayout.Button("Restaurar vista libre anterior"))
                    {
                        service.TryRestorePreviousView();
                        Repaint();
                    }
                }
            }
        }


        private bool IsAutomatedTestRunning
        {
            get
            {
                return phase != TestPhase.Idle && phase != TestPhase.Complete;
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode ||
                state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
            {
                FindRuntimeReferences();
                Repaint();
            }
        }

        private void RefreshRuntimeReferencesForManualPreview()
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

        private void ActivateManualView(BistroBuilderCameraViewId viewId)
        {
            RefreshRuntimeReferencesForManualPreview();
            if (service != null)
            {
                service.TryActivateView(viewId);
            }
            Repaint();
        }

        private void DrawStatus()
        {
            string status;
            MessageType messageType;
            if (phase != TestPhase.Idle && phase != TestPhase.Complete)
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

        private void StartTest()
        {
            passed.Clear();
            failed.Clear();
            FindRuntimeReferences();
            if (service == null || controller == null || !controller.IsInitialized)
            {
                failed.Add("No existe un servicio 369B inicializado en la escena.");
                Repaint();
                return;
            }

            originalState = controller.CurrentState;
            originalInputEnabled = controller.InputEnabled;
            controller.SetInputEnabled(false);
            phase = TestPhase.General;
            BeginPhase(BistroBuilderCameraViewId.General);
        }

        private void BeginPhase(BistroBuilderCameraViewId viewId)
        {
            settledTime = 0.0f;
            phaseStartTime = EditorApplication.timeSinceStartup;
            if (!service.TryActivateView(viewId))
            {
                failed.Add("No se pudo activar la vista " + viewId + ".");
                CompleteTest();
            }
        }

        private void OnEditorUpdate()
        {
            if (phase == TestPhase.Idle || phase == TestPhase.Complete)
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
            if (controller.IsMotionSettled(0.08f, 0.12f, 0.08f))
            {
                settledTime += deltaTime;
            }
            else
            {
                settledTime = 0.0f;
            }

            if (elapsed > 6.0)
            {
                failed.Add("La fase " + phase + " no se asentó dentro de 6 segundos.");
                CompleteTest();
                return;
            }

            if (settledTime < 0.18f)
            {
                Repaint();
                return;
            }

            ValidateCurrentPhase();
            AdvancePhase();
            Repaint();
        }

        private void ValidateCurrentPhase()
        {
            BistroBuilderCameraNavigationState current = controller.CurrentState;
            if (!current.IsFinite ||
                !BistroBuilderProfessionalCameraMath.IsFinite(controller.ControlledCamera.transform.position))
            {
                failed.Add("La fase " + phase + " produjo un estado no finito.");
                return;
            }

            if (phase == TestPhase.TopDown)
            {
                if (current.Pitch >= 84.0f && controller.ExternalPitchRangeActive)
                {
                    passed.Add("La vista Cenital alcanzó pitch extendido sin modificar el perfil manual.");
                }
                else
                {
                    failed.Add("La vista Cenital no alcanzó el pitch extendido esperado.");
                }
            }

            if (phase == TestPhase.General ||
                phase == TestPhase.Isometric ||
                phase == TestPhase.TopDown)
            {
                BistroBuilderCameraViewDefinition definition;
                if (service.ViewSettings.TryGetView(CurrentViewIdForPhase(), out definition) &&
                    service.NavigationBounds.TryGetWorldFramingCorners(
                        definition.FramingContentHeight,
                        framingCorners) &&
                    BistroBuilderCameraViewMath.ArePointsInsideViewport(
                        controller.ControlledCamera,
                        framingCorners,
                        0.005f))
                {
                    passed.Add("La vista " + CurrentViewIdForPhase() + " encuadró el volumen completo del local.");
                }
                else
                {
                    failed.Add("La vista " + CurrentViewIdForPhase() + " no mantuvo todo el volumen dentro del encuadre.");
                }
            }

            if (phase == TestPhase.Close)
            {
                BistroBuilderCameraViewDefinition close;
                service.ViewSettings.TryGetView(BistroBuilderCameraViewId.Close, out close);
                if (close != null && Mathf.Abs(current.Distance - close.FixedDistance) <= 0.12f)
                {
                    passed.Add("La vista Cercana alcanzó su distancia de inspección.");
                }
                else
                {
                    failed.Add("La vista Cercana no alcanzó la distancia configurada.");
                }
            }

            if (phase == TestPhase.Restore)
            {
                float positionError = Vector3.Distance(
                    BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                        current.FocusPoint,
                        current.Yaw,
                        current.Pitch,
                        current.Distance),
                    BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                        originalState.FocusPoint,
                        originalState.Yaw,
                        originalState.Pitch,
                        originalState.Distance));
                if (positionError <= 0.12f &&
                    Mathf.Abs(Mathf.DeltaAngle(current.Yaw, originalState.Yaw)) <= 0.2f &&
                    Mathf.Abs(current.Pitch - originalState.Pitch) <= 0.2f)
                {
                    passed.Add("La vista libre original se restauró sin deriva significativa.");
                }
                else
                {
                    failed.Add("La restauración de la vista libre dejó una deriva superior a la tolerancia.");
                }
            }
        }

        private void AdvancePhase()
        {
            switch (phase)
            {
                case TestPhase.General:
                    phase = TestPhase.Isometric;
                    BeginPhase(BistroBuilderCameraViewId.Isometric);
                    break;
                case TestPhase.Isometric:
                    phase = TestPhase.TopDown;
                    BeginPhase(BistroBuilderCameraViewId.TopDown);
                    break;
                case TestPhase.TopDown:
                    phase = TestPhase.Close;
                    BeginPhase(BistroBuilderCameraViewId.Close);
                    break;
                case TestPhase.Close:
                    phase = TestPhase.Restore;
                    settledTime = 0.0f;
                    phaseStartTime = EditorApplication.timeSinceStartup;
                    if (!service.TryRestorePreviousView())
                    {
                        failed.Add("No se pudo restaurar la vista libre original.");
                        CompleteTest();
                    }
                    break;
                case TestPhase.Restore:
                    CompleteTest();
                    break;
            }
        }

        private BistroBuilderCameraViewId CurrentViewIdForPhase()
        {
            switch (phase)
            {
                case TestPhase.General:
                    return BistroBuilderCameraViewId.General;
                case TestPhase.Isometric:
                    return BistroBuilderCameraViewId.Isometric;
                case TestPhase.TopDown:
                    return BistroBuilderCameraViewId.TopDown;
                case TestPhase.Close:
                    return BistroBuilderCameraViewId.Close;
                default:
                    return BistroBuilderCameraViewId.None;
            }
        }

        private void CompleteTest()
        {
            if (controller != null)
            {
                controller.SetInputEnabled(originalInputEnabled);
            }
            phase = TestPhase.Idle;

            string summary =
                "BISTRO BUILDER — PRUEBA FUNCIONAL DE VISTAS 369B " +
                (failed.Count == 0 ? "SUPERADA" : "FALLIDA") + "\n" +
                "Comprobaciones correctas: " + passed.Count + "\n" +
                "Comprobaciones fallidas: " + failed.Count;
            for (int index = 0; index < passed.Count; index++)
            {
                summary += "\n- OK: " + passed[index];
            }
            for (int index = 0; index < failed.Count; index++)
            {
                summary += "\n- ERROR: " + failed[index];
            }

            if (failed.Count == 0)
            {
                Debug.Log(summary, service);
            }
            else
            {
                Debug.LogError(summary, service);
            }
            Repaint();
        }

        private void AbortAndRestore()
        {
            if (phase != TestPhase.Idle && phase != TestPhase.Complete && controller != null)
            {
                controller.SetExternalPitchRange(false, 0.0f, 0.0f);
                if (originalState.IsFinite)
                {
                    controller.SetTargetState(originalState, true);
                }
                controller.SetInputEnabled(originalInputEnabled);
            }
            phase = TestPhase.Idle;
        }

        private void FindRuntimeReferences()
        {
            Scene scene = SceneManager.GetActiveScene();
            service = BistroBuilderCamera369BInstaller.FindSingleInScene<
                BistroBuilderCameraViewService>(scene);
            controller = service != null ? service.Controller : null;
        }
    }
}
#endif
