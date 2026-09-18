using System.Collections.Generic;
using UnityEngine;

/// <summary>Visual draft only: no colliders, subjects, navigation or published document.</summary>
public sealed class BistroBuilderConstructionDraftView : MonoBehaviour
{
    private GameObject root;
    private Material material;
    private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();

    public void Show(BistroBuilderEditDocument document)
    {
        Clear();
        var committed = FindFirstObjectByType<BistroBuilderArchitectureRuntimeMaterializer>();
        if (committed != null)
            foreach (var renderer in committed.GetComponentsInChildren<Renderer>())
            { hidden[renderer] = renderer.enabled; renderer.enabled = false; }
        root = new GameObject("ConstructionDraftPreview"); root.transform.SetParent(transform, false);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "ConstructionDraftMaterial" };
            material.color = new Color(0.72f, 0.78f, 0.68f);
        }
        var openings = new List<BistroBuilderOpeningRecord>();
        foreach (var wall in document.walls)
        {
            openings.Clear();
            foreach (var opening in document.openings) if (opening.hostWallId == wall.wallId) openings.Add(opening);
            var go = new GameObject("DraftWall"); go.transform.SetParent(root.transform, false);
            go.transform.position = new Vector3(wall.axisStart.x, wall.baseElevation, wall.axisStart.y);
            var axis = wall.axisEnd-wall.axisStart;
            go.transform.rotation = Quaternion.FromToRotation(Vector3.right, new Vector3(axis.x, 0, axis.y).normalized);
            go.AddComponent<MeshFilter>().sharedMesh = BistroBuilderWallGeometryBuilder.Build(wall, openings);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            BistroBuilderOpeningVisuals.Build(go.transform, wall, openings, material);
        }
        var topology = new BistroBuilderWallTopologyBuilder().Build(document.walls,document.revision);
        if (!topology.HasBlockingDiagnostics)
        foreach (var face in new BistroBuilderRoomFaceDetector().Detect(topology))
        {
            var floor = new GameObject("DraftFloor"); floor.transform.SetParent(root.transform,false);
            floor.AddComponent<MeshFilter>().sharedMesh = BistroBuilderPlanarGeometryBuilder.BuildHorizontalPolygon(face.boundary,0.012f);
            var kit = BistroBuilderConstructionAssetKit.Load();
            floor.AddComponent<MeshRenderer>().sharedMaterial = kit != null ? kit.floorMaterial : material;
        }
    }

    public void Clear()
    {
        foreach (var item in hidden) if (item.Key != null) item.Key.enabled = item.Value;
        hidden.Clear();
        if (root == null) return;
        root.SetActive(false);
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null && (filter.gameObject.name == "DraftWall" || filter.gameObject.name == "DraftFloor")) Destroy(filter.sharedMesh);
        Destroy(root); root = null;
    }
    private void OnDisable() => Clear();
    private void OnDestroy() { Clear(); if (material != null) Destroy(material); }
}
