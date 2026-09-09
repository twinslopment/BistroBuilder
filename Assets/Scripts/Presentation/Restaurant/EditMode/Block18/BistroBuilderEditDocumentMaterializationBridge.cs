using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Restaurant/Edit Mode/Block 18/Edit Document Materialization Bridge")]
public sealed class BistroBuilderEditDocumentMaterializationBridge : MonoBehaviour
{
    [SerializeField] private BistroBuilderEditDocumentRuntimeService runtimeService;
    [SerializeField] private BistroBuilderArchitectureRuntimeMaterializer materializer;
    [SerializeField] private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField] private BistroBuilderOperationalSpatialCoordinator operationalSpatialCoordinator;
    [SerializeField] private BistroBuilderNavigationService navigationService;
    [SerializeField] private bool rebuildOnEnable = true;

    private void OnEnable()
    {
        CacheDependencies();
        if (runtimeService != null)
        {
            runtimeService.DocumentPublished -= HandlePublished;
            runtimeService.DocumentPublished += HandlePublished;
        }
        if (rebuildOnEnable && runtimeService != null)
            RebuildProjection(runtimeService.GetCommittedSnapshot());
    }

    private void OnDisable()
    {
        if (runtimeService != null)
            runtimeService.DocumentPublished -= HandlePublished;
    }
    private void HandlePublished(BistroBuilderEditDocument document)
    {
        RebuildProjection(document);
    }

    public void RebuildProjection(BistroBuilderEditDocument document)
    {
        CacheDependencies();
        if (document == null) return;
        if (materializer != null)
            materializer.Rebuild(document);

        spatialService?.RebuildSubjects();
        operationalSpatialCoordinator?.RebuildBindings();
        navigationService?.RebuildNavigationTopology();
    }

    private void CacheDependencies()
    {
        if (runtimeService == null)
            runtimeService = FindFirstObjectByType<BistroBuilderEditDocumentRuntimeService>();
        if (materializer == null)
            materializer = FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>();
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
        if (operationalSpatialCoordinator == null)
            operationalSpatialCoordinator = FindFirstObjectByType<BistroBuilderOperationalSpatialCoordinator>();
        if (navigationService == null)
            navigationService = FindFirstObjectByType<BistroBuilderNavigationService>();
    }
}
