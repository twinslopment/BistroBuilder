#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23CRuntimeFunctionalTestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;
    private int runtimeErrors;
    private bool running;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C - Prueba funcional runtime")]
    public static void Open()
    {
        BistroBuilderSuppliers23CRuntimeFunctionalTestWindow window =
            GetWindow<BistroBuilderSuppliers23CRuntimeFunctionalTestWindow>("Prueba runtime 2.3C");
        window.minSize = new Vector2(780f, 520f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PRUEBA FUNCIONAL RUNTIME 2.3C", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Debe ejecutarse en Play Mode. La prueba usa una semilla controlada, avanza el mercado " +
            "hasta los días 5 y 10 y restaura al final exactamente el snapshot que había antes.",
            MessageType.Info);

        GUI.enabled = EditorApplication.isPlaying && !running;
        if (GUILayout.Button("Ejecutar prueba completa", GUILayout.Height(32f)))
        {
            RunTest();
        }
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(
            "Correctos: " + passed + "   Fallos: " + failed +
            "   Errores/Excepciones/Asserts: " + runtimeErrors,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
        {
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunTest()
    {
        results.Clear();
        passed = 0;
        failed = 0;
        runtimeErrors = 0;
        running = true;
        Application.logMessageReceived += HandleLog;

        BistroBuilderSupplierMarketService service = null;
        BistroBuilderSupplierMarketSnapshot original = null;

        try
        {
            BistroBuilderSupplierMarketService[] services =
                UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierMarketService>(UnityEngine.FindObjectsSortMode.None);
            Check(services.Length == 1,
                "Existe exactamente una autoridad runtime BistroBuilderSupplierMarketService.");
            if (services.Length != 1)
            {
                return;
            }

            service = services[0];
            Check(service != null, "La autoridad runtime es accesible.");
            Check(service.IsInitialized, "El mercado runtime está inicializado.");
            Check(string.IsNullOrEmpty(service.LastInitializationError),
                "No existe error residual de inicialización.");

            original = service.CreateSnapshot();
            Check(original != null, "Se puede capturar snapshot del mercado actual.");
            Check(original != null && original.offerStates.Count >= 66,
                "El mercado runtime contiene al menos las 66 ofertas cerradas en 2.3B.");

            BistroBuilderSupplierAuthoringDatabase suppliers =
                Resources.Load<BistroBuilderSupplierAuthoringDatabase>(
                    BistroBuilderSupplierMarketService.SupplierAuthoringResourcePath);
            BistroBuilderSupplierMarketSettings settings =
                Resources.Load<BistroBuilderSupplierMarketSettings>(
                    BistroBuilderSupplierMarketService.MarketSettingsResourcePath);
            Check(suppliers != null, "Runtime localiza supplier.authoring en Resources.");
            Check(settings != null, "Runtime localiza supplier.market.settings en Resources.");
            if (suppliers == null || settings == null)
            {
                return;
            }

            Check(settings.ReviewEveryGameDays == 5,
                "Runtime consume ciclo de mercado de 5 días.");

            int authoringRevisionBefore = suppliers.ContentRevision;
            ulong testSeed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
                "runtime-functional-23c", settings.DeterministicSalt);
            Check(service.TryInitializeFresh(testSeed),
                "La autoridad acepta inicialización determinista controlada.");
            Check(service.MarketSeed == testSeed,
                "La semilla runtime coincide con la semilla controlada.");
            Check(service.CurrentGameDay == 1,
                "La simulación controlada comienza en día 1.");
            Check(service.LastReviewGameDay == 0,
                "No existe revisión ficticia al iniciar.");
            Check(service.NextReviewGameDay == 5,
                "La primera revisión runtime queda programada en día 5.");
            Check(service.OfferCount >= 66,
                "La autoridad crea un estado dinámico por oferta activa.");

            long revisionInitial = service.MarketRevision;
            string error;
            Check(service.TryAdvanceToGameDay(4, out error),
                "Runtime avanza al día 4 sin error.");
            Check(service.LastReviewGameDay == 0,
                "Día 4 todavía no ejecuta revisión.");
            Check(service.MarketRevision == revisionInitial,
                "Avanzar sin alcanzar revisión no incrementa MarketRevision.");

            int reviewEvents = 0;
            int offerChangeEvents = 0;
            Action<BistroBuilderSupplierMarketReviewOutcome> reviewHandler =
                delegate { reviewEvents++; };
            Action<BistroBuilderSupplierMarketChangeRecord> changeHandler =
                delegate { offerChangeEvents++; };
            service.MarketReviewed += reviewHandler;
            service.MarketOfferChanged += changeHandler;

            Check(service.TryAdvanceToGameDay(5, out error),
                "Runtime ejecuta el ciclo del día 5 sin error.");
            Check(service.LastReviewGameDay == 5,
                "La primera revisión real queda registrada en día 5.");
            Check(service.NextReviewGameDay == 10,
                "Tras revisar día 5, la próxima revisión es día 10.");
            Check(service.MarketRevision == revisionInitial + 1,
                "Día 5 incrementa MarketRevision exactamente una vez.");
            Check(reviewEvents == 1,
                "La revisión del día 5 publica un único evento MarketReviewed.");

            BistroBuilderSupplierMarketSnapshot day5 = service.CreateSnapshot();
            Check(day5 != null && day5.reviews.Count == 1,
                "Snapshot día 5 contiene una revisión.");
            Check(day5 != null && day5.reviews[0].offersReviewed == service.OfferCount,
                "La revisión runtime procesa todas las ofertas.");
            Check(day5 != null && day5.changes.Count > 0,
                "La primera revisión controlada genera cambios de mercado significativos.");
            Check(day5 != null && offerChangeEvents == day5.changes.Count,
                "Los eventos de oferta coinciden con los cambios registrados en día 5.");
            Check(day5 != null && AllPositivePrices(day5),
                "Todos los precios runtime siguen siendo positivos.");
            Check(day5 != null && AllAvailabilityDefined(day5),
                "Toda disponibilidad runtime usa Disponible/Stock limitado/Temporalmente agotado.");

            BistroBuilderSupplierMarketOfferState first =
                day5 != null && day5.offerStates.Count > 0 ? day5.offerStates[0] : null;
            BistroBuilderSupplierMarketOfferState queried = null;
            bool queryOk = first != null &&
                service.TryGetOfferState(first.supplierOfferId, out queried);
            Check(queryOk, "La API consulta un estado por SupplierOfferId.");

            bool defensiveCopyOk = false;
            bool priceQueryOk = false;
            bool availabilityQueryOk = false;
            if (queryOk)
            {
                long storedPrice = first.currentPriceCents;
                queried.currentPriceCents++;
                BistroBuilderSupplierMarketOfferState queriedAgain;
                service.TryGetOfferState(first.supplierOfferId, out queriedAgain);
                defensiveCopyOk = queriedAgain != null &&
                    queriedAgain.currentPriceCents == storedPrice;
                priceQueryOk = service.GetCurrentPriceCents(first.supplierOfferId) == storedPrice;
                availabilityQueryOk = service.IsAvailableForNewOrders(first.supplierOfferId) ==
                    (first.availability != BistroBuilderSupplierOfferAvailability.TemporalmenteAgotado);
            }
            Check(defensiveCopyOk,
                "La API de consulta devuelve copia defensiva, no estado mutable.");
            Check(priceQueryOk,
                "GetCurrentPriceCents coincide con el estado dinámico.");
            Check(availabilityQueryOk,
                "IsAvailableForNewOrders respeta TemporalmenteAgotado.");

            Check(service.TryAdvanceToGameDay(10, out error),
                "Runtime ejecuta segunda revisión en día 10.");
            Check(service.LastReviewGameDay == 10 && service.NextReviewGameDay == 15,
                "Ciclo runtime queda 10 → 15 correctamente.");
            Check(reviewEvents == 2,
                "Dos revisiones publican exactamente dos eventos MarketReviewed.");

            BistroBuilderSupplierMarketSnapshot day10 = service.CreateSnapshot();
            Check(day10 != null && day10.reviews.Count == 2,
                "Snapshot día 10 contiene dos revisiones.");
            Check(day10 != null && day10.offerStates.Count == service.OfferCount,
                "Las revisiones no crean ni eliminan ofertas.");
            Check(day10 != null && BistroBuilderSupplierMarketEngine.ValidateSnapshotAgainstAuthoring(
                    day10, suppliers, out error),
                "El snapshot runtime sigue convergiendo con supplier.authoring.");
            Check(suppliers.ContentRevision == authoringRevisionBefore,
                "El mercado runtime no modifica supplier.authoring.");

            string fingerprintDay10 = BistroBuilderSupplierMarketEngine.BuildFingerprint(day10);
            Check(service.TryInitializeFresh(testSeed),
                "Puede reiniciarse con la misma semilla para comprobar determinismo.");
            Check(service.TryAdvanceToGameDay(10, out error),
                "La repetición determinista llega de nuevo a día 10.");
            BistroBuilderSupplierMarketSnapshot repeated = service.CreateSnapshot();
            Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(repeated) == fingerprintDay10,
                "Misma semilla y mismos días reproducen exactamente el mismo mercado.");

            BistroBuilderSupplierMarketGameDayResolver resolver =
                new BistroBuilderSupplierMarketGameDayResolver();
            int resolvedDay;
            bool clockResolved = resolver.TryGetGameDay(out resolvedDay);
            Check(clockResolved,
                "El adaptador automático localiza día absoluto o GameClock para contar medianoches.");
            Check(!string.IsNullOrWhiteSpace(resolver.Diagnostic),
                "El adaptador de calendario publica diagnóstico legible: " + resolver.Diagnostic);

            service.MarketReviewed -= reviewHandler;
            service.MarketOfferChanged -= changeHandler;
        }
        catch (Exception exception)
        {
            failed++;
            results.Add("[FALLO] Excepción de la prueba: " + exception);
        }
        finally
        {
            if (service != null && original != null)
            {
                string restoreError;
                bool restored = service.TryRestoreSnapshot(original, out restoreError);
                Check(restored, "La prueba restaura el snapshot runtime original al terminar.");
                if (!restored)
                {
                    results.Add("[FALLO] Restore: " + restoreError);
                }
            }

            Application.logMessageReceived -= HandleLog;
            Check(runtimeErrors == 0,
                "La ejecución termina sin Error, Exception ni Assert capturados por 2.3C.");
            running = false;
            Debug.Log(
                "PRUEBA FUNCIONAL RUNTIME 2.3C — " + passed +
                " superadas / " + failed + " fallidas / " + runtimeErrors +
                " Error-Exception-Assert.");
            Repaint();
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (!running)
        {
            return;
        }

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            runtimeErrors++;
        }
    }

    private void Check(bool condition, string description)
    {
        if (condition)
        {
            passed++;
            results.Add("[OK] " + description);
        }
        else
        {
            failed++;
            results.Add("[FALLO] " + description);
        }
    }

    private static bool AllPositivePrices(BistroBuilderSupplierMarketSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.offerStates.Count; index++)
        {
            if (snapshot.offerStates[index] == null ||
                snapshot.offerStates[index].currentPriceCents <= 0)
            {
                return false;
            }
        }
        return true;
    }

    private static bool AllAvailabilityDefined(BistroBuilderSupplierMarketSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.offerStates.Count; index++)
        {
            int value = (int)snapshot.offerStates[index].availability;
            if (value < 0 || value > 2)
            {
                return false;
            }
        }
        return true;
    }
}
#endif
