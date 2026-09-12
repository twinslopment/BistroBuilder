using UnityEngine;

[CreateAssetMenu(menuName="Bistro Builder/Construction/Asset Kit")]
public sealed class BistroBuilderConstructionAssetKit : ScriptableObject
{
    public Material wallMaterial;
    public Material floorMaterial;
    public Material trimMaterial;
    public Material glassMaterial;
    public GameObject doorPrefab;
    public GameObject windowPrefab;
    public GameObject[] wallModules;
    public static BistroBuilderConstructionAssetKit Load() =>
        Resources.Load<BistroBuilderConstructionAssetKit>("BistroBuilder/Construction/ConstructionAssetKit");
}
