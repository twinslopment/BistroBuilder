using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal enum FurnitureFinishProposalMode
    {
        ReplaceMissingMaterial,
        CompleteMissingChannels
    }

    internal sealed class FurnitureFinishProposal
    {
        public string ZoneId { get; }
        public FurnitureFinishProposalMode Mode { get; }
        public FurnitureFinishDefinition SuggestedFinish { get; }
        public FurnitureFinishChannel MissingChannels { get; }
        public Material SourceMaterial { get; }
        public string Reason { get; }

        public FurnitureFinishProposal(
            string zoneId,
            FurnitureFinishProposalMode mode,
            FurnitureFinishDefinition suggestedFinish,
            FurnitureFinishChannel missingChannels,
            Material sourceMaterial,
            string reason)
        {
            ZoneId = zoneId ?? string.Empty;
            Mode = mode;
            SuggestedFinish = suggestedFinish;
            MissingChannels = missingChannels;
            SourceMaterial = sourceMaterial;
            Reason = reason ?? string.Empty;
        }
    }

    internal static class FurnitureFinishAutoResolver
    {
        public static IReadOnlyList<FurnitureFinishProposal> Propose(
            FurnitureFinishProfile profile,
            FurnitureFinishLibrary library,
            IReadOnlyList<FurnitureFinishIssue> issues)
        {
            var proposals = new List<FurnitureFinishProposal>();
            if (profile == null || library == null || issues == null)
                return proposals;

            foreach (var zone in profile.Zones)
            {
                if (zone == null
                    || !zone.ClassificationConfirmed
                    || !HasCompatibleFamilies(zone))
                    continue;

                var replace = false;
                var missingChannels = FurnitureFinishChannel.None;
                Material commonSource = null;
                var sourceConflict = false;
                var hasActionableIssue = false;

                foreach (var issue in issues)
                {
                    if (!string.Equals(issue.ZoneId, zone.Id, StringComparison.Ordinal))
                        continue;

                    switch (issue.Kind)
                    {
                        case FurnitureFinishIssueKind.MissingMaterial:
                        case FurnitureFinishIssueKind.BrokenShader:
                        case FurnitureFinishIssueKind.PlaceholderMaterial:
                            replace = true;
                            hasActionableIssue = true;
                            break;

                        case FurnitureFinishIssueKind.MissingChannel:
                            missingChannels |= issue.Channel;
                            hasActionableIssue = true;
                            if (issue.SourceMaterial != null)
                            {
                                if (commonSource == null)
                                    commonSource = issue.SourceMaterial;
                                else if (commonSource != issue.SourceMaterial)
                                    sourceConflict = true;
                            }
                            break;
                    }
                }

                if (!hasActionableIssue)
                    continue;
                if (!replace && sourceConflict)
                    continue;

                var candidate = ChooseCandidate(library, zone, missingChannels);
                if (candidate == null)
                    continue;

                proposals.Add(new FurnitureFinishProposal(
                    zone.Id,
                    replace
                        ? FurnitureFinishProposalMode.ReplaceMissingMaterial
                        : FurnitureFinishProposalMode.CompleteMissingChannels,
                    candidate,
                    missingChannels,
                    commonSource,
                    replace
                        ? $"Reutilizar '{candidate.DisplayName}' para completar una zona sin acabado válido."
                        : $"Tomar únicamente los canales {missingChannels} que faltan desde '{candidate.DisplayName}'."));
            }

            return proposals;
        }

        public static FurnitureFinishProfile.VariantDefinition ApplyToVariant(
            FurnitureFinishProfile profile,
            IReadOnlyList<FurnitureFinishProposal> proposals,
            string variantId,
            string displayName)
        {
            if (profile == null)
                throw new InvalidOperationException("No hay perfil de mobiliario.");
            if (proposals == null || proposals.Count == 0)
                throw new InvalidOperationException("No existen propuestas automáticas aplicables.");

            var stableId = FurnitureFinishAssetUtility.StableId(variantId, "auto_finish");
            var existing = profile.FindVariant(stableId);
            var bindings = new List<FurnitureFinishProfile.ZoneFinishBinding>();
            if (existing != null)
                bindings.AddRange(existing.Bindings);

            foreach (var proposal in proposals)
            {
                if (FindBinding(bindings, proposal.ZoneId) != null)
                    continue;

                var finish = proposal.Mode == FurnitureFinishProposalMode.ReplaceMissingMaterial
                    ? proposal.SuggestedFinish
                    : FurnitureFinishMaterialComposer.Compose(
                        profile,
                        stableId,
                        proposal.ZoneId,
                        proposal.SourceMaterial,
                        proposal.SuggestedFinish,
                        proposal.MissingChannels);

                bindings.Add(new FurnitureFinishProfile.ZoneFinishBinding(
                    proposal.ZoneId,
                    finish));
            }

            var updated = new FurnitureFinishProfile.VariantDefinition(
                stableId,
                string.IsNullOrWhiteSpace(displayName) ? "Acabado automático" : displayName,
                bindings.ToArray());

            var variants = new List<FurnitureFinishProfile.VariantDefinition>();
            foreach (var variant in profile.Variants)
            {
                if (variant != null && !string.Equals(variant.Id, stableId, StringComparison.Ordinal))
                    variants.Add(variant);
            }
            variants.Add(updated);

            var defaultId = string.IsNullOrWhiteSpace(profile.DefaultVariantId)
                ? stableId
                : profile.DefaultVariantId;
            profile.EditorSetVariants(variants.ToArray(), defaultId);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return updated;
        }

        private static FurnitureFinishDefinition ChooseCandidate(
            FurnitureFinishLibrary library,
            FurnitureFinishProfile.ZoneDefinition zone,
            FurnitureFinishChannel missingChannels)
        {
            FurnitureFinishDefinition best = null;
            var bestScore = int.MinValue;
            foreach (var finish in library.Finishes)
            {
                if (finish == null || finish.Material == null || !zone.Allows(finish.Family))
                    continue;

                if (missingChannels != FurnitureFinishChannel.None
                    && (finish.ProvidedChannels & missingChannels) != missingChannels)
                    continue;

                var score = 10;
                score += TokenScore(zone.Id, finish.FinishId);
                score += TokenScore(zone.DisplayName, finish.DisplayName);
                foreach (var tag in finish.Tags)
                    score += TokenScore(zone.Id, tag);

                if (score > bestScore)
                {
                    best = finish;
                    bestScore = score;
                }
            }
            return best;
        }

        private static FurnitureFinishProfile.ZoneFinishBinding FindBinding(
            IEnumerable<FurnitureFinishProfile.ZoneFinishBinding> bindings,
            string zoneId)
        {
            foreach (var binding in bindings)
            {
                if (binding != null && string.Equals(binding.ZoneId, zoneId, StringComparison.Ordinal))
                    return binding;
            }
            return null;
        }

        private static bool HasCompatibleFamilies(
            FurnitureFinishProfile.ZoneDefinition zone)
        {
            if (zone == null)
                return false;
            foreach (var family in zone.CompatibleFamilies)
            {
                if (family != FurnitureSurfaceFamily.Unknown)
                    return true;
            }
            return zone.Family != FurnitureSurfaceFamily.Unknown;
        }

        private static int TokenScore(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
                return 0;
            var score = 0;
            var tokens = left.ToLowerInvariant().Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var target = right.ToLowerInvariant();
            foreach (var token in tokens)
            {
                if (token.Length >= 3 && target.Contains(token))
                    score += 2;
            }
            return score;
        }
    }
}