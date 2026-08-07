using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMenuPortfolio21HIValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;
    public void AddCorrect(string value) => correct.Add(value);
    public void AddWarning(string value) => warnings.Add(value);
    public void AddError(string value) => errors.Add(value);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(8192);
        builder.AppendLine("BISTRO BUILDER - 2.1H/I CARTAS Y REGLAS");
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        Append(builder, "OK", correct);
        Append(builder, "ADVERTENCIA", warnings);
        Append(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void Append(StringBuilder builder, string prefix, List<string> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(values[index]);
        }
    }
}

/// <summary>
/// Validador no destructivo de 2.1H/I. Comprueba unicidad, referencias,
/// migración v4->v5, portfolios, reglas y proyección sobre la carta histórica.
/// </summary>
public static class BistroBuilderMenuPortfolio21HIValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1H-I Multiple Menus and Rules";

    [MenuItem(MenuPath, false, 199)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuPortfolio21HIValidationResult result = ValidateCurrentProject();
        string report = result.BuildReport();
        if (result.ErrorCount > 0) Debug.LogError(report);
        else if (result.WarningCount > 0) Debug.LogWarning(report);
        else Debug.Log(report);
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderMenuPortfolio21HIValidationResult ValidateCurrentProject()
    {
        BistroBuilderMenuPortfolio21HIValidationResult result =
            new BistroBuilderMenuPortfolio21HIValidationResult();
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }
        result.AddCorrect("La escena activa está cargada y guardada.");

        List<BistroBuilderMenuActivationContextService> contexts = Find<BistroBuilderMenuActivationContextService>(scene);
        List<BistroBuilderMenuPortfolioService> portfolios = Find<BistroBuilderMenuPortfolioService>(scene);
        List<BistroBuilderMenuPortfolioRuntimeView> views = Find<BistroBuilderMenuPortfolioRuntimeView>(scene);
        List<BistroBuilderMenuStateV4ToV5Migration> migrations45 = Find<BistroBuilderMenuStateV4ToV5Migration>(scene);
        List<BistroBuilderMenuSaveSectionProvider> providers = Find<BistroBuilderMenuSaveSectionProvider>(scene);
        List<BistroBuilderSaveGameService> saveServices = Find<BistroBuilderSaveGameService>(scene);
        List<BistroBuilderActiveServiceSaveSectionProvider> activeProviders = Find<BistroBuilderActiveServiceSaveSectionProvider>(scene);
        List<BistroBuilderMenuEditorRuntimeView> editorViews = Find<BistroBuilderMenuEditorRuntimeView>(scene);

        ValidateUnique(contexts.Count, "contexto de reglas", result);
        ValidateUnique(portfolios.Count, "autoridad de portfolios", result);
        ValidateUnique(views.Count, "vista runtime de cartas y reglas", result);
        ValidateUnique(migrations45.Count, "migración menu.state v4 a v5", result);
        ValidateUnique(providers.Count, "proveedor menu.state", result);
        ValidateUnique(saveServices.Count, "servicio universal de guardado", result);
        ValidateUnique(editorViews.Count, "vista principal de carta", result);

        if (contexts.Count != 1 || portfolios.Count != 1 || views.Count != 1 ||
            migrations45.Count != 1 || providers.Count != 1 || saveServices.Count != 1 ||
            editorViews.Count != 1)
        {
            return result;
        }

        BistroBuilderMenuActivationContextService context = contexts[0];
        BistroBuilderMenuPortfolioService portfolio = portfolios[0];
        BistroBuilderMenuPortfolioRuntimeView view = views[0];
        BistroBuilderMenuStateV4ToV5Migration migration45 = migrations45[0];
        BistroBuilderMenuSaveSectionProvider provider = providers[0];
        BistroBuilderSaveGameService saveService = saveServices[0];

        Check(
            BistroBuilderMenuSaveData.CurrentSchemaVersion == 5 &&
            BistroBuilderMenuSaveSectionProvider.StableSectionVersion == 5 &&
            provider.SectionVersion == 5 &&
            provider.StateType == typeof(BistroBuilderMenuSaveData),
            "menu.state publica el contrato v5 de forma coherente.",
            "El contrato publicado de menu.state v5 es incoherente.",
            result
        );

        Check(
            ReferenceEquals(provider.PortfolioService, portfolio) &&
            ReferenceEquals(provider.ContextService, context),
            "menu.state comparte las autoridades de portfolios y contexto.",
            "menu.state no está enlazado a las autoridades 2.1H/I.",
            result
        );

        Check(
            ReferenceEquals(portfolio.ContextService, context) &&
            ReferenceEquals(portfolio.CollectionService, provider.CollectionService) &&
            ReferenceEquals(portfolio.MenuService, provider.MenuService) &&
            ReferenceEquals(portfolio.CatalogService, provider.CatalogService),
            "El portfolio comparte carta, colección, catálogo y contexto canónicos.",
            "El portfolio usa dependencias incoherentes.",
            result
        );

        Check(
            ReferenceEquals(context.OfferService, FindOne<BistroBuilderMenuOfferService>(scene)) &&
            context.GameClock != null && context.GeneralGameStateService != null,
            "El contexto combina reloj, calendario, servicio y señales activas.",
            "El contexto no está enlazado al reloj, calendario u oferta.",
            result
        );

        Check(
            ReferenceEquals(view.PortfolioService, portfolio) &&
            ReferenceEquals(view.MenuEditorView, editorViews[0]) &&
            ReferenceEquals(view.gameObject, editorViews[0].gameObject),
            "La vista 2.1H/I está integrada junto al editor jugable.",
            "La vista 2.1H/I no comparte la raíz del editor de carta.",
            result
        );

        CheckValidation(context.ValidateConfiguration, "El contexto de reglas está operativo.", result);
        CheckValidation(portfolio.ValidateConfiguration, "La autoridad de portfolios está operativa.", result);
        CheckValidation(view.ValidateConfiguration, "La vista runtime 2.1H/I está configurada.", result);
        CheckValidation(provider.ValidateConfiguration, "El proveedor menu.state v5 está operativo.", result);

        Check(
            migration45.SectionId == BistroBuilderMenuSaveSectionProvider.StableSectionId &&
            migration45.FromVersion == 4 && migration45.ToVersion == 5 &&
            migration45.FromSerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId &&
            migration45.ToSerializerId == BistroBuilderJsonSaveSerializer.StableSerializerId,
            "La migración v4 a v5 es consecutiva y usa el serializador canónico.",
            "La migración v4 a v5 no cumple el contrato consecutivo.",
            result
        );

        ValidateMigrationChain(scene, result);

        saveService.RefreshExtensions();
        CheckValidation(saveService.ValidateConfiguration, "El guardado universal registra menu.state v5 y su migración.", result);

        if (portfolio.TryGetAllPortfolioSnapshots(
                new List<BistroBuilderRestaurantMenuPortfolioRuntimeState>(),
                out string snapshotsError))
        {
            result.AddCorrect("Los portfolios pueden capturarse sin modificar el estado.");
        }
        else result.AddError(snapshotsError);

        if (portfolio.TryGetActivePortfolioSnapshot(out BistroBuilderRestaurantMenuPortfolioRuntimeState active, out string activeError))
        {
            result.AddCorrect("Existe un portfolio para el restaurante activo.");
            Check(active.MenuCount >= 1, "El portfolio conserva al menos una carta.", "El portfolio activo no contiene cartas.", result);
            Check(active.TryGetMenu(active.FallbackMenuId, out _), "La carta base pertenece al portfolio.", "La carta base no existe.", result);
            Check(active.TryGetMenu(active.ActiveMenuId, out _), "La carta efectiva pertenece al portfolio.", "La carta efectiva no existe.", result);
            Check(string.IsNullOrEmpty(active.ManualOverrideMenuId) || active.TryGetMenu(active.ManualOverrideMenuId, out _), "La anulación manual es vacía o válida.", "La anulación manual es huérfana.", result);
            ValidatePortfolioRules(active, result);
            ValidateOperationalProjection(portfolio, active, result);
        }
        else result.AddError(activeError);

        Check(
            typeof(BistroBuilderMenuOfferItemSnapshot).GetProperty("PriceCents") != null &&
            typeof(BistroBuilderMenuOfferItemSnapshot).GetProperty("OfferRevision") != null &&
            typeof(BistroBuilderMenuOfferItemSnapshot).GetProperty("RestaurantId") != null,
            "La oferta conserva precio, revisión y restaurante en instantáneas inmutables.",
            "La instantánea de oferta no protege las comandas frente a cambios de carta.",
            result
        );

        if (activeProviders.Count == 1)
        {
            Check(
                provider.ApplyOrder < activeProviders[0].ApplyOrder,
                "menu.state restaura la carta efectiva antes de las comandas activas.",
                "Las comandas activas se restauran antes que la carta efectiva.",
                result
            );
        }
        else result.AddWarning("No se pudo comprobar un único proveedor service.runtime.");

        Check(
            string.Equals(BistroBuilderMenuPortfolioService.RuntimeRevision, "MENU-2.1HI", StringComparison.Ordinal) &&
            string.Equals(BistroBuilderMenuActivationContextService.RuntimeRevision, "MENU-2.1HI-CONTEXT", StringComparison.Ordinal) &&
            string.Equals(BistroBuilderMenuPortfolioRuntimeView.RuntimeRevision, "MENU-2.1HI-UI", StringComparison.Ordinal),
            "Las revisiones runtime corresponden a 2.1H/I.",
            "Alguna revisión runtime de 2.1H/I es incoherente.",
            result
        );

        Check(
            Find<BistroBuilderMenuSaveSectionProvider>(scene).Count == 1,
            "No existe una segunda fuente persistente de cartas.",
            "Se detectaron proveedores menu.state duplicados.",
            result
        );

        return result;
    }

    private static void ValidatePortfolioRules(
        BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        BistroBuilderMenuPortfolio21HIValidationResult result
    )
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        bool valid = true;
        string error = string.Empty;
        for (int index = 0; index < portfolio.Rules.Count; index++)
        {
            BistroBuilderMenuActivationRuleRuntimeState rule = portfolio.Rules[index];
            if (rule == null || !rule.TryValidate(out error) ||
                !ids.Add(rule.RuleId) || !portfolio.TryGetMenu(rule.TargetMenuId, out _))
            {
                valid = false;
                break;
            }
        }
        Check(valid, "Las reglas guardadas son válidas, únicas y no están huérfanas.", string.IsNullOrWhiteSpace(error) ? "Hay reglas inválidas o huérfanas." : error, result);
    }

    private static void ValidateOperationalProjection(
        BistroBuilderMenuPortfolioService service,
        BistroBuilderRestaurantMenuPortfolioRuntimeState portfolio,
        BistroBuilderMenuPortfolio21HIValidationResult result
    )
    {
        string error = string.Empty;
        if (!portfolio.TryGetMenu(
                portfolio.ActiveMenuId,
                out BistroBuilderNamedMenuRuntimeState activeMenu
            ))
        {
            result.AddError("No se encontró la carta efectiva del portfolio.");
            return;
        }

        List<BistroBuilderMenuItemRuntimeState> operational =
            new List<BistroBuilderMenuItemRuntimeState>();
        if (!service.MenuService.TryGetSnapshot(operational, out error))
        {
            result.AddError(error);
            return;
        }

        bool equal = operational.Count == activeMenu.ItemCount;
        if (equal)
        {
            Dictionary<string, BistroBuilderMenuItemRuntimeState> byId = new Dictionary<string, BistroBuilderMenuItemRuntimeState>(StringComparer.Ordinal);
            for (int index = 0; index < operational.Count; index++) byId[operational[index].DishId] = operational[index];
            for (int index = 0; index < activeMenu.Items.Count; index++)
            {
                BistroBuilderMenuItemRuntimeState expected = activeMenu.Items[index];
                if (!byId.TryGetValue(expected.DishId, out BistroBuilderMenuItemRuntimeState actual) ||
                    actual.CurrentPriceCents != expected.CurrentPriceCents ||
                    actual.DisplayOrder != expected.DisplayOrder)
                {
                    equal = false;
                    break;
                }
            }
        }
        Check(equal, "La carta operativa coincide con el perfil efectivo.", "La carta operativa no coincide con el perfil efectivo.", result);
    }

    private static void ValidateMigrationChain(Scene scene, BistroBuilderMenuPortfolio21HIValidationResult result)
    {
        BistroBuilderMenuStateV1ToV2Migration m12 = FindOne<BistroBuilderMenuStateV1ToV2Migration>(scene);
        BistroBuilderMenuStateV2ToV3Migration m23 = FindOne<BistroBuilderMenuStateV2ToV3Migration>(scene);
        BistroBuilderMenuStateV3ToV4Migration m34 = FindOne<BistroBuilderMenuStateV3ToV4Migration>(scene);
        BistroBuilderMenuStateV4ToV5Migration m45 = FindOne<BistroBuilderMenuStateV4ToV5Migration>(scene);
        bool valid = m12 != null && m23 != null && m34 != null && m45 != null &&
            m12.FromVersion == 1 && m12.ToVersion == 2 &&
            m23.FromVersion == 2 && m23.ToVersion == 3 &&
            m34.FromVersion == 3 && m34.ToVersion == 4 &&
            m45.FromVersion == 4 && m45.ToVersion == 5;
        Check(valid, "La cadena de migración V1→V2→V3→V4→V5 es consecutiva.", "La cadena completa de migración no es consecutiva.", result);
    }

    private delegate bool ValidationDelegate(out string error);

    private static void CheckValidation(
        ValidationDelegate validation,
        string success,
        BistroBuilderMenuPortfolio21HIValidationResult result
    )
    {
        if (validation(out string error)) result.AddCorrect(success);
        else result.AddError(error);
    }

    private static void ValidateUnique(int count, string label, BistroBuilderMenuPortfolio21HIValidationResult result)
    {
        if (count == 1) result.AddCorrect("Existe un único " + label + ".");
        else result.AddError("Se esperaban 1 " + label + " y hay " + count + ".");
    }

    private static void Check(bool condition, string success, string failure, BistroBuilderMenuPortfolio21HIValidationResult result)
    {
        if (condition) result.AddCorrect(success);
        else result.AddError(failure);
    }

    private static T FindOne<T>(Scene scene) where T : Component
    {
        List<T> values = Find<T>(scene);
        return values.Count == 1 ? values[0] : null;
    }

    private static List<T> Find<T>(Scene scene) where T : Component
    {
        return BistroBuilderMenuEditor21EInstaller.FindSceneComponents<T>(scene);
    }
}
