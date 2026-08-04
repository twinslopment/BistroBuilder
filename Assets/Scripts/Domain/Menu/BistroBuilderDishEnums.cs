using System;
using System.Text;

/// <summary>
/// Servicios del día en los que un plato puede ofrecerse.
/// Se expresa como máscara para permitir varias franjas simultáneas.
/// </summary>
[Flags]
public enum BistroBuilderMealServiceAvailability
{
    None = 0,
    Breakfast = 1 << 0,
    Lunch = 1 << 1,
    Dinner = 1 << 2,
    All = Breakfast | Lunch | Dinner
}

/// <summary>
/// Categoría comercial del plato dentro de la carta.
/// </summary>
public enum BistroBuilderDishCategory
{
    Starter = 0,
    MainCourse = 1,
    Dessert = 2,
    Beverage = 3,
    SideDish = 4,
    SharedDish = 5,
    TastingItem = 6
}

/// <summary>
/// Identificadores estables y persistentes de las categorías canónicas.
///
/// El enum histórico se conserva para no romper los assets ya publicados,
/// pero ningún sistema nuevo debe persistir su valor numérico como identidad
/// de categoría. Los CategoryId permiten añadir, ordenar y localizar
/// categorías sin reinterpretar partidas ni definiciones existentes.
/// </summary>
public static class BistroBuilderDishCategoryIdUtility
{
    public const string Starter = "category_starter";
    public const string MainCourse = "category_main_course";
    public const string Dessert = "category_dessert";
    public const string Beverage = "category_beverage";
    public const string SideDish = "category_side_dish";
    public const string SharedDish = "category_shared_dish";
    public const string TastingItem = "category_tasting_item";

    public static string FromLegacyCategory(
        BistroBuilderDishCategory category
    )
    {
        switch (category)
        {
            case BistroBuilderDishCategory.Starter:
                return Starter;
            case BistroBuilderDishCategory.MainCourse:
                return MainCourse;
            case BistroBuilderDishCategory.Dessert:
                return Dessert;
            case BistroBuilderDishCategory.Beverage:
                return Beverage;
            case BistroBuilderDishCategory.SideDish:
                return SideDish;
            case BistroBuilderDishCategory.SharedDish:
                return SharedDish;
            case BistroBuilderDishCategory.TastingItem:
                return TastingItem;
            default:
                return string.Empty;
        }
    }

    public static bool TryGetLegacyCategory(
        string categoryId,
        out BistroBuilderDishCategory category
    )
    {
        string normalized =
            BistroBuilderMenuIdUtility.NormalizeStableId(categoryId);

        switch (normalized)
        {
            case Starter:
                category = BistroBuilderDishCategory.Starter;
                return true;
            case MainCourse:
                category = BistroBuilderDishCategory.MainCourse;
                return true;
            case Dessert:
                category = BistroBuilderDishCategory.Dessert;
                return true;
            case Beverage:
                category = BistroBuilderDishCategory.Beverage;
                return true;
            case SideDish:
                category = BistroBuilderDishCategory.SideDish;
                return true;
            case SharedDish:
                category = BistroBuilderDishCategory.SharedDish;
                return true;
            case TastingItem:
                category = BistroBuilderDishCategory.TastingItem;
                return true;
            default:
                category = default(BistroBuilderDishCategory);
                return false;
        }
    }

    public static bool IsCanonical(string categoryId)
    {
        return TryGetLegacyCategory(categoryId, out _);
    }
}

/// <summary>
/// Pase gastronómico al que pertenece el plato.
/// Se mantiene separado de la categoría para soportar menús degustación.
/// </summary>
public enum BistroBuilderDishCourse
{
    Unspecified = 0,
    Welcome = 1,
    Starter = 2,
    Main = 3,
    Dessert = 4,
    Beverage = 5
}

/// <summary>
/// Estación principal que deberá procesar el plato en la futura cocina
/// avanzada. En 367A solo se persiste como dato canónico.
/// </summary>
public enum BistroBuilderKitchenStationType
{
    None = 0,
    ColdPreparation = 1,
    HotKitchen = 2,
    Grill = 3,
    Fryer = 4,
    Oven = 5,
    Pastry = 6,
    Bar = 7
}

/// <summary>
/// Utilidades compartidas por el dominio de carta para producir y validar
/// identificadores estables. No depende de nombres visibles ni traducciones.
/// </summary>
public static class BistroBuilderMenuIdUtility
{
    /// <summary>
    /// Normaliza texto de autoría a minúsculas ASCII, sustituyendo grupos
    /// de separadores por un único guion bajo.
    /// </summary>
    public static string NormalizeStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim().ToLowerInvariant();
        StringBuilder builder = new StringBuilder(trimmed.Length);
        bool pendingSeparator = false;

        for (int index = 0; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool isLetter = character >= 'a' && character <= 'z';
            bool isDigit = character >= '0' && character <= '9';
            bool isExplicitSeparator =
                character == '_' ||
                character == '-' ||
                character == '.';

            if (isLetter || isDigit)
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                }

                builder.Append(character);
                pendingSeparator = false;
                continue;
            }

            if (isExplicitSeparator || char.IsWhiteSpace(character))
            {
                pendingSeparator = builder.Length > 0;
            }
        }

        return builder.ToString().Trim('_');
    }

    /// <summary>
    /// Comprueba que el identificador ya está normalizado y que solo usa
    /// caracteres seguros para archivos, diccionarios y partidas guardadas.
    /// </summary>
    public static bool IsValidStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 96)
        {
            return false;
        }

        char first = value[0];

        if (first < 'a' || first > 'z')
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool valid =
                (character >= 'a' && character <= 'z') ||
                (character >= '0' && character <= '9') ||
                character == '_' ||
                character == '-' ||
                character == '.';

            if (!valid)
            {
                return false;
            }
        }

        return string.Equals(
            value,
            NormalizeStableId(value),
            StringComparison.Ordinal
        );
    }

    /// <summary>
    /// Verifica que una máscara de servicios no contiene bits desconocidos.
    /// </summary>
    public static bool IsValidServiceMask(
        BistroBuilderMealServiceAvailability availability,
        bool allowNone
    )
    {
        int rawValue = (int)availability;
        int knownBits = (int)BistroBuilderMealServiceAvailability.All;

        if ((rawValue & ~knownBits) != 0)
        {
            return false;
        }

        return allowNone || availability != BistroBuilderMealServiceAvailability.None;
    }
}
