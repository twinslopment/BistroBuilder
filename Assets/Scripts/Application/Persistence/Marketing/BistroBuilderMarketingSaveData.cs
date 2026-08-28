using System;

/// <summary>
/// 7C — Estado persistente propio de Marketing.
/// Conserva campañas y el contador de leads ya materializados para que
/// una carga no duplique reservas atribuibles a Marketing.
/// </summary>
[Serializable]
public sealed class BistroBuilderMarketingSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public BistroBuilderMarketingSnapshot state =
        new BistroBuilderMarketingSnapshot();
    public int reservationLeadDay;
    public int reservationLeadsGeneratedForDay;

    public BistroBuilderMarketingSaveData DeepClone()
    {
        return new BistroBuilderMarketingSaveData
        {
            version = version,
            state = state != null ? state.DeepClone() : null,
            reservationLeadDay = reservationLeadDay,
            reservationLeadsGeneratedForDay =
                reservationLeadsGeneratedForDay
        };
    }

    public bool TryValidate(out string error)
    {
        if (version != CurrentVersion || state == null ||
            reservationLeadDay < 0 ||
            reservationLeadsGeneratedForDay < 0 ||
            reservationLeadsGeneratedForDay > 3)
        {
            error = "marketing.state contiene datos persistentes inválidos.";
            return false;
        }

        if (!BistroBuilderMarketingEngine.TryValidateSnapshot(
                state,
                out error))
        {
            return false;
        }

        if (reservationLeadDay == 0 &&
            reservationLeadsGeneratedForDay != 0)
        {
            error = "Marketing conserva leads sin un día asociado.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
