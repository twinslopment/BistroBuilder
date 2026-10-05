using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicV1ClosureGateReport
    {
        public string status = "UNKNOWN";
        public string startedUtc = string.Empty;
        public string completedUtc = string.Empty;
        public int passed;
        public int failed;
        public List<SavicV1ClosureGateResult> results =
            new List<SavicV1ClosureGateResult>();
    }

    [Serializable]
    internal sealed class SavicV1ClosureGateResult
    {
        public string id = string.Empty;
        public string status = "UNKNOWN";
        public long durationMilliseconds;
        public string message = string.Empty;
    }

    public static class SavicV1ClosureGate
    {
        private static string ReportPath => Path.Combine(
            SavicEditorContext.Instance.Layout.LogsRoot,
            "SavicV1ClosureGateReport.json");

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run SAVIC V1 Closure Gate",
            false,
            140)]
        public static void RunFromMenu()
        {
            Run(false);
        }

        public static void RunFromCommandLine()
        {
            Run(true);
        }

        private static void Run(bool commandLine)
        {
            SavicV1ClosureGateReport report =
                new SavicV1ClosureGateReport
                {
                    startedUtc = DateTime.UtcNow.ToString("O")
                };

            try
            {
                RunStep(
                    report,
                    "foundation",
                    SavicBlock1SelfTest.RunFromCommandLine);

                RunStep(
                    report,
                    "autonomous-classification",
                    SavicAutonomousClassificationSelfTest.RunFromCommandLine);

                RunStep(report, "provider-metadata",
                    SavicProviderMetadataSelfTest.RunFromCommandLine);

                RunStep(report, "floor-lamp",
                    SavicFloorLampSelfTest.RunFromCommandLine);

                RunStep(report, "bar-stool-geometry",
                    SavicBarStoolGeometrySelfTest.RunFromCommandLine);

                RunStep(report, "bar-placeable-binding",
                    SavicBarPlaceableBindingSelfTest.RunFromCommandLine);

                RunStep(report, "bar-seat-binding",
                    SavicBarSeatBindingSelfTest.RunFromCommandLine);
                RunStep(report, "bar-seat-automatic-placement",
                    SavicBarSeatBindingSelfTest.RunAutomaticAssociationFromCommandLine);
                RunStep(report, "bar-seat-persistence-contract",
                    SavicBarSeatPersistenceSelfTest.RunFromCommandLine);
                RunStep(report, "bar-stool-acceptance-queue",
                    SavicBarStoolPublicationSelfTest.RunQueueGuardsFromCommandLine);

                RunStep(report, "dynamic-bar-operational-coordinator",
                    SavicBarSeatBindingSelfTest.RunDynamicCoordinatorFromCommandLine);

                RunStep(report, "bar-body-spatial",
                    SavicBarBodySpatialSelfTest.RunFromCommandLine);

                RunStep(report, "compound-physical-footprint",
                    SavicCompoundPhysicalFootprintSelfTest.RunFromCommandLine);
                RunStep(report, "elevated-spatial-primitives",
                    SavicElevatedSpatialSelfTest.RunFromCommandLine);
                RunStep(report, "overhead-acceptance-queue",
                    SavicOverheadEquipmentPublicationSelfTest.RunQueueGuardsFromCommandLine);

                RunStep(report, "compound-body-geometry",
                    SavicCompoundBodyGeometrySelfTest.RunFromCommandLine);

                RunStep(report, "bar-counter-authoring",
                    SavicBarCounterAuthoringSelfTest.RunFromCommandLine);
                RunStep(report, "runtime-typography-ownership",
                    SavicRuntimeTypographyOwnershipSelfTest.RunFromCommandLine);

                RunStep(
                    report,
                    "batch-recovery",
                    SavicBatchRecoverySelfTest.RunFromCommandLine);

                RunStep(
                    report,
                    "legacy-adoption",
                    SavicLegacyAdoptionSelfTest.RunFromCommandLine);

                RunStep(
                    report,
                    "incremental-invalidation",
                    SavicIncrementalInvalidationSelfTest.RunFromCommandLine);

                RunStep(
                    report,
                    "publication-rollback",
                    SavicPublicationRollbackProbe.RunFromCommandLine);

                RunStep(
                    report,
                    "mass-ingestion",
                    SavicMassIngestionRealProbe.RunFromCommandLine);

                RunStep(
                    report,
                    "construction",
                    SavicConstructionVerticalProbe.RunFromCommandLine);

                RunStep(
                    report,
                    "images-ui",
                    SavicImageVerticalProbe.RunFromCommandLine);

                RunStep(
                    report,
                    "content-bundle",
                    SavicContentBundleVerticalProbe.RunFromCommandLine);

                RunStep(report, "editor-control-center", SavicEditorUxSelfTest.RunFromCommandLine);

                report.status =
                    report.failed == 0
                        ? "PASS"
                        : "FAIL";
            }
            catch (Exception exception)
            {

                report.status = "FAIL";
                report.failed++;

                report.results.Add(
                    new SavicV1ClosureGateResult
                    {
                        id = "closure-gate",
                        status = "FAIL",
                        message = exception.ToString()
                    });
            }
            finally
            {
                report.completedUtc =
                    DateTime.UtcNow.ToString("O");

                WriteReport(report);
            }

            if (string.Equals(
                    report.status,
                    "PASS",
                    StringComparison.Ordinal))
            {
                Debug.Log(
                    "[SAVIC] V1 CLOSURE GATE - PASS\n" +
                    "Checks passed: " +
                    report.passed +
                    "\nReport: " +
                    ReportPath);

                if (commandLine && Application.isBatchMode)
                    EditorApplication.Exit(0);

                return;
            }

            string failure =
                "[SAVIC] V1 CLOSURE GATE - FAIL. " +
                "Passed=" +
                report.passed +
                ", failed=" +
                report.failed +
                ". Report: " +
                ReportPath;

            Debug.LogError(failure);

            if (commandLine && Application.isBatchMode)
            {
                EditorApplication.Exit(1);
                return;
            }

            throw new InvalidOperationException(failure);
        }

        private static void RunStep(
            SavicV1ClosureGateReport report,
            string id,
            Action action)
        {
            System.Diagnostics.Stopwatch stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                action();

                stopwatch.Stop();
                report.passed++;

                report.results.Add(
                    new SavicV1ClosureGateResult
                    {
                        id = id,
                        status = "PASS",
                        durationMilliseconds =
                            stopwatch.ElapsedMilliseconds
                    });
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                report.failed++;

                report.results.Add(
                    new SavicV1ClosureGateResult
                    {
                        id = id,
                        status = "FAIL",
                        durationMilliseconds =
                            stopwatch.ElapsedMilliseconds,
                        message = exception.ToString()
                    });

                throw;
            }
        }

        private static void WriteReport(
            SavicV1ClosureGateReport report)
        {
            string absolutePath = ReportPath;

            string directory =
                Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                absolutePath,
                JsonUtility.ToJson(report, true));
        }
    }
}
