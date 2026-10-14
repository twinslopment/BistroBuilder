using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicTableNavigationReadinessProbe
    {
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
                "No published SAVIC table exists for navigation readiness validation.");

            SavicTableNavigationReadinessRecord record =
                SavicTableNavigationReadinessValidator.Validate(
                    manifest,
                    manifest.tableAuthoring,
                    manifest.tableAuthoring.prefabAssetPath);

            Require(
                record.validated &&
                record.footprintBlocksNavigation &&
                record.usesCanonicalFootprintTopology,
                "Static navigation readiness record is invalid.");

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene =
                    EditorSceneManager.NewScene(
                        NewSceneSetup.EmptyScene,
                        NewSceneMode.Single);

                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        manifest.tableAuthoring.prefabAssetPath);

                Require(
                    prefab != null,
                    "Published table prefab could not be loaded for navigation sandbox.");

                GameObject tableInstance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        scene) as GameObject;

                Require(
                    tableInstance != null,
                    "Published table could not be instantiated in navigation sandbox.");

                tableInstance.transform.position =
                    Vector3.zero;

                tableInstance.transform.rotation =
                    Quaternion.identity;

                GameObject navigationObject =
                    new GameObject(
                        "SAVIC_NavigationProbe");

                SceneManager.MoveGameObjectToScene(
                    navigationObject,
                    scene);

                BistroBuilderNavigationService navigation =
                    navigationObject.AddComponent
                        <BistroBuilderNavigationService>();

                navigation.RebuildNavigationTopology();

                Require(
                    navigation.StaticObstacleCount == 1,
                    "Navigation sandbox did not register exactly one static table obstacle.");

                RestaurantTable table =
                    tableInstance.GetComponent<RestaurantTable>();

                Require(
                    table != null &&
                    table.CustomerApproachPoint != null &&
                    table.WaiterServicePoint != null,
                    "Navigation sandbox table has incomplete interaction endpoints.");

                const float probeRadius = 0.28f;

                Vector3 farStart =
                    new Vector3(-10f, 0f, -10f);

                Vector3 farEnd =
                    new Vector3(10f, 0f, 10f);

                bool centerTraversable =
                    InvokeIsPointTraversable(
                        navigation,
                        tableInstance.transform.position,
                        probeRadius,
                        BistroBuilderNavigationAgentMask.Waiter,
                        "savic.navigation.center",
                        farStart,
                        farEnd);

                bool customerTraversable =
                    InvokeIsPointTraversable(
                        navigation,
                        table.CustomerApproachPoint.position,
                        probeRadius,
                        BistroBuilderNavigationAgentMask.Customer,
                        "savic.navigation.customer",
                        farStart,
                        farEnd);

                bool waiterTraversable =
                    InvokeIsPointTraversable(
                        navigation,
                        table.WaiterServicePoint.position,
                        probeRadius,
                        BistroBuilderNavigationAgentMask.Waiter,
                        "savic.navigation.waiter",
                        farStart,
                        farEnd);

                Require(
                    !centerTraversable,
                    "Navigation allows an agent through the table body.");

                Require(
                    customerTraversable,
                    "Customer approach point is blocked by table navigation geometry.");

                Require(
                    waiterTraversable,
                    "Waiter service point is blocked by table navigation geometry.");

                Require(
                    record.customerEndpointClearanceMeters >=
                    record.requiredEndpointClearanceMeters,
                    "Customer endpoint clearance is below SAVIC threshold.");

                Require(
                    record.waiterEndpointClearanceMeters >=
                    record.requiredEndpointClearanceMeters,
                    "Waiter endpoint clearance is below SAVIC threshold.");

                Debug.Log(
                    "[SAVIC] TABLE NAVIGATION READINESS PROBE - PASS\n" +
                    "Static obstacles: " +
                    navigation.StaticObstacleCount +
                    "\nTable center traversable: " +
                    centerTraversable +
                    "\nCustomer endpoint traversable: " +
                    customerTraversable +
                    "\nWaiter endpoint traversable: " +
                    waiterTraversable +
                    "\nCustomer clearance: " +
                    record.customerEndpointClearanceMeters.ToString("0.###") +
                    " m\nWaiter clearance: " +
                    record.waiterEndpointClearanceMeters.ToString("0.###") +
                    " m");
            }
            finally
            {
                if (previousSetup != null &&
                    previousSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        previousSetup);
                }
            }
        }

        private static bool InvokeIsPointTraversable(
            BistroBuilderNavigationService navigation,
            Vector3 point,
            float radius,
            BistroBuilderNavigationAgentMask agent,
            string requesterId,
            Vector3 routeStart,
            Vector3 routeEnd)
        {
            MethodInfo method =
                typeof(BistroBuilderNavigationService).GetMethod(
                    "IsPointTraversable",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic);

            Require(
                method != null,
                "Navigation point-traversal contract could not be resolved.");

            object result =
                method.Invoke(
                    navigation,
                    new object[]
                    {
                        point,
                        radius,
                        agent,
                        requesterId,
                        routeStart,
                        routeEnd,
                        false
                    });

            return result is bool value &&
                   value;
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
                        StringComparison.Ordinal) &&
                    candidate.tableAuthoring != null &&
                    candidate.tableAuthoring.planned)
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
