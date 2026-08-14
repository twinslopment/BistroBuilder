using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// JKL-A: prueba REAL contra BistroBuilderSaveGameService. Guarda las seis
/// autoridades de Proveedores, muta el mercado después del checkpoint, carga
/// desde disco y exige restauración bit-a-bit lógica (fingerprint agregado).
/// </summary>
public sealed class BistroBuilderSuppliers23JKLPersistenceFunctionalTestWindow : EditorWindow
{
    private enum Phase
    {
        Idle,
        Saving,
        Loading,
        DeletingSuccess,
        DeletingFailure,
        Completed,
        Failed
    }

    private Vector2 scroll;
    private string report = "Entra en Play Mode y ejecuta la prueba.";
    private MessageType reportType = MessageType.Info;
    private Phase phase;
    private BistroBuilderSaveGameService saveService;
    private BistroBuilderSupplierIntegratedSaveSectionProvider provider;
    private BistroBuilderSupplierPurchaseOrderService orderService;
    private BistroBuilderSupplierProgressionService progressionService;
    private BistroBuilderSupplierIntegratedSaveState originalState;
    private string originalFingerprint;
    private string mutatedFingerprint;
    private int diagnosticSlot;
    private string pendingFailure;
    private bool subscribed;
    private int capturedErrors;

    [MenuItem("Tools/Bistro Builder/Proveedores/2.3JKL-A - Prueba funcional Save Load", false, 2903)]
    private static void Open()
    {
        GetWindow<BistroBuilderSuppliers23JKLPersistenceFunctionalTestWindow>("Save/Load 2.3JKL");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("2.3JKL-A — Persistencia integral REAL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Usa un slot libre 980-989. Guarda C/D/E/G/H/I, crea un Draft real después del checkpoint, " +
            "carga el checkpoint, compara el estado integrado y elimina el slot diagnóstico.",
            MessageType.Info);
        bool canRun = EditorApplication.isPlaying &&
                      (phase == Phase.Idle || phase == Phase.Completed || phase == Phase.Failed);
        using (new EditorGUI.DisabledScope(!canRun))
        {
            if (GUILayout.Button("Ejecutar prueba Save/Load 2.3JKL-A", GUILayout.Height(34f))) Begin();
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(report, reportType);
        EditorGUILayout.EndScrollView();
    }

    private void Begin()
    {
        ResetRun();
        if (!Resolve(out string error))
        {
            FailImmediate(error);
            return;
        }
        saveService.RefreshExtensions();
        if (!saveService.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId))
        {
            FailImmediate("SaveGameService no registra supplier.integrated.runtime.");
            return;
        }
        if (!provider.TryCaptureIntegratedState(out originalState, out error))
        {
            FailImmediate("No se pudo capturar estado inicial: " + error);
            return;
        }
        originalFingerprint = originalState.BuildFingerprint();
        capturedErrors = 0;
        diagnosticSlot = FindFreeSlot();
        if (diagnosticSlot < 0)
        {
            FailImmediate("Slots diagnósticos 980-989 ocupados.");
            return;
        }

        Subscribe();
        phase = Phase.Saving;
        reportType = MessageType.Info;
        report = "Guardando slot " + diagnosticSlot + " · fingerprint " + originalFingerprint + "...";
        Repaint();
        if (!saveService.TrySaveSlot(diagnosticSlot, "BB 2.3JKL DIAGNOSTIC", out string rejection))
            FailAndCleanup("Guardado rechazado: " + rejection);
    }

    private void OnOperationCompleted(BistroBuilderSaveOperationResult result)
    {
        if (result == null || result.SlotIndex != diagnosticSlot) return;
        if (!result.Succeeded)
        {
            if (phase == Phase.DeletingSuccess)
            {
                FailImmediate("Estado restaurado, pero no se pudo eliminar slot diagnóstico: " + result.Message);
                return;
            }
            if (phase == Phase.DeletingFailure)
            {
                CompleteFailure(pendingFailure + " Además, no se pudo eliminar slot: " + result.Message);
                return;
            }
            FailAndCleanup(result.OperationKind + " falló: " + result.Message);
            return;
        }

        if (phase == Phase.Saving) ContinueAfterSave(result);
        else if (phase == Phase.Loading) ContinueAfterLoad(result);
        else if (phase == Phase.DeletingSuccess) CompleteSuccess();
        else if (phase == Phase.DeletingFailure) CompleteFailure(pendingFailure);
    }

