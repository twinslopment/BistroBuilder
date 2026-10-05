using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BistroBuilder.Editor.Savic
{
    // Shared interactive boundary around existing real acceptance runners.
    // Scene state and selected identity survive Play Mode/domain reload.
    [InitializeOnLoad]
    internal static class SavicRuntimeVerificationSession
    {
        private const string Key = "SAVIC.EditorVerification.";
        [Serializable] private sealed class SavedScenes { public SavedScene[] scenes; }
        [Serializable] private sealed class SavedScene { public string path; public bool loaded, active; }
        internal static bool IsActive => SessionState.GetBool(Key + "Active", false);
        internal static string SelectedId => IsActive ? SessionState.GetString(Key + "Id", "") : "";
        internal static event Action Changed;
        internal static string LastId => SessionState.GetString(Key + "ResultId", "");
        internal static string LastResult => SessionState.GetString(Key + "Result", "");
        internal static bool LastSucceeded => SessionState.GetBool(Key + "Succeeded", false);

        static SavicRuntimeVerificationSession()
        {
            EditorApplication.delayCall += RecoverInterrupted;
        }

        internal static bool CanStartEditorAction(out string reason)
        {
            reason = "";
            if (IsActive || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            { reason = "Espera a que termine la operación actual de Unity."; return false; }
            return true;
        }

        internal static void RequireSavedScenes()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty)))
                throw new InvalidOperationException("Guarda las escenas abiertas antes de actualizar o verificar un asset.");
        }

        internal static void Run(string id)
        {
            if (!CanStartEditorAction(out string reason)) throw new InvalidOperationException(reason);
            var context = SavicEditorContext.Instance;
            if (!context.Manifests.TryGetBySavicId(id, out var m) || !SavicFunctionalRuntimeAcceptance.Required(m))
                throw new InvalidOperationException("Este asset no necesita la verificación funcional adicional.");
            new SavicEditorActionService(context.Layout, context.Manifests, context.Jobs).VerifySource(m);
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (setup.Any(s => s.isLoaded && (string.IsNullOrEmpty(s.path) || SceneManager.GetSceneByPath(s.path).isDirty)))
                throw new InvalidOperationException("Guarda las escenas abiertas antes de verificar el asset. La prueba volverá a abrirlas al terminar.");
            SessionState.SetString(Key + "Scenes", JsonUtility.ToJson(new SavedScenes { scenes = setup.Select(s => new SavedScene
                { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray() }));
            SessionState.SetString(Key + "Id", id);
            SessionState.EraseString(Key + "Result"); SessionState.SetBool(Key + "Succeeded", false);
            SessionState.SetBool(Key + "Paused", context.Jobs.IsPaused);
            context.Jobs.SetPaused(true);
            SessionState.SetBool(Key + "Active", true);
            try
            {
                if (m.type == "BarCounter") SavicPublishedTableRuntimePlaytest.RunSelectedBar(id, m.status != "PUBLISHED");
                else if (m.type == "BarStool") SavicCustomerBarSeatVisualPlaytest.RunSelected(m.status == "PUBLISHED");
                else if (SavicOverheadEquipmentRuntimeAcceptance.Required(m)) SavicOverheadEquipmentRuntimePlaytest.RunSelected(id, m.status != "PUBLISHED");
                else throw new InvalidOperationException("No existe un verificador aprobado para esta familia.");
            }
            catch { Complete(false, "No se pudo iniciar la verificación."); throw; }
            Changed?.Invoke();
        }

        internal static void Complete(bool success, string message)
        {
            if (!IsActive) return;
            string id = SelectedId;
            SessionState.SetBool(Key + "Active", false);
            var context = SavicEditorContext.Instance;
            try
            {
                try
                {
                    var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + "Scenes", ""));
                    if (saved?.scenes?.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s => new SceneSetup
                        { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
                }
                catch (Exception error) { success = false; message = "No se pudo restaurar la escena: " + error.Message; }
                context.Jobs.SetPaused(SessionState.GetBool(Key + "Paused", false));
                new SavicSourceUpdateService(context).FinishVerification(id, success, message);
                if (success)
                {
                    if (!context.Manifests.TryGetBySavicId(id, out var m) || !SavicFunctionalRuntimeAcceptance.Matches(m, context.Layout))
                        throw new InvalidOperationException("La prueba no corresponde a la fuente y al prefab actuales.");
                    new SavicEditorActionService(context.Layout, context.Manifests, context.Jobs).VerifySource(m);
                    if (m.status != "PUBLISHED") context.Jobs.RequeueVerifiedAsset(m);
                    message = m.status == "PUBLISHED" ? "Verificación aprobada. Asset publicado." : "Verificación aprobada. Publicación en cola canónica.";
                }
            }
            catch (Exception error)
            {
                success = false; message = error.Message;
                try { new SavicSourceUpdateService(context).FinishVerification(id, false, message); }
                catch (Exception rollbackError) { message += " Recuperación pendiente: " + rollbackError.Message; }
            }
            finally
            {
                context.Jobs.SetPaused(SessionState.GetBool(Key + "Paused", false));
                SessionState.SetString(Key + "ResultId", id);
                SessionState.SetBool(Key + "Succeeded", success);
                SessionState.SetString(Key + "Result", success ? message : "Verificación detenida: " + message);
                SessionState.EraseString(Key + "Id");
                Changed?.Invoke();
            }
        }
        private static void RecoverInterrupted()
        {
            // An active runner retains its own checkpoint. A cold Editor launch
            // does not retain SessionState, so no previous proof is fabricated.
            if (IsActive && !EditorApplication.isPlayingOrWillChangePlaymode &&
                string.IsNullOrEmpty(SessionState.GetString("SAVIC.RuntimeTable.Stage", "")) &&
                string.IsNullOrEmpty(SessionState.GetString("SAVIC.OverheadRuntime.Stage", "")) &&
                !SessionState.GetBool("SAVIC.CustomerBarSeatVisual.Active", false))
                Complete(false, "La verificación se interrumpió; vuelve a intentarla.");
        }
    }
}
