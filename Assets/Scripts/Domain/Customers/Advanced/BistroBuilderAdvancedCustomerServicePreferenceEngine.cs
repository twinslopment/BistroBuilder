using System;
using System.Collections.Generic;

/// <summary>Motor puro de preferencias operativas, VIP y necesidades especiales.</summary>
public static class BistroBuilderAdvancedCustomerServicePreferenceEngine
{
    private const BistroBuilderCustomerSpecialNeed AllSpecialNeeds =
        BistroBuilderCustomerSpecialNeed.AccessibleSeating |
        BistroBuilderCustomerSpecialNeed.QuietSeating |
        BistroBuilderCustomerSpecialNeed.DietaryAwareness;

    public static bool TryBuildPreference(
        BistroBuilderAdvancedCustomerGroupProfile profile,
        BistroBuilderAdvancedCustomerHistoryRecord history,
        out BistroBuilderAdvancedCustomerServicePreference preference,
        out string error)
    {
        preference = null;
        if (profile == null || profile.groupId < 1 || profile.members == null ||
            profile.members.Count == 0)
        {
            error = "No existe un perfil de grupo válido para preferencias de servicio.";
            return false;
        }

        var tags = new Dictionary<string, int>(StringComparer.Ordinal);
        BistroBuilderCustomerSpecialNeed needs = BistroBuilderCustomerSpecialNeed.None;
        for (int i = 0; i < profile.members.Count; i++)
        {
            BistroBuilderAdvancedCustomerMemberProfile member = profile.members[i];
            if (member == null || (member.specialNeeds & ~AllSpecialNeeds) != 0)
            {
                error = "El grupo contiene necesidades especiales inválidas.";
                return false;
            }
            needs |= member.specialNeeds;
            AddTag(tags, member.preferredZoneTagId);
        }

        bool vip = history != null &&
            history.loyaltyTier == BistroBuilderCustomerLoyaltyTier.Vip;
        if ((needs & BistroBuilderCustomerSpecialNeed.QuietSeating) != 0)
            AddTag(tags, "quiet");
        if (vip)
        {
            AddTag(tags, "vip");
            AddTag(tags, "premium");
        }

        preference = new BistroBuilderAdvancedCustomerServicePreference
        {
            groupId = profile.groupId,
            isVip = vip,
            specialNeeds = needs
        };

        var ordered = new List<KeyValuePair<string, int>>(tags);
        ordered.Sort((a, b) =>
        {
            int count = b.Value.CompareTo(a.Value);
            return count != 0 ? count : string.CompareOrdinal(a.Key, b.Key);
        });
        for (int i = 0; i < ordered.Count; i++)
            preference.preferredZoneTagIds.Add(ordered[i].Key);

        error = string.Empty;
        return true;
    }

    public static BistroBuilderAdvancedCustomerTableEvaluation EvaluateTable(
        BistroBuilderAdvancedCustomerServicePreference preference,
        BistroBuilderAdvancedCustomerTableDescriptor table,
        int groupSize)
    {
        var result = new BistroBuilderAdvancedCustomerTableEvaluation();
        if (preference == null || table == null || groupSize < 1 ||
            table.capacity < groupSize || !table.available)
        {
            result.preferenceScore = int.MinValue / 4;
            result.unmetSpecialNeeds = int.MaxValue / 4;
            return result;
        }

        for (int i = 0; i < preference.preferredZoneTagIds.Count; i++)
        {
            string tag = Normalize(preference.preferredZoneTagIds[i]);
            if (!ContainsTag(table.semanticTags, tag)) continue;
            result.matchedPreferences++;
            result.preferenceScore += Math.Max(500, 3000 - i * 350);
        }

        if ((preference.specialNeeds &
             BistroBuilderCustomerSpecialNeed.AccessibleSeating) != 0)
        {
            if ((table.features & BistroBuilderCustomerTableFeature.Accessible) != 0)
                result.preferenceScore += 6000;
            else
            {
                result.unmetSpecialNeeds++;
                result.preferenceScore -= 6000;
            }
        }

        if ((preference.specialNeeds &
             BistroBuilderCustomerSpecialNeed.QuietSeating) != 0)
        {
            bool quiet = (table.features & BistroBuilderCustomerTableFeature.Quiet) != 0 ||
                ContainsTag(table.semanticTags, "quiet");
            if (quiet) result.preferenceScore += 3500;
            else
            {
                result.unmetSpecialNeeds++;
                result.preferenceScore -= 2500;
            }
        }

        if (preference.isVip)
        {
            bool vip = (table.features & BistroBuilderCustomerTableFeature.VipPreferred) != 0 ||
                ContainsTag(table.semanticTags, "vip") ||
                ContainsTag(table.semanticTags, "premium");
            result.preferenceScore += vip ? 3000 : 0;
        }
        return result;
    }

    private static void AddTag(Dictionary<string, int> tags, string value)
    {
        string normalized = Normalize(value);
        if (!BistroBuilderAdvancedCustomerProfileEngine.IsSafeId(normalized)) return;
        if (tags.TryGetValue(normalized, out int count))
            tags[normalized] = count + 1;
        else
            tags.Add(normalized, 1);
    }

    private static bool ContainsTag(IReadOnlyList<string> tags, string value)
    {
        if (tags == null) return false;
        string normalized = Normalize(value);
        for (int i = 0; i < tags.Count; i++)
            if (string.Equals(Normalize(tags[i]), normalized, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static string Normalize(string value) =>
        BistroBuilderAdvancedCustomerProfileEngine.NormalizeId(value);
}
