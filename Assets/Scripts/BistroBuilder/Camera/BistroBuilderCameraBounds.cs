using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    public enum BistroBuilderCameraBoundsConstraintMode
    {
        FocusPointOnly = 0,
        CameraAndFocusPoint = 1,
        FocusPointAndFramingEnvelope = 2
    }

    /// <summary>
    /// Define dos zonas relacionadas, pero no idénticas:
    /// - La huella navegable: el punto observado siempre permanece dentro del restaurante.
    /// - La envolvente de encuadre: la cámara puede situarse un margen controlado fuera de la
    ///   huella para mostrar el local completo sin atravesar un límite exterior absurdo.
    ///
    /// Separar ambas zonas evita el conflicto entre "no abandonar el restaurante" y "poder
    /// encuadrarlo entero". Las coordenadas locales permiten mover o rotar el restaurante sin
    /// recalcular manualmente los límites.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BistroBuilderCameraBounds : MonoBehaviour
    {
        [SerializeField] private Vector3 localCenter = new Vector3(0.0f, 15.0f, 0.0f);
        [SerializeField] private Vector3 localSize = new Vector3(80.0f, 30.0f, 80.0f);
        [SerializeField] private float localGroundHeight = 0.0f;
        [Min(0.0f)]
        [SerializeField] private float horizontalPadding = 1.25f;
        [Min(0.0f)]
        [SerializeField] private float cameraEnvelopePadding = 18.0f;
        [SerializeField] private BistroBuilderCameraBoundsConstraintMode constraintMode =
            BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope;
        [SerializeField] private bool drawGizmo = true;

        public Vector3 LocalCenter { get { return localCenter; } }
        public Vector3 LocalSize { get { return localSize; } }
        public float HorizontalPadding { get { return horizontalPadding; } }
        public float CameraEnvelopePadding { get { return cameraEnvelopePadding; } }
        public float LocalGroundHeight { get { return localGroundHeight; } }
        public BistroBuilderCameraBoundsConstraintMode ConstraintMode { get { return constraintMode; } }

        public bool IsValid
        {
            get { return localSize.x > 0.01f && localSize.z > 0.01f; }
        }

        public float GroundHeight
        {
            get
            {
                return transform.TransformPoint(
                    new Vector3(localCenter.x, localGroundHeight, localCenter.z)).y;
            }
        }

        public Vector3 ClampFocusPoint(Vector3 worldPoint)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float minimumX;
            float maximumX;
            float minimumZ;
            float maximumZ;
            GetFocusHorizontalLimits(out minimumX, out maximumX, out minimumZ, out maximumZ);

            localPoint.x = Mathf.Clamp(localPoint.x, minimumX, maximumX);
            localPoint.z = Mathf.Clamp(localPoint.z, minimumZ, maximumZ);
            localPoint.y = localGroundHeight;
            return transform.TransformPoint(localPoint);
        }

        /// <summary>
        /// Limita el foco a la huella navegable. En los modos que también protegen la cámara,
        /// calcula el intervalo común en el que el foco sigue dentro y la posición física de la
        /// cámara permanece dentro de su zona permitida.
        /// </summary>
        public Vector3 Constrain(
            Vector3 desiredFocusPoint,
            Quaternion cameraRotation,
            float cameraDistance)
        {
            Vector3 localFocus = transform.InverseTransformPoint(desiredFocusPoint);
            localFocus.y = localGroundHeight;

            float focusMinimumX;
            float focusMaximumX;
            float focusMinimumZ;
            float focusMaximumZ;
            GetFocusHorizontalLimits(
                out focusMinimumX,
                out focusMaximumX,
                out focusMinimumZ,
                out focusMaximumZ);

            if (constraintMode != BistroBuilderCameraBoundsConstraintMode.FocusPointOnly &&
                cameraDistance > 0.0f)
            {
                float cameraMinimumX;
                float cameraMaximumX;
                float cameraMinimumZ;
                float cameraMaximumZ;
                GetCameraHorizontalLimits(
                    out cameraMinimumX,
                    out cameraMaximumX,
                    out cameraMinimumZ,
                    out cameraMaximumZ);

                Vector3 worldCameraOffset = cameraRotation * Vector3.back * cameraDistance;
                Vector3 localCameraOffset = transform.InverseTransformVector(worldCameraOffset);

                float commonMinimumX = Mathf.Max(
                    focusMinimumX,
                    cameraMinimumX - localCameraOffset.x);
                float commonMaximumX = Mathf.Min(
                    focusMaximumX,
                    cameraMaximumX - localCameraOffset.x);
                float commonMinimumZ = Mathf.Max(
                    focusMinimumZ,
                    cameraMinimumZ - localCameraOffset.z);
                float commonMaximumZ = Mathf.Min(
                    focusMaximumZ,
                    cameraMaximumZ - localCameraOffset.z);

                localFocus.x = commonMinimumX <= commonMaximumX
                    ? Mathf.Clamp(localFocus.x, commonMinimumX, commonMaximumX)
                    : Mathf.Clamp(localFocus.x, focusMinimumX, focusMaximumX);
                localFocus.z = commonMinimumZ <= commonMaximumZ
                    ? Mathf.Clamp(localFocus.z, commonMinimumZ, commonMaximumZ)
                    : Mathf.Clamp(localFocus.z, focusMinimumZ, focusMaximumZ);
            }
            else
            {
                localFocus.x = Mathf.Clamp(localFocus.x, focusMinimumX, focusMaximumX);
                localFocus.z = Mathf.Clamp(localFocus.z, focusMinimumZ, focusMaximumZ);
            }

            return transform.TransformPoint(localFocus);
        }

        /// <summary>
        /// Distancia máxima geométricamente realizable con el modo de límites actual.
        /// La envolvente de encuadre añade espacio exterior controlado y, por tanto, no fuerza
        /// a reducir el zoom solo para mantener la cámara encima de la propia baldosa del suelo.
        /// </summary>
        public float CalculateMaximumDistanceForCameraAndFocus(
            Quaternion cameraRotation,
            float requestedMaximumDistance)
        {
            if (constraintMode == BistroBuilderCameraBoundsConstraintMode.FocusPointOnly ||
                requestedMaximumDistance <= 0.0f)
            {
                return requestedMaximumDistance;
            }

            float minimumX;
            float maximumX;
            float minimumZ;
            float maximumZ;
            GetFocusHorizontalLimits(out minimumX, out maximumX, out minimumZ, out maximumZ);

            Vector3 worldOffsetPerUnit = cameraRotation * Vector3.back;
            Vector3 localOffsetPerUnit = transform.InverseTransformVector(worldOffsetPerUnit);
            float availableWidth = Mathf.Max(0.01f, maximumX - minimumX);
            float availableDepth = Mathf.Max(0.01f, maximumZ - minimumZ);

            if (constraintMode == BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope)
            {
                // El foco permanece en la huella, pero la cámara dispone de un carril exterior
                // adicional a cada lado. Para un offset con signo fijo, el recorrido realizable
                // aumenta exactamente en el padding de la envolvente.
                availableWidth += cameraEnvelopePadding;
                availableDepth += cameraEnvelopePadding;
            }

            float maximumByX = Mathf.Abs(localOffsetPerUnit.x) > 0.0001f
                ? availableWidth / Mathf.Abs(localOffsetPerUnit.x)
                : Mathf.Infinity;
            float maximumByZ = Mathf.Abs(localOffsetPerUnit.z) > 0.0001f
                ? availableDepth / Mathf.Abs(localOffsetPerUnit.z)
                : Mathf.Infinity;

            return Mathf.Min(requestedMaximumDistance, Mathf.Min(maximumByX, maximumByZ));
        }

        public bool ContainsFocusPoint(Vector3 worldPoint, float tolerance = 0.01f)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float minimumX;
            float maximumX;
            float minimumZ;
            float maximumZ;
            GetFocusHorizontalLimits(out minimumX, out maximumX, out minimumZ, out maximumZ);
            return ContainsHorizontalPoint(
                localPoint,
                minimumX,
                maximumX,
                minimumZ,
                maximumZ,
                tolerance);
        }

        public bool ContainsCameraPosition(Vector3 worldPoint, float tolerance = 0.01f)
        {
            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float minimumX;
            float maximumX;
            float minimumZ;
            float maximumZ;
            GetCameraHorizontalLimits(out minimumX, out maximumX, out minimumZ, out maximumZ);
            return ContainsHorizontalPoint(
                localPoint,
                minimumX,
                maximumX,
                minimumZ,
                maximumZ,
                tolerance);
        }

        public void ConfigureFromWorldBounds(Bounds worldBounds, float minimumHorizontalSize = 20.0f)
        {
            Vector3 center = worldBounds.center;
            Vector3 size = worldBounds.size;
            size.x = Mathf.Max(minimumHorizontalSize, size.x);
            size.z = Mathf.Max(minimumHorizontalSize, size.z);
            size.y = Mathf.Max(10.0f, size.y);

            transform.position = new Vector3(center.x, worldBounds.min.y, center.z);
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            localGroundHeight = 0.0f;
            localCenter = new Vector3(0.0f, size.y * 0.5f, 0.0f);
            localSize = size;
        }

        public void ConfigureConstraintMode(BistroBuilderCameraBoundsConstraintMode mode)
        {
            constraintMode = mode;
        }

        public void ConfigureHorizontalPadding(float padding)
        {
            horizontalPadding = Mathf.Max(0.0f, padding);
        }

        public void ConfigureCameraEnvelopePadding(float padding)
        {
            cameraEnvelopePadding = Mathf.Max(0.0f, padding);
        }

        private static bool ContainsHorizontalPoint(
            Vector3 localPoint,
            float minimumX,
            float maximumX,
            float minimumZ,
            float maximumZ,
            float tolerance)
        {
            return localPoint.x >= minimumX - tolerance &&
                   localPoint.x <= maximumX + tolerance &&
                   localPoint.z >= minimumZ - tolerance &&
                   localPoint.z <= maximumZ + tolerance;
        }

        private void GetFocusHorizontalLimits(
            out float minimumX,
            out float maximumX,
            out float minimumZ,
            out float maximumZ)
        {
            Vector3 half = localSize * 0.5f;
            float padX = Mathf.Min(horizontalPadding, Mathf.Max(0.0f, half.x - 0.01f));
            float padZ = Mathf.Min(horizontalPadding, Mathf.Max(0.0f, half.z - 0.01f));

            minimumX = localCenter.x - half.x + padX;
            maximumX = localCenter.x + half.x - padX;
            minimumZ = localCenter.z - half.z + padZ;
            maximumZ = localCenter.z + half.z - padZ;
        }

        private void GetCameraHorizontalLimits(
            out float minimumX,
            out float maximumX,
            out float minimumZ,
            out float maximumZ)
        {
            GetFocusHorizontalLimits(out minimumX, out maximumX, out minimumZ, out maximumZ);
            if (constraintMode == BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope)
            {
                minimumX -= cameraEnvelopePadding;
                maximumX += cameraEnvelopePadding;
                minimumZ -= cameraEnvelopePadding;
                maximumZ += cameraEnvelopePadding;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            localSize.x = Mathf.Max(0.1f, localSize.x);
            localSize.y = Mathf.Max(0.1f, localSize.y);
            localSize.z = Mathf.Max(0.1f, localSize.z);
            horizontalPadding = Mathf.Max(0.0f, horizontalPadding);
            cameraEnvelopePadding = Mathf.Max(0.0f, cameraEnvelopePadding);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmo)
            {
                return;
            }

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localCenter, localSize);

            if (constraintMode == BistroBuilderCameraBoundsConstraintMode.FocusPointAndFramingEnvelope &&
                cameraEnvelopePadding > 0.0f)
            {
                Vector3 envelopeSize = localSize;
                envelopeSize.x += cameraEnvelopePadding * 2.0f;
                envelopeSize.z += cameraEnvelopePadding * 2.0f;
                Gizmos.DrawWireCube(localCenter, envelopeSize);
            }

            Gizmos.matrix = previousMatrix;
        }
#endif
    }
}
