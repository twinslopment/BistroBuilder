using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.CameraSystem
{
    /// <summary>
    /// Resolución de volumen visual para inspección. No depende de los sistemas de mesas o
    /// colocables: usa metadatos opcionales y un fallback semántico por nombre/proximidad.
    /// </summary>
    public static class BistroBuilderCameraInspectionBounds
    {
        private static readonly string[] TableTokens = { "mesa", "table" };
        private static readonly string[] BarTokens = { "barra", "bar", "counter" };
        private static readonly string[] ChairTokens = { "silla", "chair" };
        private static readonly string[] StoolTokens = { "taburete", "stool" };

        public static bool TryCalculate(
            GameObject target,
            BistroBuilderCameraInspectionSettings settings,
            bool includeRelated,
            out Bounds bounds,
            out GameObject semanticRoot)
        {
            bounds = default;
            semanticRoot = null;
            if (target == null || settings == null)
            {
                return false;
            }

            BistroBuilderCameraInspectable inspectable =
                target.GetComponentInParent<BistroBuilderCameraInspectable>();
            semanticRoot = inspectable != null
                ? inspectable.gameObject
                : FindSemanticRoot(target);

            bool hasBounds = EncapsulateRoot(
                semanticRoot.transform,
                inspectable == null || inspectable.IncludeChildren,
                settings.IncludeInactiveGeometry,
                ref bounds);

            if (inspectable != null)
            {
                IReadOnlyList<Transform> relatedRoots = inspectable.RelatedRoots;
                for (int index = 0; index < relatedRoots.Count; index++)
                {
                    Transform related = relatedRoots[index];
                    if (related != null)
                    {
                        hasBounds |= EncapsulateRoot(
                            related,
                            true,
                            settings.IncludeInactiveGeometry,
                            ref bounds);
                    }
                }
            }

            bool shouldIncludeRelated = includeRelated &&
                (inspectable != null
                    ? inspectable.IncludeRelatedSeating
                    : settings.IncludeRelatedSeatingByDefault);
            if (shouldIncludeRelated && IsTableOrBar(semanticRoot, inspectable))
            {
                hasBounds |= EncapsulateNearbySeats(
                    semanticRoot,
                    hasBounds ? bounds : new Bounds(semanticRoot.transform.position, Vector3.zero),
                    settings,
                    ref bounds);
            }

            if (!hasBounds)
            {
                bounds = new Bounds(
                    semanticRoot.transform.position,
                    Vector3.one * settings.FallbackBoundsSize);
            }

            return BistroBuilderProfessionalCameraMath.IsFinite(bounds.center) &&
                   BistroBuilderProfessionalCameraMath.IsFinite(bounds.size);
        }

        public static void GetCorners(Bounds bounds, Vector3[] corners)
        {
            if (corners == null || corners.Length < 8)
            {
                return;
            }

            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
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
        }

        public static bool IsSeatName(string name)
        {
            return ContainsToken(name, ChairTokens) || ContainsToken(name, StoolTokens);
        }

        private static GameObject FindSemanticRoot(GameObject target)
        {
            Transform current = target.transform;
            Transform best = target.transform;
            SemanticNameKind kind = SemanticNameKind.None;

            // Primero elegimos el significado más próximo al objeto pulsado. Después solo subimos
            // por ancestros del mismo tipo; así una silla hija de una mesa sigue siendo una silla,
            // mientras TableTop puede resolver correctamente la raíz MesaA.
            while (current != null)
            {
                SemanticNameKind currentKind = GetNameKind(current.name);
                if (currentKind != SemanticNameKind.None)
                {
                    best = current;
                    kind = currentKind;
                    break;
                }
                current = current.parent;
            }

            if (kind == SemanticNameKind.None)
            {
                return best.gameObject;
            }

            current = best.parent;
            while (current != null && GetNameKind(current.name) == kind)
            {
                best = current;
                current = current.parent;
            }
            return best.gameObject;
        }

        private static bool IsTableOrBar(
            GameObject root,
            BistroBuilderCameraInspectable inspectable)
        {
            if (inspectable != null)
            {
                if (inspectable.Kind == BistroBuilderCameraInspectableKind.Table ||
                    inspectable.Kind == BistroBuilderCameraInspectableKind.Bar)
                {
                    return true;
                }
                if (inspectable.Kind != BistroBuilderCameraInspectableKind.Auto)
                {
                    return false;
                }
            }

            return root != null &&
                (ContainsToken(root.name, TableTokens) || ContainsToken(root.name, BarTokens));
        }

        private static bool EncapsulateNearbySeats(
            GameObject semanticRoot,
            Bounds referenceBounds,
            BistroBuilderCameraInspectionSettings settings,
            ref Bounds result)
        {
            Transform[] allTransforms = UnityEngine.Object.FindObjectsByType<Transform>(
                settings.IncludeInactiveGeometry
                    ? FindObjectsInactive.Include
                    : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            List<SeatCandidate> candidates = new List<SeatCandidate>();
            HashSet<int> roots = new HashSet<int>();
            float radius = settings.RelatedSeatSearchRadius +
                           Mathf.Max(referenceBounds.extents.x, referenceBounds.extents.z);

            for (int index = 0; index < allTransforms.Length; index++)
            {
                Transform transform = allTransforms[index];
                if (transform == null || transform == semanticRoot.transform ||
                    !IsSeatName(transform.name))
                {
                    continue;
                }

                Transform seatRoot = FindSeatRoot(transform);
                if (seatRoot == null || !roots.Add(seatRoot.gameObject.GetInstanceID()))
                {
                    continue;
                }

                Bounds seatBounds = default;
                if (!EncapsulateRoot(
                        seatRoot,
                        true,
                        settings.IncludeInactiveGeometry,
                        ref seatBounds))
                {
                    continue;
                }

                float planarDistance = Vector2.Distance(
                    new Vector2(seatBounds.center.x, seatBounds.center.z),
                    new Vector2(referenceBounds.center.x, referenceBounds.center.z));
                if (planarDistance <= radius)
                {
                    candidates.Add(new SeatCandidate(seatBounds, planarDistance));
                }
            }

            candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            int count = Mathf.Min(settings.MaximumRelatedSeats, candidates.Count);
            bool added = false;
            for (int index = 0; index < count; index++)
            {
                if (result.size == Vector3.zero)
                {
                    result = candidates[index].Bounds;
                }
                else
                {
                    result.Encapsulate(candidates[index].Bounds);
                }
                added = true;
            }

            return added;
        }

        private static Transform FindSeatRoot(Transform transform)
        {
            Transform current = transform;
            Transform best = transform;
            while (current.parent != null && IsSeatName(current.parent.name))
            {
                current = current.parent;
                best = current;
            }
            return best;
        }

        private static bool EncapsulateRoot(
            Transform root,
            bool includeChildren,
            bool includeInactive,
            ref Bounds bounds)
        {
            if (root == null || (!includeInactive && !root.gameObject.activeInHierarchy))
            {
                return false;
            }

            bool hasBounds = bounds.size != Vector3.zero;
            bool addedBounds = false;
            int visualCount = 0;
            Renderer[] renderers = includeChildren
                ? root.GetComponentsInChildren<Renderer>(includeInactive)
                : root.GetComponents<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || (!includeInactive && !renderer.enabled) ||
                    IsDiagnosticGeometry(renderer.transform))
                {
                    continue;
                }
                AddBounds(renderer.bounds, ref bounds, ref hasBounds);
                visualCount++;
                addedBounds = true;
            }

            // Los colliders son fallback, no una ampliación del volumen visual. Los footprints,
            // triggers y colliders operativos suelen ser mayores que el mueble y deformarían el
            // encuadre de selección.
            if (visualCount == 0)
            {
                Collider[] colliders = includeChildren
                    ? root.GetComponentsInChildren<Collider>(includeInactive)
                    : root.GetComponents<Collider>();
                for (int index = 0; index < colliders.Length; index++)
                {
                    Collider collider = colliders[index];
                    if (collider == null || collider.isTrigger ||
                        (!includeInactive && !collider.enabled) ||
                        IsDiagnosticGeometry(collider.transform))
                    {
                        continue;
                    }
                    AddBounds(collider.bounds, ref bounds, ref hasBounds);
                    addedBounds = true;
                }
            }

            return addedBounds;
        }

        private static bool IsDiagnosticGeometry(Transform transform)
        {
            Transform current = transform;
            while (current != null)
            {
                string name = current.name;
                if (ContainsToken(name, new[]
                    {
                        "indicator", "preview", "ghost", "gizmo", "debug",
                        "footprint", "anchor", "highlight"
                    }))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static SemanticNameKind GetNameKind(string name)
        {
            if (ContainsToken(name, ChairTokens))
            {
                return SemanticNameKind.Chair;
            }
            if (ContainsToken(name, StoolTokens))
            {
                return SemanticNameKind.Stool;
            }
            if (ContainsToken(name, TableTokens))
            {
                return SemanticNameKind.Table;
            }
            if (ContainsToken(name, BarTokens))
            {
                return SemanticNameKind.Bar;
            }
            return SemanticNameKind.None;
        }

        private static void AddBounds(Bounds source, ref Bounds result, ref bool hasBounds)
        {
            if (!BistroBuilderProfessionalCameraMath.IsFinite(source.center) ||
                !BistroBuilderProfessionalCameraMath.IsFinite(source.size))
            {
                return;
            }

            if (!hasBounds)
            {
                result = source;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(source);
            }
        }

        private static bool ContainsToken(string value, string[] tokens)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < tokens.Length; index++)
            {
                if (value.IndexOf(tokens[index], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private enum SemanticNameKind
        {
            None = 0,
            Table = 1,
            Bar = 2,
            Chair = 3,
            Stool = 4
        }

        private readonly struct SeatCandidate
        {
            public readonly Bounds Bounds;
            public readonly float Distance;

            public SeatCandidate(Bounds bounds, float distance)
            {
                Bounds = bounds;
                Distance = distance;
            }
        }
    }
}
