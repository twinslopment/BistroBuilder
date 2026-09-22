using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicTableSpatialReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const string ContractRoot =
            "Assets/Resources/BistroBuilder/Spatial/Contracts/";

        internal static SavicTableSpatialReadinessRecord Validate(
            SavicManifest manifest,
            SavicTableAuthoringRecord plan,
            string prefabAssetPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "BBSIS readiness requires a valid table authoring plan.");
            }

            RestaurantTableSeatingConfigurationDefinition seatingDefinition =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantTableSeatingConfigurationDefinition>(
                        plan.seatingDefinitionAssetPath);

            if (seatingDefinition == null)
            {
                throw new InvalidOperationException(
                    "Table seating definition is missing.");
            }

            string configurationId =
                seatingDefinition.ConfigurationId;

            if (string.IsNullOrWhiteSpace(configurationId))
            {
                throw new InvalidOperationException(
                    "Table seating definition has no stable configuration id.");
            }

            if (seatingDefinition.MaximumCustomers !=
                plan.capacity)
            {
                throw new InvalidOperationException(
                    "Table capacity and seating definition capacity differ.");
            }

            string contractAssetPath =
                ContractRoot +
                "BB_SpatialContract_Table_" +
                configurationId +
                ".asset";

            BistroBuilderSpatialContractDefinition contract =
                AssetDatabase.LoadAssetAtPath
                    <BistroBuilderSpatialContractDefinition>(
                        contractAssetPath);

            if (contract == null)
            {
                throw new InvalidOperationException(
                    "No BBSIS table contract exists for seating configuration '" +
                    configurationId +
                    "'.");
            }

            if (!contract.ValidateDefinition(
                    out string contractError))
            {
                throw new InvalidOperationException(
                    "BBSIS table contract is invalid: " +
                    contractError);
            }

            if (!string.Equals(
                    contract.FamilyId,
                    "seating.table",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "BBSIS table contract belongs to the wrong family.");
            }

            RequireTrait(
                contract,
                "seating.table");

            RequireTrait(
                contract,
                "seat.bays");

            RequireTrait(
                contract,
                "service.table");

            int seatBayPorts =
                ValidateSeatBayPorts(
                    contract,
                    plan.capacity);

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabAssetPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Published table prefab cannot be loaded for BBSIS validation.");
            }

            RestaurantTableSeatingConfiguration table =
                prefab.GetComponent
                    <RestaurantTableSeatingConfiguration>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent
                    <RestaurantPlacementFootprint>();

            if (table == null ||
                footprint == null)
            {
                throw new InvalidOperationException(
                    "Published table prefab is not BBSIS-bindable.");
            }

            if (!ReferenceEquals(
                    table.Definition,
                    seatingDefinition))
            {
                throw new InvalidOperationException(
                    "Published table uses a different seating definition than its authoring plan.");
            }

            if (!Approximately(
                    footprint.Size.x,
                    plan.finalWidthMeters,
                    0.002f) ||
                !Approximately(
                    footprint.Size.y,
                    plan.finalDepthMeters,
                    0.002f))
            {
                throw new InvalidOperationException(
                    "Published table footprint is inconsistent with BBSIS proxy dimensions.");
            }

            int emittedSeatBays =
                ValidateRuntimeBinding(
                    prefab,
                    contract,
                    manifest.savicId,
                    plan.capacity);

            return new SavicTableSpatialReadinessRecord
            {
                validated = true,
                validatorVersion = Version,
                contractAssetPath =
                    contractAssetPath,
                contractId =
                    contract.ContractId,
                familyId =
                    contract.FamilyId,
                configurationId =
                    configurationId,
                seatBayPortCount =
                    seatBayPorts,
                emittedSeatBayCount =
                    emittedSeatBays,
                runtimeBindingValidated =
                    true,
                evidence =
                    "Canonical seating configuration maps to a valid seating.table " +
                    "contract; runtime BindTable generated a valid subject/proxy and " +
                    emittedSeatBays +
                    " SeatBay semantic volume(s).",
                validatedUtc =
                    DateTime.UtcNow.ToString("O")
            };
        }

        private static int ValidateSeatBayPorts(
            BistroBuilderSpatialContractDefinition contract,
            int expectedCapacity)
        {
            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.Ordinal);

            int count = 0;

            for (int index = 0;
                 index < contract.Ports.Count;
                 index++)
            {
                BistroBuilderSpatialPortDefinition port =
                    contract.Ports[index];

                if (port == null ||
                    port.kind !=
                        BistroBuilderSpatialPortKind.SeatBay)
                {
                    continue;
                }

                count++;

                if (!ids.Add(
                        port.portId ?? string.Empty))
                {
                    throw new InvalidOperationException(
                        "BBSIS table contract contains duplicate SeatBay port ids.");
                }

                if (port.radius < 0.12f)
                {
                    throw new InvalidOperationException(
                        "BBSIS SeatBay port radius is implausibly small.");
                }
            }

            if (count !=
                expectedCapacity)
            {
                throw new InvalidOperationException(
                    "BBSIS table contract exposes " +
                    count +
                    " SeatBay port(s), but the table capacity is " +
                    expectedCapacity +
                    ".");
            }

            for (int index = 0;
                 index < expectedCapacity;
                 index++)
            {
                string expectedId =
                    "seat." +
                    index;

                if (!ids.Contains(
                        expectedId))
                {
                    throw new InvalidOperationException(
                        "BBSIS table contract is missing expected port '" +
                        expectedId +
                        "'.");
                }
            }

            return count;
        }

        private static int ValidateRuntimeBinding(
            GameObject prefab,
            BistroBuilderSpatialContractDefinition contract,
            string savicId,
            int expectedCapacity)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab) as GameObject;

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate table prefab for BBSIS validation.");
            }

            try
            {
                instance.transform.position =
                    Vector3.zero;

                instance.transform.rotation =
                    Quaternion.identity;

                RestaurantTableSeatingConfiguration table =
                    instance.GetComponent
                        <RestaurantTableSeatingConfiguration>();

                if (table == null)
                {
                    throw new InvalidOperationException(
                        "Runtime table seating component is missing.");
                }

                string subjectId =
                    "savic.validation.table." +
                    (string.IsNullOrWhiteSpace(savicId)
                        ? "unknown"
                        : savicId);

                if (!BistroBuilderSpatialBindingUtility.BindTable(
                        table,
                        contract,
                        subjectId))
                {
                    throw new InvalidOperationException(
                        "BistroBuilderSpatialBindingUtility.BindTable rejected the published table.");
                }

                BistroBuilderSpatialSubject subject =
                    instance.GetComponent
                        <BistroBuilderSpatialSubject>();

                BistroBuilderTableSpatialAdapter adapter =
                    instance.GetComponent
                        <BistroBuilderTableSpatialAdapter>();

                if (subject == null ||
                    adapter == null)
                {
                    throw new InvalidOperationException(
                        "BBSIS runtime binding did not create subject and table adapter.");
                }

                if (!subject.ValidateSubject(
                        out string subjectError))
                {
                    throw new InvalidOperationException(
                        "BBSIS subject validation failed: " +
                        subjectError);
                }

                if (!ReferenceEquals(
                        subject.Contract,
                        contract))
                {
                    throw new InvalidOperationException(
                        "BBSIS subject resolved the wrong contract.");
                }

                List<BistroBuilderSpatialSemanticVolume> volumes =
                    new List<BistroBuilderSpatialSemanticVolume>(
                        expectedCapacity);

                int emitted =
                    adapter.WriteSemanticVolumes(
                        volumes);

                if (emitted !=
                    expectedCapacity)
                {
                    throw new InvalidOperationException(
                        "BBSIS table adapter emitted " +
                        emitted +
                        " SeatBay volume(s), expected " +
                        expectedCapacity +
                        ".");
                }

                for (int index = 0;
                     index < volumes.Count;
                     index++)
                {
                    BistroBuilderSpatialSemanticVolume volume =
                        volumes[index];

                    if (volume.role !=
                            BistroBuilderSpatialSemanticRole.SeatBay ||
                        volume.volume.shapeKind !=
                            BistroBuilderSpatialShapeKind.Circle ||
                        volume.volume.radius < 0.12f)
                    {
                        throw new InvalidOperationException(
                            "BBSIS emitted an invalid SeatBay semantic volume.");
                    }
                }

                return emitted;
            }
            finally
            {
                Object.DestroyImmediate(
                    instance);
            }
        }

        private static void RequireTrait(
            BistroBuilderSpatialContractDefinition contract,
            string traitId)
        {
            if (!contract.HasTrait(
                    traitId))
            {
                throw new InvalidOperationException(
                    "BBSIS table contract is missing required trait '" +
                    traitId +
                    "'.");
            }
        }

        private static bool Approximately(
            float a,
            float b,
            float tolerance)
        {
            return Math.Abs(
                       a -
                       b) <=
                   Math.Max(
                       0f,
                       tolerance);
        }
    }
}
