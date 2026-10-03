using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicCanonicalContentInventorySnapshot
    {
        internal List<SavicCanonicalContentInventoryRow> Rows { get; } =
            new List<SavicCanonicalContentInventoryRow>();

        internal int Total => Rows.Count;
        internal int InboxPending =>
            Rows.Count(row => string.Equals(
                row.Lifecycle,
                "INBOX_PENDING",
                StringComparison.Ordinal));
        internal int Published =>
            Rows.Count(row =>
                string.Equals(row.Lifecycle, "PUBLISHED", StringComparison.Ordinal) ||
                string.Equals(row.Lifecycle, "CATALOG", StringComparison.Ordinal));
        internal int InCatalog =>
            Rows.Count(row => row.InMainCatalog);
        internal int NeedsReview =>
            Rows.Count(row => string.Equals(
                row.Lifecycle,
                "NEEDS_REVIEW",
                StringComparison.Ordinal));
        internal int Failed =>
            Rows.Count(row => string.Equals(
                row.Lifecycle,
                "FAILED",
                StringComparison.Ordinal));
        internal int Orphaned =>
            Rows.Count(row =>
                string.Equals(row.SourceLocation, "JOB_ONLY", StringComparison.Ordinal) ||
                string.Equals(row.SourceLocation, "ARCHIVED_JOB_ONLY", StringComparison.Ordinal) ||
                string.Equals(row.SourceLocation, "CONTENT_SOURCE_ORPHAN", StringComparison.Ordinal));
    }

    internal sealed class SavicCanonicalContentInventoryRow
    {
        internal SavicManifest Manifest { get; set; }
        internal SavicJobRecord LatestJob { get; set; }
        internal string StableKey { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string SavicId { get; set; } = string.Empty;
        internal string SourceHash { get; set; } = string.Empty;
        internal string CanonicalContentId { get; set; } = string.Empty;
        internal string Family { get; set; } = string.Empty;
        internal string Type { get; set; } = string.Empty;
        internal string Category { get; set; } = string.Empty;
        internal string Lifecycle { get; set; } = string.Empty;
        internal string PipelineStatus { get; set; } = string.Empty;
        internal string SourceLocation { get; set; } = string.Empty;
        internal string CatalogState { get; set; } = string.Empty;
        internal string ArchivedRelativePath { get; set; } = string.Empty;
        internal string InboxRelativePath { get; set; } = string.Empty;
        internal bool SourceArchivedExists { get; set; }
        internal bool InMainCatalog { get; set; }
        internal string Reason { get; set; } = string.Empty;
        internal string UpdatedUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
        internal List<string> ArtifactPaths { get; } =
            new List<string>();
    }

    internal static class SavicCanonicalContentInventoryService
    {
        internal static SavicCanonicalContentInventorySnapshot Build(
            SavicStorageLayout layout,
            IEnumerable<SavicManifest> manifests,
            IEnumerable<SavicJobRecord> jobs,
            SavicProjectInventorySnapshot projectInventory)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            layout.EnsureInfrastructure();

            List<SavicManifest> manifestList =
                (manifests ?? Array.Empty<SavicManifest>())
                    .Where(item => item != null)
                    .ToList();

            List<SavicJobRecord> jobList =
                (jobs ?? Array.Empty<SavicJobRecord>())
                    .Where(item => item != null)
                    .ToList();

            projectInventory ??=
                new SavicProjectInventorySnapshot();

            SavicCanonicalContentInventorySnapshot snapshot =
                new SavicCanonicalContentInventorySnapshot();

            Dictionary<string, SavicManifest> manifestBySavicId =
                manifestList
                    .Where(item => !string.IsNullOrWhiteSpace(item.savicId))
                    .GroupBy(
                        item => item.savicId,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Last(),
                        StringComparer.OrdinalIgnoreCase);

            Dictionary<string, SavicManifest> manifestBySourceHash =
                manifestList
                    .Where(item =>
                        item.source != null &&
                        !string.IsNullOrWhiteSpace(item.source.sourceHash))
                    .GroupBy(
                        item => item.source.sourceHash,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Last(),
                        StringComparer.OrdinalIgnoreCase);

            Dictionary<string, SavicJobRecord> latestJobBySourceHash =
                BuildLatestJobMap(jobList);

            for (int index = 0;
                 index < manifestList.Count;
                 index++)
            {
                SavicManifest manifest =
                    manifestList[index];

                snapshot.Rows.Add(
                    BuildManifestRow(
                        layout,
                        manifest,
                        latestJobBySourceHash,
                        projectInventory));
            }

            AppendOrphanJobs(
                layout,
                snapshot,
                jobList,
                manifestBySavicId,
                manifestBySourceHash);

            AppendInboxRows(
                layout,
                snapshot);

            AppendOrphanArchiveRows(
                layout,
                snapshot,
                manifestBySourceHash);

            DeduplicatePhysicalRows(snapshot);

            snapshot.Rows.Sort(
                (left, right) =>
                {
                    int timestamp =
                        right.SortTimestamp.CompareTo(
                            left.SortTimestamp);

                    return timestamp != 0
                        ? timestamp
                        : string.Compare(
                            left.DisplayName,
                            right.DisplayName,
                            StringComparison.OrdinalIgnoreCase);
                });

            return snapshot;
        }

        private static Dictionary<string, SavicJobRecord>
            BuildLatestJobMap(
                IEnumerable<SavicJobRecord> jobs)
        {
            Dictionary<string, SavicJobRecord> result =
                new Dictionary<string, SavicJobRecord>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (SavicJobRecord job in jobs)
            {
                if (job == null ||
                    string.IsNullOrWhiteSpace(job.sourceHash))
                {
                    continue;
                }

                if (!result.TryGetValue(
                        job.sourceHash,
                        out SavicJobRecord current) ||
                    ShouldReplacePrimaryJob(current, job))
                {
                    result[job.sourceHash] =
                        job;
                }
            }

            return result;
        }

        // Duplicate intake is an identity observation, not a processing
        // outcome. A newer duplicate must not hide the original review/failure.
        private static bool ShouldReplacePrimaryJob(
            SavicJobRecord current, SavicJobRecord candidate)
        {
            bool currentDuplicate = string.Equals(current.state,
                SavicJobState.DuplicateExact.ToString(), StringComparison.OrdinalIgnoreCase);
            bool candidateDuplicate = string.Equals(candidate.state,
                SavicJobState.DuplicateExact.ToString(), StringComparison.OrdinalIgnoreCase);
            if (currentDuplicate != candidateDuplicate)
                return currentDuplicate;
            return ParseTimestamp(FirstNonEmpty(candidate.updatedUtc,
                       candidate.createdUtc)) >
                   ParseTimestamp(FirstNonEmpty(current.updatedUtc,
                       current.createdUtc));
        }

        private static SavicCanonicalContentInventoryRow
            BuildManifestRow(
                SavicStorageLayout layout,
                SavicManifest manifest,
                IReadOnlyDictionary<string, SavicJobRecord>
                    latestJobBySourceHash,
                SavicProjectInventorySnapshot projectInventory)
        {
            string sourceHash =
                manifest.source?.sourceHash ?? string.Empty;

            latestJobBySourceHash.TryGetValue(
                sourceHash,
                out SavicJobRecord latestJob);

            SavicProjectInventoryItemRecord catalogRecord =
                FindCatalogRecord(
                    manifest,
                    projectInventory);

            string archivedRelativePath =
                manifest.source?.archivedRelativePath ??
                string.Empty;

            bool archiveExists =
                !string.IsNullOrWhiteSpace(
                    archivedRelativePath) &&
                File.Exists(
                    layout.FromProjectRelativePath(
                        archivedRelativePath));

            SavicCanonicalContentInventoryRow row =
                new SavicCanonicalContentInventoryRow
                {
                    Manifest =
                        manifest,
                    LatestJob =
                        latestJob,
                    StableKey =
                        "manifest:" +
                        (manifest.savicId ?? sourceHash),
                    DisplayName =
                        FirstNonEmpty(
                            manifest.source?.originalFileName,
                            manifest.canonicalContentId,
                            manifest.savicId,
                            "Asset sin identidad"),
                    SavicId =
                        manifest.savicId ?? string.Empty,
                    SourceHash =
                        sourceHash,
                    CanonicalContentId =
                        manifest.canonicalContentId ??
                        string.Empty,
                    Family =
                        FirstUseful(
                            manifest.family,
                            manifest.classification?.family),
                    Type =
                        FirstUseful(
                            manifest.type,
                            manifest.classification?.type),
                    Category =
                        FirstUseful(
                            manifest.category,
                            manifest.classification?.category),
                    PipelineStatus =
                        FirstNonEmpty(
                            manifest.status,
                            latestJob?.state,
                            "UNKNOWN"),
                    ArchivedRelativePath =
                        archivedRelativePath,
                    SourceArchivedExists =
                        archiveExists,
                    SourceLocation =
                        archiveExists
                            ? "ARCHIVED"
                            : "MANIFEST_ONLY",
                    InMainCatalog =
                        catalogRecord != null &&
                        catalogRecord.inMainCatalog,
                    UpdatedUtc =
                        FirstNonEmpty(
                            manifest.updatedUtc,
                            latestJob?.updatedUtc,
                            manifest.createdUtc),
                    Reason =
                        ResolveReason(
                            manifest,
                            latestJob)
                };

            row.CatalogState =
                row.InMainCatalog
                    ? "IN_CATALOG"
                    : string.IsNullOrWhiteSpace(
                          row.CanonicalContentId)
                        ? "NO_CONTENT_ID"
                        : "NOT_IN_CATALOG";

            row.Lifecycle =
                ResolveLifecycle(
                    manifest,
                    latestJob,
                    row.InMainCatalog);

            row.SortTimestamp =
                ParseTimestamp(row.UpdatedUtc);

            if (manifest.artifacts != null)
            {
                for (int index = 0;
                     index < manifest.artifacts.Count;
                     index++)
                {
                    SavicArtifactRecord artifact =
                        manifest.artifacts[index];

                    if (artifact == null ||
                        string.IsNullOrWhiteSpace(
                            artifact.projectRelativePath) ||
                        string.Equals(
                            artifact.role,
                            "unity.source_mirror",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    row.ArtifactPaths.Add(
                        artifact.projectRelativePath);
                }
            }

            return row;
        }

        private static void AppendOrphanJobs(
            SavicStorageLayout layout,
            SavicCanonicalContentInventorySnapshot snapshot,
            IEnumerable<SavicJobRecord> jobs,
            IReadOnlyDictionary<string, SavicManifest> manifestBySavicId,
            IReadOnlyDictionary<string, SavicManifest> manifestBySourceHash)
        {
            Dictionary<string, SavicJobRecord> latest =
                new Dictionary<string, SavicJobRecord>(
                    StringComparer.OrdinalIgnoreCase);

            foreach (SavicJobRecord job in jobs)
            {
                if (job == null)
                    continue;

                bool linkedById =
                    !string.IsNullOrWhiteSpace(
                        job.manifestSavicId) &&
                    manifestBySavicId.ContainsKey(
                        job.manifestSavicId);

                bool linkedByHash =
                    !string.IsNullOrWhiteSpace(
                        job.sourceHash) &&
                    manifestBySourceHash.ContainsKey(
                        job.sourceHash);

                if (linkedById || linkedByHash)
                    continue;

                string key =
                    FirstNonEmpty(
                        job.sourceHash,
                        job.originalFileName,
                        job.jobId);

                if (!latest.TryGetValue(
                        key,
                        out SavicJobRecord current) ||
                    ShouldReplacePrimaryJob(current, job))
                {
                    latest[key] =
                        job;
                }
            }

            foreach (
                KeyValuePair<string, SavicJobRecord> pair
                in latest)
            {
                SavicJobRecord job =
                    pair.Value;

                bool archiveExists =
                    !string.IsNullOrWhiteSpace(
                        job.archivedRelativePath) &&
                    File.Exists(
                        layout.FromProjectRelativePath(
                            job.archivedRelativePath));

                string updated =
                    FirstNonEmpty(
                        job.updatedUtc,
                        job.createdUtc);

                snapshot.Rows.Add(
                    new SavicCanonicalContentInventoryRow
                    {
                        LatestJob =
                            job,
                        StableKey =
                            "job:" +
                            FirstNonEmpty(
                                job.jobId,
                                pair.Key),
                        DisplayName =
                            FirstNonEmpty(
                                job.originalFileName,
                                job.sourceHash,
                                "Trabajo sin manifest"),
                        SavicId =
                            job.manifestSavicId ??
                            string.Empty,
                        SourceHash =
                            job.sourceHash ??
                            string.Empty,
                        Family =
                            "Unknown",
                        Type =
                            "Unknown",
                        Category =
                            "Unknown",
                        PipelineStatus =
                            job.state ??
                            string.Empty,
                        Lifecycle =
                            ResolveJobLifecycle(
                                job.state),
                        SourceLocation =
                            archiveExists
                                ? "ARCHIVED_JOB_ONLY"
                                : "JOB_ONLY",
                        CatalogState =
                            "NOT_IN_CATALOG",
                        ArchivedRelativePath =
                            job.archivedRelativePath ??
                            string.Empty,
                        SourceArchivedExists =
                            archiveExists,
                        InMainCatalog =
                            false,
                        Reason =
                            archiveExists
                                ? "Archived original exists, but its SAVIC manifest is missing."
                                : "Original is missing from its recorded ContentSource path, and its SAVIC manifest is missing. The queue record alone cannot be published.",
                        UpdatedUtc =
                            updated,
                        SortTimestamp =
                            ParseTimestamp(updated)
                    });
            }
        }

        private static void AppendInboxRows(
            SavicStorageLayout layout,
            SavicCanonicalContentInventorySnapshot snapshot)
        {
            if (!Directory.Exists(
                    layout.DropHereRoot))
            {
                return;
            }

            string[] paths =
                Directory.GetFiles(
                    layout.DropHereRoot,
                    "*",
                    SearchOption.TopDirectoryOnly);

            Array.Sort(
                paths,
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                 index < paths.Length;
                 index++)
            {
                string path =
                    paths[index];

                if (string.Equals(
                        Path.GetFileName(path),
                        ".gitkeep",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative =
                    layout.ToProjectRelativePath(
                        path);

                snapshot.Rows.Add(
                    new SavicCanonicalContentInventoryRow
                    {
                        StableKey =
                            "inbox:" +
                            relative,
                        DisplayName =
                            Path.GetFileName(path),
                        Family =
                            "Pending",
                        Type =
                            "Pending",
                        Category =
                            "Pending",
                        Lifecycle =
                            "INBOX_PENDING",
                        PipelineStatus =
                            "NOT_INGESTED",
                        SourceLocation =
                            "DROP_HERE",
                        CatalogState =
                            "NOT_IN_CATALOG",
                        InboxRelativePath =
                            relative,
                        InMainCatalog =
                            false,
                        Reason =
                            "File is still waiting in ContentInbox/DropHere.",
                        UpdatedUtc =
                            File.GetLastWriteTimeUtc(path)
                                .ToString("O"),
                        SortTimestamp =
                            new DateTimeOffset(
                                File.GetLastWriteTimeUtc(path),
                                TimeSpan.Zero)
                    });
            }
        }

        private static void AppendOrphanArchiveRows(
            SavicStorageLayout layout,
            SavicCanonicalContentInventorySnapshot snapshot,
            IReadOnlyDictionary<string, SavicManifest> manifestBySourceHash)
        {
            string shaRoot =
                Path.Combine(
                    layout.ContentSourceRoot,
                    "SHA256");

            if (!Directory.Exists(shaRoot))
                return;

            string[] paths =
                Directory.GetFiles(
                    shaRoot,
                    "*",
                    SearchOption.AllDirectories);

            Array.Sort(
                paths,
                StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                 index < paths.Length;
                 index++)
            {
                string path =
                    paths[index];

                DirectoryInfo parent =
                    Directory.GetParent(path);

                string sourceHash =
                    parent?.Name ?? string.Empty;

                if (sourceHash.Length == 64 &&
                    manifestBySourceHash.ContainsKey(
                        sourceHash))
                {
                    continue;
                }

                string relative =
                    layout.ToProjectRelativePath(
                        path);

                snapshot.Rows.Add(
                    new SavicCanonicalContentInventoryRow
                    {
                        StableKey =
                            "archive:" +
                            relative,
                        DisplayName =
                            Path.GetFileName(path),
                        SourceHash =
                            sourceHash.Length == 64
                                ? sourceHash
                                : string.Empty,
                        Family =
                            "Unknown",
                        Type =
                            "Unknown",
                        Category =
                            "Unknown",
                        Lifecycle =
                            "ARCHIVED_ORPHAN",
                        PipelineStatus =
                            "NO_MANIFEST",
                        SourceLocation =
                            "CONTENT_SOURCE_ORPHAN",
                        CatalogState =
                            "NOT_IN_CATALOG",
                        ArchivedRelativePath =
                            relative,
                        SourceArchivedExists =
                            true,
                        InMainCatalog =
                            false,
                        Reason =
                            "Archived source exists but no SAVIC manifest owns it.",
                        UpdatedUtc =
                            File.GetLastWriteTimeUtc(path)
                                .ToString("O"),
                        SortTimestamp =
                            new DateTimeOffset(
                                File.GetLastWriteTimeUtc(path),
                                TimeSpan.Zero)
                    });
            }
        }

        private static void DeduplicatePhysicalRows(
            SavicCanonicalContentInventorySnapshot snapshot)
        {
            HashSet<string> claimedHashes =
                new HashSet<string>(
                    snapshot.Rows
                        .Where(row =>
                            (row.Manifest != null || row.LatestJob != null) &&
                            !string.IsNullOrWhiteSpace(row.SourceHash))
                        .Select(row => row.SourceHash),
                    StringComparer.OrdinalIgnoreCase);

            snapshot.Rows.RemoveAll(
                row =>
                    row.Manifest == null &&
                    string.Equals(
                        row.SourceLocation,
                        "CONTENT_SOURCE_ORPHAN",
                        StringComparison.Ordinal) &&
                    !string.IsNullOrWhiteSpace(
                        row.SourceHash) &&
                    claimedHashes.Contains(
                        row.SourceHash));
        }

        private static SavicProjectInventoryItemRecord
            FindCatalogRecord(
                SavicManifest manifest,
                SavicProjectInventorySnapshot inventory)
        {
            if (manifest == null ||
                inventory?.items == null)
            {
                return null;
            }

            for (int index = 0;
                 index < inventory.items.Count;
                 index++)
            {
                SavicProjectInventoryItemRecord item =
                    inventory.items[index];

                if (item == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(
                        manifest.savicId) &&
                    string.Equals(
                        item.savicId,
                        manifest.savicId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                if (!string.IsNullOrWhiteSpace(
                        manifest.canonicalContentId) &&
                    string.Equals(
                        item.itemId,
                        manifest.canonicalContentId,
                        StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static string ResolveLifecycle(
            SavicManifest manifest,
            SavicJobRecord latestJob,
            bool inMainCatalog)
        {
            if (inMainCatalog)
                return "CATALOG";

            if (string.Equals(
                    manifest?.status,
                    "PUBLISHED",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "PUBLISHED";
            }

            if (latestJob != null &&
                !string.Equals(latestJob.state,
                    SavicJobState.DuplicateExact.ToString(),
                    StringComparison.OrdinalIgnoreCase))
                return ResolveJobLifecycle(
                    latestJob.state);

            return FirstNonEmpty(
                    manifest?.status,
                    "UNKNOWN")
                .Trim()
                .ToUpperInvariant();
        }

        private static string ResolveJobLifecycle(
            string state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return "UNKNOWN";

            if (string.Equals(
                    state,
                    SavicJobState.NeedsReview.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "NEEDS_REVIEW";
            }

            if (string.Equals(
                    state,
                    SavicJobState.FailedSource.ToString(),
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    state,
                    SavicJobState.FailedProcessing.ToString(),
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    state,
                    SavicJobState.Quarantined.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "FAILED";
            }

            if (string.Equals(
                    state,
                    SavicJobState.Processing.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "PROCESSING";
            }

            if (string.Equals(
                    state,
                    SavicJobState.DuplicateExact.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "DUPLICATE_EXACT";
            }

            if (string.Equals(
                    state,
                    SavicJobState.Ingested.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "INGESTED";
            }

            if (string.Equals(
                    state,
                    SavicJobState.Done.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "DONE";
            }

            return state.Trim().ToUpperInvariant();
        }

        private static string ResolveReason(
            SavicManifest manifest,
            SavicJobRecord latestJob)
        {
            if (!string.IsNullOrWhiteSpace(
                    latestJob?.message) &&
                !string.Equals(latestJob.state,
                    SavicJobState.DuplicateExact.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return latestJob.message;
            }

            if (manifest?.validations == null)
                return string.Empty;

            SavicValidationRecord issue =
                manifest.validations
                    .Where(item =>
                        item != null &&
                        (!string.Equals(
                             item.result,
                             "PASS",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             item.severity,
                             "WARNING",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             item.severity,
                             "ERROR",
                             StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(
                        item =>
                            string.Equals(
                                item.severity,
                                "ERROR",
                                StringComparison.OrdinalIgnoreCase)
                                ? 2
                                : string.Equals(
                                    item.severity,
                                    "WARNING",
                                    StringComparison.OrdinalIgnoreCase)
                                    ? 1
                                    : 0)
                    .FirstOrDefault();

            return issue?.message ??
                   string.Empty;
        }

        private static string FirstUseful(
            params string[] values)
        {
            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                string value =
                    values[index];

                if (!string.IsNullOrWhiteSpace(value) &&
                    !string.Equals(
                        value,
                        "Unknown",
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(
                        value,
                        "UNSET",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return value.Trim();
                }
            }

            return "Unknown";
        }

        private static string FirstNonEmpty(
            params string[] values)
        {
            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                if (!string.IsNullOrWhiteSpace(
                        values[index]))
                {
                    return values[index].Trim();
                }
            }

            return string.Empty;
        }

        private static DateTimeOffset ParseTimestamp(
            string value)
        {
            return DateTimeOffset.TryParse(
                    value,
                    out DateTimeOffset parsed)
                ? parsed
                : DateTimeOffset.MinValue;
        }
    }
}
