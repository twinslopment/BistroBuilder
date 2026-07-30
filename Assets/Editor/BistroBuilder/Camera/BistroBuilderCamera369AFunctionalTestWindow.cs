#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.CameraSystem.Editor
{
    public sealed class BistroBuilderCamera369AFunctionalTestWindow : EditorWindow
    {
        public const int DiagnosticRevision = 8;

        private const string MenuRoot = "Bistro Builder/Camera/";
        private const float MovementPhaseTimeout = 4.0f;
        private const float SettlingPhaseTimeout = 1.5f;
        private const float RequiredStableDuration = 0.18f;
        private const float FinalLinearSpeedThreshold = 1.0f;
        private const float FinalAngularSpeedThreshold = 5.0f;
        private const float FinalZoomSpeedThreshold = 1.0f;

        private enum TestPhase
        {
            Idle,
            MovingToTestTarget,
            SettlingAtTestTarget,
            ReturningToOriginal,
            SettlingAtOriginal,
            Completed,
            Failed
        }

        private TestPhase phase;
        private BistroBuilderProfessionalCameraController controller;
        private BistroBuilderCameraNavigationState originalState;
        private BistroBuilderCameraNavigationState effectiveTestTarget;
        private bool originalInputEnabled;
        private bool originalStateCaptured;
        private double phaseStartTime;
        private double stableStartTime = -1.0d;
        private int lastSampledFrame = -1;
        private Vector3 previousCameraPosition;
        private float initialTravelDistance;
        private float maximumSingleFrameTravel;
        private float previousError;
        private int meaningfulErrorIncreases;
        private bool allSamplesFinite = true;
        private bool remainedInsideBounds = true;
        private bool remainedInsideCameraEnvelope = true;
        private float peakLinearSpeed;
        private float peakAngularSpeed;
        private float peakZoomSpeed;
        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();
        private Vector2 scroll;

        [MenuItem(MenuRoot + "369A Functional Camera Motion Test", false, 36903)]
        public static void OpenWindow()
        {
            BistroBuilderCamera369AFunctionalTestWindow window =
                GetWindow<BistroBuilderCamera369AFunctionalTestWindow>();
            window.titleContent = new GUIContent("BB 369A Camera");
            window.minSize = new Vector2(560.0f, 380.0f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            RestoreInputIfPossible();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8.0f);
            EditorGUILayout.LabelField(
                "369A — Prueba funcional de movimiento cinematográfico",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "En Play Mode, la prueba desplaza, gira y acerca la cámara mediante el mismo " +
                "estado objetivo usado por el jugador. Comprueba convergencia, ausencia de salto, " +
                "estabilidad numérica, límites y una fase final de asentamiento sostenido. La cámara " +
                "no se considera detenida al entrar por primera vez en la tolerancia geométrica: debe " +
                "mantener velocidades residuales bajas durante varios fotogramas. Esta fase no simula " +
                "el hardware real del ratón ni del teclado; rueda, arrastres y R/F deben validarse manualmente.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || IsRunning))
            {
                if (GUILayout.Button("Ejecutar prueba funcional 369A", GUILayout.Height(34.0f)))
                {
                    StartTest();
                }
            }

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Entra en Play Mode para ejecutar la prueba.", MessageType.Warning);
            }

            EditorGUILayout.Space(8.0f);
            EditorGUILayout.LabelField("Estado: " + GetStatusText(), EditorStyles.boldLabel);

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

            if (phase == TestPhase.Completed || phase == TestPhase.Failed)
            {
                EditorGUILayout.Space(6.0f);
                EditorGUILayout.HelpBox(
                    phase == TestPhase.Completed
                        ? "PRUEBA AUTOMÁTICA 369A SUPERADA. Revisa también: zoom continuo sin escalones, órbita bajo cursor, " +
                          "rueda mantenida sin vibración, botón derecho horizontal/vertical y R/F con frenada suave " +
                          "antes de sus límites de altura operativos."
                        : "PRUEBA FUNCIONAL 369A FALLIDA. Revisa el detalle y la consola.",
                    phase == TestPhase.Completed ? MessageType.Info : MessageType.Error);
            }
        }

        private void Update()
        {
            if (!IsRunning || !EditorApplication.isPlaying)
            {
                return;
            }

            if (controller == null)
            {
                FailNow("El controlador desapareció durante la prueba.");
                return;
            }

            if (Time.frameCount == lastSampledFrame)
            {
                return;
            }
            lastSampledFrame = Time.frameCount;

            SampleCurrentFrame();
            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - phaseStartTime;

            switch (phase)
            {
                case TestPhase.MovingToTestTarget:
                {
                    float error = CalculateStateError(controller.CurrentState, effectiveTestTarget);
                    TrackError(error);
                    if (HasConverged(controller.CurrentState, effectiveTestTarget))
                    {
                        BeginSettlingAtTestTarget();
                    }
                    else if (elapsed >= MovementPhaseTimeout)
                    {
                        FailNow("La cámara no convergió al encuadre de prueba dentro de " +
                                MovementPhaseTimeout.ToString("F1") + " s.");
                    }
                    break;
                }

                case TestPhase.SettlingAtTestTarget:
                {
                    if (UpdateStableWindow(effectiveTestTarget, now))
                    {
                        EvaluateOutboundMotion();
                        BeginReturnPhase();
                    }
                    else if (elapsed >= SettlingPhaseTimeout)
                    {
                        FailNow(BuildSettlingFailure("encuadre de prueba"));
                    }
                    break;
                }

                case TestPhase.ReturningToOriginal:
                {
                    if (HasConverged(controller.CurrentState, originalState))
                    {
                        BeginSettlingAtOriginal();
                    }
                    else if (elapsed >= MovementPhaseTimeout)
                    {
                        FailNow("La cámara no restauró el encuadre original dentro de " +
                                MovementPhaseTimeout.ToString("F1") + " s.");
                    }
                    break;
                }

                case TestPhase.SettlingAtOriginal:
                {
                    if (UpdateStableWindow(originalState, now))
                    {
                        Pass("El encuadre original se restauró y quedó estable sin deriva residual.");
                        CompleteTest();
                    }
                    else if (elapsed >= SettlingPhaseTimeout)
                    {
                        FailNow(BuildSettlingFailure("encuadre original"));
                    }
                    break;
                }
            }

            Repaint();
        }

        private bool IsRunning
        {
            get
            {
                return phase == TestPhase.MovingToTestTarget ||
                       phase == TestPhase.SettlingAtTestTarget ||
                       phase == TestPhase.ReturningToOriginal ||
                       phase == TestPhase.SettlingAtOriginal;
            }
        }

        private void StartTest()
        {
            passed.Clear();
            failed.Clear();
            phase = TestPhase.Idle;

            controller = FindActiveController();
            if (controller == null)
            {
                FailNow("No se encontró un controlador 369A activo en Play Mode.");
                return;
            }

            if (!controller.IsInitialized)
            {
                controller.ReinitializeFromCurrentCamera();
            }

            if (!controller.IsInitialized)
            {
                FailNow("El controlador no pudo inicializarse. Revisa sus referencias.");
                return;
            }

            originalState = controller.CurrentState;
            originalStateCaptured = true;
            originalInputEnabled = controller.InputEnabled;
            controller.SetInputEnabled(false);
            Pass("El controlador real de la escena está inicializado.");
            Pass("La entrada del jugador queda aislada durante el diagnóstico.");

            Vector3 right;
            Vector3 forward;
            BistroBuilderProfessionalCameraMath.GetGroundAlignedAxes(originalState.Yaw, out right, out forward);
            Vector3 desiredFocus = originalState.FocusPoint + right * 7.0f + forward * 4.0f;
            float desiredYaw = originalState.Yaw + 38.0f;
            float desiredPitch = Mathf.Clamp(
                originalState.Pitch + 4.0f,
                controller.Settings.MinimumPitch,
                controller.Settings.MaximumPitch);
            float desiredDistance = Mathf.Clamp(
                originalState.Distance * 0.72f,
                controller.Settings.MinimumDistance,
                controller.Settings.MaximumDistance);

            BistroBuilderCameraNavigationState requested = new BistroBuilderCameraNavigationState(
                desiredFocus,
                desiredYaw,
                desiredPitch,
                desiredDistance);

            previousCameraPosition = controller.ControlledCamera.transform.position;
            controller.SetTargetState(requested, false);
            effectiveTestTarget = controller.TargetState;
            initialTravelDistance = Vector3.Distance(
                previousCameraPosition,
                BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                    effectiveTestTarget.FocusPoint,
                    effectiveTestTarget.Yaw,
                    effectiveTestTarget.Pitch,
                    effectiveTestTarget.Distance));
            maximumSingleFrameTravel = 0.0f;
            meaningfulErrorIncreases = 0;
            previousError = CalculateStateError(controller.CurrentState, effectiveTestTarget);
            allSamplesFinite = true;
            remainedInsideBounds = true;
            remainedInsideCameraEnvelope = true;
            peakLinearSpeed = 0.0f;
            peakAngularSpeed = 0.0f;
            peakZoomSpeed = 0.0f;
            stableStartTime = -1.0d;
            phaseStartTime = EditorApplication.timeSinceStartup;
            lastSampledFrame = -1;
            phase = TestPhase.MovingToTestTarget;
            Pass("El objetivo combina desplazamiento, giro, inclinación y zoom.");
            Repaint();
        }

        private void SampleCurrentFrame()
        {
            BistroBuilderCameraNavigationState current = controller.CurrentState;
            allSamplesFinite &= current.IsFinite;

            Vector3 cameraPosition = controller.ControlledCamera.transform.position;
            maximumSingleFrameTravel = Mathf.Max(
                maximumSingleFrameTravel,
                Vector3.Distance(previousCameraPosition, cameraPosition));
            previousCameraPosition = cameraPosition;

            peakLinearSpeed = Mathf.Max(peakLinearSpeed, controller.LastFrameLinearSpeed);
            peakAngularSpeed = Mathf.Max(peakAngularSpeed, controller.LastFrameAngularSpeed);
            peakZoomSpeed = Mathf.Max(peakZoomSpeed, controller.LastFrameZoomSpeed);

            BistroBuilderCameraBounds bounds = controller.NavigationBounds;
            if (bounds != null && bounds.IsValid)
            {
                remainedInsideBounds &= bounds.ContainsFocusPoint(current.FocusPoint, 0.05f);
                remainedInsideCameraEnvelope &= bounds.ContainsCameraPosition(cameraPosition, 0.05f);
            }
        }

        private void TrackError(float currentError)
        {
            if (currentError > previousError + 0.025f)
            {
                meaningfulErrorIncreases++;
            }
            previousError = currentError;
        }

        private void BeginSettlingAtTestTarget()
        {
            phaseStartTime = EditorApplication.timeSinceStartup;
            stableStartTime = -1.0d;
            lastSampledFrame = -1;
            phase = TestPhase.SettlingAtTestTarget;
        }

        private void BeginSettlingAtOriginal()
        {
            phaseStartTime = EditorApplication.timeSinceStartup;
            stableStartTime = -1.0d;
            lastSampledFrame = -1;
            phase = TestPhase.SettlingAtOriginal;
        }

        private bool UpdateStableWindow(
            BistroBuilderCameraNavigationState target,
            double now)
        {
            bool geometricallyConverged = HasConverged(controller.CurrentState, target);
            bool speedsSettled = controller.IsMotionSettled(
                FinalLinearSpeedThreshold,
                FinalAngularSpeedThreshold,
                FinalZoomSpeedThreshold);

            if (!geometricallyConverged || !speedsSettled)
            {
                stableStartTime = -1.0d;
                return false;
            }

            if (stableStartTime < 0.0d)
            {
                stableStartTime = now;
                return false;
            }

            return now - stableStartTime >= RequiredStableDuration;
        }

        private void EvaluateOutboundMotion()
        {
            Pass("La cámara convergió al encuadre objetivo dentro del tiempo previsto.");

            if (allSamplesFinite)
            {
                Pass("Todos los estados intermedios fueron numéricamente finitos.");
            }
            else
            {
                Fail("Se detectó un estado NaN o infinito durante la transición.");
            }

            if (remainedInsideBounds)
            {
                Pass("El punto de interés permaneció dentro de los límites navegables.");
            }
            else
            {
                Fail("El punto de interés salió de los límites navegables.");
            }

            if (remainedInsideCameraEnvelope)
            {
                Pass("La cámara permaneció dentro de la envolvente de encuadre exterior.");
            }
            else
            {
                Fail("La cámara abandonó la envolvente de encuadre exterior.");
            }

            float jumpLimit = Mathf.Max(0.25f, initialTravelDistance * 0.45f);
            if (maximumSingleFrameTravel <= jumpLimit)
            {
                Pass("No se produjo teletransporte ni salto dominante en un solo fotograma.");
            }
            else
            {
                Fail("Un fotograma recorrió " + maximumSingleFrameTravel.ToString("F3") +
                     " m; límite de salto: " + jumpLimit.ToString("F3") + " m.");
            }

            if (meaningfulErrorIncreases <= 2)
            {
                Pass("La aproximación fue estable y sin oscilación perceptible.");
            }
            else
            {
                Fail("La distancia al objetivo aumentó en " + meaningfulErrorIncreases +
                     " muestras; posible oscilación.");
            }

            Pass(
                "La llegada mantuvo una desaceleración final estable durante " +
                RequiredStableDuration.ToString("F2") + " s " +
                "(picos: " + FormatSpeeds(peakLinearSpeed, peakAngularSpeed, peakZoomSpeed) +
                "; final: " + FormatCurrentSpeeds() + ").");
        }

        private void BeginReturnPhase()
        {
            controller.SetTargetState(originalState, false);
            phaseStartTime = EditorApplication.timeSinceStartup;
            stableStartTime = -1.0d;
            lastSampledFrame = -1;
            phase = TestPhase.ReturningToOriginal;
        }

        private void CompleteTest()
        {
            RestoreInputIfPossible();
            phase = failed.Count == 0 ? TestPhase.Completed : TestPhase.Failed;
            LogResult();
            Repaint();
        }

        private void FailNow(string message)
        {
            Fail(message);
            if (controller != null && originalStateCaptured && originalState.IsFinite)
            {
                controller.SetTargetState(originalState, true);
            }
            RestoreInputIfPossible();
            phase = TestPhase.Failed;
            LogResult();
            Repaint();
        }

        private string BuildSettlingFailure(string destination)
        {
            return "La cámara llegó geométricamente al " + destination +
                   ", pero no mantuvo el asentamiento final durante " +
                   RequiredStableDuration.ToString("F2") + " s dentro de " +
                   SettlingPhaseTimeout.ToString("F1") + " s. Velocidades actuales: " +
                   FormatCurrentSpeeds() + ".";
        }

        private string FormatCurrentSpeeds()
        {
            return FormatSpeeds(
                controller != null ? controller.LastFrameLinearSpeed : float.PositiveInfinity,
                controller != null ? controller.LastFrameAngularSpeed : float.PositiveInfinity,
                controller != null ? controller.LastFrameZoomSpeed : float.PositiveInfinity);
        }

        private static string FormatSpeeds(float linear, float angular, float zoom)
        {
            return "lineal " + linear.ToString("F3") + " m/s, angular " +
                   angular.ToString("F3") + " °/s, zoom " + zoom.ToString("F3") + " m/s";
        }

        private void RestoreInputIfPossible()
        {
            if (controller != null)
            {
                controller.SetInputEnabled(originalInputEnabled);
            }
        }

        private void Pass(string message)
        {
            if (!passed.Contains(message))
            {
                passed.Add(message);
            }
        }

        private void Fail(string message)
        {
            if (!failed.Contains(message))
            {
                failed.Add(message);
            }
        }

        private void LogResult()
        {
            StringBuilder builder = new StringBuilder(1280);
            builder.AppendLine("BISTRO BUILDER — PRUEBA FUNCIONAL DE CÁMARA 369A " +
                               (failed.Count == 0 ? "SUPERADA" : "FALLIDA"));
            builder.AppendLine("Diagnóstico: 369A3");
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
                Debug.Log(builder.ToString().TrimEnd(), controller);
            }
            else
            {
                Debug.LogError(builder.ToString().TrimEnd(), controller);
            }
        }

        private string GetStatusText()
        {
            switch (phase)
            {
                case TestPhase.Idle:
                    return "Preparada";
                case TestPhase.MovingToTestTarget:
                    return "Probando transición al encuadre de diagnóstico";
                case TestPhase.SettlingAtTestTarget:
                    return "Verificando desaceleración y asentamiento final";
                case TestPhase.ReturningToOriginal:
                    return "Restaurando encuadre original";
                case TestPhase.SettlingAtOriginal:
                    return "Verificando estabilidad del encuadre restaurado";
                case TestPhase.Completed:
                    return "SUPERADA";
                case TestPhase.Failed:
                    return "FALLIDA";
                default:
                    return phase.ToString();
            }
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode ||
                change == PlayModeStateChange.EnteredEditMode)
            {
                phase = TestPhase.Idle;
                controller = null;
                originalStateCaptured = false;
                stableStartTime = -1.0d;
                Repaint();
            }
        }

        private static BistroBuilderProfessionalCameraController FindActiveController()
        {
            BistroBuilderProfessionalCameraController[] all =
                Resources.FindObjectsOfTypeAll<BistroBuilderProfessionalCameraController>();
            for (int index = 0; index < all.Length; index++)
            {
                BistroBuilderProfessionalCameraController candidate = all[index];
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.gameObject.scene.IsValid())
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool HasConverged(
            BistroBuilderCameraNavigationState current,
            BistroBuilderCameraNavigationState target)
        {
            return Vector3.Distance(current.FocusPoint, target.FocusPoint) < 0.08f &&
                   Mathf.Abs(Mathf.DeltaAngle(current.Yaw, target.Yaw)) < 0.25f &&
                   Mathf.Abs(Mathf.DeltaAngle(current.Pitch, target.Pitch)) < 0.20f &&
                   Mathf.Abs(current.Distance - target.Distance) < 0.05f;
        }

        private static float CalculateStateError(
            BistroBuilderCameraNavigationState current,
            BistroBuilderCameraNavigationState target)
        {
            float position = Vector3.Distance(current.FocusPoint, target.FocusPoint);
            float yaw = Mathf.Abs(Mathf.DeltaAngle(current.Yaw, target.Yaw)) * 0.05f;
            float pitch = Mathf.Abs(Mathf.DeltaAngle(current.Pitch, target.Pitch)) * 0.05f;
            float distance = Mathf.Abs(current.Distance - target.Distance);
            return position + yaw + pitch + distance;
        }
    }
}
#endif
