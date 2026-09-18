using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/BBPLFS/Preview Service")]
public sealed class BBPLFSPreviewService : MonoBehaviour
{
    private readonly List<GameObject> previewObjects = new();
    private Transform previewRoot;

    public bool HasPreview => previewObjects.Count > 0;

    public void ShowCandidate(BBPLFSLayoutCandidate candidate)
    {
        ClearPreview();
        if (candidate == null)
        {
            return;
        }

        GameObject rootObject = new("BBPLFS_PREVIEW");
        previewRoot = rootObject.transform;
        previewRoot.SetParent(transform, false);

        IReadOnlyList<BBPLFSLayoutPlacement> placements = candidate.Placements;
        for (int index = 0; index < placements.Count; index++)
        {
            BBPLFSLayoutPlacement placement = placements[index];
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Preview_" + placement.Role + "_" + index;
            marker.transform.SetParent(previewRoot, true);
            marker.transform.SetPositionAndRotation(placement.WorldPosition + Vector3.up * 0.05f, placement.WorldRotation);
            marker.transform.localScale = placement.Role == "table"
                ? new Vector3(1.2f, 0.08f, 0.8f)
                : new Vector3(0.45f, 0.08f, 0.45f);

            if (marker.TryGetComponent(out Collider collider))
            {
                collider.enabled = false;
            }

            previewObjects.Add(marker);
        }
    }
    public void ClearPreview()
    {
        for (int index = previewObjects.Count - 1; index >= 0; index--)
        {
            GameObject previewObject = previewObjects[index];
            if (previewObject != null)
            {
                Destroy(previewObject);
            }
        }

        previewObjects.Clear();

        if (previewRoot != null)
        {
            Destroy(previewRoot.gameObject);
            previewRoot = null;
        }
    }

    private void OnDisable()
    {
        ClearPreview();
    }
}