using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 8F — Panel jugable consultivo de reputación, satisfacción y reseñas.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Reputation/Reputation Player Screen")]
public sealed class BistroBuilderReputationPlayerScreen : MonoBehaviour
{
    [SerializeField] private BistroBuilderReputationPlayerFacade facade;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text summaryText;
    [SerializeField] private TMP_Text globalScoreText;
    [SerializeField] private TMP_Text satisfactionText;
    [SerializeField] private TMP_Text demandText;
    [SerializeField] private TMP_Text serviceAspectText;
    [SerializeField] private TMP_Text waitingAspectText;
    [SerializeField] private TMP_Text foodAspectText;
    [SerializeField] private TMP_Text valueAspectText;
    [SerializeField] private TMP_Text ambienceAspectText;
    [SerializeField] private TMP_Text experienceText;
    [SerializeField] private TMP_Text habitualText;
    [SerializeField] private TMP_Text discoveryText;
    [SerializeField] private TMP_Text reviewsText;
    [SerializeField] private TMP_Text feedbackText;

    private bool bound;
    public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        CacheDependencies();
        Bind();
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        CacheDependencies();
        Bind();
        if (facade != null)
        {
            facade.ViewInvalidated -= HandleInvalidated;
            facade.ViewInvalidated += HandleInvalidated;
        }
    }

    private void OnDisable()
    {
        if (facade != null) facade.ViewInvalidated -= HandleInvalidated;
        Unbind();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependencies();
        if (facade == null || panelRoot == null || canvasGroup == null ||
            closeButton == null || summaryText == null || globalScoreText == null ||
            satisfactionText == null || demandText == null ||
            serviceAspectText == null || waitingAspectText == null ||
            foodAspectText == null || valueAspectText == null ||
            ambienceAspectText == null || experienceText == null ||
            habitualText == null || discoveryText == null || reviewsText == null ||
            feedbackText == null)
        {
            error = "La UI jugable de Reputación está incompleta.";
            return false;
        }
        return facade.ValidateConfiguration(out error);
    }

    public void Show()
    {
        if (panelRoot == null) return;
        panelRoot.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        Refresh();
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (!IsVisible) return;
        if (!ValidateConfiguration(out string error) ||
            !facade.TryBuildSnapshot(
                out BistroBuilderReputationPlayerUiSnapshot snapshot,
                out error))
        {
            feedbackText.text = error;
            return;
        }

        summaryText.text = "Día " + snapshot.dayIndex + " · " +
            snapshot.totalExperiences + " experiencias · " +
            snapshot.reviewCount + " reseñas";
        globalScoreText.text = "REPUTACIÓN GLOBAL  " +
            FormatScore(snapshot.globalScoreBasisPoints);
        satisfactionText.text = "Satisfacción: " + BandLabel(snapshot.satisfactionBand) +
            " · última " + FormatScore(snapshot.latestSatisfactionBasisPoints);
        demandText.text = "Boca a boca " + FormatSignedPercent(snapshot.wordOfMouthBasisPoints) +
            " · demanda " + FormatSignedPercent(snapshot.persistentDemandBasisPoints) +
            " · retorno orgánico " + FormatSignedPercent(snapshot.organicRepeatVisitBasisPoints);

        serviceAspectText.text = "Servicio\n" + FormatScore(snapshot.serviceScoreBasisPoints);
        waitingAspectText.text = "Esperas\n" + FormatScore(snapshot.waitingScoreBasisPoints);
        foodAspectText.text = "Calidad percibida\n" + FormatScore(snapshot.foodScoreBasisPoints);
        valueAspectText.text = "Calidad / precio\n" + FormatScore(snapshot.valueScoreBasisPoints);
        ambienceAspectText.text = "Ambiente\n" + FormatScore(snapshot.ambienceScoreBasisPoints);

        experienceText.text = "Experiencias\n" + snapshot.totalExperiences +
            " totales · " + snapshot.positiveExperiences + " positivas · " +
            snapshot.negativeExperiences + " negativas · " +
            snapshot.activeVisitCount + " en curso";
        habitualText.text = "Clientes habituales\n" +
            snapshot.recurrentCohortCount + " cohortes conocidas · potencial de retorno " +
            FormatSignedPercent(snapshot.organicRepeatVisitBasisPoints);
        discoveryText.text = "Descubrimiento\nOrgánico " + snapshot.organicDiscoveries +
            " · Marketing " + snapshot.marketingDiscoveries +
            " · Boca a boca " + snapshot.wordOfMouthDiscoveries +
            " · Habituales " + snapshot.returningGuestDiscoveries +
            " · Reservas " + snapshot.reservationDiscoveries;
        reviewsText.text = BuildReviews(snapshot);
        feedbackText.text = string.Empty;
    }

    private static string BuildReviews(BistroBuilderReputationPlayerUiSnapshot snapshot)
    {
        if (snapshot.recentReviews == null || snapshot.recentReviews.Count == 0)
            return "RESEÑAS RECIENTES\nAún no hay reseñas.";

        var builder = new StringBuilder("RESEÑAS RECIENTES\n");
        for (int i = 0; i < snapshot.recentReviews.Count; i++)
        {
            BistroBuilderReputationPlayerReviewRow row = snapshot.recentReviews[i];
            if (row == null) continue;
            if (builder.Length > 18) builder.Append('\n');
            builder.Append('D').Append(row.dayIndex).Append("  ")
                .Append(new string('★', Math.Max(1, Math.Min(5, row.stars))))
                .Append(new string('☆', Math.Max(0, 5 - row.stars)))
                .Append("  ").Append(ReviewLabel(row.summaryKey));
        }
        return builder.ToString();
    }

    private static string ReviewLabel(string key)
    {
        bool negative = key != null && key.StartsWith("negative.", StringComparison.Ordinal);
        string aspect = key != null && key.Contains(".")
            ? key.Substring(key.IndexOf('.') + 1)
            : string.Empty;
        string label = aspect switch
        {
            "service" => "servicio",
            "waiting" => "tiempos de espera",
            "food" => "comida",
            "value" => "relación calidad/precio",
            "ambience" => "ambiente",
            _ => "experiencia general"
        };
        return negative ? "Crítica sobre " + label : "Destaca " + label;
    }

    private static string BandLabel(BistroBuilderCustomerSatisfactionBand band)
    {
        return band switch
        {
            BistroBuilderCustomerSatisfactionBand.VeryBad => "Muy mala",
            BistroBuilderCustomerSatisfactionBand.Bad => "Mala",
            BistroBuilderCustomerSatisfactionBand.Neutral => "Correcta",
            BistroBuilderCustomerSatisfactionBand.Good => "Buena",
            BistroBuilderCustomerSatisfactionBand.Excellent => "Excelente",
            _ => "Sin datos"
        };
    }

    private static string FormatScore(int basisPoints)
    {
        basisPoints = Mathf.Clamp(basisPoints, 0, 10000);
        return (basisPoints / 100f).ToString("0.0") + " %";
    }

    private static string FormatSignedPercent(int basisPoints)
    {
        string sign = basisPoints > 0 ? "+" : basisPoints < 0 ? "−" : string.Empty;
        return sign + (Math.Abs(basisPoints) / 100f).ToString("0.0") + " %";
    }

    private void Bind()
    {
        if (bound) return;
        closeButton?.onClick.AddListener(Hide);
        bound = true;
    }

    private void Unbind()
    {
        if (!bound) return;
        closeButton?.onClick.RemoveListener(Hide);
        bound = false;
    }

    private void HandleInvalidated()
    {
        if (isActiveAndEnabled && IsVisible) Refresh();
    }

    private void CacheDependencies()
    {
        if (facade == null) TryGetComponent(out facade);
    }

#if UNITY_EDITOR
    private void Reset() => CacheDependencies();
    private void OnValidate() => CacheDependencies();
#endif
}
