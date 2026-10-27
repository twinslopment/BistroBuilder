using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Shared, deterministic photo-key normalization used by the importer and
/// Carta runtime. This maps presentation names, not game-play DishIds.
/// </summary>
public static class BistroBuilderDishImageKeyUtility
{
    private static readonly Dictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "gazpacho", "gazpacho_andaluz" },
            { "pantumaca", "pan_con_tomate" },
            { "pan_tumaca", "pan_con_tomate" },
            { "pancontomate", "pan_con_tomate" },
            { "tablaembutidos", "tabla_embutidos_ibericos" },
            { "tabla_embutidos", "tabla_embutidos_ibericos" },
            { "tabla_de_embutidos_ibericos", "tabla_embutidos_ibericos" },
            { "tabla_de_ibericos", "tabla_embutidos_ibericos" },
            { "tabla_ibericos", "tabla_embutidos_ibericos" },
            { "patatasbravas", "patatas_bravas" },
            { "pimientospadron", "pimientos_padron" },
            { "pimientosdepadron", "pimientos_padron" },
            { "tortillapatatas", "tortilla_patatas" },
            { "tortilladepatatas", "tortilla_patatas" },
            { "ensaladillarusa", "ensaladilla_rusa" },
            { "aceitunasalinadas", "aceitunas_alinadas" },
            { "arrozconleche", "arroz_con_leche" },
            { "champinonesajillo", "champinones_al_ajillo" },
            { "polloajillo", "pollo_al_ajillo" },
            { "calamaresromana", "calamares_a_la_romana" },
            { "huevosrotos", "huevos_rotos" },
            { "pulpogallega", "pulpo_a_la_gallega" },
            { "quesomanchego", "queso_manchego" },
            { "atunencebollado", "atun_encebollado" },
            { "cazonadobo", "cazon_en_adobo" },
            { "jamoniberico", "jamon_iberico" },
            { "tartaqueso", "tarta_queso" },
            { "tarta_de_queso", "tarta_queso" },
            { "cocido", "cocido_madrileno" },
            { "fabada", "fabada_asturiana" },
            { "lentejas", "lentejas_estofadas" },
            { "marmitako_stew", "marmitako" },
            { "pote", "pote_asturiano" },
            { "croquetasjamoniberico", "croquetas_jamon_iberico" },
            { "croquetasdejamoniberico", "croquetas_jamon_iberico" }
        };

    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string input = value.Trim().ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(input.Length);
        bool separator = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (CharUnicodeInfo.GetUnicodeCategory(c) ==
                UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                separator = false;
            }
            else if (!separator && builder.Length > 0)
            {
                builder.Append('_');
                separator = true;
            }
        }
        return builder.ToString().Trim('_');
    }

    /// <summary>Same alias rules as the historical filename importer.</summary>
    public static string ResolveFileStem(string stem)
    {
        string value = Normalize(stem);
        if (Aliases.TryGetValue(value, out string alias))
            return alias;
        string compact = value.Replace("_", string.Empty);
        return Aliases.TryGetValue(compact, out alias) ? alias : value;
    }

    /// <summary>
    /// Compare a filename, display name or stable DishId without changing the
    /// DishId itself. Removes one conventional dish_ prefix.
    /// </summary>
    public static string CanonicalKey(string dishIdOrDisplayName)
    {
        string value = Normalize(dishIdOrDisplayName);
        if (value.StartsWith("dish_", StringComparison.Ordinal))
            value = value.Substring(5);
        return ResolveFileStem(value);
    }
}
