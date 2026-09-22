using System;
using System.Collections.Generic;

namespace BistroBuilder.Editor.Savic
{
    internal static class SavicManifestMutations
    {
        internal static void UpsertArtifact(
            SavicManifest manifest,
            string role,
            string projectRelativePath,
            string builderId,
            string builderVersion,
            string inputFingerprint = "")
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException(
                    "Artifact role is required.",
                    nameof(role));

            manifest.artifacts ??=
                new List<SavicArtifactRecord>();

            SavicArtifactRecord record = null;

            for (int index = 0;
                 index < manifest.artifacts.Count;
                 index++)
            {
                SavicArtifactRecord candidate =
                    manifest.artifacts[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.role,
                        role,
                        StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            if (record == null)
            {
                record = new SavicArtifactRecord
                {
                    role = role
                };

                manifest.artifacts.Add(record);
            }

            record.projectRelativePath =
                projectRelativePath ?? string.Empty;
            record.builderId =
                builderId ?? string.Empty;
            record.builderVersion =
                builderVersion ?? string.Empty;
            record.inputFingerprint =
                inputFingerprint ?? string.Empty;
        }

        internal static void UpsertValidation(
            SavicManifest manifest,
            string validationId,
            string result,
            string severity,
            string message,
            string validatorVersion)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(validationId))
                throw new ArgumentException(
                    "Validation id is required.",
                    nameof(validationId));

            manifest.validations ??=
                new List<SavicValidationRecord>();

            SavicValidationRecord record = null;

            for (int index = 0;
                 index < manifest.validations.Count;
                 index++)
            {
                SavicValidationRecord candidate =
                    manifest.validations[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.validationId,
                        validationId,
                        StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            if (record == null)
            {
                record = new SavicValidationRecord
                {
                    validationId = validationId
                };

                manifest.validations.Add(record);
            }

            record.result = result ?? string.Empty;
            record.severity = severity ?? string.Empty;
            record.message = message ?? string.Empty;
            record.validatorVersion =
                validatorVersion ?? string.Empty;
        }

        internal static void UpsertDecision(
            SavicManifest manifest,
            string key,
            string value,
            string confidence,
            string evidence,
            string ruleId)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException(
                    "Decision key is required.",
                    nameof(key));

            manifest.decisions ??=
                new List<SavicDecisionRecord>();

            SavicDecisionRecord record = null;

            for (int index = 0;
                 index < manifest.decisions.Count;
                 index++)
            {
                SavicDecisionRecord candidate =
                    manifest.decisions[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.key,
                        key,
                        StringComparison.Ordinal))
                {
                    record = candidate;
                    break;
                }
            }

            if (record == null)
            {
                record = new SavicDecisionRecord
                {
                    key = key
                };

                manifest.decisions.Add(record);
            }

            record.value = value ?? string.Empty;
            record.confidence =
                string.IsNullOrWhiteSpace(confidence)
                    ? "UNKNOWN"
                    : confidence;
            record.evidence = evidence ?? string.Empty;
            record.ruleId = ruleId ?? string.Empty;
        }
    }
}
