using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

/// <summary>
/// Sustituye visualmente una mesa whitebox por una mesa sencilla pero creíble,
/// sin tocar colliders, asientos, footprints, navegación ni persistencia.
///
/// Es una capa de presentación runtime: la mesa funcional original sigue siendo
/// la autoridad y conserva toda su geometría lógica.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Presentation/Primitive Table Presentation Proxy")]
public sealed class BistroBuilderPrimitiveTablePresentationProxy : MonoBehaviour
{
    private const string ProxyRootName =
        "BB_TablePresentationProxy";

    private MeshRenderer sourceRenderer;
    private Material presentationMaterial;
    private bool sourceWasEnabled;
    private Transform proxyRoot;

    private readonly List<MeshRenderer>
        proxyRenderers =
            new List<MeshRenderer>(8);

    private readonly MaterialPropertyBlock
        sourcePropertyBlock =
            new MaterialPropertyBlock();

    private static Mesh unitCubeMesh;

    public void Configure(
        MeshRenderer source,
        Material material)
    {
        if (source == null ||
            material == null)
        {
            return;
        }

        if (sourceRenderer != null &&
            !ReferenceEquals(
                sourceRenderer,
                source))
        {
            RestoreSource();
            DestroyProxy();
        }

        sourceRenderer = source;
        presentationMaterial = material;

        BuildOrRefreshProxy();
    }

    private void OnEnable()
    {
        if (sourceRenderer != null &&
            presentationMaterial != null)
        {
            BuildOrRefreshProxy();
        }
    }

    private void LateUpdate()
    {
        SynchronizeSourceVisualState();
    }

    private void OnDisable()
    {
        RestoreSource();
        SetProxyVisible(false);
    }

    private void OnDestroy()
    {
        RestoreSource();
        DestroyProxy();
    }

    private void BuildOrRefreshProxy()
    {
        if (sourceRenderer == null ||
            presentationMaterial == null)
        {
            return;
        }

        Bounds worldBounds =
            sourceRenderer.bounds;

        Vector3 scale =
            transform.lossyScale;

        float safeScaleX =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(scale.x));

