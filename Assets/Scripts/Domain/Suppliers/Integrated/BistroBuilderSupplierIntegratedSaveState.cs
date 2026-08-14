using System;
using UnityEngine;

/// <summary>
/// Estado agregado de Proveedores 2.3JKL.
///
/// Una sola sección de guardado captura de forma atómica las seis autoridades
/// runtime con estado propio creadas en 2.3C/2.3D/2.3E/2.3G/2.3H/2.3I.
/// 2.3F es derivado/read-only y por tanto NO se persiste.
///
/// El orden canónico de restauración es:
/// 2.3C mercado -> 2.3D comercial -> 2.3E pedidos -> 2.3G logística
/// -> 2.3H presentación -> 2.3I progresión.
/// </summary>
[Serializable]
public sealed class BistroBuilderSupplierIntegratedSaveState
{
    public const string CurrentSchemaId = "supplier.integrated.runtime";
    public const int CurrentSchemaVersion = 1;

    public string schemaId = CurrentSchemaId;
    public int schemaVersion = CurrentSchemaVersion;

    public BistroBuilderSupplierMarketSnapshot market;
    public BistroBuilderSupplierCommercialIntelligenceSnapshot commercial;
    public BistroBuilderSupplierPurchaseOrdersSnapshot orders;
    public BistroBuilderSupplierLogisticsSnapshot logistics;
    public BistroBuilderSupplierDeliveryPresentationSnapshot deliveryPresentation;
    public BistroBuilderSupplierProgressionSnapshot progression;

    public BistroBuilderSupplierIntegratedSaveState DeepClone()
    {
        return new BistroBuilderSupplierIntegratedSaveState
        {
            schemaId = schemaId,
            schemaVersion = schemaVersion,
            market = market != null ? market.DeepClone() : null,
            commercial = commercial != null ? commercial.DeepClone() : null,
            orders = orders != null ? orders.DeepClone() : null,
            logistics = logistics != null ? logistics.DeepClone() : null,
            deliveryPresentation = deliveryPresentation != null
                ? deliveryPresentation.DeepClone()
                : null,
            progression = progression != null ? progression.DeepClone() : null
        };
    }

