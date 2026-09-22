using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicBlock1SelfTest
    {
        [MenuItem("Tools/Bistro Builder/SAVIC/Diagnostics/Run Block 1 Self-Test", false, 110)]
        public static void RunFromMenu()
        {
            RunOrThrow();
            Debug.Log("[SAVIC] BLOCK 1 SELF-TEST — PASS");
        }

        public static void RunFromCommandLine()
        {
            try
            {
                RunOrThrow();
                Debug.Log("[SAVIC] BLOCK 1 SELF-TEST — PASS");
            }
            catch (Exception exception)
            {
                Debug.LogError("[SAVIC] BLOCK 1 SELF-TEST — FAIL\n" + exception);
                throw;
            }
        }

        private static void RunOrThrow()
        {
            string sandboxRoot = Path.Combine(
                Path.GetTempPath(),
                "BistroBuilder_SAVIC_SelfTest_" + Guid.NewGuid().ToString("N"));

            try
            {
                SavicStorageLayout layout = new SavicStorageLayout(sandboxRoot);
                layout.EnsureInfrastructure();

                SavicManifestRepository manifests = new SavicManifestRepository(layout);
                SavicJobStore jobs = new SavicJobStore(layout);
                SavicIntakeService intake = new SavicIntakeService(layout, manifests, jobs);

                byte[] payload = BuildDeterministicPayload();
                string firstIncoming = Path.Combine(layout.DropHereRoot, "TestTable.fbx");
                File.WriteAllBytes(firstIncoming, payload);

                SavicIntakeOutcome first = intake.IngestSynchronously(firstIncoming);

                Require(first.Succeeded, "First intake did not succeed.");
                Require(!first.DuplicateExact, "First intake was incorrectly treated as duplicate.");
                Require(!File.Exists(firstIncoming), "DropHere was not cleared after intake.");
                Require(manifests.Count == 1, "Exactly one manifest was expected.");

                Require(
                    manifests.TryGetBySourceHash(first.SourceHash, out SavicManifest manifest),
                    "Manifest could not be resolved by SourceHash.");

                Require(
                    manifests.TryGetBySavicId(
                        first.ManifestSavicId,
                        out SavicManifest manifestById) &&
                    ReferenceEquals(manifest, manifestById),
                    "Manifest could not be resolved by SavicId.");

                manifests.Reload();

                Require(
                    manifests.TryGetBySavicId(
                        first.ManifestSavicId,
                        out SavicManifest reloadedManifest) &&
                    string.Equals(
                        reloadedManifest.source.sourceHash,
                        first.SourceHash,
                        StringComparison.OrdinalIgnoreCase),
                    "SavicId index was not rebuilt after repository reload.");

                manifest = reloadedManifest;

                string archivedPath =
                    layout.FromProjectRelativePath(manifest.source.archivedRelativePath);

                Require(File.Exists(archivedPath), "Archived source does not exist.");
                Require(
                    File.ReadAllBytes(archivedPath).SequenceEqual(payload),
                    "Archived source differs from the original bytes.");

                string duplicateIncoming = Path.Combine(
                    layout.DropHereRoot,
                    "SameBytesDifferentName.fbx");
                File.WriteAllBytes(duplicateIncoming, payload);

                SavicIntakeOutcome duplicate =
                    intake.IngestSynchronously(duplicateIncoming);

                Require(duplicate.Succeeded, "Duplicate intake did not succeed safely.");
                Require(duplicate.DuplicateExact, "Exact duplicate was not detected.");
                Require(!File.Exists(duplicateIncoming), "Duplicate remained in DropHere.");
                Require(manifests.Count == 1, "Duplicate created an extra manifest.");
                Require(
                    string.Equals(
                        first.ManifestSavicId,
                        duplicate.ManifestSavicId,
                        StringComparison.Ordinal),
                    "Duplicate did not preserve SAVIC identity.");
                Require(
                    jobs.CountByState(SavicJobState.Ingested) == 1,
                    "Expected one normal ingested job.");
                Require(
                    jobs.CountByState(SavicJobState.DuplicateExact) == 1,
                    "Expected one exact-duplicate job.");

                SavicIntakeOutcome rebuilt = intake.IngestSynchronously(
                    CreateReplacementInput(layout.DropHereRoot, payload));

                Require(rebuilt.DuplicateExact, "Idempotency check failed.");
                Require(manifests.Count == 1, "Idempotency check created a duplicate manifest.");

                byte[] corrupted = new byte[payload.Length];
                for (int index = 0; index < corrupted.Length; index++)
                    corrupted[index] = (byte)(payload[index] ^ 0x5A);

                File.WriteAllBytes(archivedPath, corrupted);

                string repairIncoming = Path.Combine(
                    layout.DropHereRoot,
                    "RepairArchive.fbx");
                File.WriteAllBytes(repairIncoming, payload);

                SavicIntakeOutcome repaired =
                    intake.IngestSynchronously(repairIncoming);

                Require(repaired.Succeeded, "Archive repair intake failed.");
                Require(repaired.DuplicateExact, "Archive repair changed logical identity.");
                Require(
                    File.ReadAllBytes(archivedPath).SequenceEqual(payload),
                    "Corrupted source archive was not repaired.");
                Require(
                    Directory.GetFiles(
                        Path.GetDirectoryName(archivedPath),
                        Path.GetFileName(archivedPath) + ".corrupt.*")
                    .Length == 1,
                    "Corrupted archive bytes were not preserved for audit.");
            }
            finally
            {
                TryDeleteDirectory(sandboxRoot);
            }
        }

        private static string CreateReplacementInput(
            string dropHereRoot,
            byte[] payload)
        {
            string path = Path.Combine(dropHereRoot, "Reprocessed.fbx");
            File.WriteAllBytes(path, payload);
            return path;
        }

        private static byte[] BuildDeterministicPayload()
        {
            byte[] payload = new byte[4096];
            for (int index = 0; index < payload.Length; index++)
                payload[index] = (byte)((index * 31 + 17) % 251);
            return payload;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[SAVIC] Self-test sandbox cleanup warning: " +
                    exception.Message);
            }
        }
    }
}
