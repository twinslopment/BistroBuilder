using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Self-test puro LA10 del traductor universal de feedback.
    /// </summary>
    public static class ArchitectureEditFeedbackSelfTest
    {
        public static IReadOnlyList<string> Run()
        {
            var failures = new List<string>();
            RunCase(failures, "01_idle_without_proposal", IdleWithoutProposal);
            RunCase(failures, "02_ready_proposal_is_valid", ReadyProposalIsValid);
            RunCase(failures, "03_rejected_proposal_is_invalid", RejectedProposalIsInvalid);
            RunCase(failures, "04_warning_remains_confirmable", WarningRemainsConfirmable);
            RunCase(failures, "05_blocking_disables_confirm", BlockingDisablesConfirm);
            RunCase(failures, "06_snap_candidates_create_positioned_cues", SnapCandidatesCreatePositionedCues);
            RunCase(failures, "07_snap_cues_are_bounded", SnapCuesAreBounded);
            RunCase(failures, "08_correction_hint_is_exposed", CorrectionHintIsExposed);
            RunCase(failures, "09_transient_commit_cue", TransientCommitCue);
            RunCase(failures, "10_service_does_not_mutate_inputs", ServiceDoesNotMutateInputs);
            return failures;
        }

        private static void IdleWithoutProposal()
        {
            var frame = new ArchitectureEditFeedbackService().Build(null, null, null);
            Require(frame.State == ArchitectureFeedbackState.Idle, "idle state expected");
            Require(!frame.HasPreview && !frame.CanConfirm, "idle flags invalid");
        }

        private static void ReadyProposalIsValid()
        {
            var proposal = ReadyProposal();
            var frame = new ArchitectureEditFeedbackService().Build(proposal, new ArchitectureImpactReport(), null);
            Require(frame.State == ArchitectureFeedbackState.Valid, "valid state expected");
            Require(frame.CanConfirm, "valid proposal should confirm");
            Require(frame.Cues.Any(x => x.Kind == ArchitectureFeedbackCueKind.PreviewGhost), "preview cue missing");
        }

        private static void RejectedProposalIsInvalid()
        {
            var proposal = new ArchitectureOperationProposal
            {
                Operation = Descriptor(),
                Status = ArchitectureProposalStatus.Rejected,
                DiagnosticCode = "TEST_REJECTED",
                DiagnosticMessage = "Rejected by test"
            };
            var frame = new ArchitectureEditFeedbackService().Build(proposal, null, null);
            Require(frame.State == ArchitectureFeedbackState.Invalid, "invalid state expected");
            Require(!frame.CanConfirm, "rejected proposal must not confirm");
            Require(frame.DiagnosticCode == "TEST_REJECTED", "diagnostic lost");
        }

        private static void WarningRemainsConfirmable()
        {
            var impact = new ArchitectureImpactReport();
            impact.Issues.Add(new ArchitectureImpactIssue
            {
                Severity = ArchitectureImpactSeverity.Warning,
                ReasonCode = "TEST_WARNING",
                HumanMessage = "Warning"
            });
            var frame = new ArchitectureEditFeedbackService().Build(ReadyProposal(), impact, null);
            Require(frame.State == ArchitectureFeedbackState.Warning, "warning state expected");
            Require(frame.CanConfirm, "warning should remain confirmable");
        }

        private static void BlockingDisablesConfirm()
        {
            var impact = new ArchitectureImpactReport();
            impact.Issues.Add(new ArchitectureImpactIssue
            {
                Severity = ArchitectureImpactSeverity.Blocking,
                ReasonCode = "TEST_BLOCK",
                HumanMessage = "Blocking"
            });
            var frame = new ArchitectureEditFeedbackService().Build(ReadyProposal(), impact, null);
            Require(frame.State == ArchitectureFeedbackState.Invalid, "blocking should be invalid feedback");
            Require(!frame.CanConfirm, "blocking should disable confirm");
        }

        private static void SnapCandidatesCreatePositionedCues()
        {
            var snaps = new[]
            {
                new ArchitectureSnapCandidate
                {
                    Type = ArchitectureSnapType.Vertex,
                    Confidence = ArchitectureSnapConfidence.High,
                    SnappedPoint = new ArchitecturePoint(2, 3),
                    ReasonCode = "SNAP_VERTEX",
                    SourceEntityId = "v1"
                }
            };
            var frame = new ArchitectureEditFeedbackService().Build(ReadyProposal(), null, snaps);
            var cue = frame.Cues.FirstOrDefault(x => x.Kind == ArchitectureFeedbackCueKind.SnapPoint);
            Require(cue != null && cue.HasPosition, "positioned snap cue missing");
            Require(cue.Position.Equals(new ArchitecturePoint(2, 3)), "snap position changed");
            Require(cue.SnapConfidence == ArchitectureSnapConfidence.High, "confidence lost");
        }

        private static void SnapCuesAreBounded()
        {
            var snaps = Enumerable.Range(0, 20).Select(i => new ArchitectureSnapCandidate
            {
                Type = ArchitectureSnapType.Vertex,
                Confidence = ArchitectureSnapConfidence.Low,
                SnappedPoint = new ArchitecturePoint(i, 0),
                ReasonCode = "SNAP_VERTEX",
                SourceEntityId = "v" + i
            });
            var frame = new ArchitectureEditFeedbackService(3).Build(ReadyProposal(), null, snaps);
            Require(frame.Cues.Count(x => x.Kind == ArchitectureFeedbackCueKind.SnapPoint) == 3, "snap cue bound ignored");
        }

        private static void CorrectionHintIsExposed()
        {
            var impact = new ArchitectureImpactReport();
            impact.Issues.Add(new ArchitectureImpactIssue
            {
                Severity = ArchitectureImpactSeverity.Warning,
                ReasonCode = "CLEARANCE",
                HumanMessage = "Need clearance",
                SuggestedDelta = new ArchitectureSuggestedDelta
                {
                    DeltaX = 0.12,
                    DeltaY = 0,
                    Unit = "m",
                    Explanation = "+12 cm resolvería el conflicto"
                }
            });
            var frame = new ArchitectureEditFeedbackService().Build(ReadyProposal(), impact, null);
            Require(frame.Cues.Any(x => x.Kind == ArchitectureFeedbackCueKind.CorrectionHint && x.Message.Contains("12 cm")), "correction hint missing");
        }

        private static void TransientCommitCue()
        {
            var frame = new ArchitectureEditFeedbackService().BuildTransient(
                ArchitectureFeedbackCueKind.CommitPulse,
                "op1",
                "Committed");
            Require(frame.Cues.Count == 1, "transient cue count invalid");
            Require(frame.Cues[0].Kind == ArchitectureFeedbackCueKind.CommitPulse, "commit cue missing");
            Require(!frame.HasPreview, "transient must not claim preview authority");
        }

        private static void ServiceDoesNotMutateInputs()
        {
            var proposal = ReadyProposal();
            var originalStatus = proposal.Status;
            var impact = new ArchitectureImpactReport();
            impact.Issues.Add(new ArchitectureImpactIssue
            {
                Severity = ArchitectureImpactSeverity.Info,
                ReasonCode = "INFO",
                HumanMessage = "Info"
            });
            var beforeCount = impact.Issues.Count;
            new ArchitectureEditFeedbackService().Build(proposal, impact, Array.Empty<ArchitectureSnapCandidate>());
            Require(proposal.Status == originalStatus, "proposal mutated");
            Require(impact.Issues.Count == beforeCount, "impact mutated");
        }

        private static ArchitectureOperationProposal ReadyProposal()
        {
            return new ArchitectureOperationProposal
            {
                Operation = Descriptor(),
                Status = ArchitectureProposalStatus.Ready,
                ProposedSnapshot = new ArchitectureSnapshot(),
                ProposedFingerprint = "test-fingerprint"
            };
        }

        private static ArchitectureOperationDescriptor Descriptor()
        {
            return new ArchitectureOperationDescriptor
            {
                Id = ArchitectureOperationId.New(),
                Kind = ArchitectureOperationKind.CreateWall,
                Label = "Test"
            };
        }

        private static void RunCase(ICollection<string> failures, string name, Action test)
        {
            try { test(); }
            catch (Exception ex) { failures.Add(name + ": " + ex.Message); }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