    private void ContinueAfterSave(BistroBuilderSaveOperationResult result)
    {
        if (!Resolve(out string error))
        {
            FailAndCleanup("No se pudieron resolver autoridades para la mutación post-Save: " + error);
            return;
        }

        List<BistroBuilderSupplierAccessEvaluation> access =
            new List<BistroBuilderSupplierAccessEvaluation>();
        progressionService.CopySupplierAccess(access, true);
        string supplierId = string.Empty;
        for (int i = 0; i < access.Count; i++)
        {
            if (access[i] != null && access[i].isUnlocked)
            {
                supplierId = access[i].supplierId;
                break;
            }
        }
        if (string.IsNullOrEmpty(supplierId) ||
            !progressionService.TryCreatePlayerDraft(
                supplierId, out BistroBuilderPurchaseOrderRecord draft, out error))
        {
            FailAndCleanup("No se pudo crear una mutación real de PurchaseOrder después del Save: " + error);
            return;
        }

        if (!provider.TryCaptureIntegratedState(out BistroBuilderSupplierIntegratedSaveState mutated, out error))
        {
            FailAndCleanup("No se pudo capturar estado mutado: " + error);
            return;
        }
        mutatedFingerprint = mutated.BuildFingerprint();
        if (mutatedFingerprint == originalFingerprint ||
            !orderService.TryGetOrder(draft.purchaseOrderId, out BistroBuilderPurchaseOrderRecord storedDraft) ||
            storedDraft == null || storedDraft.status != BistroBuilderPurchaseOrderStatus.Draft)
        {
            FailAndCleanup("El Draft post-Save no produjo una mutación significativa del estado integrado.");
            return;
        }

        phase = Phase.Loading;
        report = "Checkpoint guardado (" + result.PayloadBytes + " bytes). Draft real " +
                 draft.displayCode + " creado después del Save · fingerprint " +
                 mutatedFingerprint + ". Cargando checkpoint...";
        Repaint();
        if (!saveService.TryLoadSlot(diagnosticSlot, out string rejection))
            FailAndCleanup("Carga rechazada: " + rejection);
    }

    private void ContinueAfterLoad(BistroBuilderSaveOperationResult result)
    {
        if (!Resolve(out string error))
        {
            FailAndCleanup("Dependencias ausentes tras Load: " + error);
            return;
        }
        if (!provider.TryCaptureIntegratedState(out BistroBuilderSupplierIntegratedSaveState loaded, out error))
        {
            FailAndCleanup("No se pudo capturar estado cargado: " + error);
            return;
        }
        string loadedFingerprint = loaded.BuildFingerprint();
        if (loadedFingerprint != originalFingerprint)
        {
            FailAndCleanup(
                "El estado cargado no coincide con el checkpoint. Esperado=" + originalFingerprint +
                " actual=" + loadedFingerprint + ".");
            return;
        }
        if (capturedErrors > 0)
        {
            FailAndCleanup("Save/Load 2.3JKL-A capturó " + capturedErrors +
                           " Error/Exception/Assert.");
            return;
        }
        if (loaded.market.currentGameDay != originalState.market.currentGameDay ||
            loaded.commercial.commercialRevision != originalState.commercial.commercialRevision ||
            loaded.orders.ordersRevision != originalState.orders.ordersRevision ||
            loaded.logistics.logisticsRevision != originalState.logistics.logisticsRevision ||
            loaded.deliveryPresentation.presentationRevision != originalState.deliveryPresentation.presentationRevision ||
            loaded.progression.progressionRevision != originalState.progression.progressionRevision)
        {
            FailAndCleanup("Alguna revisión interna C/D/E/G/H/I no volvió al checkpoint.");
            return;
        }

        phase = Phase.DeletingSuccess;
        report = "Load correcto y fingerprint restaurado. Eliminando slot diagnóstico...";
        Repaint();
        if (!saveService.TryDeleteSlot(diagnosticSlot, out string rejection))
        {
            FailImmediate("Persistencia validada, pero no se pudo eliminar slot: " + rejection);
        }
    }

