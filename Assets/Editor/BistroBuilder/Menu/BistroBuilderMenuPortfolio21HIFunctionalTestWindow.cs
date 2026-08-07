using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Prueba funcional runtime de 2.1H/I. Crea una segunda carta, valida su
/// independencia, resolución por evento/promoción, prioridad, anulación
/// manual, persistencia v5 y restauración completa del estado original.
/// </summary>
public sealed class BistroBuilderMenuPortfolio21HIFunctionalTestWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/2.1H-I Multiple Menus and Rules Functional Test";

    private string report = "Entra en Play Mode y ejecuta la prueba funcional 2.1H/I.";
    private Vector2 scroll;

    [MenuItem(MenuPath, false, 201)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderMenuPortfolio21HIFunctionalTestWindow>("BB 2.1H-I Test");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BistroBuilder 2.1H/I — Prueba funcional", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Cierra el editor jugable antes de ejecutar. La prueba modifica solo " +
            "estado runtime, valida menu.state v5 y restaura todo al terminar.",
            MessageType.Info
        );
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        if (GUILayout.Button("Ejecutar prueba funcional 2.1H/I", GUILayout.Height(42f)))
        {
            RunFunctionalTest();
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunFunctionalTest()
    {
        StringBuilder builder = new StringBuilder(12288);
        int passed = 0;
        int failed = 0;
        BistroBuilderMenuSaveSectionProvider provider = null;
        BistroBuilderMenuSaveData original = null;

        void Expect(bool condition, string message)
        {
            if (condition)
            {
                passed++;
                builder.AppendLine("- OK: " + message);
            }
            else
            {
                failed++;
                builder.AppendLine("- FALLO: " + message);
            }
        }

        try
        {
            if (!Application.isPlaying)
            {
                throw new InvalidOperationException("Entra en Play Mode antes de ejecutar la prueba.");
            }

            if (!TryResolve(out provider, out string error))
            {
                throw new InvalidOperationException(error);
            }

            BistroBuilderMenuPortfolioService portfolio = provider.PortfolioService;
            BistroBuilderMenuActivationContextService context = provider.ContextService;
            BistroBuilderRestaurantMenuService menuService = provider.MenuService;
            BistroBuilderMenuOfferService offerService = context.OfferService;

            List<BistroBuilderMenuPortfolioRuntimeView> views =
                BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                    BistroBuilderMenuPortfolioRuntimeView
                >(SceneManager.GetActiveScene());
            BistroBuilderMenuPortfolioRuntimeView portfolioView =
                views.Count == 1 ? views[0] : null;

            if (portfolio.EditSessionService != null && portfolio.EditSessionService.HasOpenSession)
            {
                throw new InvalidOperationException(
                    "Cierra o descarta el editor de carta antes de ejecutar la prueba."
                );
            }

            string visualError = string.Empty;
            bool visualValid = portfolioView != null &&
                portfolioView.TryValidateVisibleContent(out visualError);
            Expect(
                visualValid,
                string.IsNullOrWhiteSpace(visualError)
                    ? "La vista jugable muestra cartas y reglas con scrolls visibles."
                    : visualError
            );

            original = Capture(provider);
            Expect(
                original != null && original.schemaVersion == 5 &&
                original.portfolios.Count > 0,
                "Se captura el estado original como menu.state v5."
            );

            BistroBuilderRestaurantMenuPortfolioRuntimeState before = null;
            BistroBuilderNamedMenuRuntimeState originalMenu = null;
            bool initialResolved = portfolio.TryGetActivePortfolioSnapshot(
                out before,
                out error
            );
            if (initialResolved)
            {
                initialResolved = before.TryGetMenu(
                    before.ActiveMenuId,
                    out originalMenu
                );
            }
            Expect(
                initialResolved,
                string.IsNullOrWhiteSpace(error)
                    ? "Existe una carta efectiva inicial."
                    : error
            );

            if (before == null || originalMenu == null || originalMenu.ItemCount == 0)
            {
                throw new InvalidOperationException("La carta inicial necesita al menos un plato.");
            }

            BistroBuilderMenuItemRuntimeState referenceItem = originalMenu.Items[0];
            string dishId = referenceItem.DishId;
            int originalPrice = referenceItem.CurrentPriceCents;
            int changedPrice = originalPrice + 137 <= BistroBuilderDishDefinition.MaximumPriceCents
                ? originalPrice + 137
                : Math.Max(0, originalPrice - 137);

            BistroBuilderMenuOfferItemSnapshot frozenOffer = default(BistroBuilderMenuOfferItemSnapshot);
            bool offerCaptured = offerService.TryEvaluateDish(
                dishId,
                offerService.CurrentMealService,
                BistroBuilderServiceMode.TableService,
                out frozenOffer,
                out _
            );

            string suffix = Guid.NewGuid().ToString("N").Substring(0, 10);
            string alternateName = "Carta funcional " + suffix;
            Expect(
                portfolio.TryDuplicateMenu(
                    before.ActiveMenuId,
                    alternateName,
                    true,
                    out string alternateMenuId,
                    out error
                ),
                string.IsNullOrWhiteSpace(error)
                    ? "Se duplica la carta activa como perfil independiente."
                    : error
            );

            List<BistroBuilderMenuItemRuntimeState> operational =
                new List<BistroBuilderMenuItemRuntimeState>();
            if (!menuService.TryGetSnapshot(operational, out error))
            {
                throw new InvalidOperationException(error);
            }
            ReplacePrice(operational, dishId, changedPrice);
            if (!menuService.TryReplaceAll(operational, true, out error))
            {
                throw new InvalidOperationException(error);
            }

            Expect(
                portfolio.TryGetActivePortfolioSnapshot(out BistroBuilderRestaurantMenuPortfolioRuntimeState afterEdit, out error) &&
                afterEdit.TryGetMenu(alternateMenuId, out BistroBuilderNamedMenuRuntimeState alternateMenu) &&
                TryGetPrice(alternateMenu, dishId, out int alternatePrice) &&
                alternatePrice == changedPrice,
                "Editar la carta operativa actualiza únicamente el perfil activo."
            );

            Expect(
                afterEdit.TryGetMenu(before.FallbackMenuId, out BistroBuilderNamedMenuRuntimeState baseMenu) &&
                TryGetPrice(baseMenu, dishId, out int basePrice) &&
                basePrice == originalPrice,
                "La carta base conserva su precio independiente."
            );

            Expect(
                portfolio.TryClearManualOverride(out error) &&
                portfolio.ActiveMenuId == before.FallbackMenuId &&
                menuService.TryGetItemSnapshot(dishId, out BistroBuilderMenuItemRuntimeState baseOperational) &&
                baseOperational.CurrentPriceCents == originalPrice,
                "Al volver a reglas automáticas se proyecta la carta base."
            );

            string eventId = "event_hi_" + suffix;
            string eventRuleId = "rule_event_hi_" + suffix;
            BistroBuilderMenuActivationRuleRuntimeState eventRule =
                new BistroBuilderMenuActivationRuleRuntimeState(
                    eventRuleId,
                    "Evento funcional",
                    true,
                    alternateMenuId,
                    50,
                    BistroBuilderMenuActivationRuleType.Event,
                    0,
                    0,
                    0,
                    BistroBuilderMealServiceAvailability.None,
                    BistroBuilderMenuActivationRuleRuntimeState.AnyMinute,
                    BistroBuilderMenuActivationRuleRuntimeState.AnyMinute,
                    eventId,
                    string.Empty
                );
            Expect(
                portfolio.TryUpsertRule(eventRule, out error),
                string.IsNullOrWhiteSpace(error)
                    ? "Se registra una regla de evento para la carta alternativa."
                    : error
            );

            Expect(
                context.TrySetEventActive(eventId, true, out error) &&
                portfolio.ActiveMenuId == alternateMenuId &&
                menuService.TryGetItemSnapshot(dishId, out BistroBuilderMenuItemRuntimeState eventOperational) &&
                eventOperational.CurrentPriceCents == changedPrice,
                "Activar el evento selecciona y proyecta la carta alternativa."
            );

            string promotionId = "promo_hi_" + suffix;
            string promotionRuleId = "rule_promo_hi_" + suffix;
            BistroBuilderMenuActivationRuleRuntimeState promotionRule =
                new BistroBuilderMenuActivationRuleRuntimeState(
                    promotionRuleId,
                    "Promoción prioritaria",
                    true,
                    before.FallbackMenuId,
                    100,
                    BistroBuilderMenuActivationRuleType.Promotion,
                    0,
                    0,
                    0,
                    BistroBuilderMealServiceAvailability.None,
                    BistroBuilderMenuActivationRuleRuntimeState.AnyMinute,
                    BistroBuilderMenuActivationRuleRuntimeState.AnyMinute,
                    string.Empty,
                    promotionId
                );
            Expect(
                portfolio.TryUpsertRule(promotionRule, out error) &&
                context.TrySetPromotionActive(promotionId, true, out error) &&
                portfolio.ActiveMenuId == before.FallbackMenuId,
                "La promoción de mayor prioridad vence a la regla de evento."
            );

            Expect(
                context.TrySetPromotionActive(promotionId, false, out error) &&
                portfolio.ActiveMenuId == alternateMenuId,
                "Al terminar la promoción vuelve a ganar la regla de evento."
            );

            Expect(
                portfolio.TrySetManualOverride(before.FallbackMenuId, out error) &&
                portfolio.ActiveMenuId == before.FallbackMenuId,
                "La anulación manual prevalece sobre las reglas activas."
            );

            Expect(
                portfolio.TryClearManualOverride(out error) &&
                portfolio.ActiveMenuId == alternateMenuId,
                "Al retirar la anulación manual se recupera la resolución automática."
            );

            Expect(
                !portfolio.TryDeleteMenu(alternateMenuId, out _) &&
                portfolio.TryGetActivePortfolioSnapshot(out BistroBuilderRestaurantMenuPortfolioRuntimeState protectedState, out _) &&
                protectedState.TryGetMenu(alternateMenuId, out _),
                "Una carta activa o referenciada por reglas no puede eliminarse."
            );

            Expect(
                !portfolio.TryUpsertRule(
                    new BistroBuilderMenuActivationRuleRuntimeState(
                        "rule_orphan_" + suffix,
                        "Huérfana",
                        true,
                        "menu_missing_hi",
                        0,
                        BistroBuilderMenuActivationRuleType.Schedule,
                        0,
                        0,
                        0,
                        BistroBuilderMealServiceAvailability.None,
                        -1,
                        -1,
                        string.Empty,
                        string.Empty
                    ),
                    out _
                ),
                "Una regla no puede apuntar a una carta inexistente."
            );

            BistroBuilderMenuSaveData captured = Capture(provider);
            BistroBuilderRestaurantMenuPortfolioSaveData savedPortfolio =
                FindPortfolio(captured, captured.activeRestaurantId);
            Expect(
                captured.schemaVersion == 5 && savedPortfolio != null &&
                FindMenu(savedPortfolio, alternateMenuId) != null &&
                FindRule(savedPortfolio, eventRuleId) != null &&
                captured.activeEventIds.Contains(eventId),
                "menu.state v5 captura cartas, reglas y señales activas."
            );

            BistroBuilderMenuSaveData roundTrip = JsonUtility.FromJson<BistroBuilderMenuSaveData>(
                JsonUtility.ToJson(captured, false)
            );
            BistroBuilderRestaurantMenuPortfolioSaveData roundTripPortfolio =
                FindPortfolio(roundTrip, roundTrip.activeRestaurantId);
            Expect(
                roundTripPortfolio != null &&
                FindMenu(roundTripPortfolio, alternateMenuId) != null &&
                FindRule(roundTripPortfolio, eventRuleId) != null,
                "El round-trip JSON conserva identidades y vínculos de reglas."
            );

            context.TrySetEventActive(eventId, false, out _);
            portfolio.TryDeleteRule(eventRuleId, out _);
            Apply(provider, captured);
            Expect(
                portfolio.TryGetActivePortfolioSnapshot(out BistroBuilderRestaurantMenuPortfolioRuntimeState reloaded, out error) &&
                reloaded.TryGetMenu(alternateMenuId, out BistroBuilderNamedMenuRuntimeState reloadedMenu) &&
                reloaded.TryGetRule(eventRuleId, out _) &&
                context.IsEventActive(eventId) &&
                TryGetPrice(reloadedMenu, dishId, out int reloadedPrice) &&
                reloadedPrice == changedPrice,
                "La carga reconstruye cartas, reglas, señales y contenido independiente."
            );

            Expect(
                !offerCaptured ||
                (frozenOffer.DishId == dishId &&
                 frozenOffer.PriceCents == originalPrice),
                "La instantánea de oferta previa conserva su precio tras cambiar de carta."
            );

            Expect(
                provider.SectionVersion == 5 &&
                ReferenceEquals(provider.PortfolioService, portfolio) &&
                ReferenceEquals(provider.ContextService, context),
                "La persistencia permanece integrada en una única sección menu.state v5."
            );
        }
        catch (Exception exception)
        {
            failed++;
            builder.AppendLine("- FALLO: Excepción: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            if (provider != null && original != null)
            {
                try
                {
                    Apply(provider, original);
                    passed++;
                    builder.AppendLine("- OK: El estado original de cartas, reglas y señales se restaura.");
                }
                catch (Exception restoreException)
                {
                    failed++;
                    builder.AppendLine("- FALLO: No se pudo restaurar el estado original: " + restoreException.Message);
                    Debug.LogException(restoreException);
                }
            }
        }

        report = (failed == 0
            ? "PRUEBA FUNCIONAL 2.1H/I SUPERADA"
            : "PRUEBA FUNCIONAL 2.1H/I FALLIDA") +
            "\nCorrectos: " + passed +
            "\nFallos: " + failed +
            "\n" + builder.ToString().TrimEnd();

        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);
    }

    private static bool TryResolve(
        out BistroBuilderMenuSaveSectionProvider provider,
        out string error
    )
    {
        List<BistroBuilderMenuSaveSectionProvider> providers =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<BistroBuilderMenuSaveSectionProvider>(
                SceneManager.GetActiveScene()
            );
        provider = providers.Count == 1 ? providers[0] : null;
        if (provider == null)
        {
            error = "Debe existir un único proveedor menu.state.";
            return false;
        }
        if (provider.PortfolioService == null || provider.ContextService == null)
        {
            error = "Faltan los servicios 2.1H/I.";
            return false;
        }
        return provider.ValidateConfiguration(out error);
    }

    private static void ReplacePrice(
        List<BistroBuilderMenuItemRuntimeState> items,
        string dishId,
        int price
    )
    {
        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = items[index];
            if (!string.Equals(item.DishId, dishId, StringComparison.Ordinal)) continue;
            items[index] = new BistroBuilderMenuItemRuntimeState(
                item.DishId,
                price,
                item.Unlocked,
                item.Enabled,
                item.ManuallySoldOut,
                item.SignatureDish,
                item.AvailableServices,
                item.DisplayOrder,
                item.PreparationDifficulty,
                item.BasePreparationSeconds
            );
            return;
        }
        throw new InvalidOperationException("No se encontró el plato de referencia.");
    }

    private static bool TryGetPrice(
        BistroBuilderNamedMenuRuntimeState menu,
        string dishId,
        out int price
    )
    {
        price = 0;
        if (menu == null) return false;
        for (int index = 0; index < menu.Items.Count; index++)
        {
            if (string.Equals(menu.Items[index].DishId, dishId, StringComparison.Ordinal))
            {
                price = menu.Items[index].CurrentPriceCents;
                return true;
            }
        }
        return false;
    }

    private static BistroBuilderRestaurantMenuPortfolioSaveData FindPortfolio(
        BistroBuilderMenuSaveData data,
        string restaurantId
    )
    {
        if (data == null || data.portfolios == null) return null;
        for (int index = 0; index < data.portfolios.Count; index++)
        {
            if (data.portfolios[index] != null &&
                string.Equals(data.portfolios[index].restaurantId, restaurantId, StringComparison.Ordinal))
                return data.portfolios[index];
        }
        return null;
    }

    private static BistroBuilderNamedMenuSaveData FindMenu(
        BistroBuilderRestaurantMenuPortfolioSaveData portfolio,
        string menuId
    )
    {
        if (portfolio == null || portfolio.menus == null) return null;
        for (int index = 0; index < portfolio.menus.Count; index++)
        {
            if (portfolio.menus[index] != null &&
                string.Equals(portfolio.menus[index].menuId, menuId, StringComparison.Ordinal))
                return portfolio.menus[index];
        }
        return null;
    }

    private static BistroBuilderMenuActivationRuleSaveData FindRule(
        BistroBuilderRestaurantMenuPortfolioSaveData portfolio,
        string ruleId
    )
    {
        if (portfolio == null || portfolio.rules == null) return null;
        for (int index = 0; index < portfolio.rules.Count; index++)
        {
            if (portfolio.rules[index] != null &&
                string.Equals(portfolio.rules[index].ruleId, ruleId, StringComparison.Ordinal))
                return portfolio.rules[index];
        }
        return null;
    }

    private static BistroBuilderMenuSaveData Capture(BistroBuilderMenuSaveSectionProvider provider)
    {
        BistroBuilderSaveCaptureContext context = new BistroBuilderSaveCaptureContext(210109);
        RunEnumerator(provider.CaptureState(context));
        if (context.HasFailed || !(context.State is BistroBuilderMenuSaveData data))
        {
            throw new InvalidOperationException(
                context.HasFailed ? context.ErrorMessage : "menu.state no devolvió el DTO esperado."
            );
        }
        return Clone(data);
    }

    private static void Apply(
        BistroBuilderMenuSaveSectionProvider provider,
        BistroBuilderMenuSaveData state
    )
    {
        BistroBuilderSaveLoadContext context = new BistroBuilderSaveLoadContext(210109, false, 64);
        RunEnumerator(provider.PrepareForLoad(context));
        if (!context.HasFailed) RunEnumerator(provider.ApplyState(Clone(state), context));
        provider.FinalizeLoad(context);
        if (context.HasFailed) throw new InvalidOperationException(context.ErrorMessage);
    }

    private static BistroBuilderMenuSaveData Clone(BistroBuilderMenuSaveData source)
    {
        return JsonUtility.FromJson<BistroBuilderMenuSaveData>(JsonUtility.ToJson(source, false));
    }

    private static void RunEnumerator(IEnumerator enumerator)
    {
        while (enumerator != null && enumerator.MoveNext()) { }
    }
}
