using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal readonly struct SavicModelFamilyProcessingOutcome
    {
        internal SavicModelFamilyProcessingOutcome(
            bool succeeded,
            string message,
            string reasonCode = "")
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
            ReasonCode =
                string.IsNullOrWhiteSpace(reasonCode)
                    ? succeeded
                        ? "PUBLISHED"
                        : "FAMILY_PUBLICATION_FAILED"
                    : reasonCode;
        }

        internal bool Succeeded { get; }
        internal string Message { get; }
        internal string ReasonCode { get; }

        internal static SavicModelFamilyProcessingOutcome Success(
            string message)
        {
            return new SavicModelFamilyProcessingOutcome(
                true,
                message);
        }

        internal static SavicModelFamilyProcessingOutcome Failure(
            string message,
            string reasonCode = "FAMILY_PUBLICATION_FAILED")
        {
            return new SavicModelFamilyProcessingOutcome(
                false,
                message,
                reasonCode);
        }
    }

    internal interface ISavicModelFamilyModule
    {
        string TypeId { get; }

        SavicModelFamilyProcessingOutcome Process(
            SavicManifest manifest,
            GameObject sourceModel);

        SavicModelFamilyProcessingOutcome ProcessAppearanceOnly(
            SavicManifest manifest,
            GameObject sourceModel);
    }

    internal sealed class SavicModelFamilyRegistry
    {
        private readonly Dictionary<string, ISavicModelFamilyModule>
            modulesByType =
                new Dictionary<string, ISavicModelFamilyModule>(
                    StringComparer.Ordinal);

        internal SavicModelFamilyRegistry(
            params ISavicModelFamilyModule[] modules)
        {
            if (modules == null)
                throw new ArgumentNullException(nameof(modules));

            for (int index = 0;
                 index < modules.Length;
                 index++)
            {
                ISavicModelFamilyModule module =
                    modules[index];

                if (module == null)
                    continue;

                string typeId =
                    NormalizeTypeId(
                        module.TypeId);

                if (string.IsNullOrWhiteSpace(typeId))
                {
                    throw new InvalidOperationException(
                        "SAVIC family module has no TypeId.");
                }

                if (modulesByType.ContainsKey(typeId))
                {
                    throw new InvalidOperationException(
                        "Duplicate SAVIC family module for type '" +
                        typeId +
                        "'.");
                }

                modulesByType.Add(
                    typeId,
                    module);
            }
        }

        internal bool TryResolve(
            string typeId,
            out ISavicModelFamilyModule module)
        {
            return modulesByType.TryGetValue(
                NormalizeTypeId(typeId),
                out module);
        }

        internal int Count =>
            modulesByType.Count;

        private static string NormalizeTypeId(
            string typeId)
        {
            return string.IsNullOrWhiteSpace(typeId)
                ? string.Empty
                : typeId.Trim();
        }
    }
}
