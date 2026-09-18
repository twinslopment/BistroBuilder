using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BistroBuilder.FurnitureFinishes.Editor
{
    internal enum FurnitureFinishValidationSeverity
    {
        Information,
        Warning,
        Error
    }

    internal sealed class FurnitureFinishValidationMessage
    {
        public FurnitureFinishValidationSeverity Severity { get; }
        public string Text { get; }

        public FurnitureFinishValidationMessage(
            FurnitureFinishValidationSeverity severity,
            string text)
        {
            Severity = severity;
            Text = text ?? string.Empty;
        }
    }

    internal static class FurnitureFinishValidator
    {
        private static readonly Regex IdPattern =
            new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

        public static IReadOnlyList<FurnitureFinishValidationMessage> ValidateForPublish(
            FurnitureFinishProfile profile)
        {
            var result = new List<FurnitureFinishValidationMessage>();
            if (profile == null)
            {
                result.Add(Error("No hay perfil."));
                return result;
            }
            if (profile.SourceAsset == null)
                result.Add(Error("El perfil no tiene asset fuente."));
            if (!IdPattern.IsMatch(profile.FurnitureId ?? string.Empty))
                result.Add(Error($"Furniture ID inválido: '{profile.FurnitureId}'."));

            var zoneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var zone in profile.Zones)
            {
                if (zone == null)
                    continue;
                if (!IdPattern.IsMatch(zone.Id ?? string.Empty))
                    result.Add(Error($"Zone ID inválido: '{zone.Id}'."));
                else if (!zoneIds.Add(zone.Id))
                    result.Add(Error($"Zone ID duplicado: '{zone.Id}'."));

                if (!zone.ClassificationConfirmed || !HasCompatibleFamilies(zone))
                    result.Add(Warning($"{zone.DisplayName}: clasificación de superficie pendiente."));
            }

            if (profile.Variants.Count == 0)
                result.Add(Error("No existe ninguna variante para publicar."));

            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var variant in profile.Variants)
            {
                if (variant == null)
                    continue;
                if (!IdPattern.IsMatch(variant.Id ?? string.Empty))
                    result.Add(Error($"Variant ID inválido: '{variant.Id}'."));
                else if (!variantIds.Add(variant.Id))
                    result.Add(Error($"Variant ID duplicado: '{variant.Id}'."));

                ValidateEffectiveVariant(profile, variant, result);
            }

            if (!string.IsNullOrWhiteSpace(profile.DefaultVariantId)
                && !variantIds.Contains(profile.DefaultVariantId))
                result.Add(Error("La variante predeterminada no existe."));

            if (!HasErrors(result))
                result.Add(Info($"Perfil válido para publicación: {profile.Variants.Count} variante(s)."));
            return result;
        }

        public static bool HasErrors(IReadOnlyList<FurnitureFinishValidationMessage> messages)
        {
            if (messages == null)
                return true;
            foreach (var message in messages)
            {
                if (message.Severity == FurnitureFinishValidationSeverity.Error)
                    return true;
            }
            return false;
        }

        private static void ValidateEffectiveVariant(
            FurnitureFinishProfile profile,
            FurnitureFinishProfile.VariantDefinition variant,
            ICollection<FurnitureFinishValidationMessage> result)
        {
            foreach (var zone in profile.Zones)
            {
                if (zone == null)
                    continue;

                var finish = variant.FindFinish(zone.Id);
                if (finish != null && finish.Material == null)
                {
                    result.Add(Error($"{variant.DisplayName}/{zone.DisplayName}: acabado sin material."));
                    continue;
                }
                if (finish != null && !zone.Allows(finish.Family))
                {
                    result.Add(Error(
                        $"{variant.DisplayName}/{zone.DisplayName}: " +
                        $"familia {finish.Family} no compatible con la zona."));
                    continue;
                }

                foreach (var slot in zone.Slots)
                {
                    var renderer = FurnitureFinishAssetUtility.FindRenderer(profile.SourceAsset, slot.RendererPath);
                    if (renderer == null)
                    {
                        result.Add(Error($"{zone.DisplayName}: renderer inexistente '{slot.RendererPath}'."));
                        continue;
                    }
                    var materials = renderer.sharedMaterials;
                    if (slot.MaterialIndex < 0 || slot.MaterialIndex >= materials.Length)
                    {
                        result.Add(Error($"{zone.DisplayName}: slot {slot.MaterialIndex} fuera de rango."));
                        continue;
                    }

                    var effective = finish != null ? finish.Material : materials[slot.MaterialIndex];
                    if (effective == null)
                    {
                        result.Add(Error($"{variant.DisplayName}/{zone.DisplayName}: material sin resolver."));
                        continue;
                    }
                    if (FurnitureFinishAssetUtility.IsShaderBroken(effective))
                        result.Add(Error($"{variant.DisplayName}/{zone.DisplayName}: shader roto."));
                    if (FurnitureFinishAssetUtility.IsPlaceholder(effective))
                        result.Add(Error($"{variant.DisplayName}/{zone.DisplayName}: placeholder no publicable."));

                    var missing = zone.RequiredChannels
                        & ~FurnitureFinishAssetUtility.DetectProvidedChannels(effective);
                    if (missing != FurnitureFinishChannel.None)
                        result.Add(Error($"{variant.DisplayName}/{zone.DisplayName}: faltan canales {missing}."));
                }
            }
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

        private static FurnitureFinishValidationMessage Info(string text) =>
            new FurnitureFinishValidationMessage(FurnitureFinishValidationSeverity.Information, text);

        private static FurnitureFinishValidationMessage Warning(string text) =>
            new FurnitureFinishValidationMessage(FurnitureFinishValidationSeverity.Warning, text);

        private static FurnitureFinishValidationMessage Error(string text) =>
            new FurnitureFinishValidationMessage(FurnitureFinishValidationSeverity.Error, text);
    }
}