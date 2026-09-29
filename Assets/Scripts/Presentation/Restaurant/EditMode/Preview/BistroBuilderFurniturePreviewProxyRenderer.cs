using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Proxy visual del mobiliario durante una colocación.
/// Oculta únicamente los MeshRenderer visibles y dibuja su geometría en una
/// pose visual elevada, dejando la lógica espacial/transaccional en sus servicios.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Furniture Preview Proxy")]
public sealed class BistroBuilderFurniturePreviewProxyRenderer : MonoBehaviour
{
    [SerializeField] private RestaurantEditInteractionController interactionController;
    [SerializeField] private RestaurantPlacementTransactionService transactionService;
    [SerializeField] private RestaurantPlacementLinkedGroupService linkedGroupService;
    [SerializeField] private BistroBuilderUniversalPreviewService previewService;

    [Header("Sensación de transporte")]
    [SerializeField, Min(0f)] private float liftHeight = 0.085f;
    [SerializeField, Min(0.01f)] private float liftDuration = 0.09f;
    [SerializeField, Min(0.01f)] private float settleDuration = 0.12f;

    private readonly List<PreviewMeshEntry> entries = new List<PreviewMeshEntry>(24);
    private readonly List<RestaurantAreaMember> linkedBuffer = new List<RestaurantAreaMember>(16);
    private readonly HashSet<int> rendererIds =
        new HashSet<int>();

    private readonly HashSet<int> lodRendererIds =
        new HashSet<int>();

    private readonly HashSet<int> lod0RendererIds =
        new HashSet<int>();

    private readonly List<MeshRenderer> suppressedLodRenderers =
        new List<MeshRenderer>(16);

    private RestaurantAreaMember activeRoot;
    private float currentLift;
    private bool settling;

    private void Awake()
    {
        ResolveDependencies();
    }