    private bool Resolve(out string error)
    {
        error = string.Empty;
        saveService = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSaveGameService>();
        provider = UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierIntegratedSaveSectionProvider>();
        orderService = BistroBuilderSupplierPurchaseOrderService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierPurchaseOrderService>();
        progressionService = BistroBuilderSupplierProgressionService.Instance ??
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderSupplierProgressionService>();
        if (saveService == null) error = "Falta SaveGameService.";
        else if (provider == null) error = "Falta provider 2.3J.";
        else if (orderService == null || !orderService.IsInitialized) error = "2.3E no inicializado.";
        else if (progressionService == null || !progressionService.IsInitialized) error = "2.3I no inicializado.";
        return string.IsNullOrEmpty(error);
    }

    private int FindFreeSlot()
    {
        for (int slot = 980; slot <= 989; slot++)
            if (!saveService.SlotExists(slot)) return slot;
        return -1;
    }

    private void FailAndCleanup(string message)
    {
        pendingFailure = message;
        if (provider != null && originalState != null)
        {
            if (!provider.TryRestoreIntegratedState(originalState, out string restoreError))
                pendingFailure += " Restauración local falló: " + restoreError;
        }
        if (saveService != null && !saveService.IsBusy && diagnosticSlot >= 0 && saveService.SlotExists(diagnosticSlot))
        {
            phase = Phase.DeletingFailure;
            reportType = MessageType.Error;
            report = "Fallo detectado. Restaurando estado inicial y eliminando slot...\n" + pendingFailure;
            Repaint();
            if (saveService.TryDeleteSlot(diagnosticSlot, out string rejection)) return;
            pendingFailure += " No se pudo eliminar slot: " + rejection;
        }
        CompleteFailure(pendingFailure);
    }

    private void FailImmediate(string message)
    {
        CompleteFailure(message);
    }

    private void CompleteSuccess()
    {
        phase = Phase.Completed;
        reportType = MessageType.Info;
        report =
            "PRUEBA FUNCIONAL 2.3JKL-A SUPERADA\n\n" +
            "- supplier.integrated.runtime escrito por SaveGameService real.\n" +
            "- Fingerprint checkpoint: " + originalFingerprint + "\n" +
            "- Fingerprint mutado por Draft post-Save: " + mutatedFingerprint + "\n" +
            "- Carga elimina la mutación y restaura C/D/E/G/H/I exactamente al checkpoint.\n" +
            "- Error/Exception/Assert capturados: 0.\n" +
            "- Slot diagnóstico eliminado.";
        Unsubscribe();
        Debug.Log(report);
        Repaint();
    }

    private void CompleteFailure(string message)
    {
        phase = Phase.Failed;
        reportType = MessageType.Error;
        report = "PRUEBA FUNCIONAL 2.3JKL-A FALLIDA\n\n" + message;
        Unsubscribe();
        Debug.LogError(report);
        Repaint();
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (phase == Phase.Idle || phase == Phase.Completed || phase == Phase.Failed) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) capturedErrors++;
    }

    private void Subscribe()
    {
        if (subscribed || saveService == null) return;
        saveService.OperationCompleted += OnOperationCompleted;
        Application.logMessageReceived += HandleLog;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (subscribed && saveService != null) saveService.OperationCompleted -= OnOperationCompleted;
        if (subscribed) Application.logMessageReceived -= HandleLog;
        subscribed = false;
    }

    private void ResetRun()
    {
        Unsubscribe();
        phase = Phase.Idle;
        originalState = null;
        originalFingerprint = string.Empty;
        mutatedFingerprint = string.Empty;
        pendingFailure = string.Empty;
        diagnosticSlot = -1;
        capturedErrors = 0;
    }

    private void OnDisable()
    {
        Unsubscribe();
    }
}
