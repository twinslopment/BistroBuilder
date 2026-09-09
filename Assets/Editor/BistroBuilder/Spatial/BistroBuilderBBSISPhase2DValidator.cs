using System;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2DValidator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2D/Validar")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2D - VALIDACION");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderMobilitySpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMobilitySpatialCoordinator>();
        BistroBuilderSupplierDeliveryPresentationService presentations =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierDeliveryPresentationService>();
        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderSpatialContractDefinition>(
                "Assets/Resources/BistroBuilder/Spatial/Contracts/" +
                "BB_SpatialContract_Logistics_Cart.asset");
        BistroBuilderMobilitySpatialProfileDefinition profile =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderMobilitySpatialProfileDefinition>(
                "Assets/Resources/BistroBuilder/Spatial/Profiles/" +
                "BB_MobilityProfile_Logistics_Cart.asset");
        Check(spatial != null,
            "Autoridad BBSIS disponible", report);
        Check(coordinator != null &&
              coordinator.ValidateConfiguration(out _),
            "Coordinador de movilidad configurado", report);
        Check(typeof(BistroBuilderSupplierDeliveryPresentationService)
                .GetProperty("ActiveController") != null &&
              typeof(BistroBuilderSupplierDeliveryPresentationService)
                .GetProperty("Instance") != null,
            "Contrato runtime de logistica disponible", report);
        Check(contract != null &&
              contract.ValidateDefinition(out _),
            "Spatial Contract de carrito valido", report);
        Check(contract != null &&
              contract.HasTrait("mobility.cart") &&
              contract.HasTrait("carry.envelope"),
            "Traits Mobility y Carry declarados", report);
        Check(profile != null &&
              profile.ValidateDefinition(out _),
            "Perfil espacial data-driven valido", report);
        Check(spatial != null &&
              spatial.FamilyCatalog != null &&
              spatial.FamilyCatalog.ContainsFamily("logistics.cart"),
            "Familia logistics.cart catalogada", report);
        Check(Enum.IsDefined(
                typeof(BistroBuilderSpatialClaimKind),
                BistroBuilderSpatialClaimKind.Mobility) &&
              Enum.IsDefined(
                typeof(BistroBuilderSpatialClaimKind),
                BistroBuilderSpatialClaimKind.Carry),
            "Claims Mobility y Carry disponibles", report);
        Check(Enum.IsDefined(
                typeof(BistroBuilderSpatialSemanticRole),
                BistroBuilderSpatialSemanticRole.MobilityEnvelope) &&
              Enum.IsDefined(
                typeof(BistroBuilderSpatialSemanticRole),
                BistroBuilderSpatialSemanticRole.CarryEnvelope),
            "Semantica Mobility/Carry disponible", report);
        Check(!RequiresRigidbody(
                typeof(BistroBuilderMobileSpatialAdapter)) &&
              !RequiresRigidbody(
                typeof(BistroBuilderMobilitySpatialCoordinator)),
            "BBSIS 2D no exige Rigidbody", report);
        Check(profile != null &&
              profile.BodyWidth > 0f &&
              profile.BodyDepth > 0f &&
              profile.MaximumLoadUnits > 0,
            "Dimensiones independientes del render", report);
        Check(contract != null &&
              contract.PreferredProxyMode ==
                BistroBuilderAdaptiveSpatialProxyMode.Layered,
            "Proxy Layered para cuerpo/movilidad/carga", report);
        Check(coordinator == null ||
              coordinator.BoundObjectCount <= 1,
            "Binding logistico unico e idempotente", report);
        Check(typeof(BistroBuilderMobileSpatialAdapter)
                .GetMethod("TickSpatial") != null,
            "Actualizacion espacial explicita sin rutas paralelas", report);
        Check(typeof(BistroBuilderMobilitySpatialCoordinator)
                .GetMethod("ReconcileNow") != null,
            "Reconstruccion post-Load disponible", report);

        Finish(report);
    }

    private static bool RequiresRigidbody(Type type)
    {
        object[] values = type.GetCustomAttributes(
            typeof(RequireComponent),
            true);
        for (int i = 0; i < values.Length; i++)
        {
            RequireComponent requirement = values[i] as RequireComponent;
            if (requirement != null &&
                (requirement.m_Type0 == typeof(Rigidbody) ||
                 requirement.m_Type1 == typeof(Rigidbody) ||
                 requirement.m_Type2 == typeof(Rigidbody)))
                return true;
        }
        return false;
    }
    private static void Check(
        bool condition,
        string label,
        StringBuilder report)
    {
        if (condition)
        {
            LastPassed++;
            report.AppendLine("OK - " + label);
        }
        else
        {
            LastFailed++;
            report.AppendLine("FAIL - " + label);
        }
    }

    private static void Finish(StringBuilder report)
    {
        report.AppendLine(
            "Resultado: " + LastPassed + " OK / " +
            LastFailed + " errores.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Validacion BBSIS Fase 2D fallida.");
    }
}
