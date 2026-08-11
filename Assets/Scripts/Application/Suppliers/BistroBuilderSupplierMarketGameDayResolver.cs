using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Adaptador desacoplado del calendario existente. 2.3C no fuerza una API
/// nueva sobre GameClock: primero busca un día absoluto y, si el reloj solo
/// publica hora/minuto, sintetiza días contando cruces de medianoche.
/// </summary>
public sealed class BistroBuilderSupplierMarketGameDayResolver
{
    private static readonly string[] PreferredDayMembers =
    {
        "CurrentGameDay", "GameDay", "CurrentDay", "DayNumber",
        "CalendarDay", "CurrentCalendarDay", "Day"
    };

    private static readonly string[] PreferredHourMembers =
    {
        "CurrentHour", "Hour", "currentHour", "hour"
    };

    private Component daySource;
    private MemberInfo dayMember;
    private Component hourSource;
    private MemberInfo hourMember;
    private int syntheticDay = 1;
    private int lastObservedHour = -1;
    private bool diagnosticResolved;
    private string diagnostic = "Sin resolver";

    public string Diagnostic => diagnostic;
    public bool HasAbsoluteDaySource => daySource != null && dayMember != null;
    public bool HasClockFallback => hourSource != null && hourMember != null;

    public void Reset(int startGameDay)
    {
        syntheticDay = Mathf.Max(1, startGameDay);
        lastObservedHour = -1;
        daySource = null;
        dayMember = null;
        hourSource = null;
        hourMember = null;
        diagnosticResolved = false;
        diagnostic = "Sin resolver";
    }

    public bool TryGetGameDay(out int gameDay)
    {
        gameDay = syntheticDay;

        if (!diagnosticResolved)
        {
            ResolveSources();
        }

        if (daySource != null && dayMember != null)
        {
            int absoluteDay;
            if (TryReadInteger(daySource, dayMember, out absoluteDay) && absoluteDay >= 1)
            {
                syntheticDay = absoluteDay;
                gameDay = absoluteDay;
                return true;
            }
        }

        if (hourSource != null && hourMember != null)
        {
            int hour;
            if (TryReadInteger(hourSource, hourMember, out hour) && hour >= 0 && hour <= 23)
            {
                if (lastObservedHour >= 0 && hour < lastObservedHour)
                {
                    syntheticDay++;
                }

                lastObservedHour = hour;
                gameDay = syntheticDay;
                return true;
            }
        }

        return false;
    }

    public void ForceSyntheticDayForRestore(int gameDay)
    {
        syntheticDay = Mathf.Max(1, gameDay);
        lastObservedHour = -1;
    }

    private void ResolveSources()
    {
        diagnosticResolved = true;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(UnityEngine.FindObjectsSortMode.None);

        for (int nameIndex = 0; nameIndex < PreferredDayMembers.Length; nameIndex++)
        {
            string memberName = PreferredDayMembers[nameIndex];
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (!IsCandidateBehaviour(behaviour))
                {
                    continue;
                }

                MemberInfo member = FindNumericMember(behaviour.GetType(), memberName);
                int value;
                if (member != null && TryReadInteger(behaviour, member, out value) && value >= 1)
                {
                    daySource = behaviour;
                    dayMember = member;
                    diagnostic = "Día absoluto: " + behaviour.GetType().Name + "." + member.Name;
                    return;
                }
            }
        }

        for (int nameIndex = 0; nameIndex < PreferredHourMembers.Length; nameIndex++)
        {
            string memberName = PreferredHourMembers[nameIndex];
            for (int behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
            {
                MonoBehaviour behaviour = behaviours[behaviourIndex];
                if (behaviour == null ||
                    behaviour.GetType().Name.IndexOf("Clock", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                MemberInfo member = FindNumericMember(behaviour.GetType(), memberName);
                int value;
                if (member != null && TryReadInteger(behaviour, member, out value) &&
                    value >= 0 && value <= 23)
                {
                    hourSource = behaviour;
                    hourMember = member;
                    lastObservedHour = value;
                    diagnostic = "Fallback por medianoche: " + behaviour.GetType().Name + "." + member.Name;
                    return;
                }
            }
        }

        diagnostic = "No se encontró día absoluto ni hora legible. El mercado sigue disponible mediante TryAdvanceToGameDay y las pruebas controladas.";
    }

    private static bool IsCandidateBehaviour(MonoBehaviour behaviour)
    {
        if (behaviour == null)
        {
            return false;
        }

        string typeName = behaviour.GetType().Name;
        return typeName.IndexOf("Clock", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Calendar", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("GameIdentity", StringComparison.OrdinalIgnoreCase) >= 0 ||
               typeName.IndexOf("Date", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static MemberInfo FindNumericMember(Type type, string exactName)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        FieldInfo field = type.GetField(exactName, Flags);
        if (field != null && IsIntegerType(field.FieldType))
        {
            return field;
        }

        PropertyInfo property = type.GetProperty(exactName, Flags);
        if (property != null && property.GetIndexParameters().Length == 0 &&
            property.GetGetMethod(true) != null && IsIntegerType(property.PropertyType))
        {
            return property;
        }

        return null;
    }

    private static bool TryReadInteger(object target, MemberInfo member, out int value)
    {
        value = 0;
        try
        {
            object raw;
            FieldInfo field = member as FieldInfo;
            if (field != null)
            {
                raw = field.GetValue(target);
            }
            else
            {
                PropertyInfo property = member as PropertyInfo;
                if (property == null)
                {
                    return false;
                }

                raw = property.GetValue(target, null);
            }

            if (raw is int)
            {
                value = (int)raw;
                return true;
            }

            if (raw is long)
            {
                long longValue = (long)raw;
                if (longValue >= int.MinValue && longValue <= int.MaxValue)
                {
                    value = (int)longValue;
                    return true;
                }
            }

            if (raw is short)
            {
                value = (short)raw;
                return true;
            }

            if (raw is byte)
            {
                value = (byte)raw;
                return true;
            }
        }
        catch
        {
            // El resolver nunca debe romper gameplay por una fuente de reloj no compatible.
        }

        return false;
    }

    private static bool IsIntegerType(Type type)
    {
        return type == typeof(int) || type == typeof(long) ||
               type == typeof(short) || type == typeof(byte);
    }
}