        float safeScaleY =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(scale.y));

        float safeScaleZ =
            Mathf.Max(
                0.0001f,
                Mathf.Abs(scale.z));

        Vector3 localSize =
            new Vector3(
                worldBounds.size.x /
                    safeScaleX,
                worldBounds.size.y /
                    safeScaleY,
                worldBounds.size.z /
                    safeScaleZ);

        localSize.x =
            Mathf.Max(
                0.55f,
                localSize.x);

        localSize.y =
            Mathf.Max(
                0.58f,
                localSize.y);

        localSize.z =
            Mathf.Max(
                0.55f,
                localSize.z);

        Vector3 localCenter =
            transform.InverseTransformPoint(
                worldBounds.center);

        EnsureProxyRoot();
        ClearProxyChildren();
        proxyRenderers.Clear();

        float tabletopThickness =
            Mathf.Clamp(
                localSize.y * 0.105f,
                0.055f,
                0.085f);

        float legHeight =
            Mathf.Max(
                0.46f,
                localSize.y -
                tabletopThickness);

        float shortestSide =
            Mathf.Min(
                localSize.x,
                localSize.z);

        float legWidth =
            Mathf.Clamp(
                shortestSide * 0.065f,
                0.045f,
                0.075f);

        float insetX =
            Mathf.Clamp(
                localSize.x * 0.105f,
                0.08f,
                0.14f);

        float insetZ =
            Mathf.Clamp(
                localSize.z * 0.105f,
                0.08f,
                0.14f);

        float tabletopY =
            localCenter.y +
            localSize.y * 0.5f -
            tabletopThickness * 0.5f;

        CreateVisualBox(
            "Top",
            new Vector3(
                localCenter.x,
                tabletopY,
                localCenter.z),
            new Vector3(
                localSize.x,
                tabletopThickness,
                localSize.z));

        float legCenterY =
            tabletopY -
            tabletopThickness * 0.5f -
            legHeight * 0.5f;

        float halfX =
            Mathf.Max(
                0.12f,
                localSize.x * 0.5f -
                insetX);

        float halfZ =
            Mathf.Max(
                0.12f,
                localSize.z * 0.5f -
                insetZ);

        CreateLeg(
            localCenter,
            -halfX,
            legCenterY,
            -halfZ,
            legWidth,
            legHeight);

        CreateLeg(
            localCenter,
            halfX,
            legCenterY,
            -halfZ,
            legWidth,
            legHeight);

        CreateLeg(
            localCenter,
            -halfX,
            legCenterY,
            halfZ,
            legWidth,
            legHeight);

        CreateLeg(
            localCenter,
            halfX,
            legCenterY,
            halfZ,
            legWidth,
            legHeight);

        /*
         * Un faldón muy fino ayuda a leer el tablero como mobiliario real
         * sin cambiar silueta funcional ni inventar una mesa ornamental.
         */
        float apronHeight =
            Mathf.Clamp(
                localSize.y * 0.075f,
                0.045f,
                0.065f);

        CreateVisualBox(
            "Apron",
            new Vector3(
                localCenter.x,
                tabletopY -
                tabletopThickness * 0.5f -
                apronHeight * 0.5f,
                localCenter.z),
            new Vector3(
                Mathf.Max(
                    0.20f,
                    localSize.x -
                    insetX * 1.05f),
                apronHeight,
                Mathf.Max(
                    0.20f,
                    localSize.z -
                    insetZ * 1.05f)));

        sourceWasEnabled =
            sourceRenderer.enabled;

        sourceRenderer.enabled =
            false;

        SetProxyVisible(true);
    }

    private void CreateLeg(
        Vector3 localCenter,
        float offsetX,
        float centerY,
        float offsetZ,
        float width,
        float height)
    {
        CreateVisualBox(
            "Leg",
            new Vector3(
                localCenter.x + offsetX,
                centerY,
                localCenter.z + offsetZ),
            new Vector3(
                width,
                height,
                width));
    }

    private void EnsureProxyRoot()
    {
        if (proxyRoot != null)
            return;

        Transform existing =
            transform.Find(
                ProxyRootName);

        if (existing != null)
        {
            proxyRoot =
                existing;
            return;
        }

        GameObject root =
            new GameObject(
                ProxyRootName);

        root.transform.SetParent(
            transform,
            false);

        proxyRoot =
            root.transform;
    }

    private void ClearProxyChildren()
    {
        if (proxyRoot == null)
            return;

        for (int index =
                 proxyRoot.childCount - 1;
             index >= 0;
             index--)
        {
            Transform child =
                proxyRoot.GetChild(index);

            if (child == null)
                continue;

            if (Application.isPlaying)
            {
                Destroy(
                    child.gameObject);
            }
            else
            {
                DestroyImmediate(
                    child.gameObject);
            }
        }
    }

    private void CreateVisualBox(
        string name,
        Vector3 localPosition,
        Vector3 localSize)
    {
        EnsureUnitCubeMesh();

        if (unitCubeMesh == null ||
            proxyRoot == null)
        {
            return;
        }

        GameObject go =
            new GameObject(name);

        go.transform.SetParent(
            proxyRoot,
            false);

        go.transform.localPosition =
            localPosition;

        go.transform.localRotation =
            Quaternion.identity;

        go.transform.localScale =
            localSize;

        MeshFilter filter =
            go.AddComponent<MeshFilter>();

        filter.sharedMesh =
            unitCubeMesh;

        MeshRenderer renderer =
            go.AddComponent<MeshRenderer>();

        renderer.sharedMaterial =
            presentationMaterial;

        renderer.shadowCastingMode =
            ShadowCastingMode.On;

        renderer.receiveShadows =
            true;

        renderer.lightProbeUsage =
            LightProbeUsage.BlendProbes;

        renderer.reflectionProbeUsage =
            ReflectionProbeUsage.BlendProbes;

        proxyRenderers.Add(
            renderer);

        /*
         * Intencionadamente no se añade ningún Collider.
         * El whitebox original conserva toda la autoridad física/espacial.
         */
    }

    private void SynchronizeSourceVisualState()
    {
        if (sourceRenderer == null ||
            proxyRenderers.Count == 0)
        {
            return;
        }

        sourcePropertyBlock.Clear();

        sourceRenderer.GetPropertyBlock(
            sourcePropertyBlock);

        for (int index = 0;
             index < proxyRenderers.Count;
             index++)
        {
            MeshRenderer renderer =
                proxyRenderers[index];

            if (renderer != null)
            {
                renderer.SetPropertyBlock(
                    sourcePropertyBlock);
            }
        }
    }

    private void RestoreSource()
    {
        if (sourceRenderer != null)
        {
            sourceRenderer.enabled =
                sourceWasEnabled;
        }
    }

    private void SetProxyVisible(
        bool visible)
    {
        if (proxyRoot != null)
            proxyRoot.gameObject.SetActive(
                visible);
    }

    private void DestroyProxy()
    {
        if (proxyRoot == null)
            return;

        GameObject target =
            proxyRoot.gameObject;

        proxyRoot = null;
        proxyRenderers.Clear();

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static void EnsureUnitCubeMesh()
    {
        if (unitCubeMesh != null)
            return;

        Vector3[] vertices =
        {
            // Front
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),

            // Back
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),

            // Left
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f,  0.5f),
            new Vector3(-0.5f,  0.5f, -0.5f),

            // Right
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f, -0.5f),
            new Vector3( 0.5f,  0.5f,  0.5f),

            // Top
            new Vector3(-0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f,  0.5f),
            new Vector3( 0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),

            // Bottom
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f, -0.5f),
            new Vector3( 0.5f, -0.5f,  0.5f),
            new Vector3(-0.5f, -0.5f,  0.5f)
        };

        int[] triangles =
        {
             0, 1, 2,  0, 2, 3,
             4, 5, 6,  4, 6, 7,
             8, 9,10,  8,10,11,
            12,13,14, 12,14,15,
            16,17,18, 16,18,19,
            20,21,22, 20,22,23
        };

        Vector3[] normals =
        {
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down
        };

        Vector2[] uvs =
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),

            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),

            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),

            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),

            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),

            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };

        unitCubeMesh =
            new Mesh
            {
                name =
                    "BB_Presentation_UnitCube",
                hideFlags =
                    HideFlags.HideAndDontSave
            };

        unitCubeMesh.vertices =
            vertices;

        unitCubeMesh.triangles =
            triangles;

        unitCubeMesh.normals =
            normals;

        unitCubeMesh.uv =
            uvs;

        unitCubeMesh.RecalculateBounds();
    }
}
