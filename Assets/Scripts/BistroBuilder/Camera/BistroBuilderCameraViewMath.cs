using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Matemática de encuadre independiente de escena para 369B.
    /// </summary>
    public static class BistroBuilderCameraViewMath
    {
        public static bool TryCalculateDistanceToFit(
            UnityEngine.Camera camera,
            Vector3 focusPoint,
            Quaternion rotation,
            Vector3[] worldPoints,
            float framingMargin,
            float currentDistance,
            float minimumDistance,
            float maximumDistance,
            out float distance)
        {
            distance = 0.0f;
            if (camera == null || worldPoints == null || worldPoints.Length == 0 ||
                framingMargin < 1.0f || minimumDistance <= 0.0f ||
                maximumDistance <= minimumDistance)
            {
                return false;
            }

            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            float aspect = Mathf.Max(0.01f, camera.aspect);

            if (camera.orthographic)
            {
                float requiredHalfHeight = 0.0f;
                for (int index = 0; index < worldPoints.Length; index++)
                {
                    Vector3 local = inverseRotation * (worldPoints[index] - focusPoint);
                    if (!BistroBuilderProfessionalCameraMath.IsFinite(local))
                    {
                        return false;
                    }

                    requiredHalfHeight = Mathf.Max(
                        requiredHalfHeight,
                        Mathf.Abs(local.y),
                        Mathf.Abs(local.x) / aspect);
                }

                requiredHalfHeight *= framingMargin;
                float sizePerDistance = camera.orthographicSize /
                                        Mathf.Max(0.01f, currentDistance);
                if (sizePerDistance <= 0.0001f)
                {
                    return false;
                }

                distance = Mathf.Clamp(
                    requiredHalfHeight / sizePerDistance,
                    minimumDistance,
                    maximumDistance);
                return BistroBuilderProfessionalCameraMath.IsFinite(distance);
            }

            float verticalHalfAngle = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float tanVertical = Mathf.Tan(verticalHalfAngle);
            float tanHorizontal = tanVertical * aspect;
            if (tanVertical <= 0.0001f || tanHorizontal <= 0.0001f)
            {
                return false;
            }

            float requiredDistance = minimumDistance;
            float nearSafety = Mathf.Max(0.05f, camera.nearClipPlane + 0.05f);
            for (int index = 0; index < worldPoints.Length; index++)
            {
                Vector3 local = inverseRotation * (worldPoints[index] - focusPoint);
                if (!BistroBuilderProfessionalCameraMath.IsFinite(local))
                {
                    return false;
                }

                float horizontalRequirement =
                    Mathf.Abs(local.x) * framingMargin / tanHorizontal - local.z;
                float verticalRequirement =
                    Mathf.Abs(local.y) * framingMargin / tanVertical - local.z;
                float nearRequirement = nearSafety - local.z;

                requiredDistance = Mathf.Max(
                    requiredDistance,
                    horizontalRequirement,
                    verticalRequirement,
                    nearRequirement);
            }

            distance = Mathf.Clamp(requiredDistance, minimumDistance, maximumDistance);
            return BistroBuilderProfessionalCameraMath.IsFinite(distance);
        }

        public static bool ArePointsInsideViewport(
            UnityEngine.Camera camera,
            Vector3[] worldPoints,
            float viewportPadding)
        {
            if (camera == null || worldPoints == null || worldPoints.Length == 0)
            {
                return false;
            }

            float minimum = Mathf.Clamp(viewportPadding, 0.0f, 0.49f);
            float maximum = 1.0f - minimum;
            for (int index = 0; index < worldPoints.Length; index++)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldPoints[index]);
                if (!BistroBuilderProfessionalCameraMath.IsFinite(viewport) ||
                    viewport.z <= 0.0f ||
                    viewport.x < minimum || viewport.x > maximum ||
                    viewport.y < minimum || viewport.y > maximum)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
