using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicPublicationRollbackProbe
    {
        private const string ProbeFolder =
            "Assets/Generated/BistroBuilder/SAVIC/Published/Tables/" +
            "bb_table_b90c47bde3e949918ec76a13dc17c61c";

        private const string ExistingAssetPath =
            ProbeFolder + "/__SAVIC_RollbackExisting.txt";

        private const string NewAssetPath =
            ProbeFolder + "/__SAVIC_RollbackNew.txt";

        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Publication Rollback Probe",
            false,
            115)]
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
            SavicStorageLayout layout =
                SavicEditorContext.Instance.Layout;

            CleanupProbeAsset(ExistingAssetPath);
            CleanupProbeAsset(NewAssetPath);

            string existingAbsolute =
                ToAbsoluteProjectPath(ExistingAssetPath);

            string newAbsolute =
                ToAbsoluteProjectPath(NewAssetPath);

            try
            {
                WriteUtf8(
                    existingAbsolute,
                    "SAVIC rollback baseline\n");

                AssetDatabase.ImportAsset(
                    ExistingAssetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                string guidBefore =
                    AssetDatabase.AssetPathToGUID(
                        ExistingAssetPath);

                Require(
                    !string.IsNullOrWhiteSpace(guidBefore),
                    "Rollback baseline asset has no Unity GUID.");

                string hashBefore =
                    SavicHashService.ComputeSha256(
                        existingAbsolute);

                string metaPath =
                    existingAbsolute + ".meta";

                Require(
                    File.Exists(metaPath),
                    "Rollback baseline asset has no .meta file.");

                string metaHashBefore =
                    SavicHashService.ComputeSha256(
                        metaPath);

                bool intentionalFailureObserved = false;
                string transientGuid = string.Empty;

                try
                {
                    using SavicAssetMutationScope transaction =
                        new SavicAssetMutationScope(
                            layout,
                            "rollback_probe");

                    transaction.CaptureAsset(
                        ExistingAssetPath);

                    transaction.CaptureAsset(
                        NewAssetPath);

                    WriteUtf8(
                        existingAbsolute,
                        "SAVIC rollback mutated existing\n");

                    WriteUtf8(
                        newAbsolute,
                        "SAVIC rollback newly created\n");

                    AssetDatabase.ImportAsset(
                        ExistingAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);

                    AssetDatabase.ImportAsset(
                        NewAssetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);

                    Require(
                        File.Exists(newAbsolute),
                        "Rollback probe could not create the transient asset.");

                    transientGuid =
                        AssetDatabase.AssetPathToGUID(
                            NewAssetPath);

                    Require(
                        !string.IsNullOrWhiteSpace(
                            transientGuid),
                        "Transient rollback asset has no Unity GUID.");

                    throw new IntentionalRollbackException();
                }
                catch (IntentionalRollbackException)
                {
                    intentionalFailureObserved = true;
                }

                Require(
                    intentionalFailureObserved,
                    "Rollback probe did not execute its intentional failure.");

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);

                Require(
                    File.Exists(existingAbsolute),
                    "Rollback removed an asset that existed before the transaction.");

                Require(
                    string.Equals(
                        SavicHashService.ComputeSha256(
                            existingAbsolute),
                        hashBefore,
                        StringComparison.OrdinalIgnoreCase),
                    "Rollback did not restore original asset bytes.");

                Require(
                    File.Exists(metaPath) &&
                    string.Equals(
                        SavicHashService.ComputeSha256(
                            metaPath),
                        metaHashBefore,
                        StringComparison.OrdinalIgnoreCase),
                    "Rollback did not restore original .meta bytes.");

                string guidAfter =
                    AssetDatabase.AssetPathToGUID(
                        ExistingAssetPath);

                Require(
                    string.Equals(
                        guidBefore,
                        guidAfter,
                        StringComparison.Ordinal),
                    "Rollback changed the Unity GUID of the restored asset.");

                Require(
                    !File.Exists(newAbsolute),
                    "Rollback left a newly-created asset behind.");

                Require(
                    !File.Exists(newAbsolute + ".meta"),
                    "Rollback left a newly-created .meta file behind.");

                Require(
                    AssetDatabase.LoadMainAssetAtPath(
                        NewAssetPath) == null,
                    "AssetDatabase still loads the rolled-back new asset.");

                // Unity may retain a Library-level GUID tombstone for a
                // deleted path until a later editor lifecycle. The authoritative
                // rollback contract is that neither payload nor .meta exists and
                // AssetDatabase cannot load an object from the path.
                Debug.Log(
                    "[SAVIC] PUBLICATION ROLLBACK PROBE - PASS\n" +
                    "Existing GUID preserved: " +
                    guidAfter +
                    "\nExisting bytes restored: " +
                    hashBefore +
                    "\nNew asset removed completely.");
            }
            finally
            {
                CleanupProbeAsset(
                    ExistingAssetPath);

                CleanupProbeAsset(
                    NewAssetPath);

                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void CleanupProbeAsset(
            string assetPath)
        {
            string absolute =
                ToAbsoluteProjectPath(
                    assetPath);

            if (AssetDatabase.LoadMainAssetAtPath(
                    assetPath) != null)
            {
                AssetDatabase.DeleteAsset(
                    assetPath);
            }

            if (File.Exists(absolute))
                File.Delete(absolute);

            if (File.Exists(absolute + ".meta"))
                File.Delete(absolute + ".meta");
        }

        private static void WriteUtf8(
            string absolutePath,
            string content)
        {
            string directory =
                Path.GetDirectoryName(
                    absolutePath)
                ?? throw new InvalidOperationException(
                    "Probe asset directory could not be resolved.");

            Directory.CreateDirectory(
                directory);

            File.WriteAllText(
                absolutePath,
                content,
                new UTF8Encoding(false));
        }

        private static string ToAbsoluteProjectPath(
            string assetPath)
        {
            string projectRoot =
                Directory.GetParent(
                    Application.dataPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Unity project root could not be resolved.");

            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static void Require(
            bool condition,
            string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private sealed class IntentionalRollbackException :
            Exception
        {
        }
    }
}
