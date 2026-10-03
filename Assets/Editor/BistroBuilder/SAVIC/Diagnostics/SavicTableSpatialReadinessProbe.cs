using System;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicTableSpatialReadinessProbe
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Table BBSIS Readiness Probe",
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
                "No published SAVIC table exists for BBSIS readiness validation.");

            SavicTableSpatialReadinessRecord record =
                SavicTableSpatialReadinessValidator.Validate(
                    manifest,
                    manifest.tableAuthoring,
                    manifest.tableAuthoring.prefabAssetPath);

            Require(
                record.validated &&
                record.runtimeBindingValidated,
                "BBSIS table readiness did not validate.");

            Require(
                string.Equals(
                    record.familyId,
                    "seating.table",
                    StringComparison.Ordinal),
                "BBSIS table resolved the wrong family.");

            Require(
                record.seatBayPortCount ==
                manifest.tableAuthoring.capacity,
                "BBSIS contract SeatBay port count differs from table capacity.");

            Require(
                record.emittedSeatBayCount ==
                manifest.tableAuthoring.capacity,
                "BBSIS runtime adapter emitted the wrong SeatBay count.");

            BistroBuilderSpatialContractDefinition contract =
                AssetDatabase.LoadAssetAtPath
                    <BistroBuilderSpatialContractDefinition>(
                        record.contractAssetPath);

            Require(
                contract != null,
                "Resolved BBSIS contract is missing.");

            Require(
                contract.ValidateDefinition(
                    out string error),
                "Resolved BBSIS contract is invalid: " +
                error);

            Debug.Log(
                "[SAVIC] TABLE BBSIS READINESS PROBE - PASS\n" +
                "Contract: " +
                record.contractId +
                "\nFamily: " +
                record.familyId +
                "\nConfiguration: " +
                record.configurationId +
                "\nSeatBay ports: " +
                record.seatBayPortCount +
                "\nRuntime SeatBays emitted: " +
                record.emittedSeatBayCount);
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
