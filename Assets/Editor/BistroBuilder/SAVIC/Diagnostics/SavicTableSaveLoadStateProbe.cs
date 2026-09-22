using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicTableSaveLoadStateProbe
    {
        private const string PrototypeScenePath =
            "Assets/Scenes/Prototype_Restaurant.unity";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Table SaveLoad State Probe",
            false,
            122)]
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
                "No published SAVIC table exists.");

            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();

            try
            {
                Scene scene =
                    EditorSceneManager.OpenScene(
                        PrototypeScenePath,
                        OpenSceneMode.Single);

                Require(
                    scene.IsValid() &&
                    scene.isLoaded,
                    "Prototype scene could not be loaded.");

                RestaurantStructureSaveSectionProvider provider =
                    UnityEngine.Object.FindFirstObjectByType
                        <RestaurantStructureSaveSectionProvider>(
                            FindObjectsInactive.Include);

                Require(
                    provider != null,
                    "RestaurantStructureSaveSectionProvider is missing.");

                Require(
                    provider.ValidateConfiguration(
                        out string configurationError),
                    "Restaurant structure provider configuration is invalid: " +
                    configurationError);

                RestaurantStructureSaveData state =
                    BuildState(
                        scene.name,
                        manifest.canonicalContentId);

                Require(
                    provider.ValidateState(
                        state,
                        out string stateError),
                    "SAVIC table is not a valid restaurant.structure save record: " +
                    stateError);

                BistroBuilderJsonSaveSerializer serializer =
                    new BistroBuilderJsonSaveSerializer();

                byte[] payload =
                    serializer.Serialize(
                        state,
                        prettyPrint: true);

                Require(
                    payload != null &&
                    payload.Length > 0,
                    "Restaurant structure payload was not serialized.");

                RestaurantStructureSaveData roundTrip =
                    serializer.Deserialize(
                        payload,
                        typeof(RestaurantStructureSaveData))
                    as RestaurantStructureSaveData;

                Require(
                    roundTrip != null &&
                    roundTrip.placeables != null &&
                    roundTrip.placeables.Count == 1,
                    "Serialized restaurant structure did not round-trip.");

                RestaurantPlaceableSaveRecord record =
                    roundTrip.placeables[0];

                Require(
                    record != null &&
                    string.Equals(
                        record.itemId,
                        manifest.canonicalContentId,
                        StringComparison.Ordinal),
                    "Canonical SAVIC ItemId changed during serialization round-trip.");

                Require(
                    provider.ValidateState(
                        roundTrip,
                        out string roundTripError),
                    "Round-tripped restaurant.structure state is invalid: " +
                    roundTripError);

                Debug.Log(
                    "[SAVIC] TABLE SAVELOAD STATE PROBE - PASS\n" +
                    "Scene: " +
                    scene.name +
                    "\nCanonical ItemId: " +
                    manifest.canonicalContentId +
                    "\nSerialized bytes: " +
                    payload.Length +
                    "\nProvider validation: PASS\n" +
                    "JSON round-trip: PASS");
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

        private static RestaurantStructureSaveData BuildState(
            string sceneName,
            string itemId)
        {
            RestaurantStructureSaveData state =
                new RestaurantStructureSaveData
                {
                    sceneName =
                        sceneName ?? string.Empty
                };

            state.placeables.Add(
                new RestaurantPlaceableSaveRecord
                {
                    instanceId =
                        "savic_probe_table_instance",
                    itemId =
                        itemId ?? string.Empty,
                    functionalTableId =
                        900001,
                    worldPosition =
                        new BistroBuilderSaveVector3(
                            new Vector3(
                                0f,
                                0f,
                                0f)),
                    worldRotation =
                        new BistroBuilderSaveQuaternion(
                            Quaternion.identity),
                    localScale =
                        new BistroBuilderSaveVector3(
                            Vector3.one)
                });

            return state;
        }

        private static SavicManifest FindPublishedTable(
            IReadOnlyList<SavicManifest> manifests)
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
                    !string.IsNullOrWhiteSpace(
                        candidate.canonicalContentId))
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
                throw new InvalidOperationException(
                    message);
        }
    }
}
