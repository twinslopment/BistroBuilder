using System;
using System.Collections.Generic;

[Serializable]
public sealed class BistroBuilderAdvancedOrderPlayerLine
{
    public string orderId = string.Empty;
    public string lineId = string.Empty;
    public string dishId = string.Empty;
    public string dishName = string.Empty;
    public int priceCents;
    public BistroBuilderCanonicalOrderLineState state;
    public string stateLabel = string.Empty;
    public BistroBuilderAdvancedOrderLineOriginKind origin;
    public string originLabel = string.Empty;
    public BistroBuilderAdvancedOrderBillingMode billingMode;
    public string billingLabel = string.Empty;
    public string sourceLineId = string.Empty;
    public BistroBuilderAdvancedOrderIncidentKind incidentKind;
    public string incidentLabel = string.Empty;
    public string changeReason = string.Empty;
    public BistroBuilderAdvancedOrderActionAvailability actions;
}

[Serializable]
public sealed class BistroBuilderAdvancedOrderReplacementOption
{
    public string dishId = string.Empty;
    public string displayName = string.Empty;
    public int priceCents;
}

[Serializable]
public sealed class BistroBuilderAdvancedOrderPlayerSnapshot
{
    public readonly List<BistroBuilderAdvancedOrderPlayerLine> lines =
        new List<BistroBuilderAdvancedOrderPlayerLine>();
    public readonly List<BistroBuilderAdvancedOrderReplacementOption> replacements =
        new List<BistroBuilderAdvancedOrderReplacementOption>();
    public int activeOrderCount;
    public int totalPriceCents;
    public int changedLineCount;
    public int incidentCount;
}
