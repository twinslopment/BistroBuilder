using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fila reutilizable de la UI de Marketing. Solo devuelve la identidad
/// seleccionada y no conserva estado autoritativo de campañas.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderMarketingPlayerRowView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text familyText;
    [SerializeField] private TMP_Text metaText;
    [SerializeField] private TMP_Text statusText;

    private string rowId = string.Empty;
    private Action<string> selectionHandler;

    private static readonly Color Normal = new Color(0.09f, 0.105f, 0.10f, 0.98f);
    private static readonly Color Selected = new Color(0.19f, 0.24f, 0.19f, 1f);

    private void OnEnable()
    {
        if (selectButton == null) return;
        selectButton.onClick.RemoveListener(HandleSelected);
        selectButton.onClick.AddListener(HandleSelected);
    }

    private void OnDisable()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleSelected);
    }

    public bool ValidateConfiguration(out string error)
    {
        if (selectButton == null || backgroundImage == null || nameText == null ||
            familyText == null || metaText == null || statusText == null)
        {
            error = "La fila de Marketing tiene referencias visuales incompletas.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public void BindCampaign(
        BistroBuilderMarketingPlayerCampaignRow row,
        bool isSelected,
        Action<string> onSelected)
    {
        selectionHandler = onSelected;
        rowId = row != null ? row.campaignId : string.Empty;
        if (row == null)
        {
            Clear();
            return;
        }

        nameText.text = row.displayName;
        familyText.text = FamilyLabel(row.type);
        metaText.text = FormatMoney(row.costCents) + " · " + row.durationDays + " día(s)";
        statusText.text = string.IsNullOrWhiteSpace(row.blockedReason)
            ? (row.targetKind == BistroBuilderMarketingTargetKind.None
                ? "Disponible"
                : "Requiere objetivo")
            : row.blockedReason;
        selectButton.interactable = true;
        backgroundImage.color = isSelected ? Selected : Normal;
    }

    public void BindActive(
        BistroBuilderMarketingPlayerActiveRow row,
        bool isSelected,
        Action<string> onSelected)
    {
        selectionHandler = onSelected;
        rowId = row != null ? row.instanceId : string.Empty;
        if (row == null)
        {
            Clear();
            return;
        }

        nameText.text = row.displayName;
        familyText.text = FamilyLabel(row.type);
        metaText.text = row.targetDisplayName;
        statusText.text = row.daysRemaining + " día(s) restante(s)";
        selectButton.interactable = true;
        backgroundImage.color = isSelected ? Selected : Normal;
    }

    private void Clear()
    {
        rowId = string.Empty;
        nameText.text = "—";
        familyText.text = string.Empty;
        metaText.text = string.Empty;
        statusText.text = string.Empty;
        if (selectButton != null) selectButton.interactable = false;
        if (backgroundImage != null) backgroundImage.color = Normal;
    }

    private void HandleSelected()
    {
        if (!string.IsNullOrWhiteSpace(rowId))
            selectionHandler?.Invoke(rowId);
    }

    public static string FamilyLabel(BistroBuilderMarketingCampaignType type)
    {
        switch (type)
        {
            case BistroBuilderMarketingCampaignType.LocalAwareness: return "Difusión local";
            case BistroBuilderMarketingCampaignType.Promotions: return "Promociones";
            case BistroBuilderMarketingCampaignType.Digital: return "Digital";
            case BistroBuilderMarketingCampaignType.InfluencersPress: return "Prensa e influencers";
            case BistroBuilderMarketingCampaignType.EventsExperiences: return "Eventos y experiencias";
            case BistroBuilderMarketingCampaignType.LoyaltyReferral: return "Fidelización";
            case BistroBuilderMarketingCampaignType.MenuDishPromotion: return "Carta y platos";
            default: return type.ToString();
        }
    }

    private static string FormatMoney(long cents)
    {
        long absolute = Math.Abs(cents);
        return (cents < 0 ? "−" : string.Empty) +
               (absolute / 100L) + "," +
               (absolute % 100L).ToString("00") + " €";
    }
}
