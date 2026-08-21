using System;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEngine;

namespace BistroBuilder.LivingArchitecture.Runtime
{
    /// <summary>
    /// Autoridad runtime en memoria para el edificio canónico. No deriva estado de Transforms/GameObjects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchitectureStateService : MonoBehaviour
    {
        private ArchitectureBuilding currentBuilding;
        public ArchitectureBuilding CurrentBuilding => currentBuilding;

        public void EnsureInitialized()
        {
            if (currentBuilding != null) return;
            currentBuilding = CreateEmptyBuilding();
        }

        public ArchitectureBuilding CaptureClone()
        {
            EnsureInitialized();
            return currentBuilding.DeepClone();
        }

        public bool TryReplace(ArchitectureBuilding replacement, out string error)
        {
            if (replacement == null) { error = "LA8_REPLACE_NULL"; return false; }
            var snapshot = new ArchitectureSnapshot { Building = replacement };
            var validation = ArchitectureValidator.Validate(snapshot);
            if (!validation.IsValid)
            {
                error = "LA8_REPLACE_INVALID";
                return false;
            }
            currentBuilding = replacement.DeepClone();
            error = string.Empty;
            return true;
        }

        public void ResetToEmpty()
        {
            currentBuilding = CreateEmptyBuilding();
        }

        private static ArchitectureBuilding CreateEmptyBuilding()
        {
            var building = new ArchitectureBuilding { Id = BuildingId.New() };
            building.Levels.Add(new ArchitectureLevel { Id = LevelId.New(), Elevation = 0d });
            return building;
        }

        private void Awake() => EnsureInitialized();
    }
}
