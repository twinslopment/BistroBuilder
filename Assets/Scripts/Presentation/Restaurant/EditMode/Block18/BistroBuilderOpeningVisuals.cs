using System.Collections.Generic;
using UnityEngine;

/// <summary>Frames identify real wall openings. Door passages remain unobstructed.</summary>
public static class BistroBuilderOpeningVisuals
{
    public static void Build(Transform parent, BistroBuilderWallRecord wall,
        IReadOnlyList<BistroBuilderOpeningRecord> openings, Material material)
    {
        foreach (var opening in openings)
        {
            var kit = BistroBuilderConstructionAssetKit.Load();
            bool window = opening.openingType == "window";
            var prefab = kit != null ? (window ? kit.windowPrefab : kit.doorPrefab) : null;
            if (prefab != null)
            {
                var fill = Object.Instantiate(prefab, parent, false);
                fill.transform.localPosition = new Vector3(wall.Length * opening.axisPosition01, opening.bottomElevation, 0);
                fill.transform.localScale = new Vector3(opening.width / (window ? 1.2f : 0.9f), opening.height / (window ? 1.2f : 2.1f), Mathf.Max(1,wall.thickness / 0.12f));
                if (opening.flipped) fill.transform.localRotation = Quaternion.Euler(0,180,0);
                continue;
            }
            float center = wall.Length * opening.axisPosition01;
            float left = center - opening.width * 0.5f;
            float right = center + opening.width * 0.5f;
            float bottom = opening.bottomElevation;
            float top = bottom + opening.height;
            float depth = wall.thickness + 0.04f;
            const float trim = 0.045f;
            Part(parent, "Jamba", new Vector3(left,bottom+opening.height/2,0), new Vector3(trim,opening.height,depth), material);
            Part(parent, "Jamba", new Vector3(right,bottom+opening.height/2,0), new Vector3(trim,opening.height,depth), material);
            Part(parent, "Dintel", new Vector3(center,top,0), new Vector3(opening.width+trim,trim,depth), material);
            if (opening.openingType == "window")
            {
                Part(parent, "Alféizar", new Vector3(center,bottom,0), new Vector3(opening.width+trim,trim,depth+0.08f), material);
                Part(parent, "Montante", new Vector3(center,bottom+opening.height/2,0), new Vector3(trim,opening.height,trim), material);
            }
        }
    }
    private static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        var collider = go.GetComponent<Collider>(); collider.enabled = false;
        if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);
        go.GetComponent<Renderer>().sharedMaterial = material;
    }
}
