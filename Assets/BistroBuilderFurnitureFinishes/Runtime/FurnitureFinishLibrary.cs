using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes
{
    [CreateAssetMenu(
        fileName = "FurnitureFinishLibrary",
        menuName = "BistroBuilder/Furniture Finishes/Finish Library")]
    public sealed class FurnitureFinishLibrary : ScriptableObject
    {
        [SerializeField] private FurnitureFinishDefinition[] finishes = Array.Empty<FurnitureFinishDefinition>();

        public IReadOnlyList<FurnitureFinishDefinition> Finishes => finishes;

        public IEnumerable<FurnitureFinishDefinition> Enumerate(FurnitureSurfaceFamily family)
        {
            foreach (var finish in finishes ?? Array.Empty<FurnitureFinishDefinition>())
            {
                if (finish == null)
                    continue;
                if (family == FurnitureSurfaceFamily.Unknown || finish.Family == family)
                    yield return finish;
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(FurnitureFinishDefinition[] entries)
        {
            finishes = entries ?? Array.Empty<FurnitureFinishDefinition>();
        }
#endif
    }
}