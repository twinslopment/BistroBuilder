using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Funciones puras del sistema de cámara. Mantenerlas separadas permite autotests deterministas
    /// sin depender de una escena ni de dispositivos de entrada.
    /// </summary>
    public static class BistroBuilderProfessionalCameraMath
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static float NormalizeSignedAngle(float angle)
        {
            angle %= 360.0f;
            if (angle > 180.0f)
            {
                angle -= 360.0f;
            }
            else if (angle < -180.0f)
            {
                angle += 360.0f;
            }

            return angle;
        }

        public static float ClampPitch(float pitch, float minimumPitch, float maximumPitch)
        {
            return Mathf.Clamp(NormalizeSignedAngle(pitch), minimumPitch, maximumPitch);
        }

        /// <summary>
        /// Convierte las distintas escalas de rueda de ratón (legacy suele dar 1; Input System
        /// puede dar 120 píxeles por muesca en Windows) a una unidad perceptual común.
        /// </summary>
        public static float NormalizeScroll(float rawScroll, float maximumNotchesPerFrame)
        {
            if (!IsFinite(rawScroll))
            {
                return 0.0f;
            }

            float normalized = Mathf.Abs(rawScroll) > 10.0f ? rawScroll / 120.0f : rawScroll;
            return Mathf.Clamp(normalized, -maximumNotchesPerFrame, maximumNotchesPerFrame);
        }

        /// <summary>
        /// Devuelve la velocidad de dolly de la rueda para la distancia actual. Usa la misma
        /// progresión por escala que WASD: velocidad contenida cerca y más rápida cuando el
        /// encuadre es amplio, sin convertir cada muesca en un salto de distancia.
        /// </summary>
        public static float CalculateContinuousZoomSpeed(
            float currentDistance,
            float minimumOperationalDistance,
            float maximumOperationalDistance,
            float speedNear,
            float speedFar)
        {
            if (!IsFinite(currentDistance) ||
                !IsFinite(minimumOperationalDistance) ||
                !IsFinite(maximumOperationalDistance) ||
                !IsFinite(speedNear) ||
                !IsFinite(speedFar) ||
                maximumOperationalDistance <= minimumOperationalDistance ||
                speedNear <= 0.0f ||
                speedFar < speedNear)
            {
                return 0.0f;
            }

            float ratio = DistanceRatio(
                currentDistance,
                minimumOperationalDistance,
                maximumOperationalDistance);
            return Mathf.Lerp(speedNear, speedFar, ratio);
        }

        /// <summary>
        /// Zoom multiplicativo: cada muesca representa el mismo cambio perceptual en cualquier escala.
        /// Evita que el zoom sea excesivo cerca y lento cuando la cámara está lejos.
        /// </summary>
        public static float ApplyLogarithmicZoom(
            float currentTargetDistance,
            float normalizedScroll,
            float logarithmicStep,
            float minimumDistance,
            float maximumDistance)
        {
            float distance = Mathf.Max(0.0001f, currentTargetDistance);
            float next = distance * Mathf.Exp(-normalizedScroll * logarithmicStep);
            return Mathf.Clamp(next, minimumDistance, maximumDistance);
        }

        /// <summary>
        /// Consume una fracción exponencial de una orden de zoom pendiente. La rueda puede entregar
        /// impulsos discretos; distribuir cada impulso durante varios fotogramas evita el aspecto
        /// escalonado sin introducir una cola lineal dependiente del framerate.
        /// </summary>
        public static float ConsumeSmoothedZoomLogAmount(
            ref float pendingLogAmount,
            float smoothingTime,
            float deltaTime)
        {
            if (!IsFinite(pendingLogAmount) || !IsFinite(smoothingTime) || !IsFinite(deltaTime) ||
                smoothingTime <= 0.0f || deltaTime <= 0.0f)
            {
                pendingLogAmount = 0.0f;
                return 0.0f;
            }

            float consumeRatio = 1.0f - Mathf.Exp(-deltaTime / smoothingTime);
            float consumed = pendingLogAmount * Mathf.Clamp01(consumeRatio);
            pendingLogAmount -= consumed;
            if (Mathf.Abs(pendingLogAmount) <= 0.000001f)
            {
                pendingLogAmount = 0.0f;
            }

            return consumed;
        }

        /// <summary>
        /// Orbita una pose alrededor de un pivote del mundo aplicando la misma rotación a la posición
        /// física de cámara y a su orientación. 369A11 conserva la distancia al punto de mirada y no
        /// vuelve a proyectar el foco sobre el suelo; así Q/E y el botón derecho no recentran la vista
        /// ni reintroducen la dependencia entre altura, distancia y límites horizontales.
        /// </summary>
        public static bool TryOrbitStateAroundPivot(
            Vector3 currentFocusPoint,
            float currentYawDegrees,
            float currentPitchDegrees,
            float currentDistance,
            Vector3 worldPivot,
            float nextYawDegrees,
            float nextPitchDegrees,
            float groundHeight,
            float minimumDistance,
            float maximumDistance,
            out Vector3 resultFocusPoint,
            out float resultDistance)
        {
            resultFocusPoint = currentFocusPoint;
            resultDistance = currentDistance;

            if (!IsFinite(currentFocusPoint) || !IsFinite(currentYawDegrees) ||
                !IsFinite(currentPitchDegrees) || !IsFinite(currentDistance) ||
                !IsFinite(worldPivot) || !IsFinite(nextYawDegrees) ||
                !IsFinite(nextPitchDegrees) || !IsFinite(groundHeight) ||
                currentDistance <= 0.0f || minimumDistance <= 0.0f ||
                maximumDistance < minimumDistance)
            {
                return false;
            }

            Quaternion currentRotation = Quaternion.Euler(
                currentPitchDegrees,
                currentYawDegrees,
                0.0f);
            Quaternion nextRotation = Quaternion.Euler(
                nextPitchDegrees,
                nextYawDegrees,
                0.0f);
            Vector3 currentCameraPosition = CalculateCameraPosition(
                currentFocusPoint,
                currentYawDegrees,
                currentPitchDegrees,
                currentDistance);
            Quaternion rotationDelta = nextRotation * Quaternion.Inverse(currentRotation);
            Vector3 nextCameraPosition =
                worldPivot + rotationDelta * (currentCameraPosition - worldPivot);
            float safeDistance = Mathf.Clamp(currentDistance, minimumDistance, maximumDistance);
            Vector3 nextFocusPoint =
                nextCameraPosition + nextRotation * Vector3.forward * safeDistance;

            if (!IsFinite(nextCameraPosition) || !IsFinite(nextFocusPoint))
            {
                return false;
            }

            resultFocusPoint = nextFocusPoint;
            resultDistance = safeDistance;
            return true;
        }

        public static Vector2 CalculateEdgePan(
            Vector2 pointerPosition,
            int screenWidth,
            int screenHeight,
            float marginNormalized,
            int minimumMarginPixels,
            int maximumMarginPixels)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return Vector2.zero;
            }

            if (pointerPosition.x < 0.0f || pointerPosition.y < 0.0f ||
                pointerPosition.x > screenWidth || pointerPosition.y > screenHeight)
            {
                return Vector2.zero;
            }

            float reference = Mathf.Min(screenWidth, screenHeight);
            float margin = Mathf.Clamp(
                reference * marginNormalized,
                minimumMarginPixels,
                maximumMarginPixels);

            float horizontal = 0.0f;
            float vertical = 0.0f;

            if (pointerPosition.x < margin)
            {
                horizontal = -Mathf.Clamp01((margin - pointerPosition.x) / margin);
            }
            else if (pointerPosition.x > screenWidth - margin)
            {
                horizontal = Mathf.Clamp01((pointerPosition.x - (screenWidth - margin)) / margin);
            }

            if (pointerPosition.y < margin)
            {
                vertical = -Mathf.Clamp01((margin - pointerPosition.y) / margin);
            }
            else if (pointerPosition.y > screenHeight - margin)
            {
                vertical = Mathf.Clamp01((pointerPosition.y - (screenHeight - margin)) / margin);
            }

            Vector2 result = new Vector2(horizontal, vertical);
            return result.sqrMagnitude > 1.0f ? result.normalized : result;
        }

        public static void GetGroundAlignedAxes(float yaw, out Vector3 right, out Vector3 forward)
        {
            Quaternion yawRotation = Quaternion.Euler(0.0f, yaw, 0.0f);
            right = yawRotation * Vector3.right;
            forward = yawRotation * Vector3.forward;
            right.y = 0.0f;
            forward.y = 0.0f;
            right.Normalize();
            forward.Normalize();
        }

        public static Vector3 CalculateCameraPosition(
            Vector3 focusPoint,
            float yaw,
            float pitch,
            float distance)
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0.0f);
            return focusPoint - rotation * Vector3.forward * distance;
        }

        /// <summary>
        /// Convierte el delta del puntero en un desplazamiento estable sobre el plano horizontal.
        /// La escala se deriva del encuadre visible, por lo que el arrastre conserva una sensación
        /// coherente con cámaras cercanas, lejanas, en perspectiva u ortográficas. Al no volver a
        /// proyectar contra una cámara que todavía está amortiguándose, evita el bucle de realimentación
        /// que puede producir vibración al mantener pulsada la rueda sin mover el ratón.
        /// </summary>
        public static Vector3 CalculatePlanarPointerDrag(
            Vector2 pointerDelta,
            Quaternion cameraRotation,
            float visibleVerticalSpan,
            float aspect,
            int screenWidth,
            int screenHeight,
            float sensitivity,
            float deadZonePixels)
        {
            if (!IsFinite(pointerDelta.x) || !IsFinite(pointerDelta.y) ||
                !IsFinite(visibleVerticalSpan) || !IsFinite(aspect) ||
                !IsFinite(sensitivity) || !IsFinite(deadZonePixels) ||
                screenWidth <= 0 || screenHeight <= 0 || visibleVerticalSpan <= 0.0f ||
                aspect <= 0.0f || sensitivity <= 0.0f)
            {
                return Vector3.zero;
            }

            float safeDeadZone = Mathf.Max(0.0f, deadZonePixels);
            if (pointerDelta.sqrMagnitude <= safeDeadZone * safeDeadZone)
            {
                return Vector3.zero;
            }

            Vector3 groundRight = Vector3.ProjectOnPlane(cameraRotation * Vector3.right, Vector3.up);
            Vector3 groundUp = Vector3.ProjectOnPlane(cameraRotation * Vector3.up, Vector3.up);
            if (groundRight.sqrMagnitude <= 0.000001f || groundUp.sqrMagnitude <= 0.000001f)
            {
                return Vector3.zero;
            }

            groundRight.Normalize();
            groundUp.Normalize();

            float worldUnitsPerPixelY = visibleVerticalSpan / screenHeight;
            float worldUnitsPerPixelX = visibleVerticalSpan * aspect / screenWidth;
            Vector3 worldDelta =
                -groundRight * pointerDelta.x * worldUnitsPerPixelX -
                groundUp * pointerDelta.y * worldUnitsPerPixelY;
            worldDelta *= sensitivity;
            worldDelta.y = 0.0f;
            return IsFinite(worldDelta) ? worldDelta : Vector3.zero;
        }

        public static bool TryRayGroundPlane(
            Ray ray,
            float groundHeight,
            out Vector3 worldPoint)
        {
            Plane plane = new Plane(Vector3.up, new Vector3(0.0f, groundHeight, 0.0f));
            float enter;
            if (plane.Raycast(ray, out enter) && enter >= 0.0f)
            {
                worldPoint = ray.GetPoint(enter);
                return IsFinite(worldPoint);
            }

            worldPoint = default(Vector3);
            return false;
        }


        public static float ClampDistanceForHeight(
            float distance,
            float pitchDegrees,
            float minimumDistance,
            float maximumDistance,
            float minimumHeight,
            float maximumHeight)
        {
            float sine = Mathf.Sin(Mathf.Deg2Rad * Mathf.Clamp(pitchDegrees, 0.1f, 89.9f));
            sine = Mathf.Max(0.0001f, sine);

            float minimumByHeight = minimumHeight / sine;
            float maximumByHeight = maximumHeight / sine;
            float effectiveMinimum = Mathf.Max(minimumDistance, minimumByHeight);
            float effectiveMaximum = Mathf.Min(maximumDistance, maximumByHeight);

            if (effectiveMaximum < effectiveMinimum)
            {
                return Mathf.Clamp(distance, minimumDistance, maximumDistance);
            }

            return Mathf.Clamp(distance, effectiveMinimum, effectiveMaximum);
        }

        /// <summary>
        /// Limita y suaviza un desplazamiento vertical al aproximarse a los topes operativos de R/F.
        /// No modifica la altura directamente: devuelve el delta seguro que puede aplicarse al estado
        /// orbital. Si la cámara parte fuera del rango operativo, solo permite volver hacia él sin saltos.
        /// </summary>
        public static float CalculateSoftLimitedHeightDelta(
            float currentHeightAboveGround,
            float requestedHeightDelta,
            float minimumOperationalHeight,
            float maximumOperationalHeight,
            float softLimitRange)
        {
            if (!IsFinite(currentHeightAboveGround) ||
                !IsFinite(requestedHeightDelta) ||
                !IsFinite(minimumOperationalHeight) ||
                !IsFinite(maximumOperationalHeight) ||
                !IsFinite(softLimitRange) ||
                maximumOperationalHeight <= minimumOperationalHeight ||
                Mathf.Abs(requestedHeightDelta) <= 0.000001f)
            {
                return 0.0f;
            }

            bool movingUp = requestedHeightDelta > 0.0f;

            // Si una configuración anterior dejó la cámara fuera del nuevo rango, no la teletransportamos.
            // R/F solo puede conducirla de nuevo hacia el intervalo operativo.
            if (currentHeightAboveGround < minimumOperationalHeight)
            {
                if (!movingUp)
                {
                    return 0.0f;
                }

                return Mathf.Min(requestedHeightDelta,
                    minimumOperationalHeight - currentHeightAboveGround);
            }

            if (currentHeightAboveGround > maximumOperationalHeight)
            {
                if (movingUp)
                {
                    return 0.0f;
                }

                return -Mathf.Min(-requestedHeightDelta,
                    currentHeightAboveGround - maximumOperationalHeight);
            }

            float remaining = movingUp
                ? maximumOperationalHeight - currentHeightAboveGround
                : currentHeightAboveGround - minimumOperationalHeight;
            if (remaining <= 0.000001f)
            {
                return 0.0f;
            }

            float requestedMagnitude = Mathf.Min(Mathf.Abs(requestedHeightDelta), remaining);
            float safeSoftRange = Mathf.Max(0.0f, softLimitRange);
            if (safeSoftRange <= 0.0001f)
            {
                return movingUp ? requestedMagnitude : -requestedMagnitude;
            }

            float normalizedRemaining = Mathf.Clamp01(remaining / safeSoftRange);
            float smoothWeight = normalizedRemaining * normalizedRemaining *
                                 (3.0f - 2.0f * normalizedRemaining);
            // Conserva un pequeño avance mínimo para alcanzar el límite sin quedarse asintóticamente
            // bloqueada, pero reduce de forma clara la velocidad durante los últimos metros.
            float boundaryWeight = Mathf.Lerp(0.12f, 1.0f, smoothWeight);
            float softenedMagnitude = Mathf.Min(remaining, requestedMagnitude * boundaryWeight);
            return movingUp ? softenedMagnitude : -softenedMagnitude;
        }

        /// <summary>
        /// Calcula una elevación vertical recta de la cámara. La posición objetivo cambia únicamente
        /// en Y y conserva yaw y pitch. Para mantener el estado orbital coherente, el punto de interés
        /// se desliza sobre el plano del suelo y la distancia se recalcula sin inclinar la cámara.
        /// </summary>
        public static bool TryCalculateVerticalElevatorState(
            Vector3 currentFocusPoint,
            float currentYawDegrees,
            float currentPitchDegrees,
            float currentDistance,
            float requestedHeightDelta,
            float groundHeight,
            float minimumDistance,
            float maximumDistance,
            float minimumHeight,
            float maximumHeight,
            out Vector3 resultFocusPoint,
            out float resultDistance)
        {
            resultFocusPoint = currentFocusPoint;
            resultDistance = currentDistance;

            if (!IsFinite(currentFocusPoint) || !IsFinite(currentYawDegrees) ||
                !IsFinite(currentPitchDegrees) || !IsFinite(currentDistance) ||
                !IsFinite(requestedHeightDelta) || !IsFinite(groundHeight) ||
                currentDistance <= 0.0f || minimumDistance <= 0.0f ||
                maximumDistance < minimumDistance || maximumHeight < minimumHeight)
            {
                return false;
            }

            Quaternion rotation = Quaternion.Euler(
                currentPitchDegrees,
                currentYawDegrees,
                0.0f);
            Vector3 forward = rotation * Vector3.forward;
            float downwardComponent = -forward.y;
            if (!IsFinite(forward) || downwardComponent <= 0.0001f)
            {
                return false;
            }

            Vector3 currentCameraPosition =
                currentFocusPoint - forward * currentDistance;
            float currentHeightAboveGround = currentCameraPosition.y - groundHeight;

            float minimumHeightByDistance = downwardComponent * minimumDistance;
            float maximumHeightByDistance = downwardComponent * maximumDistance;
            float effectiveMinimumHeight = Mathf.Max(minimumHeight, minimumHeightByDistance);
            float effectiveMaximumHeight = Mathf.Min(maximumHeight, maximumHeightByDistance);

            if (!IsFinite(currentCameraPosition) ||
                !IsFinite(currentHeightAboveGround) ||
                !IsFinite(effectiveMinimumHeight) ||
                !IsFinite(effectiveMaximumHeight) ||
                effectiveMaximumHeight + 0.0001f < effectiveMinimumHeight)
            {
                return false;
            }

            float desiredHeightAboveGround = Mathf.Clamp(
                currentHeightAboveGround + requestedHeightDelta,
                effectiveMinimumHeight,
                effectiveMaximumHeight);
            float nextDistance = desiredHeightAboveGround / downwardComponent;

            Vector3 nextCameraPosition = currentCameraPosition;
            nextCameraPosition.y = groundHeight + desiredHeightAboveGround;
            Vector3 nextFocusPoint = nextCameraPosition + forward * nextDistance;
            nextFocusPoint.y = groundHeight;

            if (!IsFinite(nextFocusPoint) || !IsFinite(nextDistance))
            {
                return false;
            }

            resultFocusPoint = nextFocusPoint;
            resultDistance = Mathf.Clamp(nextDistance, minimumDistance, maximumDistance);
            return true;
        }

        public static float DistanceRatio(float distance, float minimumDistance, float maximumDistance)
        {
            if (maximumDistance <= minimumDistance)
            {
                return 0.0f;
            }

            return Mathf.Clamp01((distance - minimumDistance) / (maximumDistance - minimumDistance));
        }
    }
}
