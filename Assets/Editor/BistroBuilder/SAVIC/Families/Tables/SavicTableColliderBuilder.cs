using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicTableColliderBuilder
    {
        internal const string Version = "1.0.0";

        private const string CollisionRootName =
            "SAVIC_Collision";

        private const string TabletopColliderName =
            "Tabletop";

        private const string SupportPrefix =
            "Support_";

        private const float MinimumColliderSize = 0.025f;
        private const float SupportPaddingMeters = 0.018f;

        internal static SavicTableColliderAuthoringRecord Build(
            GameObject root,
            SavicManifest manifest,
            SavicTableAuthoringRecord plan)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            if (manifest?.model3D == null)
            {
                throw new InvalidOperationException(
                    "Semantic collider generation requires model analysis.");
            }

            if (plan == null || !plan.planned)
            {
                throw new InvalidOperationException(
                    "Semantic collider generation requires a table authoring plan.");
            }

            SavicSemanticPartAnalysisRecord semantic =
                manifest.model3D.semanticParts;

            if (semantic == null ||
                !semantic.analyzed ||
                !semantic.automationReady)
            {
                throw new InvalidOperationException(
                    "Semantic collider generation requires automation-ready semantic parts.");
            }

            SavicSemanticPartRecord tabletop =
                FindPart(
                    semantic,
                    "table.top");

            SavicSemanticPartRecord support =
                FindPart(
                    semantic,
                    "table.support");

            if (tabletop == null || support == null)
            {
                throw new InvalidOperationException(
                    "Table semantic model must contain table.top and table.support.");
            }

            RemoveAllColliders(root);

            Transform collisionRoot =
                EnsureDirectChild(
                    root.transform,
                    CollisionRootName);

            ClearChildren(collisionRoot);

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
                TabletopColliderName,
                tabletop,
                scaledSourceSize,
                sourceYaw);

            int supportColliderCount =
                BuildSupportColliders(
                    collisionRoot,
                    semantic,
                    tabletop,
                    support,
                    scaledSourceSize,
                    sourceYaw);

            SavicTableColliderAuthoringRecord record =
                new SavicTableColliderAuthoringRecord
                {
                    generated = true,
                    builderVersion = Version,
                    strategy =
                        semantic.supportPattern != null &&
                        string.Equals(
                            semantic.supportPattern.mode,
                            "MULTI_CONTACT",
                            StringComparison.Ordinal) &&
                        semantic.supportPattern.zones != null &&
                        semantic.supportPattern.zones.Count >= 3
                            ? "SEMANTIC_TABLETOP_PLUS_CONTACT_SUPPORTS"
                            : "SEMANTIC_TABLETOP_PLUS_SUPPORT_BOUNDS",
                    colliderCount =
                        1 +
                        supportColliderCount,
                    supportColliderCount =
                        supportColliderCount,
                    semanticBacked =
                        true,
                    evidence =
                        "Generated from SAVIC semantic parts " +
                        semantic.analyzerVersion +
                        " with support mode " +
                        (semantic.supportPattern?.mode ??
                         "UNASSESSED") +
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
                    "Generated semantic collider set is invalid: " +
                    error);
            }

            return record;
        }

        internal static bool Validate(
            GameObject root,
            SavicManifest manifest,
            SavicTableAuthoringRecord plan,
            SavicTableColliderAuthoringRecord record,
            out string error)
        {
            error = string.Empty;

            if (root == null ||
                manifest?.model3D?.semanticParts == null ||
                plan == null ||
                record == null ||
                !record.generated)
            {
                error =
                    "Missing collider validation input.";
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

            if (root.GetComponents<Collider>().Length != 0)
            {
                error =
                    "Root contains legacy/full-body colliders.";
                return false;
            }

            int tabletopCount = 0;
            int supportCount = 0;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                BoxCollider collider =
                    colliders[index];

                if (collider == null ||
                    collider.isTrigger)
                {
                    error =
                        "Collider is missing or unexpectedly configured as trigger.";
                    return false;
                }

                Vector3 size =
                    collider.size;

                if (!IsFinitePositive(size))
                {
                    error =
                        "Collider has invalid dimensions.";
                    return false;
                }

                if (string.Equals(
                        collider.gameObject.name,
                        TabletopColliderName,
                        StringComparison.Ordinal))
                {
                    tabletopCount++;
                }
                else if (collider.gameObject.name.StartsWith(
                             SupportPrefix,
                             StringComparison.Ordinal))
                {
                    supportCount++;
                }
            }

            if (tabletopCount != 1)
            {
                error =
                    "Semantic collider set must contain exactly one tabletop collider.";
                return false;
            }

            if (supportCount !=
                record.supportColliderCount ||
                supportCount <= 0)
            {
                error =
                    "Support collider count is invalid.";
                return false;
            }

            SavicSemanticPartRecord tabletop =
                FindPart(
                    manifest.model3D.semanticParts,
                    "table.top");

            if (tabletop == null)
            {
                error =
                    "Semantic tabletop record is missing.";
                return false;
            }

            BoxCollider topCollider =
                collisionRoot
                    .Find(TabletopColliderName)?
                    .GetComponent<BoxCollider>();

            if (topCollider == null)
            {
                error =
                    "Tabletop collider cannot be resolved.";
                return false;
            }

            float expectedTopHeight =
                Math.Max(
                    MinimumColliderSize,
                    tabletop.normalizedSizeY *
                    manifest.model3D.heightMeters *
                    plan.uniformScale);

            if (Math.Abs(
                    topCollider.size.y -
                    expectedTopHeight) >
                Math.Max(
                    0.015f,
                    expectedTopHeight * 0.08f))
            {
                error =
                    "Tabletop collider height does not match semantic geometry.";
                return false;
            }

            float fullHeight =
                Math.Max(
                    0.001f,
                    plan.finalHeightMeters);

            if (topCollider.size.y >=
                fullHeight * 0.80f)
            {
                error =
                    "Tabletop collider regressed to a full-body box.";
                return false;
            }

            return true;
        }

        private static int BuildSupportColliders(
            Transform parent,
            SavicSemanticPartAnalysisRecord semantic,
            SavicSemanticPartRecord tabletop,
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
                        tabletop,
                        sourceSize.y);

                for (int index = 0;
                     index < pattern.zones.Count;
                     index++)
                {
                    SavicSupportZoneRecord zone =
                        pattern.zones[index];

                    CreateSupportZoneCollider(
                        parent,
                        index,
                        zone,
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

        private static void CreatePartCollider(
            Transform parent,
            string name,
            SavicSemanticPartRecord part,
            Vector3 sourceSize,
            Quaternion sourceYaw)
        {
            GameObject child =
                new GameObject(name);

            child.transform.SetParent(
                parent,
                false);

            Vector3 sourceRelativeCenter =
                new Vector3(
                    (part.normalizedCenterX - 0.5f) *
                    sourceSize.x,
                    part.normalizedCenterY *
                    sourceSize.y,
                    (part.normalizedCenterZ - 0.5f) *
                    sourceSize.z);

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

            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size =
                new Vector3(
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
            GameObject child =
                new GameObject(
                    SupportPrefix +
                    index.ToString(
                        "00",
                        CultureInfo.InvariantCulture));

            child.transform.SetParent(
                parent,
                false);

            Vector3 sourceRelativeCenter =
                new Vector3(
                    (zone.normalizedCenterX - 0.5f) *
                    sourceSize.x,
                    supportHeight * 0.5f,
                    (zone.normalizedCenterZ - 0.5f) *
                    sourceSize.z);

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

            float maximumSupportWidth =
                Math.Max(
                    MinimumColliderSize,
                    sourceSize.x * 0.35f);

            float maximumSupportDepth =
                Math.Max(
                    MinimumColliderSize,
                    sourceSize.z * 0.35f);

            BoxCollider collider =
                child.AddComponent<BoxCollider>();

            collider.isTrigger = false;
            collider.center = Vector3.zero;
            collider.size =
                new Vector3(
                    Math.Min(
                        width,
                        maximumSupportWidth),
                    Math.Max(
                        MinimumColliderSize,
                        supportHeight),
                    Math.Min(
                        depth,
                        maximumSupportDepth));
        }

        private static float ResolveSupportHeight(
            SavicSemanticPartRecord tabletop,
            float sourceHeight)
        {
            float tabletopBottom =
                (tabletop.normalizedCenterY -
                 tabletop.normalizedSizeY * 0.5f) *
                sourceHeight;

            return Math.Max(
                MinimumColliderSize,
                tabletopBottom);
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
                parent.Find(name);

            if (child != null)
                return child;

            GameObject created =
                new GameObject(name);

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
                    parent.GetChild(index)
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
                IsFinitePositive(value.x) &&
                IsFinitePositive(value.y) &&
                IsFinitePositive(value.z);
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
