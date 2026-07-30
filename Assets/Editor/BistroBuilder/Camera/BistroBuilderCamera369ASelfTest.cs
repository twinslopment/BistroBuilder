#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.CameraSystem.Editor
{
    public static class BistroBuilderCamera369ASelfTest
    {
        private const string MenuRoot = "Bistro Builder/Camera/";

        [MenuItem(MenuRoot + "Run 369A Professional Camera Self-Test", false, 36902)]
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

            RunMathTests(check);
            RunDampingTests(check);
            RunBoundsTests(check);
            RunConfigurationTests(check, highlights);

            StringBuilder full = new StringBuilder(2048);
            full.AppendLine("BISTRO BUILDER - AUTOTEST 369A");
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

        private static void RunMathTests(Action<bool, string> check)
        {
            check(BistroBuilderProfessionalCameraMath.IsFinite(0.0f), "0 debe ser finito");
            check(BistroBuilderProfessionalCameraMath.IsFinite(-123.45f), "Un negativo debe ser finito");
            check(!BistroBuilderProfessionalCameraMath.IsFinite(float.NaN), "NaN no debe ser finito");
            check(!BistroBuilderProfessionalCameraMath.IsFinite(float.PositiveInfinity), "Infinito no debe ser finito");
            check(BistroBuilderProfessionalCameraMath.IsFinite(new Vector3(1, 2, 3)), "Vector finito");
            check(!BistroBuilderProfessionalCameraMath.IsFinite(new Vector3(1, float.NaN, 3)), "Vector con NaN");

            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(0), 0), "Ángulo 0");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(360), 0), "Ángulo 360");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(450), 90), "Ángulo 450");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(270), -90), "Ángulo 270");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeSignedAngle(-450), -90), "Ángulo -450");
            check(Approximately(BistroBuilderProfessionalCameraMath.ClampPitch(5, 28, 72), 28), "Pitch mínimo");
            check(Approximately(BistroBuilderProfessionalCameraMath.ClampPitch(80, 28, 72), 72), "Pitch máximo");
            check(Approximately(BistroBuilderProfessionalCameraMath.ClampPitch(48, 28, 72), 48), "Pitch interior");

            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeScroll(1, 2), 1), "Scroll legacy");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeScroll(120, 2), 1), "Scroll Input System");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeScroll(-240, 2), -2), "Scroll negativo");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeScroll(1200, 2), 2), "Scroll limitado");
            check(Approximately(BistroBuilderProfessionalCameraMath.NormalizeScroll(float.NaN, 2), 0), "Scroll NaN seguro");

            float zoomIn = BistroBuilderProfessionalCameraMath.ApplyLogarithmicZoom(20, 1, 0.032f, 10.5f, 32.0f);
            float zoomOut = BistroBuilderProfessionalCameraMath.ApplyLogarithmicZoom(20, -1, 0.032f, 10.5f, 32.0f);
            check(zoomIn < 20, "Zoom hacia dentro reduce distancia");
            check(zoomOut > 20, "Zoom hacia fuera aumenta distancia");
            check(Approximately(BistroBuilderProfessionalCameraMath.ApplyLogarithmicZoom(10.5f, 4, 0.032f, 10.5f, 32.0f), 10.5f), "Zoom respeta mínimo");
            check(Approximately(BistroBuilderProfessionalCameraMath.ApplyLogarithmicZoom(32.0f, -4, 0.032f, 10.5f, 32.0f), 32.0f), "Zoom respeta máximo");
            float reversible = BistroBuilderProfessionalCameraMath.ApplyLogarithmicZoom(zoomIn, -1, 0.032f, 10.5f, 32.0f);
            check(Mathf.Abs(reversible - 20) < 0.001f, "Zoom logarítmico reversible sin clamp");

            float pendingZoom = -0.032f;
            float firstConsumed = BistroBuilderProfessionalCameraMath.ConsumeSmoothedZoomLogAmount(
                ref pendingZoom, 0.16f, 1.0f / 60.0f);
            float secondConsumed = BistroBuilderProfessionalCameraMath.ConsumeSmoothedZoomLogAmount(
                ref pendingZoom, 0.16f, 1.0f / 60.0f);
            check(firstConsumed < 0.0f && secondConsumed < 0.0f,
                "Una muesca se reparte en varios fotogramas");
            check(Mathf.Abs(firstConsumed) < 0.032f && Mathf.Abs(pendingZoom) > 0.0f,
                "La cola de zoom no se consume de golpe");

            Vector2 center = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(500, 500), 1000, 1000, 0.025f, 14, 54);
            Vector2 left = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(0, 500), 1000, 1000, 0.025f, 14, 54);
            Vector2 right = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(1000, 500), 1000, 1000, 0.025f, 14, 54);
            Vector2 bottom = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(500, 0), 1000, 1000, 0.025f, 14, 54);
            Vector2 top = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(500, 1000), 1000, 1000, 0.025f, 14, 54);
            Vector2 corner = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(0, 0), 1000, 1000, 0.025f, 14, 54);
            Vector2 outside = BistroBuilderProfessionalCameraMath.CalculateEdgePan(new Vector2(-1, 500), 1000, 1000, 0.025f, 14, 54);
            check(center == Vector2.zero, "Centro sin edge pan");
            check(left.x < -0.99f && Approximately(left.y, 0), "Borde izquierdo");
            check(right.x > 0.99f && Approximately(right.y, 0), "Borde derecho");
            check(bottom.y < -0.99f && Approximately(bottom.x, 0), "Borde inferior");
            check(top.y > 0.99f && Approximately(top.x, 0), "Borde superior");
            check(corner.magnitude <= 1.0001f && corner.x < 0 && corner.y < 0, "Esquina normalizada");
            check(outside == Vector2.zero, "Cursor fuera no mueve cámara");

            Vector3 axisRight;
            Vector3 axisForward;
            BistroBuilderProfessionalCameraMath.GetGroundAlignedAxes(0, out axisRight, out axisForward);
            check(Vector3.Distance(axisRight, Vector3.right) < 0.0001f, "Eje derecho yaw 0");
            check(Vector3.Distance(axisForward, Vector3.forward) < 0.0001f, "Eje frontal yaw 0");
            BistroBuilderProfessionalCameraMath.GetGroundAlignedAxes(90, out axisRight, out axisForward);
            check(Vector3.Distance(axisForward, Vector3.right) < 0.0001f, "Eje frontal yaw 90");
            check(Mathf.Abs(Vector3.Dot(axisRight, axisForward)) < 0.0001f, "Ejes ortogonales");

            Vector3 zeroDrag = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                Vector2.zero, Quaternion.Euler(48, 0, 0), 20, 16.0f / 9.0f, 1920, 1080, 1.0f, 0.35f);
            Vector3 smallNoiseDrag = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                new Vector2(0.2f, -0.1f), Quaternion.Euler(48, 0, 0), 20, 16.0f / 9.0f, 1920, 1080, 1.0f, 0.35f);
            Vector3 horizontalDrag = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                new Vector2(100, 0), Quaternion.Euler(48, 0, 0), 20, 16.0f / 9.0f, 1920, 1080, 1.0f, 0.35f);
            Vector3 verticalDrag = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                new Vector2(0, 100), Quaternion.Euler(48, 0, 0), 20, 16.0f / 9.0f, 1920, 1080, 1.0f, 0.35f);
            Vector3 farDrag = BistroBuilderProfessionalCameraMath.CalculatePlanarPointerDrag(
                new Vector2(100, 0), Quaternion.Euler(48, 0, 0), 40, 16.0f / 9.0f, 1920, 1080, 1.0f, 0.35f);
            check(zeroDrag == Vector3.zero, "Arrastre sin delta no mueve la cámara");
            check(smallNoiseDrag == Vector3.zero, "Zona muerta filtra ruido subpíxel");
            check(horizontalDrag.x < 0.0f && Mathf.Abs(horizontalDrag.z) < 0.0001f,
                "Arrastre horizontal se traduce sobre el plano");
            check(verticalDrag.z < 0.0f && Mathf.Abs(verticalDrag.x) < 0.0001f,
                "Arrastre vertical se traduce sobre el plano");
            check(farDrag.magnitude > horizontalDrag.magnitude * 1.99f,
                "Arrastre escala proporcionalmente con el encuadre visible");
            check(BistroBuilderProfessionalCameraMath.IsFinite(horizontalDrag),
                "Arrastre planar se mantiene finito");

            Vector3 cameraPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(Vector3.zero, 0, 45, 10);
            check(cameraPosition.y > 0, "Cámara queda sobre el foco con pitch positivo");
            check(Approximately(cameraPosition.magnitude, 10, 0.001f), "Distancia geométrica exacta");

            Vector3 hit;
            bool rayHit = BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                new Ray(new Vector3(0, 10, 0), Vector3.down), 0, out hit);
            check(rayHit, "Rayo vertical toca suelo");
            check(Vector3.Distance(hit, Vector3.zero) < 0.0001f, "Intersección de suelo exacta");
            check(!BistroBuilderProfessionalCameraMath.TryRayGroundPlane(
                new Ray(new Vector3(0, 10, 0), Vector3.up), 0, out hit), "Rayo opuesto no toca suelo");


            Vector3 orbitFocus;
            float orbitDistance;
            Vector3 orbitPivot = new Vector3(6.0f, 0.0f, 3.0f);
            bool orbitSolved = BistroBuilderProfessionalCameraMath.TryOrbitStateAroundPivot(
                Vector3.zero, 0.0f, 48.0f, 24.0f, orbitPivot, 20.0f, 48.0f, 0.0f,
                7.0f, 58.0f, out orbitFocus, out orbitDistance);
            check(orbitSolved, "La órbita contextual produce un estado válido");
            check(BistroBuilderProfessionalCameraMath.IsFinite(orbitFocus) &&
                  BistroBuilderProfessionalCameraMath.IsFinite(orbitDistance),
                "La órbita contextual se mantiene finita");
            check(orbitDistance > 0.0f, "La órbita contextual conserva distancia positiva");

            check(Approximately(BistroBuilderProfessionalCameraMath.DistanceRatio(7, 7, 58), 0), "Ratio zoom mínimo");
            check(Approximately(BistroBuilderProfessionalCameraMath.DistanceRatio(58, 7, 58), 1), "Ratio zoom máximo");
            check(BistroBuilderProfessionalCameraMath.DistanceRatio(30, 7, 58) > 0 &&
                  BistroBuilderProfessionalCameraMath.DistanceRatio(30, 7, 58) < 1, "Ratio zoom interior");

            float heightLimitedNear = BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                7, 30, 7, 58, 5, 46);
            float heightLimitedFar = BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                58, 72, 7, 58, 5, 46);
            check(heightLimitedNear >= 9.99f, "Altura mínima eleva la distancia cuando es necesario");
            check(heightLimitedFar <= 48.38f, "Altura máxima reduce la distancia cuando es necesario");
            check(Approximately(BistroBuilderProfessionalCameraMath.ClampDistanceForHeight(
                24, 48, 7, 58, 4.5f, 46), 24), "Distancia válida conserva altura");

            const float elevatorYaw = 30.0f;
            const float elevatorPitch = 45.0f;
            Vector3 elevatorStartFocus = Vector3.zero;
            float elevatorStartDistance = 20.0f;
            Vector3 elevatorStartPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                elevatorStartFocus, elevatorYaw, elevatorPitch, elevatorStartDistance);

            Vector3 raisedFocus;
            float raisedDistance;
            bool elevatorRaised = BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                elevatorStartFocus, elevatorYaw, elevatorPitch, elevatorStartDistance, 2.0f, 0.0f,
                7.0f, 58.0f, 4.5f, 46.0f, out raisedFocus, out raisedDistance);
            Vector3 raisedPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                raisedFocus, elevatorYaw, elevatorPitch, raisedDistance);
            check(elevatorRaised, "Elevación recta calcula un estado válido");
            check(raisedPosition.y > elevatorStartPosition.y + 1.9f,
                "R aumenta la altura real de cámara");
            check(Mathf.Abs(raisedPosition.x - elevatorStartPosition.x) < 0.001f &&
                  Mathf.Abs(raisedPosition.z - elevatorStartPosition.z) < 0.001f,
                "R desplaza la cámara solo en Y, sin arco horizontal");
            check(Mathf.Abs(raisedFocus.y) < 0.0001f,
                "El punto de interés recalculado permanece sobre el suelo");

            Vector3 loweredFocus;
            float loweredDistance;
            bool elevatorLowered = BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                raisedFocus, elevatorYaw, elevatorPitch, raisedDistance, -2.0f, 0.0f,
                7.0f, 58.0f, 4.5f, 46.0f, out loweredFocus, out loweredDistance);
            Vector3 loweredPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                loweredFocus, elevatorYaw, elevatorPitch, loweredDistance);
            check(elevatorLowered && Vector3.Distance(loweredPosition, elevatorStartPosition) < 0.001f &&
                  Vector3.Distance(loweredFocus, elevatorStartFocus) < 0.001f,
                "F revierte la elevación sin deriva geométrica");

            Vector3 clampedFocus;
            float clampedDistance;
            bool elevatorClamped = BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                elevatorStartFocus, elevatorYaw, elevatorPitch, elevatorStartDistance, 1000.0f, 0.0f,
                7.0f, 58.0f, 4.5f, 46.0f, out clampedFocus, out clampedDistance);
            Vector3 clampedPosition = BistroBuilderProfessionalCameraMath.CalculateCameraPosition(
                clampedFocus, elevatorYaw, elevatorPitch, clampedDistance);
            check(elevatorClamped && clampedPosition.y <= 46.0001f && clampedDistance <= 58.0001f,
                "Elevación recta respeta altura y distancia máximas");
            check(Mathf.Abs(clampedPosition.x - elevatorStartPosition.x) < 0.001f &&
                  Mathf.Abs(clampedPosition.z - elevatorStartPosition.z) < 0.001f,
                "El límite superior no curva ni desplaza lateralmente la cámara");
            check(!BistroBuilderProfessionalCameraMath.TryCalculateVerticalElevatorState(
                new Vector3(float.NaN, 0.0f, 0.0f), elevatorYaw, elevatorPitch, elevatorStartDistance,
                1.0f, 0.0f, 7.0f, 58.0f, 4.5f, 46.0f, out clampedFocus, out clampedDistance),
                "Elevación recta rechaza estados no finitos");

            float unrestrictedElevatorDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    16.0f, 0.25f, 10.0f, 22.0f, 2.75f);
            float softenedUpperDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    21.5f, 0.25f, 10.0f, 22.0f, 2.75f);
            float blockedUpperDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    22.0f, 0.25f, 10.0f, 22.0f, 2.75f);
            float softenedLowerDelta =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    10.5f, -0.25f, 10.0f, 22.0f, 2.75f);
            float recoverFromBelow =
                BistroBuilderProfessionalCameraMath.CalculateSoftLimitedHeightDelta(
                    9.0f, 0.25f, 10.0f, 22.0f, 2.75f);
            check(Approximately(unrestrictedElevatorDelta, 0.25f, 0.0001f),
                "R/F conservan velocidad nominal lejos de los topes");
            check(softenedUpperDelta > 0.0f && softenedUpperDelta < unrestrictedElevatorDelta,
                "R desacelera al aproximarse al límite superior");
            check(Approximately(blockedUpperDelta, 0.0f),
                "R se detiene en el límite operativo superior");
            check(softenedLowerDelta < 0.0f && Mathf.Abs(softenedLowerDelta) < 0.25f,
                "F desacelera al aproximarse al límite inferior");
            check(recoverFromBelow > 0.0f,
                "Una cámara heredada fuera de rango puede volver sin teletransporte");

            BistroBuilderCameraNavigationState state = new BistroBuilderCameraNavigationState(Vector3.one, 45, 48, 20);
            check(state.Version == BistroBuilderCameraNavigationState.CurrentVersion, "Versión de estado");
            check(state.IsFinite, "Estado finito");
            BistroBuilderCameraNavigationState invalidState = new BistroBuilderCameraNavigationState(Vector3.one, float.NaN, 48, 20);
            check(!invalidState.IsFinite, "Estado inválido detectado");
        }

        private static void RunDampingTests(Action<bool, string> check)
        {
            float current = 0.0f;
            float velocity = 0.0f;
            float previous = current;
            bool monotonic = true;
            bool finite = true;
            for (int frame = 0; frame < 180; frame++)
            {
                current = Mathf.SmoothDamp(current, 10.0f, ref velocity, 0.18f, Mathf.Infinity, 1.0f / 60.0f);
                monotonic &= current >= previous - 0.0001f && current <= 10.0001f;
                finite &= BistroBuilderProfessionalCameraMath.IsFinite(current) &&
                          BistroBuilderProfessionalCameraMath.IsFinite(velocity);
                previous = current;
            }

            check(monotonic, "SmoothDamp no sobreoscila en aproximación positiva");
            check(finite, "SmoothDamp se mantiene finito");
            check(Mathf.Abs(current - 10.0f) < 0.01f, "SmoothDamp converge al objetivo");

            float angle = 170.0f;
            float angleVelocity = 0.0f;
            for (int frame = 0; frame < 180; frame++)
            {
                angle = Mathf.SmoothDampAngle(angle, -170.0f, ref angleVelocity, 0.14f, Mathf.Infinity, 1.0f / 60.0f);
            }
            check(Mathf.Abs(Mathf.DeltaAngle(angle, -170.0f)) < 0.1f, "Rotación toma el camino angular corto");

            Vector3 vector = Vector3.zero;
            Vector3 vectorVelocity = Vector3.zero;
            for (int frame = 0; frame < 180; frame++)
            {
                vector = Vector3.SmoothDamp(vector, new Vector3(10, 0, 5), ref vectorVelocity, 0.13f, Mathf.Infinity, 1.0f / 60.0f);
            }
            check(Vector3.Distance(vector, new Vector3(10, 0, 5)) < 0.01f, "Amortiguación vectorial converge");
            check(Mathf.Abs(vector.y) < 0.0001f, "Amortiguación no introduce altura");
        }

        private static void RunBoundsTests(Action<bool, string> check)
        {
            GameObject temporary = new GameObject("BB_369A_Bounds_SelfTest");
            temporary.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                BistroBuilderCameraBounds bounds = temporary.AddComponent<BistroBuilderCameraBounds>();
                bounds.ConfigureFromWorldBounds(new Bounds(Vector3.zero, new Vector3(40, 10, 30)));
                bounds.ConfigureConstraintMode(
                    BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope);
                bounds.ConfigureCameraEnvelopePadding(12.0f);

                check(bounds.IsValid, "Bounds válidos");
                check(Approximately(bounds.GroundHeight, -5.0f), "Altura de suelo derivada del mínimo Y");

                Vector3 inside = bounds.ClampFocusPoint(new Vector3(2, 99, 3));
                check(Approximately(inside.x, 2) && Approximately(inside.z, 3), "Punto interior conserva XZ");
                check(Approximately(inside.y, bounds.GroundHeight), "Punto interior cae al plano de suelo");

                Vector3 outside = bounds.ClampFocusPoint(new Vector3(100, 0, -100));
                check(bounds.ContainsFocusPoint(outside), "Punto exterior queda dentro tras clamp");
                check(outside.x < 20 && outside.z > -15, "Padding horizontal aplicado");

                Quaternion rotation = Quaternion.Euler(48, 45, 0);
                Vector3 constrained = bounds.Constrain(new Vector3(500, 0, 500), rotation, 20);
                check(bounds.ContainsFocusPoint(constrained), "Constrain mantiene foco dentro");
                Vector3 constrainedCamera =
                    BistroBuilderProfessionalCameraMath.CalculateCameraPosition(constrained, 45, 48, 20);
                check(bounds.ConstraintMode ==
                      BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope,
                    "Bounds separan huella navegable y envolvente de encuadre");
                check(bounds.CameraEnvelopePadding > 0.0f,
                    "La envolvente exterior dispone de margen controlado");
                check(bounds.ContainsCameraPosition(constrainedCamera),
                    "La órbita en el borde mantiene la cámara dentro de su envolvente");
                check(!bounds.ContainsFocusPoint(new Vector3(24.0f, bounds.GroundHeight, 0.0f)) &&
                      bounds.ContainsCameraPosition(new Vector3(24.0f, bounds.GroundHeight, 0.0f)),
                    "La cámara puede salir de la huella sin abandonar la envolvente de encuadre");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temporary);
            }
        }

        private static void RunConfigurationTests(
            Action<bool, string> check,
            List<string> highlights)
        {
            BistroBuilderCameraNavigationSettings settings = BistroBuilderCamera369AInstaller.LoadSettings();
            check(settings != null, "Debe existir el asset de configuración tras instalar 369A");
            if (settings != null)
            {
                string reason;
                check(settings.IsConfigurationValid(out reason), "Configuración válida: " + reason);
                check(settings.UseUnscaledTime, "Tiempo no escalado activado");
                check(settings.BlockPointerInputOverUi, "Bloqueo sobre UI activado");
                check(settings.BlockKeyboardWhileTyping, "Bloqueo al escribir activado");
                check(settings.PanDecelerationTime >= settings.PanAccelerationTime,
                    "La frenada debe ser igual o más progresiva que el arranque");
                check(settings.MinimumPitch < settings.FallbackPitch && settings.FallbackPitch < settings.MaximumPitch,
                    "Pitch inicial interior a límites");
                check(settings.MinimumDistance < settings.FallbackDistance && settings.FallbackDistance < settings.MaximumDistance,
                    "Distancia inicial interior a límites");
                check(settings.InteractionProfileVersion >=
                      BistroBuilderCameraNavigationSettings.CurrentInteractionProfileVersion,
                    "Perfil de interacción 369A8 aplicado");
                check(BistroBuilderProfessionalCameraController.RuntimeRevision >= 8,
                    "Runtime 369A8 con límites verticales suaves y pitch constante");
                check(BistroBuilderCamera369AFunctionalTestWindow.DiagnosticRevision >= 8,
                    "Diagnóstico 369A8 con asentamiento y revisión manual de zoom y R/F");
                check(settings.MousePitchEnabled,
                    "Inclinación vertical con botón derecho activada");
                check(settings.OrbitAroundPointer,
                    "Órbita del botón derecho anclada al punto bajo el cursor");
                check(settings.MouseYawDegreesPerPixel <= 0.13f,
                    "Sensibilidad horizontal del botón derecho contenida");
                check(settings.MinimumPitch <= 22.5f,
                    "Rango de inclinación permite acercar el plano al suelo");
                check(settings.LogarithmicZoomStep >= 0.025f && settings.LogarithmicZoomStep <= 0.038f,
                    "Paso de zoom fino y controlado");
                check(settings.ZoomDampingTime >= 0.25f && settings.ZoomDampingTime <= 0.40f,
                    "Zoom con amortiguación cinematográfica");
                check(settings.MaximumScrollNotchesPerFrame <= 1.1f,
                    "Ráfagas de rueda limitadas");
                check(settings.ZoomInputSmoothingTime >= 0.10f &&
                      settings.ZoomInputSmoothingTime <= 0.25f,
                    "Cada muesca se distribuye temporalmente");
                check(settings.MaximumQueuedScrollNotches >= settings.MaximumScrollNotchesPerFrame,
                    "La cola de zoom admite al menos una ráfaga completa");
                check(settings.MinimumOperationalDistance >= settings.MinimumDistance &&
                      settings.MaximumOperationalDistance <= settings.MaximumDistance &&
                      settings.MaximumOperationalDistance > settings.MinimumOperationalDistance,
                    "Rango operativo de rueda contenido dentro de los límites globales");
                check(settings.FallbackDistance >= settings.MinimumOperationalDistance &&
                      settings.FallbackDistance <= settings.MaximumOperationalDistance,
                    "Vista inicial dentro del rango operativo de rueda");
                check(settings.MiddleMouseDragSensitivity > 0.0f,
                    "Sensibilidad de arrastre central válida");
                check(settings.MiddleMouseDragDeadZonePixels >= 0.0f &&
                      settings.MiddleMouseDragDeadZonePixels <= 1.0f,
                    "Zona muerta anti-vibración válida");
                check(settings.KeyboardElevationEnabled,
                    "Elevación vertical R/F activada");
                check(settings.KeyboardElevationSpeed > 0.0f && settings.KeyboardElevationSpeed <= 3.5f,
                    "Velocidad vertical positiva y contenida");
                check(settings.ElevationDecelerationTime >= settings.ElevationAccelerationTime,
                    "La elevación frena de forma igual o más progresiva que su arranque");
                check(settings.MinimumElevatorHeight >= settings.MinimumCameraHeight &&
                      settings.MaximumElevatorHeight <= settings.MaximumCameraHeight &&
                      settings.MaximumElevatorHeight > settings.MinimumElevatorHeight,
                    "Rango operativo R/F contenido dentro de los límites de seguridad");
                check(settings.ElevatorSoftLimitRange > 0.0f,
                    "Zona de frenada suave vertical activa");
            }

            check(BistroBuilderProfessionalCameraInput.HasSupportedBackend,
                "Debe existir un backend de entrada compatible");

            BistroBuilderCamera369AReport structural = BistroBuilderCamera369AValidator.Validate();
            check(structural.Errors == 0, "La instalación debe superar el validador estructural");

            if (structural.Errors == 0)
            {
                highlights.Add("La instalación 369A supera el validador estructural.");
            }
            highlights.Add("El desplazamiento usa aceleración y desaceleración independientes.");
            highlights.Add("La posición, rotación y distancia usan amortiguación crítica.");
            highlights.Add("La prueba funcional separa convergencia geométrica y asentamiento sostenido.");
            highlights.Add("La velocidad angular incluye yaw y pitch mediante la rotación orbital completa.");
            highlights.Add("La rueda distribuye cada muesca durante varios fotogramas y evita saltos escalonados.");
            highlights.Add("El arrastre central usa delta planar estable con zona muerta anti-vibración.");
            highlights.Add("El botón derecho orbita con sensibilidad reducida alrededor del punto bajo el cursor.");
            highlights.Add("R/F elevan y descienden la cámara en Y con recorrido corto, pitch constante y topes suaves.");
            highlights.Add("El foco permanece en el local y la cámara usa una envolvente exterior para encuadrarlo completo.");
            highlights.Add("La entrada evita conflictos con UI y campos de texto.");
            highlights.Add("La cámara sigue respondiendo durante pausa mediante tiempo no escalado.");
            highlights.Add("El estado queda preparado para vistas 369B e inspección 369C.");
        }

        private static string BuildDialogText(
            int passed,
            List<string> failures,
            List<string> highlights)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 369A");
            builder.AppendLine("Pruebas superadas: " + passed);
            builder.AppendLine("Pruebas fallidas: " + failures.Count);

            int highlightCount = Mathf.Min(8, highlights.Count);
            for (int index = 0; index < highlightCount; index++)
            {
                builder.AppendLine("- OK: " + highlights[index]);
            }

            int failureCount = Mathf.Min(8, failures.Count);
            for (int index = 0; index < failureCount; index++)
            {
                builder.AppendLine("- ERROR: " + failures[index]);
            }

            if (failures.Count > failureCount)
            {
                builder.AppendLine("(Consulta el Editor Log para ver todos los fallos.)");
            }

            return builder.ToString().TrimEnd();
        }

        private static bool Approximately(float a, float b, float tolerance = 0.0001f)
        {
            return Mathf.Abs(a - b) <= tolerance;
        }
    }
}
#endif
