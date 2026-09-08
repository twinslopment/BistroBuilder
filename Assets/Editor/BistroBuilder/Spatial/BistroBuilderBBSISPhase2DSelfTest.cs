using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderBBSISPhase2DSelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/BBSIS/Fase 2D/Autotest")]
    public static void Run()
    {
        LastPassed = 0;
        LastFailed = 0;
        StringBuilder report = new StringBuilder();
        report.AppendLine("BBSIS FASE 2D - AUTOTEST");

        BistroBuilderSpatialInteractionService spatial =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSpatialInteractionService>();
        BistroBuilderMobilitySpatialCoordinator coordinator =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderMobilitySpatialCoordinator>();
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

        Check(spatial != null, "Servicio BBSIS disponible", report);
        Check(coordinator != null, "Coordinador 2D disponible", report);
        Check(contract != null && profile != null,
            "Contrato y perfil cargados", report);
        if (spatial == null || contract == null || profile == null)
        {
            Finish(report);
            return;
        }

        int leasesBefore = spatial.ActiveLeaseCount;
        int episodesBefore = spatial.ActiveEpisodeCount;
        GameObject actorObject = new GameObject(
            "__BBSIS_2D_TestCart");
        actorObject.SetActive(false);
        actorObject.transform.position =
            new Vector3(12000f, 0f, 12000f);
        BistroBuilderAdaptiveSpatialProxy proxy =
            actorObject.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
        BistroBuilderSpatialSubject subject =
            actorObject.AddComponent<BistroBuilderSpatialSubject>();
        subject.Configure(
            "spatial.test.logistics.cart",
            contract,
            proxy);
        BistroBuilderMobileSpatialAdapter adapter =
            actorObject.AddComponent<BistroBuilderMobileSpatialAdapter>();
        adapter.Configure(
            subject,
            profile,
            "bbsis.test.logistics.cart");
        actorObject.SetActive(true);
        spatial.RegisterSubject(subject);

        Check(adapter.ValidateConfiguration(out _),
            "Actor movil configurado", report);
        Check(actorObject.GetComponent<Rigidbody>() == null,
            "Actor movil funciona sin Rigidbody", report);
        Check(adapter.TickSpatial(2f),
            "Mobility Lease inicial concedido", report);
        Check(!string.IsNullOrWhiteSpace(adapter.MobilityLeaseId),
            "Mobility Lease tiene identidad estable", report);

        float mobilityHalfWidth =
            (profile.BodyWidth + profile.MovementMargin * 2f) * 0.5f;
        float carryBand = Mathf.Max(
            0.02f,
            profile.CarryWidthExpansion * 0.5f);
        float carryBlockerRadius = Mathf.Max(0.005f, carryBand * 0.15f);
        Vector3 carryBlockerPoint = actorObject.transform.position +
            actorObject.transform.right *
            (mobilityHalfWidth + carryBand * 0.65f);
        var carryOnlyBlockerRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "bbsis.test.carry.only.blocker",
            kind = BistroBuilderSpatialClaimKind.Carry,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(
                carryBlockerPoint,
                carryBlockerRadius),
            priority = 100,
            durationSeconds = 5f
        };
        bool carryOnlyBlockerGranted = spatial.TryAcquireLease(
            carryOnlyBlockerRequest,
            out BistroBuilderSpatialLease carryOnlyBlocker,
            out _);
        Check(carryOnlyBlockerGranted,
            "Bloqueador selectivo de Carry concedido", report);
        string mobilityBeforeCarryReject = adapter.MobilityLeaseId;
        adapter.SetLoadUnits(profile.MaximumLoadUnits);
        Check(!adapter.TickSpatial(2f),
            "Carry bloqueado rechaza la transaccion completa", report);
        Check(string.Equals(
                mobilityBeforeCarryReject,
                adapter.MobilityLeaseId,
                StringComparison.Ordinal) &&
              string.IsNullOrEmpty(adapter.CarryLeaseId),
            "Rechazo Carry conserva Mobility sin estado partido", report);
        if (carryOnlyBlocker != null)
            spatial.ReleaseLease(carryOnlyBlocker.leaseId);
        adapter.SetLoadUnits(3);
        Check(adapter.TickSpatial(2f),
            "Carry Lease con carga concedido tras liberar", report);
        Check(!string.IsNullOrWhiteSpace(adapter.CarryLeaseId),
            "Carry Envelope activo con carga", report);
        actorObject.transform.position += Vector3.right;
        string beforeRelocation = adapter.MobilityLeaseId;
        Check(adapter.TickSpatial(2f) &&
              !string.Equals(
                  beforeRelocation,
                  adapter.MobilityLeaseId,
                  StringComparison.Ordinal),
            "Relocalizacion atomica actualiza el lease", report);
        Check(spatial.ActiveEpisodeCount > episodesBefore,
            "Movimiento abre Spatial Episode", report);

        Vector3 blockedTarget =
            actorObject.transform.position + Vector3.right * 2f;
        var blockerRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "bbsis.test.blocker",
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(
                actorObject.transform.position + Vector3.right,
                0.35f),
            priority = 100,
            durationSeconds = 5f
        };
        bool blockerGranted = spatial.TryAcquireLease(
            blockerRequest,
            out BistroBuilderSpatialLease blocker,
            out _);
        Check(blockerGranted,
            "Bloqueador de stress concedido", report);

        string protectedLease = adapter.MobilityLeaseId;
        actorObject.transform.position = blockedTarget;
        bool rejected = !adapter.TickSpatial(2f);
        Check(rejected &&
              string.Equals(
                  protectedLease,
                  adapter.MobilityLeaseId,
                  StringComparison.Ordinal),
            "Conflicto conserva el lease anterior sin hueco", report);
        Check(adapter.RejectedRelocations > 0 &&
              adapter.LastDecision.failure ==
                BistroBuilderSpatialLeaseFailure.Conflict,
            "Rechazo movil expone causa determinista", report);
        if (blocker != null)
            spatial.ReleaseLease(blocker.leaseId);
        Check(adapter.TickSpatial(2f),
            "Movimiento reintenta tras liberar el espacio", report);

        int expectedActive = leasesBefore + 2;
        bool stressStable = true;
        Vector3 stressBase = actorObject.transform.position;
        for (int i = 0; i < 300; i++)
        {
            actorObject.transform.position = stressBase +
                Vector3.forward * (i * 0.01f);
            if (!adapter.TickSpatial(2f) ||
                spatial.ActiveLeaseCount != expectedActive)
            {
                stressStable = false;
                break;
            }
        }
        Check(stressStable,
            "Stress 300 relocalizaciones sin fugas", report);

        var semantics =
            new List<BistroBuilderSpatialSemanticVolume>(4);
        adapter.WriteSemanticVolumes(semantics);
        Check(HasRole(
                semantics,
                BistroBuilderSpatialSemanticRole.MobilityEnvelope),
            "Semantica Mobility Envelope publicada", report);
        Check(HasRole(
                semantics,
                BistroBuilderSpatialSemanticRole.CarryEnvelope),
            "Semantica Carry Envelope publicada", report);

        var work = new BistroBuilderSpatialSemanticVolume
        {
            role = BistroBuilderSpatialSemanticRole.WorkZone
        };
        bool classified =
            BistroBuilderSpatialAssessmentService.TryClassifyPair(
                semantics[0],
                work,
                out bool blocking,
                out float severity);
        Check(classified && blocking && severity > 0.8f,
            "Mobility contra Work Zone se clasifica bloqueante", report);
        spatial.ResetTransientRuntimeStateAfterLoad();
        Check(spatial.ActiveLeaseCount == 0 &&
              spatial.ActiveEpisodeCount == 0,
            "Load descarta estado movil transitorio", report);
        Check(adapter.TickSpatial(2f) &&
              spatial.ActiveLeaseCount == 2,
            "Estado movil se reconstruye despues de Load", report);

        adapter.SetLoadUnits(0);
        Check(adapter.TickSpatial(2f) &&
              string.IsNullOrEmpty(adapter.CarryLeaseId),
            "Descarga libera Carry Lease", report);

        adapter.ReleaseSpatialState(
            BistroBuilderSpatialEpisodeState.Completed);
        UnityEngine.Object.DestroyImmediate(actorObject);
        Check(spatial.ActiveLeaseCount == leasesBefore,
            "Limpieza restaura el conteo de leases", report);
        Check(spatial.ActiveEpisodeCount == episodesBefore,
            "Limpieza restaura Spatial Episodes", report);

        GameObject recoveryObject = new GameObject(
            "__BBSIS_2D_RecoveryCart");
        recoveryObject.SetActive(false);
        recoveryObject.transform.position =
            new Vector3(16000f, 0f, 16000f);
        BistroBuilderAdaptiveSpatialProxy recoveryProxy =
            recoveryObject.AddComponent<BistroBuilderAdaptiveSpatialProxy>();
        BistroBuilderSpatialSubject recoverySubject =
            recoveryObject.AddComponent<BistroBuilderSpatialSubject>();
        recoverySubject.Configure(
            "spatial.test.logistics.recovery",
            contract,
            recoveryProxy);
        BistroBuilderMobileSpatialAdapter recoveryAdapter =
            recoveryObject.AddComponent<BistroBuilderMobileSpatialAdapter>();
        recoveryAdapter.Configure(
            recoverySubject,
            profile,
            "bbsis.test.logistics.recovery");
        recoveryObject.SetActive(true);
        spatial.RegisterSubject(recoverySubject);

        var recoveryBlockRequest = new BistroBuilderSpatialClaimRequest
        {
            ownerId = "bbsis.test.recovery.blocker",
            kind = BistroBuilderSpatialClaimKind.Mobility,
            conflictMode = BistroBuilderSpatialConflictMode.Block,
            volume = BistroBuilderSpatialVolume.Circle(
                recoveryObject.transform.position,
                0.4f),
            priority = 100,
            durationSeconds = 10f
        };
        bool recoveryBlockGranted = spatial.TryAcquireLease(
            recoveryBlockRequest,
            out BistroBuilderSpatialLease recoveryBlocker,
            out _);
        Check(recoveryBlockGranted,
            "Bloqueador de arranque invalido concedido", report);
        Check(!recoveryAdapter.TickSpatial(2f) &&
              string.IsNullOrEmpty(recoveryAdapter.MobilityLeaseId),
            "Arranque bloqueado no fabrica reserva falsa", report);
        recoveryObject.transform.position += Vector3.right * 2f;
        Check(recoveryAdapter.TickSpatial(2f) &&
              !string.IsNullOrEmpty(recoveryAdapter.MobilityLeaseId),
            "Recuperacion desde pose inicial invalida sin barrido fantasma", report);
        if (recoveryBlocker != null)
            spatial.ReleaseLease(recoveryBlocker.leaseId);
        recoveryAdapter.ReleaseSpatialState(
            BistroBuilderSpatialEpisodeState.Completed);
        UnityEngine.Object.DestroyImmediate(recoveryObject);
        Check(spatial.ActiveLeaseCount == leasesBefore &&
              spatial.ActiveEpisodeCount == episodesBefore,
            "Recuperacion limpia todo el estado transitorio", report);

        Finish(report);
    }
    private static bool HasRole(
        List<BistroBuilderSpatialSemanticVolume> values,
        BistroBuilderSpatialSemanticRole role)
    {
        for (int i = 0; i < values.Count; i++)
            if (values[i] != null && values[i].role == role)
                return true;
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
            LastFailed + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(
                "Autotest BBSIS Fase 2D fallido.");
    }
}
