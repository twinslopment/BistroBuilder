using System;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [CreateAssetMenu(
        fileName = "FurnitureFinish",
        menuName = "BistroBuilder/Furniture Finishes/Finish Definition")]
    public sealed class FurnitureFinishDefinition : ScriptableObject
    {
        [SerializeField] private string finishId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private FurnitureSurfaceFamily family = FurnitureSurfaceFamily.Unknown;
        [SerializeField] private Material material;
        [SerializeField] private FurnitureFinishChannel providedChannels = FurnitureFinishChannel.None;
        [SerializeField] private string[] tags = Array.Empty<string>();

        public string FinishId => finishId;
        public string DisplayName => displayName;
        public FurnitureSurfaceFamily Family => family;
        public Material Material => material;
        public FurnitureFinishChannel ProvidedChannels => providedChannels;
        public string[] Tags => tags ?? Array.Empty<string>();

#if UNITY_EDITOR
        public void EditorConfigure(
            string id,
            string visibleName,
            FurnitureSurfaceFamily surfaceFamily,
            Material sourceMaterial,
            FurnitureFinishChannel channels,
            string[] finishTags)
        {
            finishId = id ?? string.Empty;
            displayName = visibleName ?? string.Empty;
            family = surfaceFamily;
            material = sourceMaterial;
            providedChannels = channels;
            tags = finishTags ?? Array.Empty<string>();
        }
#endif
    }
}