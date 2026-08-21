using System;
using BistroBuilder.LivingArchitecture.Domain;
using UnityEngine;

namespace BistroBuilder.LivingArchitecture.Runtime
{
    /// <summary>
    /// Fachada runtime LA9/LA10 para conectar UI/input con la sesión pura de edición.
    /// Solo sincroniza estado canónico tras confirmación/Undo/Redo; la preview y el feedback nunca sustituyen la autoridad.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArchitectureEditToolController : MonoBehaviour
    {
        [SerializeField] private ArchitectureStateService stateService;
        [SerializeField] private ArchitectureRuntimePresenter runtimePresenter;

        private ArchitectureEditSession session;
        private readonly ArchitectureEditFeedbackService feedbackService = new ArchitectureEditFeedbackService();

        public ArchitectureEditSession Session => session;
        public ArchitectureSnapshot VisibleSnapshot => session?.CaptureVisible();
        public bool HasPreview => session != null && session.HasPreview;
        public bool CanConfirm => session != null && session.CanConfirm;
        public ArchitectureEditFeedbackFrame FeedbackFrame { get; private set; }

        public event Action Changed;
        public event Action<ArchitectureEditFeedbackFrame> FeedbackChanged;

        public void EnsureInitialized()
        {
            if (session != null) return;
            if (stateService == null) stateService = GetComponent<ArchitectureStateService>();
            if (runtimePresenter == null) runtimePresenter = GetComponent<ArchitectureRuntimePresenter>();
            if (stateService == null) throw new InvalidOperationException("LA9_STATE_SERVICE_REQUIRED");
            stateService.EnsureInitialized();
            session = new ArchitectureEditSession(new ArchitectureSnapshot { Building = stateService.CaptureClone() });
            RebuildVisible();
            RefreshFeedback();
        }

        public void ReloadFromCanonicalState()
        {
            if (stateService == null) stateService = GetComponent<ArchitectureStateService>();
            if (stateService == null) throw new InvalidOperationException("LA9_STATE_SERVICE_REQUIRED");
            stateService.EnsureInitialized();
            session = new ArchitectureEditSession(new ArchitectureSnapshot { Building = stateService.CaptureClone() });
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
        }

        public bool SelectWall(WallId wallId)
        {
            EnsureInitialized();
            var result = session.SelectWall(wallId);
            RefreshFeedback();
            Changed?.Invoke();
            return result;
        }

        public bool SelectVertex(VertexId vertexId)
        {
            EnsureInitialized();
            var result = session.SelectVertex(vertexId);
            RefreshFeedback();
            Changed?.Invoke();
            return result;
        }

        public ArchitectureOperationProposal PreviewCreateWall(LevelId levelId, ArchitecturePoint start, ArchitecturePoint end, double thickness = 0.15d, double height = 2.8d)
        {
            EnsureInitialized();
            var proposal = session.PreviewCreateWall(levelId, start, end, thickness, height, true);
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
            return proposal;
        }

        public ArchitectureOperationProposal PreviewMoveWall(WallId wallId, double deltaX, double deltaY)
        {
            EnsureInitialized();
            var proposal = session.PreviewMoveWall(wallId, deltaX, deltaY);
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
            return proposal;
        }

        public ArchitectureOperationProposal PreviewMoveVertex(VertexId vertexId, ArchitecturePoint target)
        {
            EnsureInitialized();
            var proposal = session.PreviewMoveVertex(vertexId, target, true);
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
            return proposal;
        }

        public ArchitectureOperationProposal PreviewSetSelectedWallLength(double targetLength, bool preserveStart = true)
        {
            EnsureInitialized();
            if (!ArchitectureId.IsValid(session.SelectedWallId.Value)) return null;
            var proposal = session.PreviewSetWallLength(session.SelectedWallId, targetLength, preserveStart);
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
            return proposal;
        }

        public ArchitectureOperationProposal PreviewDeleteSelectedWall()
        {
            EnsureInitialized();
            var proposal = session.PreviewDeleteSelectedWall();
            RebuildVisible();
            RefreshFeedback();
            Changed?.Invoke();
            return proposal;
        }

        public bool TryConfirm(out string diagnosticCode)
        {
            EnsureInitialized();
            var operationId = session.Preview?.Operation?.Id.Value;
            if (!session.TryConfirm(out diagnosticCode))
            {
                RebuildVisible();
                RefreshFeedback();
                Changed?.Invoke();
                return false;
            }
            if (!PushSessionToCanonical(out diagnosticCode))
            {
                ReloadFromCanonicalState();
                return false;
            }
            RebuildVisible();
            PublishTransient(ArchitectureFeedbackCueKind.CommitPulse, operationId, "Reforma confirmada.");
            Changed?.Invoke();
            return true;
        }

        public void CancelPreview()
        {
            EnsureInitialized();
            var operationId = session.Preview?.Operation?.Id.Value;
            session.CancelPreview();
            RebuildVisible();
            PublishTransient(ArchitectureFeedbackCueKind.CancelFade, operationId, "Previsualización cancelada.");
            Changed?.Invoke();
        }

        public bool TryUndo(out string diagnosticCode)
        {
            EnsureInitialized();
            if (!session.TryUndo(out diagnosticCode)) return false;
            if (!PushSessionToCanonical(out diagnosticCode)) { ReloadFromCanonicalState(); return false; }
            RebuildVisible();
            PublishTransient(ArchitectureFeedbackCueKind.UndoPulse, null, "Reforma deshecha.");
            Changed?.Invoke();
            return true;
        }

        public bool TryRedo(out string diagnosticCode)
        {
            EnsureInitialized();
            if (!session.TryRedo(out diagnosticCode)) return false;
            if (!PushSessionToCanonical(out diagnosticCode)) { ReloadFromCanonicalState(); return false; }
            RebuildVisible();
            PublishTransient(ArchitectureFeedbackCueKind.RedoPulse, null, "Reforma rehecha.");
            Changed?.Invoke();
            return true;
        }

        private bool PushSessionToCanonical(out string diagnosticCode)
        {
            var snapshot = session.CaptureCurrent();
            return stateService.TryReplace(snapshot.Building, out diagnosticCode);
        }

        private void RebuildVisible()
        {
            if (runtimePresenter == null || session == null) return;
            runtimePresenter.Rebuild(session.CaptureVisible().Building);
        }

        private void RefreshFeedback()
        {
            FeedbackFrame = feedbackService.Build(session?.Preview, session?.PreviewImpact, session?.LastSnapCandidates);
            FeedbackChanged?.Invoke(FeedbackFrame);
        }

        private void PublishTransient(ArchitectureFeedbackCueKind kind, string operationId, string message)
        {
            FeedbackFrame = feedbackService.BuildTransient(kind, operationId, message);
            FeedbackChanged?.Invoke(FeedbackFrame);
        }

        private void Awake()
        {
            EnsureInitialized();
        }
    }
}
