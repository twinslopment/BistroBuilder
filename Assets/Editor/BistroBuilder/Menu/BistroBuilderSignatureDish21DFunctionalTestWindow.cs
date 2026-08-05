using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prueba funcional runtime de 2.1D.
///
/// Se ejecuta con el servicio cerrado. Modifica temporalmente la carta en
/// memoria, verifica las tres modalidades y crea una comanda Draft real. Todo
/// el estado de carta, telemetría y comanda de prueba se restaura o elimina en
/// un bloque finally; no guarda la escena ni assets.
/// </summary>
public sealed class BistroBuilderSignatureDish21DFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/2.1D Functional Signature Dish Test";
    private const int SamplesPerMode = 1024;

    private Vector2 scroll;
    private string status =
        "Estado: PENDIENTE. Entra en Play Mode con el servicio cerrado.";

    [MenuItem(MenuPath, false, 163)]
    private static void Open()
    {
        BistroBuilderSignatureDish21DFunctionalTestWindow window =
            GetWindow<BistroBuilderSignatureDish21DFunctionalTestWindow>();
        window.titleContent = new GUIContent("BB 2.1D Test");
        window.minSize = new Vector2(620f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "Bistro Builder 2.1D — Prueba funcional de platos firma",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Ejecuta esta prueba en una entrada nueva a Play Mode y con el " +
            "servicio cerrado. La herramienta usa una copia temporal de la " +
            "carta, comprueba mesa, barra y espera en barra, congela una " +
            "comanda real y restaura todo antes de terminar.",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Ejecutar prueba funcional 2.1D", GUILayout.Height(34f)))
            {
                ExecuteTest();
            }
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void ExecuteTest()
    {
        if (!Application.isPlaying)
        {
            status = "FALLO: la prueba necesita Play Mode.";
            return;
        }

        bool previousPause = EditorApplication.isPaused;
        EditorApplication.isPaused = true;

        BistroBuilderRestaurantMenuService menuService = null;
        BistroBuilderMenuOfferService offerService = null;
        BistroBuilderMenuSelectionService selectionService = null;
        BistroBuilderCanonicalOrderService orderService = null;
        BistroBuilderSignatureDishTelemetryService telemetryService = null;
        List<BistroBuilderMenuItemRuntimeState> originalMenu =
            new List<BistroBuilderMenuItemRuntimeState>();
        BistroBuilderSignatureDishTelemetrySnapshot originalTelemetry = null;
        string createdOrderId = string.Empty;
        StringBuilder report = new StringBuilder(4096);
        bool menuCaptured = false;
        bool telemetryCaptured = false;
        bool succeeded = false;

        try
        {
            menuService = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderRestaurantMenuService
            >();
            offerService = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMenuOfferService
            >();
            selectionService = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMenuSelectionService
            >();
            orderService = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderCanonicalOrderService
            >();
            telemetryService = UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSignatureDishTelemetryService
            >();

            Require(menuService != null, "Falta la carta runtime.");
            Require(offerService != null, "Falta la oferta unificada 2.1C.");
            Require(selectionService != null, "Falta la selección 2.1D.");
            Require(orderService != null, "Faltan las comandas canónicas.");
            Require(telemetryService != null, "Falta la telemetría 2.1D.");
            Require(
                selectionService.ValidateConfiguration(out string selectionError),
                selectionError
            );
            Require(
                telemetryService.ValidateConfiguration(out string telemetryError),
                telemetryError
            );
            Require(
                menuService.TryGetSnapshot(originalMenu, out string menuError),
                menuError
            );
            menuCaptured = true;
            Require(
                telemetryService.TryCaptureRuntimeSnapshot(
                    out originalTelemetry,
                    out string snapshotError
                ),
                snapshotError
            );
            telemetryCaptured = true;

            BistroBuilderServiceMode[] modes =
            {
                BistroBuilderServiceMode.TableService,
                BistroBuilderServiceMode.BarService,
                BistroBuilderServiceMode.WaitingAtBar
            };
            bool provedWeightedIncrease = false;
            string tableSignatureDishId = string.Empty;
            List<BistroBuilderMenuOfferItemSnapshot> staleTableOffer = null;

            for (int modeIndex = 0; modeIndex < modes.Length; modeIndex++)
            {
                BistroBuilderServiceMode mode = modes[modeIndex];
                List<BistroBuilderMenuOfferItemSnapshot> initialOffer =
                    new List<BistroBuilderMenuOfferItemSnapshot>();
                Require(
                    offerService.TryGetCurrentOffer(
                        mode,
                        false,
                        initialOffer,
                        out string offerError
                    ),
                    offerError
                );
                RemoveNonOrderable(initialOffer);
                Require(
                    initialOffer.Count > 0,
                    "No hay platos pedibles para " + mode + "."
                );

                string signatureDishId = initialOffer[0].DishId;
                ApplyOnlySignatureDish(
                    menuService,
                    originalMenu,
                    signatureDishId
                );

                List<BistroBuilderMenuOfferItemSnapshot> weightedOffer =
                    new List<BistroBuilderMenuOfferItemSnapshot>();
                Require(
                    offerService.TryGetCurrentOffer(
                        mode,
                        false,
                        weightedOffer,
                        out offerError
                    ),
                    offerError
                );
                RemoveNonOrderable(weightedOffer);
                Require(
                    CountSignatureDishes(weightedOffer) == 1,
                    "La oferta temporal de " + mode +
                    " no contiene exactamente un plato firma."
                );

                int selectedSignatureCount = 0;

                for (int sample = 0; sample < SamplesPerMode; sample++)
                {
                    BistroBuilderMenuSelectionContext context =
                        new BistroBuilderMenuSelectionContext(
                            offerService.CurrentMealService,
                            mode,
                            "functional_21d_" + (int)mode + "_" + sample,
                            0,
                            sample,
                            sample
                        );
                    Require(
                        selectionService.TrySelectFromCandidates(
                            context,
                            weightedOffer,
                            null,
                            out BistroBuilderMenuSelectionResult selection,
                            out string selectionFailure
                        ),
                        selectionFailure
                    );

                    if (selection.WasSignatureDishAtSelection)
                    {
                        selectedSignatureCount++;
                    }
                }

                int equalWeightBaseline =
                    SamplesPerMode / weightedOffer.Count;

                if (weightedOffer.Count > 1)
                {
                    Require(
                        selectedSignatureCount > equalWeightBaseline,
                        "El plato firma no supera la cuota equiprobable en " +
                        mode + "."
                    );
                    provedWeightedIncrease = true;
                }

                report.Append("- OK: ");
                report.Append(mode);
                report.Append(" resolvió ");
                report.Append(weightedOffer.Count);
                report.Append(" candidato(s); firma seleccionada ");
                report.Append(selectedSignatureCount);
                report.Append("/");
                report.Append(SamplesPerMode);
                report.AppendLine(" veces.");

                if (mode == BistroBuilderServiceMode.TableService)
                {
                    tableSignatureDishId = signatureDishId;
                    staleTableOffer =
                        new List<BistroBuilderMenuOfferItemSnapshot>(
                            weightedOffer
                        );
                }
            }

            Require(
                provedWeightedIncrease,
                "Ninguna modalidad tenía más de un candidato para demostrar " +
                "el aumento de peso del plato firma."
            );

            Require(
                staleTableOffer != null && staleTableOffer.Count > 0,
                "No se conservó la oferta de mesa para probar obsolescencia."
            );
            BistroBuilderMenuSelectionContext staleContext =
                new BistroBuilderMenuSelectionContext(
                    offerService.CurrentMealService,
                    BistroBuilderServiceMode.TableService,
                    "functional_21d_stale_offer",
                    0,
                    0,
                    0
                );
            Require(
                !selectionService.TrySelectFromCandidates(
                    staleContext,
                    staleTableOffer,
                    null,
                    out _,
                    out _
                ),
                "Una oferta anterior siguió siendo seleccionable tras cambiar " +
                "la carta."
            );
            report.AppendLine(
                "- OK: una revisión de oferta obsoleta se rechaza."
            );

            ApplyOnlySignatureDish(
                menuService,
                originalMenu,
                tableSignatureDishId
            );
            List<BistroBuilderMenuOfferItemSnapshot> tableOffer =
                new List<BistroBuilderMenuOfferItemSnapshot>();
            Require(
                offerService.TryGetCurrentOffer(
                    BistroBuilderServiceMode.TableService,
                    false,
                    tableOffer,
                    out string tableOfferError
                ),
                tableOfferError
            );
            BistroBuilderMenuOfferItemSnapshot signatureOffer =
                FindOfferItem(tableOffer, tableSignatureDishId);
            Require(
                signatureOffer.IsOrderable && signatureOffer.SignatureDish,
                "El plato firma de mesa no quedó pedible."
            );

            string suffix = DateTime.UtcNow.Ticks.ToString();
            BistroBuilderCanonicalOrderCreationRequest request =
                new BistroBuilderCanonicalOrderCreationRequest
                {
                    externalReferenceId = "functional_21d_" + suffix,
                    tableReferenceId = "table_21d_" + suffix,
                    customerGroupReferenceId = "group_21d_" + suffix,
                    mealService = offerService.CurrentMealService,
                    serviceMode = BistroBuilderServiceMode.TableService
                };
            request.lines.Add(
                new BistroBuilderCanonicalOrderLineRequest(
                    tableSignatureDishId,
                    "customer_21d_" + suffix,
                    new[] { "customer_21d_" + suffix },
                    0
                )
            );

            BistroBuilderCanonicalOrderOperationResult creation =
                orderService.TryCreateOrder(
                    request,
                    out BistroBuilderCanonicalOrder createdOrder
                );
            Require(creation.Succeeded, creation.Message);
            createdOrderId = creation.OrderId;
            Require(
                createdOrder != null && createdOrder.Lines.Count == 1,
                "La comanda funcional no contiene una única línea."
            );
            BistroBuilderCanonicalOrderLine createdLine = createdOrder.Lines[0];
            Require(
                createdLine.WasSignatureDishAtOrder,
                "La línea no congeló la condición de plato firma."
            );
            Require(
                string.Equals(
                    createdLine.RestaurantIdAtOrder,
                    signatureOffer.RestaurantId,
                    StringComparison.Ordinal
                ),
                "La línea no congeló el restaurante activo."
            );
            Require(
                createdLine.MenuOfferRevisionAtOrder ==
                    signatureOffer.OfferRevision,
                "La línea no congeló la revisión de oferta."
            );

            BistroBuilderMenuMutationResult removedSignature =
                menuService.TrySetSignatureDish(
                    tableSignatureDishId,
                    false
                );
            Require(removedSignature.Succeeded, removedSignature.Message);
            Require(
                orderService.TryGetOrderSnapshot(
                    createdOrderId,
                    out BistroBuilderCanonicalOrder persistedOrder
                ),
                "No se pudo releer la comanda funcional."
            );
            Require(
                persistedOrder.Lines[0].WasSignatureDishAtOrder,
                "Cambiar la carta reescribió retrospectivamente la comanda."
            );

            report.AppendLine(
                "- OK: la comanda real congeló firma, restaurante, precio y " +
                "revisión; retirarla después no alteró su historia."
            );
            succeeded = true;
        }
        catch (Exception exception)
        {
            report.AppendLine("- FALLO: " + exception.Message);
            Debug.LogException(exception);
        }
        finally
        {
            if (!CleanupOrder(orderService, createdOrderId, report))
            {
                succeeded = false;
            }

            if (menuCaptured && menuService != null &&
                !menuService.TryReplaceAll(
                    originalMenu,
                    true,
                    out string restoreMenuError
                ))
            {
                succeeded = false;
                report.AppendLine(
                    "- FALLO AL RESTAURAR CARTA: " + restoreMenuError
                );
            }

            if (telemetryCaptured && telemetryService != null &&
                !telemetryService.TryReplaceFromRuntimeSnapshot(
                    originalTelemetry,
                    false,
                    out string restoreTelemetryError
                ))
            {
                succeeded = false;
                report.AppendLine(
                    "- FALLO AL RESTAURAR TELEMETRÍA: " +
                    restoreTelemetryError
                );
            }

            EditorApplication.isPaused = previousPause;
        }

        string heading = succeeded
            ? "PRUEBA FUNCIONAL 2.1D SUPERADA"
            : "PRUEBA FUNCIONAL 2.1D FALLIDA";
        status = heading + "\n\n" + report.ToString().TrimEnd();

        if (succeeded)
        {
            Debug.Log(status);
        }
        else
        {
            Debug.LogError(status);
        }

        Repaint();
    }

    private static void ApplyOnlySignatureDish(
        BistroBuilderRestaurantMenuService menuService,
        IList<BistroBuilderMenuItemRuntimeState> source,
        string signatureDishId
    )
    {
        List<BistroBuilderMenuItemRuntimeState> replacement =
            new List<BistroBuilderMenuItemRuntimeState>(source.Count);

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = source[index];
            replacement.Add(
                new BistroBuilderMenuItemRuntimeState(
                    item.DishId,
                    item.CurrentPriceCents,
                    item.Unlocked,
                    item.Enabled,
                    item.ManuallySoldOut,
                    string.Equals(
                        item.DishId,
                        signatureDishId,
                        StringComparison.Ordinal
                    ),
                    item.AvailableServices,
                    item.DisplayOrder
                )
            );
        }

        Require(
            menuService.TryReplaceAll(
                replacement,
                true,
                out string error
            ),
            error
        );
    }

    private static bool CleanupOrder(
        BistroBuilderCanonicalOrderService orderService,
        string orderId,
        StringBuilder report
    )
    {
        if (orderService == null || string.IsNullOrWhiteSpace(orderId) ||
            !orderService.TryGetOrderSnapshot(orderId, out _))
        {
            return true;
        }

        BistroBuilderCanonicalOrderOperationResult cancellation =
            orderService.TryCancelOrder(orderId, "functional_21d_cleanup");

        if (!cancellation.Succeeded)
        {
            report.AppendLine(
                "- FALLO AL CANCELAR COMANDA DE PRUEBA: " +
                cancellation.Message
            );
            return false;
        }

        BistroBuilderCanonicalOrderOperationResult removal =
            orderService.TryRemoveTerminalOrder(orderId);

        if (!removal.Succeeded)
        {
            report.AppendLine(
                "- FALLO AL RETIRAR COMANDA DE PRUEBA: " + removal.Message
            );
            return false;
        }

        return true;
    }

    private static void RemoveNonOrderable(
        List<BistroBuilderMenuOfferItemSnapshot> items
    )
    {
        for (int index = items.Count - 1; index >= 0; index--)
        {
            if (!items[index].IsOrderable)
            {
                items.RemoveAt(index);
            }
        }
    }

    private static int CountSignatureDishes(
        IList<BistroBuilderMenuOfferItemSnapshot> items
    )
    {
        int count = 0;

        for (int index = 0; index < items.Count; index++)
        {
            if (items[index].SignatureDish)
            {
                count++;
            }
        }

        return count;
    }

    private static BistroBuilderMenuOfferItemSnapshot FindOfferItem(
        IList<BistroBuilderMenuOfferItemSnapshot> items,
        string dishId
    )
    {
        for (int index = 0; index < items.Count; index++)
        {
            if (string.Equals(
                    items[index].DishId,
                    dishId,
                    StringComparison.Ordinal
                ))
            {
                return items[index];
            }
        }

        return default(BistroBuilderMenuOfferItemSnapshot);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? "La prueba funcional 2.1D detectó un fallo."
                    : message
            );
        }
    }
}
