using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairSpatialReadinessValidator
    {
        internal const string Version = "1.0.0";

        private const string ContractRoot =
            "Assets/Resources/BistroBuilder/Spatial/Contracts/";

        internal static SavicChairSpatialReadinessRecord Validate(
            SavicManifest manifest,
            SavicChairAuthoringRecord plan,
            string prefabAssetPath)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "BBSIS readiness requires a valid chair authoring plan.");
            }

            RestaurantSeatUseProfileDefinition profile =
                AssetDatabase.LoadAssetAtPath
                    <RestaurantSeatUseProfileDefinition>(
                        plan.seatUseProfileAssetPath);

            if (profile == null)
            {
                throw new InvalidOperationException(
                    "Chair seat-use profile is missing.");
            }

            if (!profile.ValidateConfiguration(
                    out string profileError))
            {
                throw new InvalidOperationException(
                    "Chair seat-use profile is invalid: " +
                    profileError);
            }

            if (Math.Abs(
                    profile.SeatHeight -
                    plan.finalSeatHeightMeters) >
                profile.MaximumVerticalDifference)
            {
                throw new InvalidOperationException(
                    "Normalized chair seat height is incompatible with its canonical seat-use profile.");
            }

            string profileId =
                profile.ProfileId;

            if (string.IsNullOrWhiteSpace(
                    profileId))
            {
                throw new InvalidOperationException(
                    "Chair seat-use profile has no stable ProfileId.");
            }

            string contractAssetPath =
                ContractRoot +
                "BB_SpatialContract_Seat_" +
                profileId +
                ".asset";

            BistroBuilderSpatialContractDefinition contract =
                AssetDatabase.LoadAssetAtPath
                    <BistroBuilderSpatialContractDefinition>(
                        contractAssetPath);

            if (contract == null)
            {
                throw new InvalidOperationException(
                    "No BBSIS chair contract exists for SeatUseProfile '" +
                    profileId +
                    "'.");
            }

            if (!contract.ValidateDefinition(
                    out string contractError))
            {
                throw new InvalidOperationException(
                    "BBSIS chair contract is invalid: " +
                    contractError);
            }

            if (!string.Equals(
                    contract.FamilyId,
                    "seating.chair",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "BBSIS chair contract belongs to the wrong family.");
            }

            if (contract.PreferredProxyMode !=
                BistroBuilderAdaptiveSpatialProxyMode.Articulated)
            {
                throw new InvalidOperationException(
                    "BBSIS chair contract must use an Articulated proxy.");
            }

            RequireTrait(contract, "seating.chair");
            RequireTrait(contract, "dynamic.sweep");
            RequireTrait(contract, "seat.bay");
            RequireTrait(contract, "interaction.approach");

            ValidateRequiredPort(contract, "seat");
            ValidateRequiredPort(contract, "approach");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    prefabAssetPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Published chair prefab cannot be loaded for BBSIS validation.");
            }

            RestaurantSeat seat =
                prefab.GetComponent<RestaurantSeat>();

            RestaurantPlacementFootprint footprint =
                prefab.GetComponent<RestaurantPlacementFootprint>();

            if (seat == null || footprint == null)
            {
                throw new InvalidOperationException(
                    "Published chair prefab is not BBSIS-bindable.");
            }

            if (!ReferenceEquals(seat.UseProfile, profile))
            {
                throw new InvalidOperationException(
                    "Published chair uses a different seat-use profile than its authoring plan.");
            }

            if (!seat.ValidateConfiguration(out string seatError))
            {
                throw new InvalidOperationException(
                    "Published chair seating configuration is invalid: " +
                    seatError);
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
                    "Published chair footprint is inconsistent with normalized dimensions.");
            }

            RuntimeBindingCounts counts =
                ValidateRuntimeBinding(
                    prefab,
                    contract,
                    manifest.savicId);

            return new SavicChairSpatialReadinessRecord
            {
                validated = true,
                validatorVersion = Version,
                contractAssetPath = contractAssetPath,
                contractId = contract.ContractId,
                familyId = contract.FamilyId,
                configurationId = profileId,
                runtimeBindingValidated = true,
                staticVolumeCount = counts.Static,
                operationalVolumeCount = counts.Operational,
                dynamicVolumeCount = counts.Dynamic,
                semanticVolumeCount = counts.Semantic,
                evidence =
                    "Canonical standard-dining-chair profile maps to a valid " +
                    "seating.chair Articulated contract; runtime BindSeat created " +
                    "valid static, operational and dynamic proxy volumes plus " +
                    counts.Semantic +
                    " semantic volume(s), with seat/approach ports bound to the " +
                    "published functional anchors.",
                validatedUtc = DateTime.UtcNow.ToString("O")
            };
        }

        private static RuntimeBindingCounts ValidateRuntimeBinding(
            GameObject prefab,
            BistroBuilderSpatialContractDefinition contract,
            string savicId)
        {
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab) as GameObject;

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate chair prefab for BBSIS validation.");
            }

            try
            {
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                RestaurantSeat seat =
                    instance.GetComponent<RestaurantSeat>();

                if (seat == null)
                {
                    throw new InvalidOperationException(
                        "Runtime chair seat component is missing.");
                }

                string subjectId =
                    "savic.validation.chair." +
                    (string.IsNullOrWhiteSpace(savicId)
                        ? "unknown"
                        : savicId);

                if (!BistroBuilderSpatialBindingUtility.BindSeat(
                        seat,
                        contract,
                        subjectId))
                {
                    throw new InvalidOperationException(
                        "BistroBuilderSpatialBindingUtility.BindSeat rejected the published chair.");
                }

                BistroBuilderSpatialSubject subject =
                    instance.GetComponent<BistroBuilderSpatialSubject>();

                BistroBuilderAdaptiveSpatialProxy proxy =
                    instance.GetComponent<BistroBuilderAdaptiveSpatialProxy>();

                BistroBuilderSeatSpatialAdapter adapter =
                    instance.GetComponent<BistroBuilderSeatSpatialAdapter>();

                if (subject == null || proxy == null || adapter == null)
                {
                    throw new InvalidOperationException(
                        "BBSIS runtime binding did not create subject, proxy and seat adapter.");
                }

                if (!subject.ValidateSubject(out string subjectError))
                {
                    throw new InvalidOperationException(
                        "BBSIS chair subject validation failed: " +
                        subjectError);
                }

                if (!ReferenceEquals(subject.Contract, contract))
                {
                    throw new InvalidOperationException(
                        "BBSIS chair subject resolved the wrong contract.");
                }

                if (proxy.Mode !=
                    BistroBuilderAdaptiveSpatialProxyMode.Articulated)
                {
                    throw new InvalidOperationException(
                        "Runtime chair proxy is not Articulated.");
                }

                List<BistroBuilderSpatialVolume> volumes =
                    new List<BistroBuilderSpatialVolume>(4);

                int staticCount =
                    proxy.BuildWorldVolumes(
                        BistroBuilderSpatialProxyLayer.Static,
                        volumes);

                volumes.Clear();

                int operationalCount =
                    proxy.BuildWorldVolumes(
                        BistroBuilderSpatialProxyLayer.Operational,
                        volumes);

                volumes.Clear();

                int dynamicCount =
                    proxy.BuildWorldVolumes(
                        BistroBuilderSpatialProxyLayer.Dynamic,
                        volumes);

                if (staticCount < 1 ||
                    operationalCount < 1 ||
                    dynamicCount < 1)
                {
                    throw new InvalidOperationException(
                        "BBSIS chair proxy did not emit all required spatial layers.");
                }

                List<BistroBuilderSpatialSemanticVolume> semantics =
                    new List<BistroBuilderSpatialSemanticVolume>(4);

                int semanticCount =
                    adapter.WriteSemanticVolumes(semantics);

                if (semanticCount < 2)
                {
                    throw new InvalidOperationException(
                        "BBSIS chair adapter did not emit approach + dynamic sweep semantics.");
                }

                bool hasApproach = false;
                bool hasSweep = false;

                for (int index = 0;
                     index < semantics.Count;
                     index++)
                {
                    BistroBuilderSpatialSemanticVolume semantic =
                        semantics[index];

                    hasApproach |=
                        semantic.role ==
                        BistroBuilderSpatialSemanticRole.Approach;

                    hasSweep |=
                        semantic.role ==
                        BistroBuilderSpatialSemanticRole.DynamicSweep;
                }

                if (!hasApproach || !hasSweep)
                {
                    throw new InvalidOperationException(
                        "BBSIS chair semantic output is missing approach or dynamic sweep.");
                }

                ValidateBoundPort(
                    subject,
                    "seat",
                    seat.SeatPoint);

                ValidateBoundPort(
                    subject,
                    "approach",
                    seat.CustomerApproachPoint);

                return new RuntimeBindingCounts(
                    staticCount,
                    operationalCount,
                    dynamicCount,
                    semanticCount);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateBoundPort(
            BistroBuilderSpatialSubject subject,
            string portId,
            Transform expectedAnchor)
        {
            if (expectedAnchor == null)
            {
                throw new InvalidOperationException(
                    "Expected BBSIS anchor '" +
                    portId +
                    "' is missing.");
            }

            if (!subject.TryGetPortWorld(
                    portId,
                    out Vector3 position,
                    out Vector3 forward,
                    out float radius,
                    out _))
            {
                throw new InvalidOperationException(
                    "BBSIS chair subject cannot resolve port '" +
                    portId +
                    "'.");
            }

            if ((position -
                 expectedAnchor.position).sqrMagnitude >
                0.0001f)
            {
                throw new InvalidOperationException(
                    "BBSIS chair port '" +
                    portId +
                    "' is not bound to its canonical functional anchor.");
            }

            if (radius < 0.05f ||
                !IsFinite(radius) ||
                !IsFinite(forward) ||
                forward.sqrMagnitude < 0.5f)
            {
                throw new InvalidOperationException(
                    "BBSIS chair port '" +
                    portId +
                    "' produced invalid runtime geometry.");
            }
        }

        private static void ValidateRequiredPort(
            BistroBuilderSpatialContractDefinition contract,
            string portId)
        {
            int count = 0;

            for (int index = 0;
                 index < contract.Ports.Count;
                 index++)
            {
                BistroBuilderSpatialPortDefinition port =
                    contract.Ports[index];

                if (port == null ||
                    !string.Equals(
                        port.portId,
                        portId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                count++;

                if (port.radius < 0.12f)
                {
                    throw new InvalidOperationException(
                        "BBSIS chair port '" +
                        portId +
                        "' radius is implausibly small.");
                }
            }

            if (count != 1)
            {
                throw new InvalidOperationException(
                    "BBSIS chair contract must expose exactly one '" +
                    portId +
                    "' port.");
            }
        }

        private static void RequireTrait(
            BistroBuilderSpatialContractDefinition contract,
            string traitId)
        {
            if (!contract.HasTrait(traitId))
            {
                throw new InvalidOperationException(
                    "BBSIS chair contract is missing required trait '" +
                    traitId +
                    "'.");
            }
        }

        private static bool Approximately(
            float left,
            float right,
            float tolerance)
        {
            return Math.Abs(left - right) <=
                   Math.Max(0f, tolerance);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) &&
                   !float.IsInfinity(value);
        }

        private readonly struct RuntimeBindingCounts
        {
            internal RuntimeBindingCounts(
                int staticCount,
                int operationalCount,
                int dynamicCount,
                int semanticCount)
            {
                Static = staticCount;
                Operational = operationalCount;
                Dynamic = dynamicCount;
                Semantic = semanticCount;
            }

            internal int Static { get; }
            internal int Operational { get; }
            internal int Dynamic { get; }
            internal int Semantic { get; }
        }
    }
}
