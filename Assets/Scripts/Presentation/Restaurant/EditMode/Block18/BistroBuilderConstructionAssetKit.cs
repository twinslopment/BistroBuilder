using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderConstructionOpeningAsset
{
    public string definitionId = string.Empty;
    public string openingType = "door";
    public GameObject prefab;
    public Vector3 nominalSizeMeters = new Vector3(0.9f, 2.1f, 0.12f);
}

[Serializable]
public sealed class BistroBuilderConstructionWallVisualAsset
{
    public string definitionId = "wall.default";
    public GameObject prefab;
    public Vector3 nominalSizeMeters = new Vector3(0.49141f, 1.89958f, 0.03436f);
}

[CreateAssetMenu(menuName="Bistro Builder/Construction/Asset Kit")]
public sealed class BistroBuilderConstructionAssetKit : ScriptableObject
{
    public Material wallMaterial;
    public Material floorMaterial;
    public Material trimMaterial;
    public Material glassMaterial;

    // Legacy/default references remain valid for existing scenes and documents.
    public GameObject doorPrefab;
    public GameObject windowPrefab;
    public GameObject[] wallModules;

    [SerializeField] private List<BistroBuilderConstructionOpeningAsset> openingAssets =
        new List<BistroBuilderConstructionOpeningAsset>();
    [SerializeField] private List<BistroBuilderConstructionWallVisualAsset> wallVisualAssets =
        new List<BistroBuilderConstructionWallVisualAsset>();

    public IReadOnlyList<BistroBuilderConstructionOpeningAsset> OpeningAssets => openingAssets;
    public IReadOnlyList<BistroBuilderConstructionWallVisualAsset> WallVisualAssets => wallVisualAssets;

    public static BistroBuilderConstructionAssetKit Load() =>
        Resources.Load<BistroBuilderConstructionAssetKit>("BistroBuilder/Construction/ConstructionAssetKit");

    public bool TryResolveOpening(
        string openingType,
        string definitionId,
        out GameObject prefab,
        out Vector3 nominalSizeMeters)
    {
        prefab = null;
        nominalSizeMeters = Vector3.zero;

        string normalizedType = Normalize(openingType);
        string normalizedDefinition = Normalize(definitionId);

        for (int i = 0; i < openingAssets.Count; i++)
        {
            BistroBuilderConstructionOpeningAsset entry = openingAssets[i];
            if (entry == null ||
                entry.prefab == null ||
                !string.Equals(Normalize(entry.openingType), normalizedType, StringComparison.Ordinal) ||
                !string.Equals(Normalize(entry.definitionId), normalizedDefinition, StringComparison.Ordinal))
            {
                continue;
            }

            prefab = entry.prefab;
            nominalSizeMeters = SanitizeNominalSize(
                entry.nominalSizeMeters,
                normalizedType == "window"
                    ? new Vector3(1.2f, 1.2f, 0.12f)
                    : new Vector3(0.9f, 2.1f, 0.12f));
            return true;
        }

        bool requestsDefault =
            string.IsNullOrWhiteSpace(normalizedDefinition) ||
            string.Equals(normalizedDefinition, normalizedType, StringComparison.Ordinal);

        if (!requestsDefault)
            return false;

        if (string.Equals(normalizedType, "window", StringComparison.Ordinal) &&
            windowPrefab != null)
        {
            prefab = windowPrefab;
            nominalSizeMeters = new Vector3(1.2f, 1.2f, 0.12f);
            return true;
        }

        if (string.Equals(normalizedType, "door", StringComparison.Ordinal) &&
            doorPrefab != null)
        {
            prefab = doorPrefab;
            nominalSizeMeters = new Vector3(0.9f, 2.1f, 0.12f);
            return true;
        }

        return false;
    }

    public bool TryResolveWallVisual(
        string definitionId,
        out GameObject prefab,
        out Vector3 nominalSizeMeters)
    {
        prefab = null;
        nominalSizeMeters = Vector3.zero;
        string normalizedDefinition = Normalize(definitionId);

        for (int i = 0; i < wallVisualAssets.Count; i++)
        {
            BistroBuilderConstructionWallVisualAsset entry = wallVisualAssets[i];
            if (entry == null ||
                entry.prefab == null ||
                !string.Equals(Normalize(entry.definitionId), normalizedDefinition, StringComparison.Ordinal))
            {
                continue;
            }

            prefab = entry.prefab;
            nominalSizeMeters = SanitizeNominalSize(
                entry.nominalSizeMeters,
                new Vector3(0.49141f, 1.89958f, 0.03436f));
            return true;
        }

        return false;
    }

    public void UpsertOpening(
        string definitionId,
        string openingType,
        GameObject prefab,
        Vector3 nominalSizeMeters)
    {
        string normalizedDefinition = Normalize(definitionId);
        string normalizedType = Normalize(openingType);

        if (string.IsNullOrWhiteSpace(normalizedDefinition) ||
            (normalizedType != "door" && normalizedType != "window") ||
            prefab == null)
        {
            throw new ArgumentException("Construction opening registration is incomplete.");
        }

        BistroBuilderConstructionOpeningAsset entry = null;
        for (int i = 0; i < openingAssets.Count; i++)
        {
            BistroBuilderConstructionOpeningAsset candidate = openingAssets[i];
            if (candidate != null &&
                string.Equals(Normalize(candidate.definitionId), normalizedDefinition, StringComparison.Ordinal))
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null)
        {
            entry = new BistroBuilderConstructionOpeningAsset();
            openingAssets.Add(entry);
        }

        entry.definitionId = normalizedDefinition;
        entry.openingType = normalizedType;
        entry.prefab = prefab;
        entry.nominalSizeMeters = SanitizeNominalSize(
            nominalSizeMeters,
            normalizedType == "window"
                ? new Vector3(1.2f, 1.2f, 0.12f)
                : new Vector3(0.9f, 2.1f, 0.12f));
    }

    public void UpsertWallVisual(
        string definitionId,
        GameObject prefab,
        Vector3 nominalSizeMeters)
    {
        string normalizedDefinition = Normalize(definitionId);
        if (string.IsNullOrWhiteSpace(normalizedDefinition) || prefab == null)
            throw new ArgumentException("Construction wall registration is incomplete.");

        BistroBuilderConstructionWallVisualAsset entry = null;
        for (int i = 0; i < wallVisualAssets.Count; i++)
        {
            BistroBuilderConstructionWallVisualAsset candidate = wallVisualAssets[i];
            if (candidate != null &&
                string.Equals(Normalize(candidate.definitionId), normalizedDefinition, StringComparison.Ordinal))
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null)
        {
            entry = new BistroBuilderConstructionWallVisualAsset();
            wallVisualAssets.Add(entry);
        }

        entry.definitionId = normalizedDefinition;
        entry.prefab = prefab;
        entry.nominalSizeMeters = SanitizeNominalSize(
            nominalSizeMeters,
            new Vector3(0.49141f, 1.89958f, 0.03436f));
    }

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();

    private static Vector3 SanitizeNominalSize(Vector3 value, Vector3 fallback) =>
        new Vector3(
            value.x > 0.001f ? value.x : fallback.x,
            value.y > 0.001f ? value.y : fallback.y,
            value.z > 0.001f ? value.z : fallback.z);
}
