using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicTableCompoundColliderProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Table Compound Collider Probe",
            false,
            121)]
        public static void RunFromMenu()
        {
            RunOrThrow();
        }

        public static void RunFromCommandLine()
        {
            RunOrThrow();
        }

        private static void RunOrThrow()
        {
            SavicEditorContext context =
                SavicEditorContext.Instance;

            SavicManifest manifest =
                FindPublishedTable(
                    context.Manifests.GetAll());

            Require(
                manifest != null,
                "No published SAVIC table exists for collider validation.");

            Require(
                manifest.tableColliders != null &&
                manifest.tableColliders.generated,
                "Published table has no SAVIC collider authoring record.");

            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    manifest.tableAuthoring.prefabAssetPath);

            Require(
                prefab != null,
                "Published table prefab cannot be loaded.");

            Require(
                SavicTableColliderBuilder.Validate(
                    prefab,
                    manifest,
                    manifest.tableAuthoring,
                    manifest.tableColliders,
                    out string validationError),
                "Semantic compound collider validation failed: " +
                validationError);

            Require(
                prefab.GetComponents<Collider>().Length == 0,
                "Legacy root/full-body collider still exists.");

            Transform collisionRoot =
                prefab.transform.Find(
                    "SAVIC_Collision");

            Require(
                collisionRoot != null,
                "SAVIC_Collision root is missing.");

            BoxCollider[] colliders =
                collisionRoot.GetComponentsInChildren
                    <BoxCollider>(true);

            Require(
                colliders.Length ==
                manifest.tableColliders.colliderCount,
                "Prefab collider count differs from manifest.");

            BoxCollider tabletop =
                collisionRoot
                    .Find("Tabletop")?
                    .GetComponent<BoxCollider>();

            Require(
                tabletop != null,
                "Tabletop collider is missing.");

            Require(
                tabletop.size.y <
                manifest.tableAuthoring.finalHeightMeters *
                0.5f,
                "Tabletop collider is implausibly tall.");

            int supportCount = 0;
            float maximumSupportArea = 0f;

            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                BoxCollider collider =
                    colliders[index];

                if (collider == tabletop)
                    continue;

                Require(
                    collider.gameObject.name.StartsWith(
                        "Support_",
                        StringComparison.Ordinal),
                    "Unexpected collider child: " +
                    collider.gameObject.name);

                supportCount++;

                float supportArea =
                    collider.size.x *
                    collider.size.z;

                maximumSupportArea =
                    Math.Max(
                        maximumSupportArea,
                        supportArea);
            }

            Require(
                supportCount ==
                manifest.tableColliders.supportColliderCount,
                "Support collider count differs from manifest.");

            Require(
                supportCount >= 3,
                "Current multi-contact table should generate multiple support colliders.");

            float tableArea =
                manifest.tableAuthoring.finalWidthMeters *
                manifest.tableAuthoring.finalDepthMeters;

            Require(
                maximumSupportArea <
                tableArea * 0.25f,
                "A support collider occupies an implausibly large fraction of the table footprint.");

            Debug.Log(
                "[SAVIC] TABLE COMPOUND COLLIDER PROBE - PASS\n" +
                "Strategy: " +
                manifest.tableColliders.strategy +
                "\nCollider count: " +
                colliders.Length +
                "\nSupport colliders: " +
                supportCount +
                "\nTabletop height: " +
                tabletop.size.y.ToString("0.###") +
                " m\nLargest support footprint: " +
                maximumSupportArea.ToString("0.####") +
                " m2");
        }

        private static SavicManifest FindPublishedTable(
            System.Collections.Generic.IReadOnlyList
                <SavicManifest> manifests)
        {
            for (int index = 0;
                 index < manifests.Count;
                 index++)
            {
                SavicManifest candidate =
                    manifests[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.status,
                        "PUBLISHED",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        candidate.type,
                        "Table",
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
