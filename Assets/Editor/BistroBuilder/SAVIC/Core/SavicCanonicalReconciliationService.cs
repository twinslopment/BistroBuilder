using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    [Serializable]
    internal sealed class SavicContinuityReport
    {
        public int schemaVersion = 1;
        public string generatedUtc = string.Empty;
        public int readyToReconcile;
        public int alreadyManaged;
        public int sourceMissing;
        public int sourceInvalid;
        public int identityConflict;
        public int processingFailed;
        public List<SavicContinuityRow> rows = new List<SavicContinuityRow>();
    }

    [Serializable]
    internal sealed class SavicContinuityRow
    {
        public string jobId = string.Empty;
        public string savicId = string.Empty;
        public string sourceHash = string.Empty;
        public string fileName = string.Empty;
        public string archivedRelativePath = string.Empty;
        public string status = string.Empty;
        public string detail = string.Empty;
        public bool batchEligible;
    }

    internal sealed class SavicSelectedImportResult
    {
        internal int Restored;
        internal int Ingested;
        internal int Duplicates;
        internal readonly List<string> Errors = new List<string>();
    }

    internal sealed class SavicClassificationRefreshResult
    {
        internal int Updated;
        internal int Queued;
        internal int Skipped;
    }

    // The queue is evidence of an ingest, not a replacement for the archived
    // original or the manifest. This service never synthesizes source bytes.
    internal sealed class SavicCanonicalReconciliationService
    {
        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicJobStore jobs;

        internal SavicCanonicalReconciliationService(
            SavicStorageLayout layout,
            SavicManifestRepository manifests,
            SavicJobStore jobs)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        }

        internal SavicContinuityReport Audit()
        {
            SavicContinuityReport report = new SavicContinuityReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O")
            };

            // A duplicate-exact job must not replace the original pending job.
            foreach (IGrouping<string, SavicJobRecord> group in jobs.Jobs
                         .Where(job => job != null)
                         .GroupBy(job => string.IsNullOrWhiteSpace(job.sourceHash)
                                 ? "job:" + job.jobId
                                 : "hash:" + job.sourceHash,
                             StringComparer.OrdinalIgnoreCase))
            {
                SavicJobRecord job = group.FirstOrDefault(candidate =>
                        string.Equals(candidate.state, SavicJobState.Ingested.ToString(),
                            StringComparison.Ordinal)) ?? group.First();

                SavicContinuityRow row = group
                    .Select(candidate => candidate.manifestSavicId)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Skip(1)
                    .Any()
                    ? Set(NewRow(job), "IDENTITY_CONFLICT",
                        "Multiple SavicId values claim the same SourceHash.")
                    : Inspect(job);
                report.rows.Add(row);
                switch (row.status)
                {
                    case "READY_TO_RECONCILE": report.readyToReconcile++; break;
                    case "ALREADY_MANAGED": report.alreadyManaged++; break;
                    case "SOURCE_MISSING": report.sourceMissing++; break;
                    case "SOURCE_INVALID": report.sourceInvalid++; break;
                    case "PROCESSING_FAILED": report.processingFailed++; break;
                    default: report.identityConflict++; break;
                }
            }

            report.rows.Sort((left, right) =>
                string.Compare(left.fileName, right.fileName, StringComparison.OrdinalIgnoreCase));
            return report;
        }

        internal int ReconcileVerifiedSources()
        {
            int enabled = 0;
            SavicContinuityReport report = Audit();
            foreach (SavicContinuityRow row in report.rows)
            {
                if (!string.Equals(row.status, "READY_TO_RECONCILE", StringComparison.Ordinal))
                    continue;

                // Reinspect immediately before mutation. A changed source cannot
                // be accepted on the strength of an earlier audit snapshot.
                SavicJobRecord job = jobs.Jobs.FirstOrDefault(candidate =>
                    candidate != null &&
                    string.Equals(candidate.jobId, row.jobId, StringComparison.Ordinal));
                if (job == null || Inspect(job).status != "READY_TO_RECONCILE")
                    continue;

                EnsureManifest(job);
                if (jobs.EnableVerifiedLegacyJob(job.jobId, job.manifestSavicId, job.sourceHash))
                    enabled++;
            }

            return enabled;
        }

        internal int RetryVerifiedMirrorFailures()
        {
            int retried = 0;
            foreach (SavicJobRecord job in jobs.Jobs)
            {
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.state, SavicJobState.FailedProcessing.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(job.reasonCode, "SOURCE_MATERIALIZATION_FAILED", StringComparison.Ordinal) ||
                    !string.Equals(job.primaryStage, "MATERIALIZE_SOURCE_MIRROR", StringComparison.Ordinal))
                    continue;

                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId, out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash))
                        continue;

                    if (jobs.RetryVerifiedMirrorFailure(job.jobId, job.manifestSavicId, job.sourceHash))
                        retried++;
                }
                catch (IOException)
                {
                    // A missing or unreadable original is never retried.
                }
                catch (ArgumentException)
                {
                    // A malformed archive identity is never retried.
                }
            }

            return retried;
        }

        internal int RetryObsoletePreImportReviews()
        {
            int retried = 0;
            foreach (SavicJobRecord job in jobs.Jobs)
            {
                if (job == null || !job.batchEligible || job.cancelRequested ||
                    !string.Equals(job.state, SavicJobState.NeedsReview.ToString(), StringComparison.Ordinal) ||
                    !string.Equals(job.reasonCode, "FUNCTIONAL_ADAPTER_REQUIRED", StringComparison.Ordinal) ||
                    !string.Equals(job.primaryStage, "PREIMPORT_ROUTE", StringComparison.Ordinal))
                    continue;

                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId, out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        SavicEquipmentIntegrationPolicy.Resolve(job.originalFileName)
                            .RequiresGameplayAdapter)
                        continue;

                    if (jobs.RetryVerifiedObsoletePreImportReview(
                            job.jobId, job.manifestSavicId, job.sourceHash))
                        retried++;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }

            return retried;
        }

        // Classification can evolve independently of family publication.
        // Re-evaluate only verified, already enabled classification reviews.
        // Unknown or unsupported types remain in review with current evidence;
        // a newly supported type enters the normal pipeline once per version.
        internal SavicClassificationRefreshResult RefreshReviewedClassifications(
            int maximum, Func<string, bool> hasRegisteredFamily)
        {
            if (hasRegisteredFamily == null)
                throw new ArgumentNullException(nameof(hasRegisteredFamily));

            SavicClassificationRefreshResult result =
                new SavicClassificationRefreshResult();
            if (maximum <= 0)
                return result;

            int examined = 0;
            foreach (SavicJobRecord job in jobs.Jobs.Where(candidate =>
                         candidate != null && candidate.batchEligible &&
                         !candidate.cancelRequested &&
                         candidate.state == SavicJobState.NeedsReview.ToString()))
            {
                if (examined++ >= maximum)
                    break;

                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId,
                            out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        manifest.model3D == null ||
                        !manifest.model3D.analyzed ||
                        !manifest.model3D.hasUsableBounds)
                    {
                        result.Skipped++;
                        continue;
                    }

                    bool classificationCurrent = manifest.classification != null &&
                        string.Equals(manifest.classification.classifierVersion,
                            SavicContentClassifier.Version, StringComparison.Ordinal);
                    SavicClassificationRecord classification =
                        classificationCurrent
                            ? manifest.classification
                            : SavicContentClassifier.Classify(manifest, layout);
                    bool supported = hasRegisteredFamily(classification.type);
                    bool canRetryClassification = job.reasonCode == "UNSUPPORTED_PUBLICATION_FAMILY" &&
                        job.primaryStage == "CLASSIFICATION";
                    if (classificationCurrent && (!supported || !canRetryClassification))
                        continue;
                    if (!HashMatches(CanonicalArchivePath(job), job.sourceHash))
                    {
                        result.Skipped++;
                        continue;
                    }
                    if (supported && canRetryClassification &&
                        !jobs.RetryVerifiedClassificationReview(job.jobId,
                            job.manifestSavicId, job.sourceHash,
                            SavicContentClassifier.Version))
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (classificationCurrent)
                    {
                        result.Queued++;
                        continue;
                    }

                    manifest.classification = classification;
                    manifest.family = classification.family;
                    manifest.type = classification.type;
                    manifest.category = classification.category;
                    SavicManifestMutations.UpsertDecision(manifest,
                        "content.type", classification.type,
                        classification.confidence, classification.evidence,
                        "content.classification.v2");
                    SavicManifestMutations.UpsertValidation(manifest,
                        "Classification.ContentType",
                        supported ? "PASS" : "REVIEW",
                        supported ? "INFO" : "WARNING",
                        supported
                            ? canRetryClassification
                                ? "Current classifier matched a registered family; verified source queued for publication."
                                : "Current classifier identified a registered family; the existing publication review remains authoritative."
                            : classification.type == "Unknown"
                                ? "Current classifier cannot resolve the content identity from available evidence."
                                : "Content identified as " + classification.type +
                                  "; no safe publication family is registered.",
                        SavicContentClassifier.Version);
                    manifests.Save(manifest);
                    result.Updated++;
                    if (supported && canRetryClassification)
                        result.Queued++;
                }
                catch (IOException) { result.Skipped++; }
                catch (ArgumentException) { result.Skipped++; }
            }

            return result;
        }

        internal int RetryVerifiedBarRuntimeAcceptances(int maximum)
        {
            int queued = 0;
            foreach (SavicJobRecord job in jobs.Jobs.Where(j => j != null && j.batchEligible && !j.cancelRequested &&
                         j.state == SavicJobState.NeedsReview.ToString() && j.reasonCode == "BAR_COUNTER_RUNTIME_ACCEPTANCE_PENDING"))
            {
                if (queued >= maximum) break;
                if (!manifests.TryGetBySavicId(job.manifestSavicId, out var manifest) || !ManifestMatches(manifest, job) ||
                    manifest.type != "BarCounter" || !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                    !SavicBarCounterRuntimeAcceptance.Matches(manifest, layout)) continue;
                string fingerprint = manifest.barCounterRuntime.planFingerprint + ":" + manifest.barCounterRuntime.prefabDependencyHash +
                    ":" + manifest.barCounterRuntime.reportHash;
                if (jobs.RetryVerifiedBarRuntimeAcceptance(job.jobId, job.manifestSavicId, job.sourceHash, fingerprint)) queued++;
            }
            return queued;
        }

        internal int RetryVerifiedBarStoolRuntimeAcceptances(int maximum)
        {
            int queued = 0;
            foreach (var job in jobs.Jobs.Where(j => j != null && j.batchEligible && !j.cancelRequested &&
                j.state == SavicJobState.NeedsReview.ToString() && j.reasonCode == "BAR_STOOL_RUNTIME_ACCEPTANCE_PENDING"))
            {
                if (queued >= maximum) break;
                if (!manifests.TryGetBySavicId(job.manifestSavicId, out var manifest) || !ManifestMatches(manifest, job) ||
                    manifest.type != "BarStool" || !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                    !SavicBarStoolRuntimeAcceptance.Matches(manifest, layout)) continue;
                var proof = manifest.barStoolRuntime;
                string fingerprint = proof.planFingerprint + ":" + proof.prefabDependencyHash + ":" +
                    proof.customerPrefabDependencyHash + ":" + proof.animationCatalogDependencyHash + ":" + proof.reportHash;
                if (jobs.RetryVerifiedBarStoolRuntimeAcceptance(job.jobId, job.manifestSavicId, job.sourceHash, fingerprint)) queued++;
            }
            return queued;
        }

        internal int RetryVerifiedOverheadReviews(int maximum)
        {
            int queued = 0;
            foreach (var job in jobs.Jobs.Where(j => j != null && j.batchEligible && !j.cancelRequested &&
                j.state == SavicJobState.NeedsReview.ToString() && j.primaryStage == "FAMILY_PUBLICATION"))
            {
                if (queued >= maximum) break;
                if (!manifests.TryGetBySavicId(job.manifestSavicId, out var m) || !ManifestMatches(m, job) ||
                    !SavicOverheadEquipmentAuthoringPlanner.IsVerifiedHood(m, layout) || !HashMatches(CanonicalArchivePath(job), job.sourceHash)) continue;
                if (job.reasonCode == "OVERHEAD_RUNTIME_ACCEPTANCE_PENDING")
                {
                    if (!SavicOverheadEquipmentRuntimeAcceptance.Matches(m, layout)) continue;
                    var proof = m.overheadEquipmentRuntime;
                    if (jobs.RetryVerifiedOverheadReview(job.jobId, m.savicId, job.sourceHash,
                        proof.planFingerprint + ":" + proof.prefabDependencyHash + ":" + proof.reportHash, true)) queued++;
                }
                else if (job.reasonCode == "EQUIPMENT_FUNCTION_AMBIGUOUS" || job.reasonCode == "PLACEMENT_OVERHEAD_REQUIRES_ADAPTER" ||
                    job.reasonCode == "OVERHEAD_AUTHORING_REVIEW" || job.reasonCode == "OVERHEAD_COMMON_PLAN_REVIEW")
                {
                    string mirror = layout.GetUnitySourceMirrorPath(job.sourceHash, m.source.originalFileName);
                    if (!HashMatches(mirror, job.sourceHash)) continue;
                    var source = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(layout.ToProjectRelativePath(mirror));
                    if (!SavicOverheadEquipmentAuthoringPlanner.TryPlan(m, source, SavicOverheadEquipmentAuthoringPlanner.GetOrCreateProfile(),
                        out var plan, out _, layout)) continue;
                    if (jobs.RetryVerifiedOverheadReview(job.jobId, m.savicId, job.sourceHash, plan.inputFingerprint, false)) queued++;
                }
            }
            return queued;
        }

        internal int RefreshVerifiedPublishedBarAuthoring(int maximum)
        {
            int queued = 0;
            foreach (var job in jobs.Jobs.Where(j => j != null && j.batchEligible && !j.cancelRequested &&
                         j.state == SavicJobState.Done.ToString() && j.reasonCode == "PUBLISHED"))
            {
                if (queued >= maximum) break;
                if (!manifests.TryGetBySavicId(job.manifestSavicId, out var manifest) || !ManifestMatches(manifest, job) ||
                    manifest.type != "BarCounter" || !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                    !SavicBarCounterFunctionAdapter.PlanMatches(manifest)) continue;
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(manifest.genericPlaceable.prefabAssetPath);
                var adapter = new SavicBarCounterFunctionAdapter();
                if (prefab == null || adapter.Validate(prefab, manifest, out _)) continue;
                if (jobs.RetryVerifiedPublishedBarAuthoring(job.jobId, job.manifestSavicId, job.sourceHash,
                    adapter.Fingerprint(manifest))) queued++;
            }
            return queued;
        }

        internal string RetryBestVerifiedChairAuthoringReview()
        {
            foreach (SavicJobRecord job in jobs.Jobs
                         .Where(candidate => candidate != null &&
                             candidate.batchEligible && !candidate.cancelRequested &&
                             candidate.state == SavicJobState.NeedsReview.ToString() &&
                             candidate.reasonCode == "CHAIR_AUTHORING_REVIEW" &&
                             candidate.primaryStage == "FAMILY_PUBLICATION")
                         .OrderByDescending(candidate =>
                             manifests.TryGetBySavicId(candidate.manifestSavicId,
                                 out SavicManifest found)
                                 ? found.classification?.score ?? 0f : 0f))
            {
                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId, out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        !SavicChairAuthoringPlanner.TryPlan(manifest, out _, out _))
                        continue;

                    if (jobs.RetryVerifiedChairAuthoringReview(
                            job.jobId, job.manifestSavicId, job.sourceHash))
                        return job.originalFileName;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }
            return string.Empty;
        }

        // Revalidate a bounded number of already-enabled reviews once per
        // planner revision. Legacy jobs still require explicit enablement.
        internal int RetryVerifiedChairReviewsForCurrentPlanner(int maximum)
        {
            if (maximum <= 0)
                return 0;

            int retried = 0;
            foreach (SavicJobRecord job in jobs.Jobs
                         .Where(candidate => candidate != null &&
                             candidate.batchEligible && !candidate.cancelRequested &&
                             candidate.state == SavicJobState.NeedsReview.ToString() &&
                             candidate.reasonCode == "CHAIR_AUTHORING_REVIEW" &&
                             candidate.primaryStage == "FAMILY_PUBLICATION" &&
                             !string.Equals(
                                 candidate.lastAutomaticChairPlannerRetryVersion,
                                 SavicChairAuthoringPlanner.Version,
                                 StringComparison.Ordinal)))
            {
                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId,
                            out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        !SavicChairAuthoringPlanner.TryPlan(manifest, out _, out _))
                        continue;

                    if (jobs.RetryVerifiedChairAuthoringReview(
                            job.jobId, job.manifestSavicId, job.sourceHash,
                            SavicChairAuthoringPlanner.Version))
                        retried++;
                    if (retried >= maximum)
                        break;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }
            return retried;
        }

        internal int RetryVerifiedCompactSquareTableReviews(int maximum)
        {
            if (maximum <= 0)
                return 0;

            int retried = 0;
            foreach (SavicJobRecord job in jobs.Jobs.Where(candidate =>
                         candidate != null && candidate.batchEligible &&
                         !candidate.cancelRequested &&
                         candidate.state == SavicJobState.NeedsReview.ToString() &&
                         candidate.reasonCode == "FAMILY_PUBLICATION_FAILED" &&
                         candidate.primaryStage == "FAMILY_PUBLICATION" &&
                         candidate.message == "Normalized dimensions are not safe for an automatic table profile." &&
                         !string.Equals(candidate.lastAutomaticTablePlannerRetryVersion,
                             SavicTableAuthoringPlanner.Version, StringComparison.Ordinal)))
            {
                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId,
                            out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        !SavicTableAuthoringPlanner.TryPlan(manifest,
                            out SavicTableAuthoringRecord plan, out _) ||
                        plan.seatingDefinitionAssetPath !=
                            SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath)
                        continue;

                    if (jobs.RetryVerifiedTableAuthoringReview(
                            job.jobId, job.manifestSavicId, job.sourceHash,
                            SavicTableAuthoringPlanner.Version))
                        retried++;
                    if (retried >= maximum)
                        break;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }

            if (retried >= maximum)
                return retried;

            foreach (SavicJobRecord job in jobs.Jobs.Where(candidate =>
                         candidate != null && candidate.batchEligible &&
                         !candidate.cancelRequested &&
                         candidate.state == SavicJobState.Done.ToString() &&
                         candidate.reasonCode == "PUBLISHED" &&
                         !string.Equals(candidate.lastAutomaticTablePlannerRetryVersion,
                             SavicTableAuthoringPlanner.Version, StringComparison.Ordinal)))
            {
                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId,
                            out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        manifest.status != "PUBLISHED" ||
                        manifest.classification?.type != "Table" ||
                        manifest.tableAuthoring == null ||
                        !manifest.tableAuthoring.planned ||
                        manifest.tableAuthoring.seatingDefinitionAssetPath ==
                            SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath ||
                        !SavicTableAuthoringPlanner.TryPlan(manifest,
                            out SavicTableAuthoringRecord plan, out _) ||
                        plan.seatingDefinitionAssetPath !=
                            SavicTableAuthoringPlanner.CompactSquareTwoSeatingPath)
                        continue;

                    if (jobs.RetryVerifiedPublishedTableProfile(
                            job.jobId, job.manifestSavicId, job.sourceHash,
                            SavicTableAuthoringPlanner.Version))
                        retried++;
                    if (retried >= maximum)
                        break;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }
            return retried;
        }

        internal int RetryVerifiedChairPreviewFailures(int maximum)
        {
            if (maximum <= 0)
                return 0;

            int retried = 0;
            foreach (SavicJobRecord job in jobs.Jobs.Where(candidate =>
                         candidate != null && candidate.batchEligible &&
                         !candidate.cancelRequested &&
                         candidate.state == SavicJobState.NeedsReview.ToString() &&
                         candidate.reasonCode == "CHAIR_PUBLICATION_FAILED" &&
                         candidate.primaryStage == "FAMILY_PUBLICATION" &&
                         (candidate.message ?? string.Empty).IndexOf(
                             "Rendered preview appears blank or visually degenerate.",
                             StringComparison.Ordinal) >= 0 &&
                         !string.Equals(
                             candidate.lastAutomaticChairPreviewRetryVersion,
                             SavicPreviewRenderer.Version, StringComparison.Ordinal)))
            {
                try
                {
                    if (!manifests.TryGetBySavicId(job.manifestSavicId,
                            out SavicManifest manifest) ||
                        !ManifestMatches(manifest, job) ||
                        !HashMatches(CanonicalArchivePath(job), job.sourceHash) ||
                        !SavicChairAuthoringPlanner.TryPlan(manifest, out _, out _))
                        continue;

                    if (jobs.RetryVerifiedChairPreviewFailure(
                            job.jobId, job.manifestSavicId, job.sourceHash,
                            SavicPreviewRenderer.Version))
                        retried++;
                    if (retried >= maximum)
                        break;
                }
                catch (IOException) { }
                catch (ArgumentException) { }
            }
            return retried;
        }

        // The operator selects one original. Only an exact hash match against a
        // missing canonical source is accepted. The selected file is never moved.
        internal string AttachVerifiedOriginal(string originalPath)
        {
            if (string.IsNullOrWhiteSpace(originalPath) || !File.Exists(originalPath))
                throw new FileNotFoundException("Select an existing original file.", originalPath);

            string hash = SavicHashService.ComputeSha256(originalPath);
            SavicContinuityRow row = Audit().rows.FirstOrDefault(candidate =>
                string.Equals(candidate.sourceHash, hash, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.status, "SOURCE_MISSING", StringComparison.Ordinal));

            if (row == null)
                throw new InvalidOperationException(
                    "The selected file does not match a missing SAVIC source by SHA-256.");

            SavicJobRecord job = jobs.Jobs.First(candidate =>
                string.Equals(candidate.jobId, row.jobId, StringComparison.Ordinal));
            string archive = CanonicalArchivePath(job);
            string directory = Path.GetDirectoryName(archive);
            if (string.IsNullOrWhiteSpace(directory))
                throw new InvalidOperationException("Canonical archive directory is invalid.");

            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory,
                ".savic-" + Guid.NewGuid().ToString("N") + ".incoming");
            try
            {
                File.Copy(originalPath, temporary, false);
                if (!HashMatches(temporary, hash))
                    throw new IOException("The copied original failed SHA-256 verification.");
                if (File.Exists(archive))
                    throw new IOException("The canonical archive appeared during attachment; nothing was replaced.");
                File.Move(temporary, archive);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }

            // An interruption after File.Move leaves a verified archive. The
            // normal reconciliation action can finish without duplicating it.
            string postCommitStatus = Inspect(job).status;
            if (postCommitStatus != "READY_TO_RECONCILE" &&
                postCommitStatus != "ALREADY_MANAGED")
                throw new IOException("The attached archive failed post-commit verification.");
            if (postCommitStatus == "READY_TO_RECONCILE")
            {
                EnsureManifest(job);
                jobs.EnableVerifiedLegacyJob(job.jobId, job.manifestSavicId, job.sourceHash);
            }
            return job.originalFileName;
        }

        // Route selected external files by bytes, never by a truncated name.
        // Legacy identity is restored before the ordinary intake is allowed
        // to create a new identity for an unrelated source hash.
        internal SavicSelectedImportResult ImportSelectedFiles(
            IEnumerable<string> originalPaths,
            SavicIntakeService intake)
        {
            if (originalPaths == null)
                throw new ArgumentNullException(nameof(originalPaths));
            if (intake == null)
                throw new ArgumentNullException(nameof(intake));

            SavicSelectedImportResult result = new SavicSelectedImportResult();
            HashSet<string> missing = new HashSet<string>(
                Audit().rows.Where(row => row.status == "SOURCE_MISSING")
                    .Select(row => row.sourceHash), StringComparer.OrdinalIgnoreCase);

            foreach (string path in originalPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) ||
                        !IsModelFile(Path.GetFileName(path)))
                        throw new InvalidOperationException("Source must be an existing GLB file.");

                    string hash = SavicHashService.ComputeSha256(path);
                    if (missing.Contains(hash))
                    {
                        AttachVerifiedOriginal(path);
                        missing.Remove(hash);
                        result.Restored++;
                        continue;
                    }

                    SavicIntakeOutcome outcome = intake.IngestExternalCopySynchronously(path);
                    if (!outcome.Succeeded)
                        throw new InvalidOperationException(outcome.Message);
                    if (outcome.DuplicateExact)
                        result.Duplicates++;
                    else
                        result.Ingested++;
                }
                catch (Exception exception)
                {
                    result.Errors.Add(Path.GetFileName(path) + ": " + exception.Message);
                }
            }

            return result;
        }

        private SavicContinuityRow Inspect(SavicJobRecord job)
        {
            SavicContinuityRow row = NewRow(job);

            if (string.Equals(job.state, SavicJobState.FailedProcessing.ToString(), StringComparison.Ordinal))
                return Set(row, "PROCESSING_FAILED",
                    "Processing failed at " + (job.primaryStage ?? string.Empty) +
                    " (" + (job.reasonCode ?? string.Empty) + ").");

            if (!string.Equals(job.state, SavicJobState.Ingested.ToString(), StringComparison.Ordinal))
                return Set(row, "ALREADY_MANAGED", "Job is not pending ingestion.");

            string archive;
            try
            {
                if (string.IsNullOrWhiteSpace(job.manifestSavicId) ||
                    !Guid.TryParseExact(job.manifestSavicId, "N", out _) ||
                    !string.Equals(Path.GetFileName(job.originalFileName),
                        job.originalFileName, StringComparison.Ordinal) ||
                    !IsModelFile(job.originalFileName))
                    return Set(row, "IDENTITY_CONFLICT", "Incomplete or unsupported legacy identity.");

                archive = CanonicalArchivePath(job);
            }
            catch (ArgumentException exception)
            {
                return Set(row, "IDENTITY_CONFLICT", exception.Message);
            }

            if (manifests.TryGetBySavicId(job.manifestSavicId, out SavicManifest byId) &&
                !ManifestMatches(byId, job))
                return Set(row, "IDENTITY_CONFLICT", "SavicId belongs to a different source.");

            if (manifests.TryGetBySourceHash(job.sourceHash, out SavicManifest byHash) &&
                !ManifestMatches(byHash, job))
                return Set(row, "IDENTITY_CONFLICT", "SourceHash belongs to a different SavicId.");

            if (!File.Exists(archive))
                return Set(row, "SOURCE_MISSING", "Original bytes are absent from ContentSource.");

            try
            {
                if (!HashMatches(archive, job.sourceHash))
                    return Set(row, "SOURCE_INVALID", "Archived source SHA-256 differs from the queue.");
            }
            catch (IOException exception)
            {
                return Set(row, "SOURCE_INVALID", exception.Message);
            }

            if (byId != null)
            {
                if (string.Equals(byId.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) ||
                    job.batchEligible)
                    return Set(row, "ALREADY_MANAGED", "Manifest exists; no legacy repair is needed.");

                return Set(row, "READY_TO_RECONCILE", "Verified source and matching manifest; legacy queue can be enabled.");
            }

            return Set(row, "READY_TO_RECONCILE", "Verified source; manifest and legacy queue can be reconciled.");
        }

        private string CanonicalArchivePath(SavicJobRecord job)
        {
            string expected = layout.GetArchivedSourcePath(job.sourceHash, job.originalFileName);
            string recorded = layout.FromProjectRelativePath(job.archivedRelativePath);
            if (!string.Equals(expected, recorded, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Queue archive path is not the canonical ContentSource path.");
            return expected;
        }

        private void EnsureManifest(SavicJobRecord job)
        {
            if (manifests.TryGetBySavicId(job.manifestSavicId, out SavicManifest existing))
            {
                if (!ManifestMatches(existing, job))
                    throw new InvalidOperationException("Manifest identity changed during reconciliation.");
                return;
            }

            string archive = CanonicalArchivePath(job);
            if (!HashMatches(archive, job.sourceHash))
                throw new IOException("Archived source changed during reconciliation.");

            FileInfo source = new FileInfo(archive);
            SavicManifest manifest = new SavicManifest
            {
                savicId = job.manifestSavicId,
                createdUtc = job.createdUtc,
                status = "INGESTED",
                source = new SavicSourceRecord
                {
                    sourceHash = job.sourceHash,
                    originalFileName = job.originalFileName,
                    extension = Path.GetExtension(job.originalFileName).ToLowerInvariant(),
                    sourceKind = SavicSourceKind.Model3D.ToString(),
                    archivedRelativePath = job.archivedRelativePath,
                    byteLength = source.Length,
                    // The original file timestamp is not in the legacy queue.
                    // Zero means unknown rather than inventing provenance.
                    originalLastWriteUtcTicks = 0,
                    ingestedUtc = job.createdUtc
                }
            };
            manifests.Save(manifest);
        }

        private static bool ManifestMatches(SavicManifest manifest, SavicJobRecord job)
        {
            return manifest?.source != null &&
                   string.Equals(manifest.savicId, job.manifestSavicId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(manifest.source.sourceHash, job.sourceHash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(manifest.source.archivedRelativePath, job.archivedRelativePath, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(manifest.source.originalFileName, job.originalFileName, StringComparison.Ordinal) &&
                   string.Equals(manifest.source.sourceKind, SavicSourceKind.Model3D.ToString(),
                       StringComparison.Ordinal);
        }

        private static bool HashMatches(string path, string expected)
        {
            return File.Exists(path) &&
                   string.Equals(SavicHashService.ComputeSha256(path), expected,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsModelFile(string fileName)
        {
            // Legacy records being repaired here are self-contained GLB files.
            // Multi-file formats need their dependency manifest, which this
            // historical queue cannot prove.
            return string.Equals(Path.GetExtension(fileName ?? string.Empty),
                ".glb", StringComparison.OrdinalIgnoreCase);
        }

        private static SavicContinuityRow Set(SavicContinuityRow row, string status, string detail)
        {
            row.status = status;
            row.detail = detail;
            return row;
        }

        private static SavicContinuityRow NewRow(SavicJobRecord job)
        {
            return new SavicContinuityRow
            {
                jobId = job.jobId ?? string.Empty,
                savicId = job.manifestSavicId ?? string.Empty,
                sourceHash = job.sourceHash ?? string.Empty,
                fileName = job.originalFileName ?? string.Empty,
                archivedRelativePath = job.archivedRelativePath ?? string.Empty,
                batchEligible = job.batchEligible
            };
        }
    }
}
