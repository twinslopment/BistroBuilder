using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureFeedbackState
    {
        Idle = 0,
        Valid = 1,
        Warning = 2,
        Invalid = 3
    }

    public enum ArchitectureFeedbackCueKind
    {
        PreviewGhost = 0,
        SnapPoint = 1,
        SnapGuide = 2,
        Impact = 3,
        CorrectionHint = 4,
        CommitPulse = 5,
        CancelFade = 6,
        UndoPulse = 7,
        RedoPulse = 8
    }

    [Serializable]
    public sealed class ArchitectureFeedbackCue
    {
        public string Key;
        public ArchitectureFeedbackCueKind Kind;
        public ArchitectureFeedbackState State;
        public bool HasPosition;
        public ArchitecturePoint Position;
        public string EntityId;
        public string Message;
        public ArchitectureSnapConfidence? SnapConfidence;
        public double DurationSeconds;
    }

    /// <summary>
    /// Fotograma declarativo del Sistema Universal de Feedback del Modo Edición.
    /// Describe qué debe presentar la capa visual/sonora, pero nunca modifica arquitectura.
    /// </summary>
    public sealed class ArchitectureEditFeedbackFrame
    {
        public ArchitectureFeedbackState State;
        public bool HasPreview;
        public bool CanConfirm;
        public string DiagnosticCode;
        public string PrimaryMessage;
        public string ProposedFingerprint;
        public readonly List<ArchitectureFeedbackCue> Cues = new List<ArchitectureFeedbackCue>();
    }

    /// <summary>
    /// LA10 — traduce proposal + impacto + snaps a feedback universal determinista.
    /// Es deliberadamente puro: no decide construcción, no hace commit, no mueve objetos y no toca snapshots.
    /// </summary>
    public sealed class ArchitectureEditFeedbackService
    {
        private readonly int maxSnapCues;

        public ArchitectureEditFeedbackService(int maxSnapCues = 8)
        {
            this.maxSnapCues = Math.Max(0, maxSnapCues);
        }

        public ArchitectureEditFeedbackFrame Build(
            ArchitectureOperationProposal proposal,
            ArchitectureImpactReport impact,
            IEnumerable<ArchitectureSnapCandidate> snapCandidates)
        {
            var frame = new ArchitectureEditFeedbackFrame();
            if (proposal == null)
            {
                frame.State = ArchitectureFeedbackState.Idle;
                frame.PrimaryMessage = string.Empty;
                return frame;
            }

            frame.HasPreview = true;
            frame.DiagnosticCode = proposal.DiagnosticCode;
            frame.ProposedFingerprint = proposal.ProposedFingerprint;

            if (!proposal.IsReady)
            {
                frame.State = ArchitectureFeedbackState.Invalid;
                frame.CanConfirm = false;
                frame.PrimaryMessage = string.IsNullOrWhiteSpace(proposal.DiagnosticMessage)
                    ? "La operación no es válida."
                    : proposal.DiagnosticMessage;
            }
            else if (impact != null && impact.HasBlockingIssues)
            {
                frame.State = ArchitectureFeedbackState.Invalid;
                frame.CanConfirm = false;
                frame.PrimaryMessage = FirstImpactMessage(impact, ArchitectureImpactSeverity.Blocking)
                    ?? FirstImpactMessage(impact, ArchitectureImpactSeverity.SystemError)
                    ?? "La reforma tiene un impacto bloqueante.";
            }
            else if (impact != null && impact.WarningCount > 0)
            {
                frame.State = ArchitectureFeedbackState.Warning;
                frame.CanConfirm = true;
                frame.PrimaryMessage = FirstImpactMessage(impact, ArchitectureImpactSeverity.Warning)
                    ?? "La reforma es válida, pero tiene consecuencias a revisar.";
            }
            else
            {
                frame.State = ArchitectureFeedbackState.Valid;
                frame.CanConfirm = true;
                frame.PrimaryMessage = "Operación válida.";
            }

            frame.Cues.Add(new ArchitectureFeedbackCue
            {
                Key = "preview:" + (proposal.Operation?.Id.Value ?? "unknown"),
                Kind = ArchitectureFeedbackCueKind.PreviewGhost,
                State = frame.State,
                Message = frame.PrimaryMessage
            });

            AddSnapCues(frame, snapCandidates);
            AddImpactCues(frame, impact);
            return frame;
        }

        public ArchitectureEditFeedbackFrame BuildTransient(
            ArchitectureFeedbackCueKind kind,
            string operationId,
            string message,
            double durationSeconds = 0.22d)
        {
            var state = ArchitectureFeedbackState.Valid;
            return new ArchitectureEditFeedbackFrame
            {
                State = state,
                HasPreview = false,
                CanConfirm = false,
                PrimaryMessage = message ?? string.Empty,
                ProposedFingerprint = string.Empty,
                Cues =
                {
                    new ArchitectureFeedbackCue
                    {
                        Key = "transient:" + kind + ":" + (operationId ?? "none"),
                        Kind = kind,
                        State = state,
                        Message = message ?? string.Empty,
                        DurationSeconds = Math.Max(0d, durationSeconds)
                    }
                }
            };
        }

        private void AddSnapCues(ArchitectureEditFeedbackFrame frame, IEnumerable<ArchitectureSnapCandidate> candidates)
        {
            if (candidates == null || maxSnapCues <= 0) return;
            var ordered = candidates
                .Where(x => x != null)
                .Take(maxSnapCues)
                .ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                var candidate = ordered[i];
                frame.Cues.Add(new ArchitectureFeedbackCue
                {
                    Key = "snap:" + i + ":" + candidate.ReasonCode + ":" + candidate.SourceEntityId,
                    Kind = candidate.Type == ArchitectureSnapType.Vertex || candidate.Type == ArchitectureSnapType.WallProjection
                        ? ArchitectureFeedbackCueKind.SnapPoint
                        : ArchitectureFeedbackCueKind.SnapGuide,
                    State = ArchitectureFeedbackState.Valid,
                    HasPosition = true,
                    Position = candidate.SnappedPoint,
                    EntityId = candidate.SourceEntityId,
                    Message = candidate.ReasonCode,
                    SnapConfidence = candidate.Confidence
                });
            }
        }

        private static void AddImpactCues(ArchitectureEditFeedbackFrame frame, ArchitectureImpactReport impact)
        {
            if (impact?.Issues == null) return;
            var index = 0;
            foreach (var issue in impact.Issues.Where(x => x != null))
            {
                var state = issue.Severity == ArchitectureImpactSeverity.Warning
                    ? ArchitectureFeedbackState.Warning
                    : issue.Severity == ArchitectureImpactSeverity.Info
                        ? ArchitectureFeedbackState.Valid
                        : ArchitectureFeedbackState.Invalid;

                frame.Cues.Add(new ArchitectureFeedbackCue
                {
                    Key = "impact:" + index + ":" + issue.ReasonCode + ":" + issue.EntityId,
                    Kind = ArchitectureFeedbackCueKind.Impact,
                    State = state,
                    EntityId = issue.EntityId,
                    Message = issue.HumanMessage
                });

                if (issue.SuggestedDelta != null)
                {
                    frame.Cues.Add(new ArchitectureFeedbackCue
                    {
                        Key = "correction:" + index + ":" + issue.ReasonCode,
                        Kind = ArchitectureFeedbackCueKind.CorrectionHint,
                        State = state,
                        EntityId = issue.EntityId,
                        Message = issue.SuggestedDelta.Explanation
                    });
                }
                index++;
            }
        }

        private static string FirstImpactMessage(ArchitectureImpactReport report, ArchitectureImpactSeverity severity)
        {
            return report?.Issues?
                .FirstOrDefault(x => x != null && x.Severity == severity && !string.IsNullOrWhiteSpace(x.HumanMessage))?
                .HumanMessage;
        }
    }
}
