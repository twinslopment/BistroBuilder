using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Diagnóstico bajo demanda de la geometría visual y de las superficies
/// renderizadas alrededor de un artículo colocado.
///
/// Solo se ejecuta al confirmar una captura F9. No realiza búsquedas
/// continuas durante el juego.
/// </summary>
public static class RestaurantPlacementGeometryDiagnosticUtility
{
    private const string DiagnosticTag =
        "[BB-PLACEMENT-GEOMETRY]";

    private static GeometrySnapshot previousSnapshot;

    public static void LogPlacementSnapshot(
        RestaurantAreaMember member,
        Camera interactionCamera,
        int sampleNumber
    )
    {
        if (member == null)
        {
            return;
        }

        try
        {
            GeometrySnapshot snapshot =
                CaptureSnapshot(
                    member,
                    interactionCamera,
                    sampleNumber
                );

            Debug.Log(
                BuildSnapshotMessage(snapshot),
                member
            );

            if (previousSnapshot != null)
            {
                Debug.Log(
                    BuildComparisonMessage(
                        previousSnapshot,
                        snapshot
                    ),
                    member
                );
            }

            previousSnapshot = snapshot;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                DiagnosticTag +
                " No se pudo completar el diagnóstico: " +
                exception.Message,
                member
            );

            Debug.LogException(exception, member);
        }
    }

    private static GeometrySnapshot CaptureSnapshot(
        RestaurantAreaMember member,
        Camera interactionCamera,
        int sampleNumber
    )
    {
        Transform root = member.transform;

        RestaurantPlaceableObject placeable =
            member.GetComponent<RestaurantPlaceableObject>();

        Transform anchor =
            placeable != null &&
            placeable.PlacementAnchor != null
                ? placeable.PlacementAnchor
                : root;

        GeometrySnapshot snapshot =
            new GeometrySnapshot
            {
                SampleNumber = sampleNumber,
                ObjectName = member.name,
                RootPosition = root.position,
                RootRotation = root.rotation.eulerAngles,
                RootScale = root.lossyScale,
                AnchorPosition = anchor.position,
                AnchorLocalPosition =
                    root.InverseTransformPoint(anchor.position),
                CameraPosition =
                    interactionCamera != null
                        ? interactionCamera.transform.position
                        : Vector3.zero,
                CameraRotation =
                    interactionCamera != null
                        ? interactionCamera.transform
                            .rotation.eulerAngles
                        : Vector3.zero
            };

        CaptureHierarchy(root, snapshot);
        CaptureRenderers(root, snapshot);
        CaptureColliders(root, snapshot);
        CaptureSceneRenderersUnderAnchor(
            root,
            anchor.position,
            snapshot
        );
        CaptureSceneCollidersUnderAnchor(
            root,
            anchor.position,
            snapshot
        );

        return snapshot;
    }

    private static void CaptureHierarchy(
        Transform root,
        GeometrySnapshot snapshot
    )
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        for (int index = 0;
             index < transforms.Length;
             index++)
        {
            Transform current = transforms[index];

            if (current == null)
            {
                continue;
            }

            snapshot.Transforms.Add(
                new TransformRecord
                {
                    Path = BuildRelativePath(current, root),
                    Active =
                        current.gameObject.activeInHierarchy,
                    Layer = current.gameObject.layer,
                    LocalPosition = current.localPosition,
                    WorldPosition = current.position,
                    LocalRotation =
                        current.localRotation.eulerAngles,
                    WorldRotation =
                        current.rotation.eulerAngles,
                    LocalScale = current.localScale,
                    LossyScale = current.lossyScale
                }
            );
        }
    }

    private static void CaptureRenderers(
        Transform root,
        GeometrySnapshot snapshot
    )
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        float minimumY = float.PositiveInfinity;
        float maximumY = float.NegativeInfinity;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
            {
                continue;
            }

            float meshBoundsMinimumY =
                renderer.bounds.min.y;

            float meshBoundsMaximumY =
                renderer.bounds.max.y;

            string boundsMode =
                "Renderer.bounds";

            MeshFilter meshFilter =
                renderer.GetComponent<MeshFilter>();

            if (meshFilter != null &&
                meshFilter.sharedMesh != null)
            {
                CalculateTransformedBoundsY(
                    renderer.transform.localToWorldMatrix,
                    meshFilter.sharedMesh.bounds,
                    out meshBoundsMinimumY,
                    out meshBoundsMaximumY
                );

                boundsMode =
                    "Mesh.bounds transformados";
            }

            snapshot.Renderers.Add(
                new RendererRecord
                {
                    Path =
                        BuildRelativePath(
                            renderer.transform,
                            root
                        ),
                    Type = renderer.GetType().Name,
                    Enabled = renderer.enabled,
                    Active =
                        renderer.gameObject.activeInHierarchy,
                    Layer = renderer.gameObject.layer,
                    TransformPosition =
                        renderer.transform.position,
                    TransformRotation =
                        renderer.transform.rotation.eulerAngles,
                    TransformScale =
                        renderer.transform.lossyScale,
                    WorldBounds = renderer.bounds,
                    CalculatedMinimumY =
                        meshBoundsMinimumY,
                    CalculatedMaximumY =
                        meshBoundsMaximumY,
                    BoundsMode = boundsMode,
                    Materials =
                        BuildMaterialList(
                            renderer.sharedMaterials
                        )
                }
            );

            if (renderer.enabled &&
                renderer.gameObject.activeInHierarchy)
            {
                minimumY =
                    Mathf.Min(
                        minimumY,
                        meshBoundsMinimumY
                    );

                maximumY =
                    Mathf.Max(
                        maximumY,
                        meshBoundsMaximumY
                    );
            }
        }

        snapshot.VisualMinimumY =
            float.IsPositiveInfinity(minimumY)
                ? float.NaN
                : minimumY;

        snapshot.VisualMaximumY =
            float.IsNegativeInfinity(maximumY)
                ? float.NaN
                : maximumY;

        snapshot.VisualMinimumOffsetFromAnchor =
            float.IsNaN(snapshot.VisualMinimumY)
                ? float.NaN
                : snapshot.VisualMinimumY -
                  snapshot.AnchorPosition.y;
    }

    private static void CaptureColliders(
        Transform root,
        GeometrySnapshot snapshot
    )
    {
        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(true);

        float minimumY = float.PositiveInfinity;

        for (int index = 0;
             index < colliders.Length;
             index++)
        {
            Collider collider = colliders[index];

            if (collider == null)
            {
                continue;
            }

            snapshot.Colliders.Add(
                new ColliderRecord
                {
                    Path =
                        BuildRelativePath(
                            collider.transform,
                            root
                        ),
                    Type =
                        collider.GetType().Name,
                    Enabled = collider.enabled,
                    Active =
                        collider.gameObject.activeInHierarchy,
                    IsTrigger = collider.isTrigger,
                    Layer = collider.gameObject.layer,
                    WorldBounds = collider.bounds
                }
            );

            if (collider.enabled &&
                collider.gameObject.activeInHierarchy)
            {
                minimumY =
                    Mathf.Min(
                        minimumY,
                        collider.bounds.min.y
                    );
            }
        }

        snapshot.ColliderMinimumY =
            float.IsPositiveInfinity(minimumY)
                ? float.NaN
                : minimumY;

        snapshot.ColliderMinimumOffsetFromAnchor =
            float.IsNaN(snapshot.ColliderMinimumY)
                ? float.NaN
                : snapshot.ColliderMinimumY -
                  snapshot.AnchorPosition.y;
    }

    private static void CaptureSceneRenderersUnderAnchor(
        Transform articleRoot,
        Vector3 anchorPosition,
        GeometrySnapshot snapshot
    )
    {
        Renderer[] renderers =
            UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null ||
                renderer.transform.IsChildOf(articleRoot) ||
                !renderer.enabled ||
                !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;

            if (!ContainsXZ(bounds, anchorPosition) ||
                bounds.max.y < -2f ||
                bounds.min.y > 3f)
            {
                continue;
            }

            snapshot.SceneRenderers.Add(
                new SceneComponentRecord
                {
                    Path =
                        BuildAbsolutePath(
                            renderer.transform
                        ),
                    Type =
                        renderer.GetType().Name,
                    Layer =
                        renderer.gameObject.layer,
                    Position =
                        renderer.transform.position,
                    Rotation =
                        renderer.transform.rotation.eulerAngles,
                    Scale =
                        renderer.transform.lossyScale,
                    WorldBounds =
                        bounds,
                    Materials =
                        BuildMaterialList(
                            renderer.sharedMaterials
                        )
                }
            );
        }

        snapshot.SceneRenderers.Sort(
            CompareSceneComponents
        );
    }

    private static void CaptureSceneCollidersUnderAnchor(
        Transform articleRoot,
        Vector3 anchorPosition,
        GeometrySnapshot snapshot
    )
    {
        Collider[] colliders =
            UnityEngine.Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        for (int index = 0;
             index < colliders.Length;
             index++)
        {
            Collider collider = colliders[index];

            if (collider == null ||
                collider.transform.IsChildOf(articleRoot) ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = collider.bounds;

            if (!ContainsXZ(bounds, anchorPosition) ||
                bounds.max.y < -2f ||
                bounds.min.y > 3f)
            {
                continue;
            }

            snapshot.SceneColliders.Add(
                new SceneComponentRecord
                {
                    Path =
                        BuildAbsolutePath(
                            collider.transform
                        ),
                    Type =
                        collider.GetType().Name,
                    Layer =
                        collider.gameObject.layer,
                    Position =
                        collider.transform.position,
                    Rotation =
                        collider.transform.rotation.eulerAngles,
                    Scale =
                        collider.transform.lossyScale,
                    WorldBounds =
                        bounds,
                    Materials =
                        string.Empty
                }
            );
        }

        snapshot.SceneColliders.Sort(
            CompareSceneComponents
        );
    }

    private static void CalculateTransformedBoundsY(
        Matrix4x4 localToWorldMatrix,
        Bounds localBounds,
        out float minimumY,
        out float maximumY
    )
    {
        minimumY = float.PositiveInfinity;
        maximumY = float.NegativeInfinity;

        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner =
                        center +
                        Vector3.Scale(
                            extents,
                            new Vector3(x, y, z)
                        );

                    float worldY =
                        localToWorldMatrix
                            .MultiplyPoint3x4(corner)
                            .y;

                    minimumY =
                        Mathf.Min(minimumY, worldY);

                    maximumY =
                        Mathf.Max(maximumY, worldY);
                }
            }
        }
    }

    private static bool ContainsXZ(
        Bounds bounds,
        Vector3 point
    )
    {
        return
            point.x >= bounds.min.x &&
            point.x <= bounds.max.x &&
            point.z >= bounds.min.z &&
            point.z <= bounds.max.z;
    }

    private static int CompareSceneComponents(
        SceneComponentRecord first,
        SceneComponentRecord second
    )
    {
        int yComparison =
            second.WorldBounds.max.y.CompareTo(
                first.WorldBounds.max.y
            );

        if (yComparison != 0)
        {
            return yComparison;
        }

        return string.Compare(
            first.Path,
            second.Path,
            StringComparison.Ordinal
        );
    }

    private static string BuildSnapshotMessage(
        GeometrySnapshot snapshot
    )
    {
        StringBuilder builder =
            new StringBuilder(4096);

        builder.AppendLine(
            DiagnosticTag +
            " MUESTRA " +
            snapshot.SampleNumber
        );

        builder.AppendLine(
            "Objeto: " +
            snapshot.ObjectName
        );

        builder.AppendLine(
            "Raíz pos=" +
            FormatVector(snapshot.RootPosition) +
            " | rot=" +
            FormatVector(snapshot.RootRotation) +
            " | scale=" +
            FormatVector(snapshot.RootScale)
        );

        builder.AppendLine(
            "Anchor pos=" +
            FormatVector(snapshot.AnchorPosition) +
            " | local=" +
            FormatVector(snapshot.AnchorLocalPosition)
        );

        builder.AppendLine(
            "Cámara pos=" +
            FormatVector(snapshot.CameraPosition) +
            " | rot=" +
            FormatVector(snapshot.CameraRotation)
        );

        builder.AppendLine(
            "Visual minY=" +
            FormatFloat(snapshot.VisualMinimumY) +
            " | maxY=" +
            FormatFloat(snapshot.VisualMaximumY) +
            " | minY-anchorY=" +
            FormatFloat(
                snapshot.VisualMinimumOffsetFromAnchor
            )
        );

        builder.AppendLine(
            "Collider minY=" +
            FormatFloat(snapshot.ColliderMinimumY) +
            " | minY-anchorY=" +
            FormatFloat(
                snapshot.ColliderMinimumOffsetFromAnchor
            )
        );

        builder.AppendLine(
            "--- TRANSFORMS DEL ARTÍCULO ---"
        );

        for (int index = 0;
             index < snapshot.Transforms.Count;
             index++)
        {
            TransformRecord record =
                snapshot.Transforms[index];

            builder.AppendLine(
                "  " +
                record.Path +
                " | active=" +
                record.Active +
                " | layer=" +
                record.Layer +
                " (" +
                LayerMask.LayerToName(record.Layer) +
                ")" +
                " | localPos=" +
                FormatVector(record.LocalPosition) +
                " | worldPos=" +
                FormatVector(record.WorldPosition) +
                " | localRot=" +
                FormatVector(record.LocalRotation) +
                " | worldRot=" +
                FormatVector(record.WorldRotation) +
                " | localScale=" +
                FormatVector(record.LocalScale) +
                " | lossy=" +
                FormatVector(record.LossyScale)
            );
        }

        builder.AppendLine(
            "--- RENDERERS DEL ARTÍCULO ---"
        );

        for (int index = 0;
             index < snapshot.Renderers.Count;
             index++)
        {
            RendererRecord record =
                snapshot.Renderers[index];

            builder.AppendLine(
                "  " +
                record.Path +
                " | " +
                record.Type +
                " | enabled=" +
                record.Enabled +
                " | active=" +
                record.Active +
                " | pos=" +
                FormatVector(record.TransformPosition) +
                " | rot=" +
                FormatVector(record.TransformRotation) +
                " | scale=" +
                FormatVector(record.TransformScale) +
                " | bounds.min=" +
                FormatVector(record.WorldBounds.min) +
                " | bounds.max=" +
                FormatVector(record.WorldBounds.max) +
                " | calcY=" +
                FormatFloat(record.CalculatedMinimumY) +
                ".." +
                FormatFloat(record.CalculatedMaximumY) +
                " | modo=" +
                record.BoundsMode +
                " | materiales=" +
                record.Materials
            );
        }

        builder.AppendLine(
            "--- COLLIDERS DEL ARTÍCULO ---"
        );

        for (int index = 0;
             index < snapshot.Colliders.Count;
             index++)
        {
            ColliderRecord record =
                snapshot.Colliders[index];

            builder.AppendLine(
                "  " +
                record.Path +
                " | " +
                record.Type +
                " | enabled=" +
                record.Enabled +
                " | active=" +
                record.Active +
                " | trigger=" +
                record.IsTrigger +
                " | bounds.min=" +
                FormatVector(record.WorldBounds.min) +
                " | bounds.max=" +
                FormatVector(record.WorldBounds.max)
            );
        }

        builder.AppendLine(
            "--- RENDERERS DE ESCENA BAJO EL ANCLA ---"
        );

        AppendSceneComponents(
            builder,
            snapshot.SceneRenderers
        );

        builder.AppendLine(
            "--- COLLIDERS DE ESCENA BAJO EL ANCLA ---"
        );

        AppendSceneComponents(
            builder,
            snapshot.SceneColliders
        );

        return builder.ToString();
    }

    private static void AppendSceneComponents(
        StringBuilder builder,
        List<SceneComponentRecord> records
    )
    {
        if (records.Count == 0)
        {
            builder.AppendLine("  <ninguno>");
            return;
        }

        for (int index = 0;
             index < records.Count;
             index++)
        {
            SceneComponentRecord record =
                records[index];

            builder.AppendLine(
                "  " +
                record.Path +
                " | " +
                record.Type +
                " | layer=" +
                record.Layer +
                " (" +
                LayerMask.LayerToName(record.Layer) +
                ")" +
                " | pos=" +
                FormatVector(record.Position) +
                " | rot=" +
                FormatVector(record.Rotation) +
                " | scale=" +
                FormatVector(record.Scale) +
                " | bounds.min=" +
                FormatVector(record.WorldBounds.min) +
                " | bounds.max=" +
                FormatVector(record.WorldBounds.max) +
                (
                    string.IsNullOrWhiteSpace(
                        record.Materials
                    )
                        ? string.Empty
                        : " | materiales=" +
                          record.Materials
                )
            );
        }
    }

    private static string BuildComparisonMessage(
        GeometrySnapshot first,
        GeometrySnapshot second
    )
    {
        StringBuilder builder =
            new StringBuilder(1024);

        builder.AppendLine(
            DiagnosticTag +
            " COMPARACIÓN " +
            first.SampleNumber +
            " -> " +
            second.SampleNumber
        );

        builder.AppendLine(
            "Raíz Y: " +
            FormatFloat(first.RootPosition.y) +
            " -> " +
            FormatFloat(second.RootPosition.y) +
            " | delta=" +
            FormatFloat(
                second.RootPosition.y -
                first.RootPosition.y
            )
        );

        builder.AppendLine(
            "Visual minY: " +
            FormatFloat(first.VisualMinimumY) +
            " -> " +
            FormatFloat(second.VisualMinimumY) +
            " | delta=" +
            FormatFloat(
                second.VisualMinimumY -
                first.VisualMinimumY
            )
        );

        builder.AppendLine(
            "Visual minY-anchorY: " +
            FormatFloat(
                first.VisualMinimumOffsetFromAnchor
            ) +
            " -> " +
            FormatFloat(
                second.VisualMinimumOffsetFromAnchor
            ) +
            " | delta=" +
            FormatFloat(
                second.VisualMinimumOffsetFromAnchor -
                first.VisualMinimumOffsetFromAnchor
            )
        );

        builder.AppendLine(
            "Collider minY-anchorY: " +
            FormatFloat(
                first.ColliderMinimumOffsetFromAnchor
            ) +
            " -> " +
            FormatFloat(
                second.ColliderMinimumOffsetFromAnchor
            ) +
            " | delta=" +
            FormatFloat(
                second.ColliderMinimumOffsetFromAnchor -
                first.ColliderMinimumOffsetFromAnchor
            )
        );

        builder.AppendLine(
            "Rotación raíz: " +
            FormatVector(first.RootRotation) +
            " -> " +
            FormatVector(second.RootRotation)
        );

        builder.AppendLine(
            "Renderers de escena bajo ancla: " +
            first.SceneRenderers.Count +
            " -> " +
            second.SceneRenderers.Count
        );

        builder.AppendLine(
            "Lectura: si visual minY-anchorY coincide, la geometría " +
            "de la silla no cambia. Si cambian los renderers de " +
            "escena bajo el ancla, revisar suelos visuales superpuestos."
        );

        return builder.ToString();
    }

    private static string BuildMaterialList(
        Material[] materials
    )
    {
        if (materials == null ||
            materials.Length == 0)
        {
            return "<ninguno>";
        }

        StringBuilder builder =
            new StringBuilder();

        for (int index = 0;
             index < materials.Length;
             index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            Material material = materials[index];

            builder.Append(
                material != null
                    ? material.name
                    : "<nulo>"
            );
        }

        return builder.ToString();
    }

    private static string BuildRelativePath(
        Transform transform,
        Transform root
    )
    {
        if (transform == null)
        {
            return "<nulo>";
        }

        Stack<string> names =
            new Stack<string>();

        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);

            if (current == root)
            {
                break;
            }

            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static string BuildAbsolutePath(
        Transform transform
    )
    {
        if (transform == null)
        {
            return "<nulo>";
        }

        Stack<string> names =
            new Stack<string>();

        Transform current = transform;

        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names.ToArray());
    }

    private static string FormatVector(
        Vector3 value
    )
    {
        return
            "(" +
            FormatFloat(value.x) +
            ", " +
            FormatFloat(value.y) +
            ", " +
            FormatFloat(value.z) +
            ")";
    }

    private static string FormatFloat(
        float value
    )
    {
        if (float.IsNaN(value))
        {
            return "NaN";
        }

        if (float.IsPositiveInfinity(value))
        {
            return "+Infinity";
        }

        if (float.IsNegativeInfinity(value))
        {
            return "-Infinity";
        }

        return value.ToString("0.000000");
    }

    private sealed class GeometrySnapshot
    {
        public int SampleNumber;
        public string ObjectName = string.Empty;
        public Vector3 RootPosition;
        public Vector3 RootRotation;
        public Vector3 RootScale;
        public Vector3 AnchorPosition;
        public Vector3 AnchorLocalPosition;
        public Vector3 CameraPosition;
        public Vector3 CameraRotation;
        public float VisualMinimumY;
        public float VisualMaximumY;
        public float VisualMinimumOffsetFromAnchor;
        public float ColliderMinimumY;
        public float ColliderMinimumOffsetFromAnchor;

        public readonly List<TransformRecord> Transforms =
            new List<TransformRecord>();

        public readonly List<RendererRecord> Renderers =
            new List<RendererRecord>();

        public readonly List<ColliderRecord> Colliders =
            new List<ColliderRecord>();

        public readonly List<SceneComponentRecord>
            SceneRenderers =
                new List<SceneComponentRecord>();

        public readonly List<SceneComponentRecord>
            SceneColliders =
                new List<SceneComponentRecord>();
    }

    private struct TransformRecord
    {
        public string Path;
        public bool Active;
        public int Layer;
        public Vector3 LocalPosition;
        public Vector3 WorldPosition;
        public Vector3 LocalRotation;
        public Vector3 WorldRotation;
        public Vector3 LocalScale;
        public Vector3 LossyScale;
    }

    private struct RendererRecord
    {
        public string Path;
        public string Type;
        public bool Enabled;
        public bool Active;
        public int Layer;
        public Vector3 TransformPosition;
        public Vector3 TransformRotation;
        public Vector3 TransformScale;
        public Bounds WorldBounds;
        public float CalculatedMinimumY;
        public float CalculatedMaximumY;
        public string BoundsMode;
        public string Materials;
    }

    private struct ColliderRecord
    {
        public string Path;
        public string Type;
        public bool Enabled;
        public bool Active;
        public bool IsTrigger;
        public int Layer;
        public Bounds WorldBounds;
    }

    private struct SceneComponentRecord
    {
        public string Path;
        public string Type;
        public int Layer;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;
        public Bounds WorldBounds;
        public string Materials;
    }
}
