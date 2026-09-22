using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicChairColliderBuilder
    {
        internal const string Version = "1.0.0";

        private const string CollisionRootName =
            "SAVIC_Collision";

        private const string SeatColliderName =
            "Seat";

        private const string BackColliderName =
            "Backrest";

        private const string SupportPrefix =
            "Support_";

        private const string ArmPrefix =
            "Arm_";

        private const float MinimumColliderSize = 0.025f;
        private const float SupportPaddingMeters = 0.012f;

        internal static SavicChairColliderAuthoringRecord Build(
            GameObject root,
            SavicManifest manifest,
            SavicChairAuthoringRecord plan)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (manifest?.model3D == null)
            {
                throw new InvalidOperationException(
                    "Chair collider generation requires model analysis.");
            }

            if (plan == null ||
                !plan.planned)
            {
                throw new InvalidOperationException(
                    "Chair collider generation requires a valid authoring plan.");
            }

            SavicSemanticPartAnalysisRecord semantic =
                manifest.model3D.semanticParts;

            if (semantic == null ||
                !semantic.analyzed ||
                !semantic.automationReady)
            {
                throw new InvalidOperationException(
                    "Chair collider generation requires automation-ready semantic parts.");
            }

            SavicSemanticPartRecord seat =
                FindPart(
                    semantic,
                    "chair.seat");

            SavicSemanticPartRecord back =
                FindPart(
                    semantic,
                    "chair.back");

            SavicSemanticPartRecord support =
                FindPart(
                    semantic,
                    "chair.support");

            SavicSemanticPartRecord arms =
                FindPart(
                    semantic,
                    "chair.arms");

            if (seat == null ||
                back == null ||
                support == null)
            {
                throw new InvalidOperationException(
                    "Chair semantic model must contain seat, back and support.");
            }

            RemoveAllColliders(root);

            Transform collisionRoot =
                EnsureDirectChild(
                    root.transform,
                    CollisionRootName);

            ClearChildren(
                collisionRoot);

            Vector3 scaledSourceSize =
                new Vector3(
                    manifest.model3D.widthMeters,
                    manifest.model3D.heightMeters,
                    manifest.model3D.depthMeters) *
                plan.uniformScale;

            Quaternion sourceYaw =
                Quaternion.Euler(
                    0f,
                    plan.visualYawDegrees,
                    0f);

            CreatePartCollider(
                collisionRoot,
                SeatColliderName,
                seat,
                scaledSourceSize,
                sourceYaw);

            CreatePartCollider(
                collisionRoot,
                BackColliderName,
                back,
                scaledSourceSize,
                sourceYaw);

            int supportCount =
                BuildSupportColliders(
                    collisionRoot,
                    semantic,
                    seat,
                    support,
                    scaledSourceSize,
                    sourceYaw);

            int armCount =
                arms != null
                    ? BuildArmColliders(
                        collisionRoot,
                        arms,
                        manifest.model3D.chairGeometry,
                        scaledSourceSize,
                        sourceYaw)
                    : 0;

            SavicChairColliderAuthoringRecord record =
                new SavicChairColliderAuthoringRecord
                {
                    generated = true,
                    builderVersion = Version,
                    strategy =
                        armCount > 0
                            ? "SEMANTIC_SEAT_BACK_SUPPORTS_ARMS"
                            : "SEMANTIC_SEAT_BACK_SUPPORTS",
                    colliderCount =
                        2 +
                        supportCount +
                        armCount,
                    seatColliderCount =
                        1,
                    backColliderCount =
                        1,
                    supportColliderCount =
                        supportCount,
                    armColliderCount =
                        armCount,
                    semanticBacked =
                        true,
                    evidence =
                        "Generated from SAVIC chair semantic parts " +
                        semantic.analyzerVersion +
                        "; support mode " +
                        (semantic.supportPattern?.mode ??
                         "UNASSESSED") +
                        "; arms " +
                        (armCount > 0
                            ? "bilateral"
                            : "none") +
                        ".",
                    generatedUtc =
                        DateTime.UtcNow.ToString("O")
                };

            if (!Validate(
                    root,
                    manifest,
                    plan,
                    record,
                    out string error))
            {
                throw new InvalidOperationException(
                    "Generated chair collider set is invalid: " +
                    error);
            }

            return record;
        }

        internal static bool Validate(
            GameObject root,
            SavicManifest manifest,
            SavicChairAuthoringRecord plan,
            SavicChairColliderAuthoringRecord record,
            out string error)
        {
            error =
                string.Empty;

            if (root == null ||
                manifest?.model3D?.semanticParts == null ||
                plan == null ||
                record == null ||
                !record.generated)
            {
                error =
                    "Missing chair collider validation input.";
                return false;
            }

            Transform collisionRoot =
                root.transform.Find(
                    CollisionRootName);

            if (collisionRoot == null)
            {
                error =
                    "Missing SAVIC_Collision root.";
                return false;
            }

            BoxCollider[] colliders =
                collisionRoot.GetComponentsInChildren
                    <BoxCollider>(true);

            if (colliders.Length !=
                record.colliderCount)
            {
                error =
                    "Collider count mismatch. Expected " +
                    record.colliderCount +
                    ", got " +
                    colliders.Length +
                    ".";
                return false;
            }

            Collider[] allColliders =
                root.GetComponentsInChildren
                    <Collider>(true);

            if (allColliders.Length !=
                colliders.Length)
            {
                error =
                    "Chair contains colliders outside SAVIC_Collision.";
                return false;
            }

            int seatCount = 0;
            int backCount = 0;
            int supportCount = 0;
            int armCount = 0;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                BoxCollider collider =
                    colliders[index];

                if (collider == null ||
                    collider.isTrigger ||
                    !IsFinitePositive(
                        collider.size))
                {
                    error =
                        "Chair collider is invalid or configured as trigger.";
                    return false;
                }

                string name =
                    collider.gameObject.name;

                if (string.Equals(
                        name,
                        SeatColliderName,
                        StringComparison.Ordinal))
                {
                    seatCount++;
                }
                else if (string.Equals(
                             name,
                             BackColliderName,
                             StringComparison.Ordinal))
                {
                    backCount++;
                }
                else if (name.StartsWith(
                             SupportPrefix,
                             StringComparison.Ordinal))
                {
                    supportCount++;
                }
                else if (name.StartsWith(
                             ArmPrefix,
                             StringComparison.Ordinal))
                {
                    armCount++;
                }
                else
                {
                    error =
                        "Unexpected chair collider child: " +
                        name +
                        ".";
                    return false;
                }
            }

            if (seatCount != 1 ||
                backCount != 1)
            {
                error =
                    "Chair requires exactly one seat and one backrest collider.";
                return false;
            }

            if (supportCount <= 0 ||
                supportCount !=
                    record.supportColliderCount)
            {
                error =
                    "Chair support collider count is invalid.";
                return false;
            }

            if (armCount !=
                record.armColliderCount)
            {
                error =
                    "Chair arm collider count is invalid.";
                return false;
            }

            BoxCollider seatCollider =
                collisionRoot
                    .Find(
                        SeatColliderName)
                    ?.GetComponent<BoxCollider>();

            if (seatCollider == null)
            {
                error =
                    "Chair seat collider cannot be resolved.";
                return false;
            }

            float seatTop =
                seatCollider.transform.localPosition.y +
                seatCollider.size.y *
                0.5f;

            if (Math.Abs(
                    seatTop -
                    plan.finalSeatHeightMeters) >
                0.10f)
            {
                error =
                    "Chair seat collider height is inconsistent with detected seat height.";
                return false;
            }

            BoxCollider backCollider =
                collisionRoot
                    .Find(
                        BackColliderName)
                    ?.GetComponent<BoxCollider>();

            if (backCollider == null ||
                backCollider.transform.localPosition.y <=
                    seatCollider.transform.localPosition.y)
            {
                error =
                    "Chair backrest collider is not above the seat collider.";
                return false;
            }

            float fullBodyVolume =
                Math.Max(
                    0.000001f,
                    plan.finalWidthMeters *
                    plan.finalHeightMeters *
                    plan.finalDepthMeters);

            float colliderVolume =
                0f;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Vector3 size =
                    colliders[index].size;

                colliderVolume +=
                    size.x *
                    size.y *
                    size.z;
            }

            if (colliderVolume >=
                fullBodyVolume *
                0.72f)
            {
                error =
                    "Chair collider set regressed toward an oversized full-body volume.";
                return false;
            }

            return true;
        }

        private static int BuildSupportColliders(
            Transform parent,
            SavicSemanticPartAnalysisRecord semantic,
            SavicSemanticPartRecord seat,
            SavicSemanticPartRecord support,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            SavicSupportPatternRecord pattern =
                semantic.supportPattern;

            if (pattern != null &&
                string.Equals(
                    pattern.mode,
                    "MULTI_CONTACT",
                    StringComparison.Ordinal) &&
                pattern.zones != null &&
                pattern.zones.Count >= 3)
            {
                float supportHeight =
                    ResolveSupportHeight(
                        seat,
                        sourceSize.y);

                for (int index = 0;
                     index < pattern.zones.Count;
                     index++)
                {
                    CreateSupportZoneCollider(
                        parent,
                        index,
                        pattern.zones[index],
                        supportHeight,
                        sourceSize,
                        sourceYaw);
                }

                return pattern.zones.Count;
            }

            CreatePartCollider(
                parent,
                SupportPrefix + "00",
                support,
                sourceSize,
                sourceYaw);

            return 1;
        }

        private static int BuildArmColliders(
            Transform parent,
            SavicSemanticPartRecord arms,
            SavicChairGeometryProfileRecord geometry,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            if (geometry == null ||
                arms == null)
            {
                return 0;
            }

            bool backAxisX =
                string.Equals(
                    geometry.backAxis,
                    "X",
                    StringComparison.Ordinal);

            Vector3 center =
                ResolvePartCenter(
                    arms,
                    sourceSize);

            Vector3 size =
                ResolvePartSize(
                    arms,
                    sourceSize);

            float lateralSpan =
                backAxisX
                    ? size.z
                    : size.x;

            float armThickness =
                Math.Max(
                    MinimumColliderSize,
                    lateralSpan *
                    0.18f);

            float lateralOffset =
                Math.Max(
                    0f,
                    lateralSpan *
                    0.5f -
                    armThickness *
                    0.5f);

            for (int side = 0;
                 side < 2;
                 side++)
            {
                float sign =
                    side == 0
                        ? -1f
                        : 1f;

                Vector3 armCenter =
                    center;

                Vector3 armSize =
                    size;

                if (backAxisX)
                {
                    armCenter.z +=
                        sign *
                        lateralOffset;

                    armSize.z =
                        armThickness;
                }
                else
                {
                    armCenter.x +=
                        sign *
                        lateralOffset;

                    armSize.x =
                        armThickness;
                }

                CreateCollider(
                    parent,
                    ArmPrefix +
                    side.ToString(
                        "00",
                        CultureInfo.InvariantCulture),
                    armCenter,
                    armSize,
                    sourceYaw);
            }

            return 2;
        }

        private static void CreatePartCollider(
            Transform parent,
            string name,
            SavicSemanticPartRecord part,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            CreateCollider(
                parent,
                name,
                ResolvePartCenter(
                    part,
                    sourceSize),
                ResolvePartSize(
                    part,
                    sourceSize),
                sourceYaw);
        }

        private static Vector3 ResolvePartCenter(
            SavicSemanticPartRecord part,
            Vector3 sourceSize)
        {
            return new Vector3(
                (part.normalizedCenterX -
                 0.5f) *
                sourceSize.x,
                part.normalizedCenterY *
                sourceSize.y,
                (part.normalizedCenterZ -
                 0.5f) *
                sourceSize.z);
        }

        private static Vector3 ResolvePartSize(
            SavicSemanticPartRecord part,
            Vector3 sourceSize)
        {
            return new Vector3(
                Math.Max(
                    MinimumColliderSize,
                    part.normalizedSizeX *
                    sourceSize.x),
                Math.Max(
                    MinimumColliderSize,
                    part.normalizedSizeY *
                    sourceSize.y),
                Math.Max(
                    MinimumColliderSize,
                    part.normalizedSizeZ *
                    sourceSize.z));
        }

        private static void CreateSupportZoneCollider(
            Transform parent,
            int index,
            SavicSupportZoneRecord zone,
            float supportHeight,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            float width =
                Math.Max(
                    MinimumColliderSize,
                    zone.normalizedSizeX *
                    sourceSize.x +
                    SupportPaddingMeters);

            float depth =
                Math.Max(
                    MinimumColliderSize,
                    zone.normalizedSizeZ *
                    sourceSize.z +
                    SupportPaddingMeters);

            float maximumWidth =
                Math.Max(
                    MinimumColliderSize,
                    sourceSize.x *
                    0.24f);

            float maximumDepth =
                Math.Max(
                    MinimumColliderSize,
                    sourceSize.z *
                    0.24f);

            CreateCollider(
                parent,
                SupportPrefix +
                index.ToString(
                    "00",
                    CultureInfo.InvariantCulture),
                new Vector3(
                    (zone.normalizedCenterX -
                     0.5f) *
                    sourceSize.x,
                    supportHeight *
                    0.5f,
                    (zone.normalizedCenterZ -
                     0.5f) *
                    sourceSize.z),
                new Vector3(
                    Math.Min(
                        width,
                        maximumWidth),
                    Math.Max(
                        MinimumColliderSize,
                        supportHeight),
                    Math.Min(
                        depth,
                        maximumDepth)),
                sourceYaw);
        }

        private static void CreateCollider(
            Transform parent,
            string name,
            Vector3 sourceRelativeCenter,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            GameObject child =
                new GameObject(
                    name);

            child.transform.SetParent(
                parent,
                false);

            Vector3 rotatedCenter =
                sourceYaw *
                new Vector3(
                    sourceRelativeCenter.x,
                    0f,
                    sourceRelativeCenter.z);

            child.transform.localPosition =
                new Vector3(
                    rotatedCenter.x,
                    sourceRelativeCenter.y,
                    rotatedCenter.z);

            child.transform.localRotation =
                sourceYaw;

            child.transform.localScale =
                Vector3.one;

            BoxCollider collider =
                child.AddComponent<BoxCollider>();

            collider.isTrigger =
                false;

            collider.center =
                Vector3.zero;

            collider.size =
                new Vector3(
                    Math.Max(
                        MinimumColliderSize,
                        sourceSize.x),
                    Math.Max(
                        MinimumColliderSize,
                        sourceSize.y),
                    Math.Max(
                        MinimumColliderSize,
                        sourceSize.z));
        }

        private static float ResolveSupportHeight(
            SavicSemanticPartRecord seat,
            float sourceHeight)
        {
            float seatBottom =
                (seat.normalizedCenterY -
                 seat.normalizedSizeY *
                 0.5f) *
                sourceHeight;

            return Math.Max(
                MinimumColliderSize,
                seatBottom);
        }

        private static SavicSemanticPartRecord FindPart(
            SavicSemanticPartAnalysisRecord semantic,
            string partId)
        {
            if (semantic?.parts == null)
                return null;

            for (int index = 0;
                 index < semantic.parts.Count;
                 index++)
            {
                SavicSemanticPartRecord part =
                    semantic.parts[index];

                if (part != null &&
                    string.Equals(
                        part.partId,
                        partId,
                        StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static Transform EnsureDirectChild(
            Transform parent,
            string name)
        {
            Transform child =
                parent.Find(
                    name);

            if (child != null)
                return child;

            GameObject created =
                new GameObject(
                    name);

            created.transform.SetParent(
                parent,
                false);

            return created.transform;
        }

        private static void ClearChildren(
            Transform parent)
        {
            for (int index =
                     parent.childCount - 1;
                 index >= 0;
                 index--)
            {
                Object.DestroyImmediate(
                    parent
                        .GetChild(index)
                        .gameObject);
            }
        }

        private static void RemoveAllColliders(
            GameObject root)
        {
            Collider[] colliders =
                root.GetComponentsInChildren
                    <Collider>(true);

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Object.DestroyImmediate(
                    colliders[index]);
            }
        }

        private static bool IsFinitePositive(
            Vector3 value)
        {
            return
                IsFinitePositive(
                    value.x) &&
                IsFinitePositive(
                    value.y) &&
                IsFinitePositive(
                    value.z);
        }

        private static bool IsFinitePositive(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value) &&
                value > 0f;
        }
    }
}
