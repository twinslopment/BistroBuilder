using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    public static class SavicLegacyAdoptionSelfTest
    {
        [MenuItem(
            "Tools/Bistro Builder/SAVIC/Diagnostics/Run Legacy Adoption Self-Test",
            false,
            126)]
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

            SavicProjectInventorySnapshot inventory =
                context.ProjectInventory.ScanAndPersist();

            int manifestsBefore =
                context.Manifests.GetAll().Count;

            IReadOnlyList<SavicLegacyAdoptionCandidate> preview =
                context.LegacyAdoption.GetPreview(false);

            Require(
                preview.Count == inventory.legacyPendingAdoption,
                "Legacy preview count does not match the canonical inventory.");

            Require(
                context.Manifests.GetAll().Count == manifestsBefore,
                "Preview mutated SAVIC manifests.");

            HashSet<string> ids =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            int eligible = 0;
            int recommended = 0;
            int manualOnly = 0;

            foreach (SavicLegacyAdoptionCandidate candidate in preview)
            {
                Require(
                    candidate?.Record != null,
                    "Legacy preview contains a null record.");

                Require(
                    !string.IsNullOrWhiteSpace(
                        candidate.ProposedSavicId),
                    "Legacy candidate has no deterministic SavicId.");

                Require(
                    ids.Add(candidate.ProposedSavicId),
                    "Two legacy assets resolve to the same deterministic SavicId.");

                string second =
                    SavicLegacyAdoptionService
                        .BuildDeterministicSavicId(
                            candidate.Record.assetGuid);

                Require(
                    string.Equals(
                        candidate.ProposedSavicId,
                        second,
                        StringComparison.Ordinal),
                    "Legacy SavicId is not deterministic.");

                string sourceA =
                    SavicLegacyAdoptionService
                        .BuildDeterministicSourceHash(
                            candidate.Record.assetGuid,
                            candidate.Record.itemId);

                string sourceB =
                    SavicLegacyAdoptionService
                        .BuildDeterministicSourceHash(
                            candidate.Record.assetGuid,
                            candidate.Record.itemId);

                Require(
                    string.Equals(
                        sourceA,
                        sourceB,
                        StringComparison.Ordinal) &&
                    sourceA.Length == 64,
                    "Legacy source fingerprint is not deterministic SHA-256.");

                if (candidate.Eligible)
                    eligible++;

                if (candidate.RecommendedForBatch)
                    recommended++;

                if (SavicLegacyAdoptionService
                        .LooksLikeTestContent(
                            candidate.Record))
                {
                    Require(
                        !candidate.RecommendedForBatch,
                        "Test content must not be batch-adopted automatically.");
                    manualOnly++;
                }
            }

            Require(
                context.Manifests.GetAll().Count == manifestsBefore,
                "Legacy self-test mutated SAVIC manifests.");

            Debug.Log(
                "[SAVIC] LEGACY ADOPTION SELF-TEST - PASS\n" +
                "Candidates: " + preview.Count +
                "\nEligible: " + eligible +
                "\nBatch recommended: " + recommended +
                "\nManual-only/test content: " + manualOnly +
                "\nPreview is deterministic and non-destructive.");
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
