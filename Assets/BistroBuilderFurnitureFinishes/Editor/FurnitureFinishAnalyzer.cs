using System;
using System.Collections.Generic;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal enum FurnitureFinishIssueKind
    {
        MissingRenderer,
        InvalidMaterialIndex,
        MissingMaterial,
        BrokenShader,
        PlaceholderMaterial,
        MissingChannel,
        UnclassifiedSurface
    }

    internal enum FurnitureFinishIssueSeverity
    {
        Information,
        Warning,
        Error
    }

    internal sealed class FurnitureFinishIssue
    {
        public FurnitureFinishIssueKind Kind { get; }
        public FurnitureFinishIssueSeverity Severity { get; }
        public string ZoneId { get; }
        public string RendererPath { get; }
        public int MaterialIndex { get; }
        public FurnitureFinishChannel Channel { get; }
        public Material SourceMaterial { get; }
        public string Message { get; }

        public FurnitureFinishIssue(
            FurnitureFinishIssueKind kind,
            FurnitureFinishIssueSeverity severity,
            string zoneId,
            string rendererPath,
            int materialIndex,
            FurnitureFinishChannel channel,
            Material sourceMaterial,
            string message)
        {
            Kind = kind;
            Severity = severity;
            ZoneId = zoneId ?? string.Empty;
            RendererPath = rendererPath ?? string.Empty;
            MaterialIndex = materialIndex;
            Channel = channel;
            SourceMaterial = sourceMaterial;
            Message = message ?? string.Empty;
        }
    }

    internal static class FurnitureFinishAnalyzer
    {
        private static readonly FurnitureFinishChannel[] Channels =
        {
            FurnitureFinishChannel.BaseColorMap,
            FurnitureFinishChannel.NormalMap,
            FurnitureFinishChannel.MaskMap,
            FurnitureFinishChannel.OcclusionMap
        };

        public static IReadOnlyList<FurnitureFinishIssue> Analyze(FurnitureFinishProfile profile)
        {
            var issues = new List<FurnitureFinishIssue>();
            if (profile == null || profile.SourceAsset == null)
                return issues;

            foreach (var zone in profile.Zones)
            {
                if (zone == null)
                    continue;

                if (!zone.ClassificationConfirmed || !HasCompatibleFamilies(zone))
                {
                    issues.Add(new FurnitureFinishIssue(
                        FurnitureFinishIssueKind.UnclassifiedSurface,
                        FurnitureFinishIssueSeverity.Warning,
                        zone.Id,
                        string.Empty,
                        -1,
                        FurnitureFinishChannel.None,
                        null,
                        $"{zone.DisplayName}: tipo de superficie incierto."));
                }

                foreach (var slot in zone.Slots)
                    AnalyzeSlot(profile.SourceAsset, zone, slot, issues);
            }

            return issues;
        }

        private static void AnalyzeSlot(
            GameObject source,
            FurnitureFinishProfile.ZoneDefinition zone,
            FurnitureFinishProfile.SlotBinding slot,
            ICollection<FurnitureFinishIssue> issues)
        {
            var renderer = FurnitureFinishAssetUtility.FindRenderer(source, slot.RendererPath);
            if (renderer == null)
            {
                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.MissingRenderer,
                    FurnitureFinishIssueSeverity.Error,
                    zone,
                    slot,
                    null,
                    FurnitureFinishChannel.None,
                    $"No existe el renderer '{slot.RendererPath}'."));
                return;
            }

            var materials = renderer.sharedMaterials;
            if (slot.MaterialIndex < 0 || slot.MaterialIndex >= materials.Length)
            {
                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.InvalidMaterialIndex,
                    FurnitureFinishIssueSeverity.Error,
                    zone,
                    slot,
                    null,
                    FurnitureFinishChannel.None,
                    $"{zone.DisplayName}: slot {slot.MaterialIndex} fuera de rango."));
                return;
            }

            var material = materials[slot.MaterialIndex];
            if (material == null)
            {
                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.MissingMaterial,
                    FurnitureFinishIssueSeverity.Error,
                    zone,
                    slot,
                    null,
                    FurnitureFinishChannel.None,
                    $"{zone.DisplayName}: sin material."));
                return;
            }

            if (FurnitureFinishAssetUtility.IsShaderBroken(material))
            {
                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.BrokenShader,
                    FurnitureFinishIssueSeverity.Error,
                    zone,
                    slot,
                    material,
                    FurnitureFinishChannel.None,
                    $"{zone.DisplayName}: shader roto o no disponible."));
                return;
            }

            if (FurnitureFinishAssetUtility.IsPlaceholder(material))
            {
                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.PlaceholderMaterial,
                    FurnitureFinishIssueSeverity.Warning,
                    zone,
                    slot,
                    material,
                    FurnitureFinishChannel.None,
                    $"{zone.DisplayName}: material temporal/placeholder."));
            }

            foreach (var channel in Channels)
            {
                if ((zone.RequiredChannels & channel) == 0)
                    continue;
                if (FurnitureFinishAssetUtility.GetTexture(material, channel) != null)
                    continue;

                issues.Add(NewIssue(
                    FurnitureFinishIssueKind.MissingChannel,
                    FurnitureFinishIssueSeverity.Warning,
                    zone,
                    slot,
                    material,
                    channel,
                    $"{zone.DisplayName}: falta {ChannelLabel(channel)}."));
            }
        }

        private static FurnitureFinishIssue NewIssue(
            FurnitureFinishIssueKind kind,
            FurnitureFinishIssueSeverity severity,
            FurnitureFinishProfile.ZoneDefinition zone,
            FurnitureFinishProfile.SlotBinding slot,
            Material material,
            FurnitureFinishChannel channel,
            string message)
        {
            return new FurnitureFinishIssue(
                kind,
                severity,
                zone.Id,
                slot.RendererPath,
                slot.MaterialIndex,
                channel,
                material,
                message);
        }

        private static bool HasCompatibleFamilies(
            FurnitureFinishProfile.ZoneDefinition zone)
        {
            foreach (var family in zone.CompatibleFamilies)
            {
                if (family != FurnitureSurfaceFamily.Unknown)
                    return true;
            }
            return zone.Family != FurnitureSurfaceFamily.Unknown;
        }

        private static string ChannelLabel(FurnitureFinishChannel channel)
        {
            switch (channel)
            {
                case FurnitureFinishChannel.BaseColorMap:
                    return "Base Color";
                case FurnitureFinishChannel.NormalMap:
                    return "Normal";
                case FurnitureFinishChannel.MaskMap:
                    return "Mask/Roughness";
                case FurnitureFinishChannel.OcclusionMap:
                    return "AO";
                default:
                    return channel.ToString();
            }
        }
    }
}