    private void OnEnable()
    {
        ResolveDependencies();
        Subscribe();

        if (transactionService != null &&
            transactionService.HasActiveTransaction &&
            transactionService.ActiveMember != null)
        {
            BeginProxy(transactionService.ActiveMember);
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreSourceRenderers();
        ResetState();
    }

    private void LateUpdate()
    {
        if (entries.Count == 0) return;

        float delta = Mathf.Max(0f, Time.unscaledDeltaTime);
        if (settling)
        {
            float speed = liftHeight / Mathf.Max(0.01f, settleDuration);
            currentLift = Mathf.MoveTowards(currentLift, 0f, speed * delta);
        }
        else
        {
            float speed = liftHeight / Mathf.Max(0.01f, liftDuration);
            currentLift = Mathf.MoveTowards(currentLift, liftHeight, speed * delta);
        }

        DrawProxyMeshes(currentLift);

        if (settling && currentLift <= 0.0005f)
        {
            RestoreSourceRenderers();
            ResetState();
        }
    }

    private void HandlePlacementStarted(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result)
    {
        BeginProxy(member);
    }

    private void HandleCommitted(
        RestaurantAreaMember member,
        RestaurantPlacementValidationResult result)
    {
        if (activeRoot == null || !ReferenceEquals(activeRoot, member))
            return;

        previewService?.ClearOwner(
            BistroBuilderUniversalPreviewService.FurnitureOwner);
        settling = true;
    }

    private void HandleCancelled(RestaurantAreaMember member)
    {
        if (activeRoot != null &&
            member != null &&
            !ReferenceEquals(activeRoot, member))
            return;

        RestoreSourceRenderers();
        previewService?.ClearOwner(
            BistroBuilderUniversalPreviewService.FurnitureOwner);
        ResetState();
    }

    private void BeginProxy(RestaurantAreaMember root)
    {
        RestoreSourceRenderers();
        ResetState();

        if (root == null) return;

        activeRoot = root;
        CaptureMember(root);

        linkedBuffer.Clear();
        if (linkedGroupService != null)
            linkedGroupService.CopyLinkedMembers(root, linkedBuffer);

        for (int i = 0; i < linkedBuffer.Count; i++)
            CaptureMember(linkedBuffer[i]);

        currentLift = 0f;
        settling = false;
    }

    private void CaptureMember(RestaurantAreaMember member)
    {
        if (member == null) return;

        CollectLodRenderers(member);

        MeshRenderer[] renderers =
            member.GetComponentsInChildren<MeshRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;

            int rendererId =
                renderer.GetInstanceID();

            if (!rendererIds.Add(rendererId))
                continue;

            if (lodRendererIds.Contains(rendererId) &&
                !lod0RendererIds.Contains(rendererId))
            {
                suppressedLodRenderers.Add(renderer);
                renderer.enabled = false;
                continue;
            }

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0) continue;

            MaterialPropertyBlock propertyBlock = null;
            if (renderer.HasPropertyBlock())
            {
                propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
            }

            entries.Add(new PreviewMeshEntry(
                renderer,
                filter.sharedMesh,
                materials,
                propertyBlock,
                renderer.gameObject.layer,
                renderer.shadowCastingMode,
                renderer.receiveShadows));

            renderer.enabled = false;
        }
    }

    private void DrawProxyMeshes(float verticalOffset)
    {
        Matrix4x4 liftMatrix =
            Matrix4x4.Translate(Vector3.up * verticalOffset);

        for (int i = 0; i < entries.Count; i++)
        {
            PreviewMeshEntry entry = entries[i];
            if (!entry.IsUsable) continue;

            Matrix4x4 matrix =
                liftMatrix * entry.Renderer.transform.localToWorldMatrix;

            int subMeshCount = entry.Mesh.subMeshCount;
            int materialCount = entry.Materials.Length;
            int drawCount = Mathf.Min(subMeshCount, materialCount);

            for (int subMesh = 0; subMesh < drawCount; subMesh++)
            {
                Material material = entry.Materials[subMesh];
                if (material == null) continue;

                Graphics.DrawMesh(
                    entry.Mesh,
                    matrix,
                    material,
                    entry.Layer,
                    null,
                    subMesh,
                    entry.PropertyBlock,
                    entry.ShadowCastingMode,
                    entry.ReceiveShadows);
            }
        }
    }

    private void CollectLodRenderers(
        RestaurantAreaMember member)
    {
        LODGroup[] groups =
            member.GetComponentsInChildren<LODGroup>(true);

        for (int groupIndex = 0;
             groupIndex < groups.Length;
             groupIndex++)
        {
            LODGroup group =
                groups[groupIndex];

            if (group == null)
                continue;

            LOD[] lods =
                group.GetLODs();

            for (int lodIndex = 0;
                 lodIndex < lods.Length;
                 lodIndex++)
            {
                Renderer[] lodRenderers =
                    lods[lodIndex].renderers;

                for (int rendererIndex = 0;
                     rendererIndex < lodRenderers.Length;
                     rendererIndex++)
                {
                    Renderer renderer =
                        lodRenderers[rendererIndex];

                    if (renderer == null)
                        continue;

                    int rendererId =
                        renderer.GetInstanceID();

                    lodRendererIds.Add(rendererId);

                    if (lodIndex == 0)
                        lod0RendererIds.Add(rendererId);
                }
            }
        }
    }

    private void RestoreSourceRenderers()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PreviewMeshEntry entry = entries[i];
            if (entry.Renderer != null)
                entry.Renderer.enabled = true;
        }

        for (int i = 0;
             i < suppressedLodRenderers.Count;
             i++)
        {
            MeshRenderer renderer =
                suppressedLodRenderers[i];

            if (renderer != null)
                renderer.enabled = true;
        }
    }

    private void ResetState()
    {
        entries.Clear();
        linkedBuffer.Clear();
        rendererIds.Clear();
        lodRendererIds.Clear();
        lod0RendererIds.Clear();
        suppressedLodRenderers.Clear();
        activeRoot = null;
        currentLift = 0f;
        settling = false;
    }

    private void ResolveDependencies()
    {
        if (interactionController == null)
            interactionController = GetComponent<RestaurantEditInteractionController>();
        if (interactionController == null)
            interactionController = FindFirstObjectByType<RestaurantEditInteractionController>();

        if (transactionService == null && interactionController != null)
            transactionService = interactionController.PlacementTransactionService;
        if (linkedGroupService == null && interactionController != null)
            linkedGroupService = interactionController.PlacementLinkedGroupService;
        if (previewService == null)
            previewService = BistroBuilderUniversalPreviewService.GetOrCreate();
    }

    private void Subscribe()
    {
        if (transactionService != null)
        {
            transactionService.PlacementStarted -= HandlePlacementStarted;
            transactionService.PlacementCommitted -= HandleCommitted;
            transactionService.PlacementCancelled -= HandleCancelled;
            transactionService.PlacementStarted += HandlePlacementStarted;
            transactionService.PlacementCommitted += HandleCommitted;
            transactionService.PlacementCancelled += HandleCancelled;
        }
    }

    private void Unsubscribe()
    {
        if (transactionService != null)
        {
            transactionService.PlacementStarted -= HandlePlacementStarted;
            transactionService.PlacementCommitted -= HandleCommitted;
            transactionService.PlacementCancelled -= HandleCancelled;
        }
    }

    private sealed class PreviewMeshEntry
    {
        public MeshRenderer Renderer { get; }
        public Mesh Mesh { get; }
        public Material[] Materials { get; }
        public MaterialPropertyBlock PropertyBlock { get; }
        public int Layer { get; }
        public ShadowCastingMode ShadowCastingMode { get; }
        public bool ReceiveShadows { get; }

        public bool IsUsable =>
            Renderer != null &&
            Mesh != null &&
            Materials != null;

        public PreviewMeshEntry(
            MeshRenderer renderer,
            Mesh mesh,
            Material[] materials,
            MaterialPropertyBlock propertyBlock,
            int layer,
            ShadowCastingMode shadowCastingMode,
            bool receiveShadows)
        {
            Renderer = renderer;
            Mesh = mesh;
            Materials = materials;
            PropertyBlock = propertyBlock;
            Layer = layer;
            ShadowCastingMode = shadowCastingMode;
            ReceiveShadows = receiveShadows;
        }
    }
}