    /// <summary>
    /// Valida estructura, schemas y enlaces de sesión sin tocar ninguna autoridad.
    /// Los source seeds con valor 0 se interpretan como estado todavía no vinculado
    /// y son válidos únicamente si no contradicen otra seed no nula.
    /// </summary>
    public bool TryValidateBasic(out string error)
    {
        error = string.Empty;

        if (!string.Equals(schemaId, CurrentSchemaId, StringComparison.Ordinal) ||
            schemaVersion != CurrentSchemaVersion)
        {
            error = "supplier.integrated.runtime usa un schema incompatible.";
            return false;
        }

        if (market == null || commercial == null || orders == null ||
            logistics == null || deliveryPresentation == null || progression == null)
        {
            error = "supplier.integrated.runtime contiene una o más secciones internas nulas.";
            return false;
        }

        if (!ValidateSchema(
                market.schemaId,
                market.schemaVersion,
                BistroBuilderSupplierMarketSnapshot.CurrentSchemaId,
                BistroBuilderSupplierMarketSnapshot.CurrentSchemaVersion,
                "2.3C",
                out error) ||
            !ValidateSchema(
                commercial.schemaId,
                commercial.schemaVersion,
                BistroBuilderSupplierCommercialIntelligenceSnapshot.CurrentSchemaId,
                BistroBuilderSupplierCommercialIntelligenceSnapshot.CurrentSchemaVersion,
                "2.3D",
                out error) ||
            !ValidateSchema(
                orders.schemaId,
                orders.schemaVersion,
                BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaId,
                BistroBuilderSupplierPurchaseOrdersSnapshot.CurrentSchemaVersion,
                "2.3E",
                out error) ||
            !ValidateSchema(
                logistics.schemaId,
                logistics.schemaVersion,
                BistroBuilderSupplierLogisticsSnapshot.CurrentSchemaId,
                BistroBuilderSupplierLogisticsSnapshot.CurrentSchemaVersion,
                "2.3G",
                out error) ||
            !ValidateSchema(
                deliveryPresentation.schemaId,
                deliveryPresentation.schemaVersion,
                BistroBuilderSupplierDeliveryPresentationSnapshot.CurrentSchemaId,
                BistroBuilderSupplierDeliveryPresentationSnapshot.CurrentSchemaVersion,
                "2.3H",
                out error) ||
            !ValidateSchema(
                progression.schemaId,
                progression.schemaVersion,
                BistroBuilderSupplierProgressionSnapshot.CurrentSchemaId,
                BistroBuilderSupplierProgressionSnapshot.CurrentSchemaVersion,
                "2.3I",
                out error))
        {
            return false;
        }

        int day = market.currentGameDay;
        if (day < 1 || commercial.currentGameDay != day || orders.currentGameDay != day ||
            logistics.currentGameDay != day || deliveryPresentation.currentGameDay != day ||
            progression.currentGameDay != day)
        {
            error = "Las subsecciones de Proveedores no pertenecen al mismo día de juego.";
            return false;
        }

        if (market.marketSeed == 0UL)
        {
            error = "El snapshot integrado no contiene una MarketSeed válida de 2.3C.";
            return false;
        }

        if (commercial.sourceMarketSeed != market.marketSeed)
        {
            error = "2.3D no está vinculado a la MarketSeed del snapshot 2.3C.";
            return false;
        }

        if (commercial.commercialSeed == 0UL)
        {
            error = "El snapshot integrado no contiene una CommercialSeed válida de 2.3D.";
            return false;
        }

        if (!MatchesOrUnbound(orders.sourceMarketSeed, market.marketSeed) ||
            !MatchesOrUnbound(orders.sourceCommercialSeed, commercial.commercialSeed))
        {
            error = "2.3E pertenece a una sesión diferente de 2.3C/2.3D.";
            return false;
        }

        if (!MatchesOrUnbound(logistics.sourceMarketSeed, market.marketSeed) ||
            !MatchesOrUnbound(logistics.sourceCommercialSeed, commercial.commercialSeed))
        {
            error = "2.3G pertenece a una sesión diferente de 2.3C/2.3D.";
            return false;
        }

        if (logistics.logisticsSeed == 0UL)
        {
            error = "El snapshot integrado no contiene una LogisticsSeed válida de 2.3G.";
            return false;
        }

        if (deliveryPresentation.sourceLogisticsSeed != 0UL &&
            deliveryPresentation.sourceLogisticsSeed != logistics.logisticsSeed)
        {
            error = "2.3H pertenece a una sesión logística diferente de 2.3G.";
            return false;
        }

        if (!MatchesOrUnbound(progression.sourceMarketSeed, market.marketSeed) ||
            !MatchesOrUnbound(progression.sourceCommercialSeed, commercial.commercialSeed))
        {
            error = "2.3I pertenece a una sesión diferente de 2.3C/2.3D.";
            return false;
        }

        if (market.offerStates == null || commercial.activePromotions == null ||
            commercial.promotionHistory == null || orders.orders == null ||
            logistics.plans == null || deliveryPresentation.presentations == null ||
            progression.suppliers == null || progression.countedQualifiedPurchaseOrderIds == null)
        {
            error = "Una colección obligatoria del snapshot integrado es nula.";
            return false;
        }

        return true;
    }

    public string BuildFingerprint()
    {
        // JsonUtility mantiene orden de campos/listas, por lo que sobre un snapshot
        // canónico sirve como fingerprint diagnóstico estable dentro de la sesión.
        string json = JsonUtility.ToJson(this, false);
        unchecked
        {
            ulong hash = 1469598103934665603UL;
            for (int index = 0; index < json.Length; index++)
            {
                hash ^= json[index];
                hash *= 1099511628211UL;
            }
            return hash.ToString("X16");
        }
    }

    private static bool ValidateSchema(
        string actualId,
        int actualVersion,
        string expectedId,
        int expectedVersion,
        string label,
        out string error)
    {
        error = string.Empty;
        if (string.Equals(actualId, expectedId, StringComparison.Ordinal) &&
            actualVersion == expectedVersion)
        {
            return true;
        }
        error = label + " usa schema incompatible dentro de supplier.integrated.runtime.";
        return false;
    }

    private static bool MatchesOrUnbound(ulong candidate, ulong expected)
    {
        return candidate == 0UL || candidate == expected;
    }
}
