using System;

/// <summary>
/// Identidad estable de empleado. Nunca deriva de nombre, índice, GameObject
/// ni posición de escena.
/// </summary>
public static class BistroBuilderEmployeeIdUtility
{
    public const string Prefix = "emp_";
    private const int PayloadLength = 32;

    public static string CreateNew()
    {
        return Prefix + Guid.NewGuid().ToString("N").ToLowerInvariant();
    }

    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Length != Prefix.Length + PayloadLength ||
            !normalized.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        bool anyNonZero = false;
        for (int index = Prefix.Length; index < normalized.Length; index++)
        {
            char c = normalized[index];
            bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
            if (!hex)
            {
                return false;
            }
            anyNonZero |= c != '0';
        }
        return anyNonZero;
    }
}

/// <summary>
/// Identificadores de configuración de Personal: roles, zonas,
/// responsabilidades y adaptadores operativos.
/// </summary>
public static class BistroBuilderStaffStableIdUtility
{
    public static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    public static bool IsValid(string value)
    {
        string normalized = Normalize(value);
        if (normalized.Length < 1 || normalized.Length > 64)
        {
            return false;
        }

        for (int index = 0; index < normalized.Length; index++)
        {
            char c = normalized[index];
            bool valid = (c >= 'a' && c <= 'z') ||
                         (c >= '0' && c <= '9') ||
                         c == '.' || c == '_' || c == '-';
            if (!valid)
            {
                return false;
            }
        }
        return true;
    }

    public static bool IsValidOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) || IsValid(value);
    }
}

/// <summary>
/// IDs canónicos de extensiones operativas. El núcleo Employee no conoce
/// clases concretas como Waiter; 4D enlazará mediante este adaptador.
/// </summary>
public static class BistroBuilderStaffOperationalAdapterIds
{
    public const string WaiterAgent = "waiter.agent";
}
