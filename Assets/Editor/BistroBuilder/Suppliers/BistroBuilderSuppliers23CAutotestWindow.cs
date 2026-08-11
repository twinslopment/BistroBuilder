#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderSuppliers23CAutotestWindow : EditorWindow
{
    private Vector2 scroll;
    private readonly List<string> results = new List<string>();
    private int passed;
    private int failed;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3C - Autotest mercado determinista")]
    public static void Open()
    {
        BistroBuilderSuppliers23CAutotestWindow window =
            GetWindow<BistroBuilderSuppliers23CAutotestWindow>("Autotest 2.3C");
        window.minSize = new Vector2(760f, 500f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("AUTOTEST 2.3C — Mercado determinista", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Ejecutar autotest", GUILayout.Height(30f)))
        {
            RunTests();
        }

        EditorGUILayout.LabelField(
            "Pruebas superadas: " + passed + " / Pruebas fallidas: " + failed,
            EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int index = 0; index < results.Count; index++)
        {
            EditorGUILayout.LabelField(results[index], EditorStyles.wordWrappedLabel);
        }
        EditorGUILayout.EndScrollView();
    }

    private void RunTests()
    {
        results.Clear();
        passed = 0;
        failed = 0;

        BistroBuilderSupplierAuthoringDatabase suppliers =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierAuthoringDatabase>(
                BistroBuilderSuppliers23CPaths.SupplierAuthoringAsset);
        BistroBuilderSupplierMarketSettings settings =
            AssetDatabase.LoadAssetAtPath<BistroBuilderSupplierMarketSettings>(
                BistroBuilderSuppliers23CPaths.MarketSettingsAsset);

        Check(suppliers != null, "Existe supplier.authoring.");
        Check(settings != null, "Existe supplier.market.settings.");
        if (suppliers == null || settings == null)
        {
            Finish();
            return;
        }

        Check(settings.SchemaId == BistroBuilderSupplierMarketSettings.CurrentSchemaId,
            "SchemaId de market.settings correcto.");
        Check(settings.SchemaVersion == BistroBuilderSupplierMarketSettings.CurrentSchemaVersion,
            "SchemaVersion de market.settings correcto.");
        Check(settings.ReviewEveryGameDays == 5,
            "El ciclo global es exactamente de 5 días.");
        Check(settings.MaximumChangeHistoryEntries >= 16,
            "El historial de cambios tiene capacidad segura.");
        Check(settings.MaximumReviewHistoryEntries >= 4,
            "El historial de revisiones tiene capacidad segura.");
        Check(settings.StablePriceChangeChance < settings.ModeratePriceChangeChance,
            "Perfil Estable cambia menos que Moderado.");
        Check(settings.ModeratePriceChangeChance < settings.VariablePriceChangeChance,
            "Perfil Moderado cambia menos que Variable.");
        Check(settings.StableMaximumStepPercent < settings.ModerateMaximumStepPercent,
            "Paso Estable menor que Moderado.");
        Check(settings.ModerateMaximumStepPercent < settings.VariableMaximumStepPercent,
            "Paso Moderado menor que Variable.");

        int activeSuppliers = CountActiveSuppliers(suppliers);
        int activeOffers = CountActiveOffers(suppliers);
        Check(activeSuppliers >= 6, "Hay al menos 6 proveedores activos.");
        Check(activeOffers >= 66, "Hay al menos 66 ofertas base activas.");
        Check(AllSupplierReviewIntervalsAreFive(suppliers),
            "Todos los proveedores activos revisan mercado cada 5 días.");
        Check(AllOfferBoundsAreValid(suppliers),
            "Todas las ofertas tienen límites de mercado compatibles.");
        Check(AllAvailabilityWeightsAreValid(suppliers),
            "Todos los perfiles de disponibilidad tienen pesos válidos.");

        int supplierRevisionBefore = suppliers.ContentRevision;
        ulong seed = BistroBuilderSupplierMarketEngine.StableSeedFromText(
            "autotest-23c-primary", settings.DeterministicSalt);
        BistroBuilderSupplierMarketSnapshot snapshotA =
            BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(suppliers, settings, seed, 1);
        BistroBuilderSupplierMarketSnapshot snapshotB =
            BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(suppliers, settings, seed, 1);

        Check(snapshotA != null, "Se crea snapshot inicial de mercado.");
        Check(snapshotA.schemaId == BistroBuilderSupplierMarketSnapshot.CurrentSchemaId,
            "Snapshot usa supplier.market.runtime.");
        Check(snapshotA.schemaVersion == BistroBuilderSupplierMarketSnapshot.CurrentSchemaVersion,
            "Snapshot runtime usa versión vigente.");
        Check(snapshotA.offerStates.Count == activeOffers,
            "Un estado por cada oferta activa.");
        Check(snapshotA.currentGameDay == 1, "Mercado comienza en día 1.");
        Check(snapshotA.lastReviewGameDay == 0, "No existe revisión inicial ficticia.");
        Check(snapshotA.nextReviewGameDay == 5, "Primera revisión programada para día 5.");
        Check(AllInitialPricesMatchBase(snapshotA),
            "Precio inicial coincide con precio base.");
        Check(AllInitialAvailabilityMatchesAuthoring(snapshotA, suppliers),
            "Disponibilidad inicial coincide con la oferta base.");
        Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotA) ==
              BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotB),
            "Misma semilla produce snapshot inicial idéntico.");

        List<BistroBuilderSupplierMarketReviewOutcome> outcomes;
        string error;
        bool advanced4 = BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
            snapshotA, suppliers, settings, 4, out outcomes, out error);
        Check(advanced4, "Avanzar al día 4 es válido.");
        Check(advanced4 && outcomes.Count == 0,
            "Antes del día 5 no se ejecuta ninguna revisión.");
        Check(snapshotA.lastReviewGameDay == 0,
            "Día 4 mantiene LastReviewGameDay=0.");
        Check(snapshotA.nextReviewGameDay == 5,
            "Día 4 mantiene próxima revisión en día 5.");

        bool advanced5 = BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
            snapshotA, suppliers, settings, 5, out outcomes, out error);
        Check(advanced5, "Avanzar al día 5 es válido.");
        Check(advanced5 && outcomes.Count == 1,
            "Día 5 ejecuta exactamente una revisión.");
        Check(advanced5 && outcomes.Count == 1 && outcomes[0].reviewDay == 5,
            "La revisión se etiqueta con día 5.");
        Check(advanced5 && outcomes.Count == 1 && outcomes[0].offersReviewed == activeOffers,
            "Día 5 revisa todas las ofertas activas.");
        Check(snapshotA.lastReviewGameDay == 5,
            "Tras revisión LastReviewGameDay=5.");
        Check(snapshotA.nextReviewGameDay == 10,
            "Tras día 5 la siguiente revisión es día 10.");
        Check(snapshotA.marketRevision == 2,
            "Una revisión incrementa una vez MarketRevision.");
        Check(AllPricesWithinBounds(snapshotA, suppliers),
            "Todos los precios permanecen dentro de límites tras día 5.");
        Check(AllAvailabilityValuesAreDefined(snapshotA),
            "Toda disponibilidad runtime usa estados válidos.");
        Check(AllStatesReviewedExactly(snapshotA, 5, 1),
            "Todas las ofertas registran una revisión en día 5.");

        bool advanced30 = BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
            snapshotA, suppliers, settings, 30, out outcomes, out error);
        Check(advanced30, "Avanzar del día 5 al 30 es válido.");
        Check(advanced30 && outcomes.Count == 5,
            "Del día 5 al 30 se ejecutan cinco revisiones adicionales.");
        Check(snapshotA.reviews.Count == 6,
            "A día 30 existen exactamente seis revisiones.");
        Check(ReviewDaysAreExact(snapshotA.reviews),
            "Las revisiones ocurren exactamente en 5,10,15,20,25,30.");
        Check(snapshotA.lastReviewGameDay == 30 && snapshotA.nextReviewGameDay == 35,
            "Día 30 deja la próxima revisión en día 35.");
        Check(AllStatesReviewedExactly(snapshotA, 30, 6),
            "Todas las ofertas han sido revisadas seis veces a día 30.");
        Check(AllPricesWithinBounds(snapshotA, suppliers),
            "Los precios siguen dentro de límites tras seis revisiones.");
        Check(snapshotA.changes.Count > 0,
            "El mercado genera al menos un cambio significativo en seis revisiones.");
        Check(snapshotA.changes.Count <= settings.MaximumChangeHistoryEntries,
            "El historial de cambios respeta su límite.");
        Check(snapshotA.reviews.Count <= settings.MaximumReviewHistoryEntries,
            "El historial de revisiones respeta su límite.");

        bool advancedB = BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
            snapshotB, suppliers, settings, 30, out outcomes, out error);
        Check(advancedB, "Segunda simulación con misma semilla llega a día 30.");
        Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotA) ==
              BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotB),
            "Misma semilla produce idéntico mercado a día 30.");

        ulong seed2 = BistroBuilderSupplierMarketEngine.StableSeedFromText(
            "autotest-23c-secondary", settings.DeterministicSalt);
        BistroBuilderSupplierMarketSnapshot snapshotC =
            BistroBuilderSupplierMarketEngine.CreateInitialSnapshot(suppliers, settings, seed2, 1);
        bool advancedC = BistroBuilderSupplierMarketEngine.TryAdvanceToGameDay(
            snapshotC, suppliers, settings, 30, out outcomes, out error);
        Check(advancedC, "Simulación con segunda semilla llega a día 30.");
        Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotA) !=
              BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotC),
            "Semillas distintas producen evolución distinta.");

        BistroBuilderSupplierMarketSnapshot clone = snapshotA.DeepClone();
        Check(clone != snapshotA, "DeepClone crea otra instancia.");
        Check(clone.offerStates != snapshotA.offerStates,
            "DeepClone no expone la lista de estados original.");
        Check(BistroBuilderSupplierMarketEngine.BuildFingerprint(clone) ==
              BistroBuilderSupplierMarketEngine.BuildFingerprint(snapshotA),
            "DeepClone conserva el contenido exacto.");
        bool cloneIsolation = false;
        if (clone.offerStates.Count > 0)
        {
            long originalPrice = snapshotA.offerStates[0].currentPriceCents;
            clone.offerStates[0].currentPriceCents++;
            cloneIsolation = snapshotA.offerStates[0].currentPriceCents == originalPrice;
        }
        Check(cloneIsolation, "Mutar el clon no modifica el mercado original.");

        string validationError;
        Check(BistroBuilderSupplierMarketEngine.ValidateSnapshotAgainstAuthoring(
                snapshotA, suppliers, out validationError),
            "Snapshot día 30 valida contra autoría.");
        Check(suppliers.ContentRevision == supplierRevisionBefore,
            "El motor no modifica ContentRevision de supplier.authoring.");

        Finish();
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

    private void Finish()
    {
        Debug.Log("AUTOTEST 2.3C — " + passed + " superadas / " + failed + " fallidas.");
        Repaint();
    }

    private static int CountActiveSuppliers(BistroBuilderSupplierAuthoringDatabase db)
    {
        int count = 0;
        for (int i = 0; i < db.Suppliers.Count; i++)
        {
            if (db.Suppliers[i] != null && db.Suppliers[i].isActive) count++;
        }
        return count;
    }

    private static int CountActiveOffers(BistroBuilderSupplierAuthoringDatabase db)
    {
        int count = 0;
        for (int s = 0; s < db.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[s];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null) continue;
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                if (supplier.baseOffers[o] != null && supplier.baseOffers[o].isActive) count++;
            }
        }
        return count;
    }

    private static bool AllSupplierReviewIntervalsAreFive(BistroBuilderSupplierAuthoringDatabase db)
    {
        for (int i = 0; i < db.Suppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[i];
            if (supplier == null || !supplier.isActive) continue;
            if (supplier.priceEvolutionProfile == null ||
                supplier.priceEvolutionProfile.reviewEveryGameDays != 5) return false;
        }
        return true;
    }

    private static bool AllOfferBoundsAreValid(BistroBuilderSupplierAuthoringDatabase db)
    {
        for (int s = 0; s < db.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[s];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null ||
                supplier.priceEvolutionProfile == null) continue;
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer == null || !offer.isActive) continue;
                float min = Mathf.Max(supplier.priceEvolutionProfile.minimumVariationPercent,
                    offer.minimumMarketVariationPercent);
                float max = Mathf.Min(supplier.priceEvolutionProfile.maximumVariationPercent,
                    offer.maximumMarketVariationPercent);
                if (offer.basePriceCents <= 0 || min > max) return false;
            }
        }
        return true;
    }

    private static bool AllAvailabilityWeightsAreValid(BistroBuilderSupplierAuthoringDatabase db)
    {
        for (int i = 0; i < db.Suppliers.Count; i++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[i];
            if (supplier == null || !supplier.isActive) continue;
            if (supplier.availabilityProfile == null) return false;
            float limited = supplier.availabilityProfile.limitedStockWeight;
            float outage = supplier.availabilityProfile.temporaryOutOfStockWeight;
            if (limited < 0f || outage < 0f || limited + outage > 1f) return false;
        }
        return true;
    }

    private static bool AllInitialPricesMatchBase(BistroBuilderSupplierMarketSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.offerStates.Count; i++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[i];
            if (state == null || state.basePriceCents != state.currentPriceCents) return false;
        }
        return true;
    }

    private static bool AllInitialAvailabilityMatchesAuthoring(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase db)
    {
        Dictionary<string, BistroBuilderSupplierOfferAvailability> expected =
            new Dictionary<string, BistroBuilderSupplierOfferAvailability>(StringComparer.Ordinal);
        for (int s = 0; s < db.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[s];
            if (supplier == null || !supplier.isActive || supplier.baseOffers == null) continue;
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer != null && offer.isActive) expected[offer.SupplierOfferId] = offer.initialAvailability;
            }
        }
        for (int i = 0; i < snapshot.offerStates.Count; i++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[i];
            BistroBuilderSupplierOfferAvailability availability;
            if (state == null || !expected.TryGetValue(state.supplierOfferId, out availability) ||
                state.availability != availability) return false;
        }
        return true;
    }

    private static bool AllPricesWithinBounds(
        BistroBuilderSupplierMarketSnapshot snapshot,
        BistroBuilderSupplierAuthoringDatabase db)
    {
        Dictionary<string, BistroBuilderSupplierAuthoringRecord> supplierById =
            new Dictionary<string, BistroBuilderSupplierAuthoringRecord>(StringComparer.Ordinal);
        Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord> offerById =
            new Dictionary<string, BistroBuilderSupplierBaseOfferAuthoringRecord>(StringComparer.Ordinal);
        for (int s = 0; s < db.Suppliers.Count; s++)
        {
            BistroBuilderSupplierAuthoringRecord supplier = db.Suppliers[s];
            if (supplier == null || !supplier.isActive) continue;
            supplierById[supplier.SupplierId] = supplier;
            if (supplier.baseOffers == null) continue;
            for (int o = 0; o < supplier.baseOffers.Count; o++)
            {
                BistroBuilderSupplierBaseOfferAuthoringRecord offer = supplier.baseOffers[o];
                if (offer != null && offer.isActive) offerById[offer.SupplierOfferId] = offer;
            }
        }
        for (int i = 0; i < snapshot.offerStates.Count; i++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[i];
            BistroBuilderSupplierAuthoringRecord supplier;
            BistroBuilderSupplierBaseOfferAuthoringRecord offer;
            if (state == null || !supplierById.TryGetValue(state.supplierId, out supplier) ||
                !offerById.TryGetValue(state.supplierOfferId, out offer)) return false;
            float minPct = Mathf.Max(supplier.priceEvolutionProfile.minimumVariationPercent,
                offer.minimumMarketVariationPercent);
            float maxPct = Mathf.Min(supplier.priceEvolutionProfile.maximumVariationPercent,
                offer.maximumMarketVariationPercent);
            long min = Math.Max(1L, (long)Math.Round(offer.basePriceCents * (1.0 + minPct / 100.0)));
            long max = Math.Max(min, (long)Math.Round(offer.basePriceCents * (1.0 + maxPct / 100.0)));
            if (state.currentPriceCents < min || state.currentPriceCents > max) return false;
        }
        return true;
    }

    private static bool AllAvailabilityValuesAreDefined(BistroBuilderSupplierMarketSnapshot snapshot)
    {
        for (int i = 0; i < snapshot.offerStates.Count; i++)
        {
            int value = (int)snapshot.offerStates[i].availability;
            if (value < 0 || value > 2) return false;
        }
        return true;
    }

    private static bool AllStatesReviewedExactly(
        BistroBuilderSupplierMarketSnapshot snapshot,
        int lastDay,
        int reviewCount)
    {
        for (int i = 0; i < snapshot.offerStates.Count; i++)
        {
            BistroBuilderSupplierMarketOfferState state = snapshot.offerStates[i];
            if (state == null || state.lastReviewedGameDay != lastDay || state.reviewCount != reviewCount)
                return false;
        }
        return true;
    }

    private static bool ReviewDaysAreExact(List<BistroBuilderSupplierMarketReviewRecord> reviews)
    {
        int[] expected = { 5, 10, 15, 20, 25, 30 };
        if (reviews.Count != expected.Length) return false;
        for (int i = 0; i < expected.Length; i++)
        {
            if (reviews[i] == null || reviews[i].gameDay != expected[i]) return false;
        }
        return true;
    }
}
#endif
