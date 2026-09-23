using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BistroBuilder.Editor.Savic
{
    internal enum SavicEditorSection
    {
        Summary = 0,
        Queue = 1,
        Review = 2,
        Library = 3,
        Validation = 4,
        History = 5,
        Settings = 6
    }

    internal sealed class SavicEditorSummary
    {
        internal int TotalManaged { get; set; }
        internal int Passed { get; set; }
        internal int AutoCorrected { get; set; }
        internal int NeedsReview { get; set; }
        internal int Errors { get; set; }
        internal int Stale { get; set; }
        internal int QueuedOrActive { get; set; }
        internal int InventoryIssues { get; set; }
        internal int LegacyPendingAdoption { get; set; }
    }

    internal sealed class SavicEditorAssetRow
    {
        internal SavicManifest Manifest { get; set; }
        internal string SavicId { get; set; } = string.Empty;
        internal string CanonicalContentId { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string Family { get; set; } = string.Empty;
        internal string Type { get; set; } = string.Empty;
        internal string Category { get; set; } = string.Empty;
        internal string Status { get; set; } = string.Empty;
        internal string Origin { get; set; } = string.Empty;
        internal string Version { get; set; } = string.Empty;
        internal string UpdatedUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
    }

    internal sealed class SavicEditorJobRow
    {
        internal SavicJobRecord Job { get; set; }
        internal string JobId { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string State { get; set; } = string.Empty;
        internal string Message { get; set; } = string.Empty;
        internal string UpdatedUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
    }

    internal sealed class SavicEditorReviewRow
    {
        internal SavicManifest Manifest { get; set; }
        internal string StableKey { get; set; } = string.Empty;
        internal string Source { get; set; } = string.Empty;
        internal string SavicId { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string Status { get; set; } = string.Empty;
        internal string Severity { get; set; } = string.Empty;
        internal string Issue { get; set; } = string.Empty;
        internal string AssetPath { get; set; } = string.Empty;
        internal string UpdatedUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
    }

    internal sealed class SavicEditorValidationRow
    {
        internal SavicManifest Manifest { get; set; }
        internal string StableKey { get; set; } = string.Empty;
        internal string Source { get; set; } = string.Empty;
        internal string SavicId { get; set; } = string.Empty;
        internal string DisplayName { get; set; } = string.Empty;
        internal string ValidationId { get; set; } = string.Empty;
        internal string Result { get; set; } = string.Empty;
        internal string Severity { get; set; } = string.Empty;
        internal string Message { get; set; } = string.Empty;
        internal string ValidatorVersion { get; set; } = string.Empty;
        internal string AssetPath { get; set; } = string.Empty;
        internal string UpdatedUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
    }

    internal sealed class SavicEditorHistoryRow
    {
        internal string StableKey { get; set; } = string.Empty;
        internal string Kind { get; set; } = string.Empty;
        internal string Title { get; set; } = string.Empty;
        internal string Detail { get; set; } = string.Empty;
        internal string TimestampUtc { get; set; } = string.Empty;
        internal DateTimeOffset SortTimestamp { get; set; }
    }

    internal sealed class SavicEditorSnapshot
    {
        internal static readonly SavicEditorSnapshot Empty =
            new SavicEditorSnapshot(
                new SavicEditorSummary(),
                Array.Empty<SavicEditorAssetRow>(),
                Array.Empty<SavicEditorJobRow>(),
                Array.Empty<SavicEditorReviewRow>(),
                Array.Empty<SavicEditorValidationRow>(),
                Array.Empty<SavicEditorHistoryRow>(),
                new SavicProjectInventorySnapshot());

        internal SavicEditorSnapshot(
            SavicEditorSummary summary,
            IReadOnlyList<SavicEditorAssetRow> assets,
            IReadOnlyList<SavicEditorJobRow> jobs,
            IReadOnlyList<SavicEditorReviewRow> reviews,
            IReadOnlyList<SavicEditorValidationRow> validations,
            IReadOnlyList<SavicEditorHistoryRow> history,
            SavicProjectInventorySnapshot inventory)
        {
            Summary = summary ?? new SavicEditorSummary();
            Assets = assets ?? Array.Empty<SavicEditorAssetRow>();
            Jobs = jobs ?? Array.Empty<SavicEditorJobRow>();
            Reviews = reviews ?? Array.Empty<SavicEditorReviewRow>();
            Validations = validations ?? Array.Empty<SavicEditorValidationRow>();
            History = history ?? Array.Empty<SavicEditorHistoryRow>();
            Inventory = inventory ?? new SavicProjectInventorySnapshot();
        }

        internal SavicEditorSummary Summary { get; }
        internal IReadOnlyList<SavicEditorAssetRow> Assets { get; }
        internal IReadOnlyList<SavicEditorJobRow> Jobs { get; }
        internal IReadOnlyList<SavicEditorReviewRow> Reviews { get; }
        internal IReadOnlyList<SavicEditorValidationRow> Validations { get; }
        internal IReadOnlyList<SavicEditorHistoryRow> History { get; }
        internal SavicProjectInventorySnapshot Inventory { get; }
    }

    internal static class SavicEditorReadModel
    {
        private const string AllChoice = "Todos";

        internal static SavicEditorSnapshot Build(
            IEnumerable<SavicManifest> manifests,
            IEnumerable<SavicJobRecord> jobs,
            SavicProjectInventorySnapshot inventory)
        {
            List<SavicManifest> manifestList =
                manifests?
                    .Where(manifest => manifest != null)
                    .ToList() ??
                new List<SavicManifest>();

            List<SavicJobRecord> jobList =
                jobs?
                    .Where(job => job != null)
                    .ToList() ??
                new List<SavicJobRecord>();

            inventory ??= new SavicProjectInventorySnapshot();

            List<SavicEditorAssetRow> assets =
                BuildAssets(manifestList);

            List<SavicEditorJobRow> jobRows =
                BuildJobs(jobList);

            List<SavicEditorValidationRow> validations =
                BuildValidations(manifestList, inventory);

            List<SavicEditorReviewRow> reviews =
                BuildReviews(manifestList, inventory);

            List<SavicEditorHistoryRow> history =
                BuildHistory(assets, jobRows);

            SavicEditorSummary summary =
                BuildSummary(
                    manifestList,
                    jobList,
                    inventory,
                    reviews);

            return new SavicEditorSnapshot(
                summary,
                assets,
                jobRows,
                reviews,
                validations,
                history,
                inventory);
        }

        internal static List<SavicEditorAssetRow> FilterAssets(
            IEnumerable<SavicEditorAssetRow> rows,
            string search,
            string family,
            string category,
            string status,
            string origin,
            string version)
        {
            return (rows ?? Array.Empty<SavicEditorAssetRow>())
                .Where(row =>
                    row != null &&
                    MatchesSearch(
                        search,
                        row.DisplayName,
                        row.SavicId,
                        row.CanonicalContentId,
                        row.Family,
                        row.Type,
                        row.Category,
                        row.Status,
                        row.Origin,
                        row.Version) &&
                    MatchesChoice(row.Family, family) &&
                    MatchesChoice(row.Category, category) &&
                    MatchesChoice(row.Status, status) &&
                    MatchesChoice(row.Origin, origin) &&
                    MatchesChoice(row.Version, version))
                .ToList();
        }

        internal static List<SavicEditorJobRow> FilterJobs(
            IEnumerable<SavicEditorJobRow> rows,
            string search,
            string state)
        {
            return (rows ?? Array.Empty<SavicEditorJobRow>())
                .Where(row =>
                    row != null &&
                    MatchesChoice(row.State, state) &&
                    MatchesSearch(
                        search,
                        row.DisplayName,
                        row.JobId,
                        row.State,
                        row.Message,
                        row.Job?.manifestSavicId,
                        row.Job?.sourceHash))
                .ToList();
        }

        internal static List<SavicEditorReviewRow> FilterReviews(
            IEnumerable<SavicEditorReviewRow> rows,
            string search,
            string severity,
            string source)
        {
            return (rows ?? Array.Empty<SavicEditorReviewRow>())
                .Where(row =>
                    row != null &&
                    MatchesChoice(row.Severity, severity) &&
                    MatchesChoice(row.Source, source) &&
                    MatchesSearch(
                        search,
                        row.DisplayName,
                        row.SavicId,
                        row.Status,
                        row.Severity,
                        row.Issue,
                        row.AssetPath))
                .ToList();
        }

        internal static List<SavicEditorValidationRow> FilterValidations(
            IEnumerable<SavicEditorValidationRow> rows,
            string search,
            string result,
            string severity,
            string source)
        {
            return (rows ?? Array.Empty<SavicEditorValidationRow>())
                .Where(row =>
                    row != null &&
                    MatchesChoice(row.Result, result) &&
                    MatchesChoice(row.Severity, severity) &&
                    MatchesChoice(row.Source, source) &&
                    MatchesSearch(
                        search,
                        row.DisplayName,
                        row.SavicId,
                        row.ValidationId,
                        row.Result,
                        row.Severity,
                        row.Message,
                        row.ValidatorVersion,
                        row.AssetPath))
                .ToList();
        }

        internal static List<SavicEditorHistoryRow> FilterHistory(
            IEnumerable<SavicEditorHistoryRow> rows,
            string search,
            string kind)
        {
            return (rows ?? Array.Empty<SavicEditorHistoryRow>())
                .Where(row =>
                    row != null &&
                    MatchesChoice(row.Kind, kind) &&
                    MatchesSearch(
                        search,
                        row.Kind,
                        row.Title,
                        row.Detail,
                        row.TimestampUtc))
                .ToList();
        }

        private static List<SavicEditorAssetRow> BuildAssets(
            IEnumerable<SavicManifest> manifests)
        {
            List<SavicEditorAssetRow> result =
                new List<SavicEditorAssetRow>();

            foreach (SavicManifest manifest in manifests)
            {
                string sourceName =
                    manifest.source?.originalFileName ?? string.Empty;

                string displayName =
                    FirstNonEmpty(
                        sourceName,
                        manifest.canonicalContentId,
                        manifest.savicId,
                        "Asset sin identidad");

                string family =
                    FirstUsefulValue(
                        manifest.family,
                        manifest.classification?.family);

                string type =
                    FirstUsefulValue(
                        manifest.type,
                        manifest.classification?.type);

                string category =
                    FirstUsefulValue(
                        manifest.category,
                        manifest.classification?.category);

                string sourceKind =
                    manifest.source?.sourceKind ?? string.Empty;

                string extension =
                    manifest.source?.extension ?? string.Empty;

                string origin =
                    string.IsNullOrWhiteSpace(extension)
                        ? NormalizeValue(sourceKind)
                        : NormalizeValue(sourceKind) + " · " + extension;

                string updatedUtc =
                    FirstNonEmpty(
                        manifest.updatedUtc,
                        manifest.createdUtc);

                result.Add(
                    new SavicEditorAssetRow
                    {
                        Manifest = manifest,
                        SavicId = manifest.savicId ?? string.Empty,
                        CanonicalContentId =
                            manifest.canonicalContentId ?? string.Empty,
                        DisplayName = displayName,
                        Family = NormalizeValue(family),
                        Type = NormalizeValue(type),
                        Category = NormalizeValue(category),
                        Status = NormalizeValue(manifest.status),
                        Origin = origin,
                        Version = FirstNonEmpty(
                            manifest.pipelineVersion,
                            manifest.savicVersion,
                            "Sin versión"),
                        UpdatedUtc = updatedUtc,
                        SortTimestamp = ParseTimestamp(updatedUtc)
                    });
            }

            result.Sort(
                (left, right) =>
                {
                    int timestamp =
                        right.SortTimestamp.CompareTo(
                            left.SortTimestamp);

                    return timestamp != 0
                        ? timestamp
                        : string.Compare(
                            left.SavicId,
                            right.SavicId,
                            StringComparison.Ordinal);
                });

            return result;
        }

        private static List<SavicEditorJobRow> BuildJobs(
            IEnumerable<SavicJobRecord> jobs)
        {
            List<SavicEditorJobRow> result =
                new List<SavicEditorJobRow>();

            foreach (SavicJobRecord job in jobs)
            {
                string timestamp =
                    FirstNonEmpty(
                        job.updatedUtc,
                        job.createdUtc);

                result.Add(
                    new SavicEditorJobRow
                    {
                        Job = job,
                        JobId = job.jobId ?? string.Empty,
                        DisplayName = FirstNonEmpty(
                            job.originalFileName,
                            job.manifestSavicId,
                            job.jobId,
                            "Trabajo sin identidad"),
                        State = NormalizeValue(job.state),
                        Message = job.message ?? string.Empty,
                        UpdatedUtc = timestamp,
                        SortTimestamp = ParseTimestamp(timestamp)
                    });
            }

            result.Sort(
                (left, right) =>
                {
                    int timestamp =
                        right.SortTimestamp.CompareTo(
                            left.SortTimestamp);

                    return timestamp != 0
                        ? timestamp
                        : string.Compare(
                            left.JobId,
                            right.JobId,
                            StringComparison.Ordinal);
                });

            return result;
        }

        private static List<SavicEditorValidationRow> BuildValidations(
            IEnumerable<SavicManifest> manifests,
            SavicProjectInventorySnapshot inventory)
        {
            List<SavicEditorValidationRow> result =
                new List<SavicEditorValidationRow>();

            IReadOnlyList<SavicProjectInventoryIssueRecord> inventoryIssues =
                inventory.issues;

            inventoryIssues ??=
                Array.Empty<SavicProjectInventoryIssueRecord>();

            foreach (SavicManifest manifest in manifests)
            {
                if (manifest.validations == null)
                    continue;

                string displayName =
                    FirstNonEmpty(
                        manifest.source?.originalFileName,
                        manifest.canonicalContentId,
                        manifest.savicId,
                        "Asset sin identidad");

                string timestamp =
                    FirstNonEmpty(
                        manifest.updatedUtc,
                        manifest.createdUtc);

                for (int index = 0;
                     index < manifest.validations.Count;
                     index++)
                {
                    SavicValidationRecord validation =
                        manifest.validations[index];

                    if (validation == null)
                        continue;

                    result.Add(
                        new SavicEditorValidationRow
                        {
                            Manifest = manifest,
                            StableKey =
                                "manifest:" +
                                (manifest.savicId ?? string.Empty) +
                                ":" +
                                (validation.validationId ?? index.ToString(
                                    CultureInfo.InvariantCulture)),
                            Source = "MANIFEST",
                            SavicId = manifest.savicId ?? string.Empty,
                            DisplayName = displayName,
                            ValidationId = NormalizeValue(
                                validation.validationId),
                            Result = NormalizeValue(validation.result),
                            Severity = NormalizeValue(validation.severity),
                            Message = validation.message ?? string.Empty,
                            ValidatorVersion =
                                validation.validatorVersion ?? string.Empty,
                            UpdatedUtc = timestamp,
                            SortTimestamp = ParseTimestamp(timestamp)
                        });
                }
            }

            string inventoryTimestamp =
                inventory.generatedUtc ?? string.Empty;

            for (int index = 0;
                 index < inventoryIssues.Count;
                 index++)
            {
                SavicProjectInventoryIssueRecord issue =
                    inventoryIssues[index];

                if (issue == null)
                    continue;

                result.Add(
                    new SavicEditorValidationRow
                    {
                        StableKey =
                            "inventory:" +
                            (issue.assetPath ?? string.Empty) +
                            ":" +
                            (issue.code ?? index.ToString(
                                CultureInfo.InvariantCulture)),
                        Source = "INVENTARIO",
                        DisplayName = FirstNonEmpty(
                            issue.itemId,
                            issue.assetPath,
                            "Entrada de inventario"),
                        ValidationId = NormalizeValue(issue.code),
                        Result = "FAIL",
                        Severity = NormalizeValue(issue.severity),
                        Message = issue.message ?? string.Empty,
                        ValidatorVersion =
                            inventory.scannerVersion ?? string.Empty,
                        AssetPath = issue.assetPath ?? string.Empty,
                        UpdatedUtc = inventoryTimestamp,
                        SortTimestamp = ParseTimestamp(inventoryTimestamp)
                    });
            }

            result.Sort(CompareValidations);
            return result;
        }

        private static List<SavicEditorReviewRow> BuildReviews(
            IEnumerable<SavicManifest> manifests,
            SavicProjectInventorySnapshot inventory)
        {
            List<SavicEditorReviewRow> result =
                new List<SavicEditorReviewRow>();

            IReadOnlyList<SavicProjectInventoryIssueRecord> inventoryIssues =
                inventory.issues;

            inventoryIssues ??=
                Array.Empty<SavicProjectInventoryIssueRecord>();

            foreach (SavicManifest manifest in manifests)
            {
                bool reviewStatus =
                    IsStatus(manifest.status, "NEEDS_REVIEW");

                bool errorStatus =
                    IsErrorStatus(manifest.status);

                if (!reviewStatus && !errorStatus)
                    continue;

                string displayName =
                    FirstNonEmpty(
                        manifest.source?.originalFileName,
                        manifest.canonicalContentId,
                        manifest.savicId,
                        "Asset sin identidad");

                string timestamp =
                    FirstNonEmpty(
                        manifest.updatedUtc,
                        manifest.createdUtc);

                List<SavicValidationRecord> problems =
                    manifest.validations?
                        .Where(IsProblemValidation)
                        .ToList() ??
                    new List<SavicValidationRecord>();

                if (problems.Count == 0)
                {
                    result.Add(
                        CreateManifestReviewRow(
                            manifest,
                            displayName,
                            timestamp,
                            errorStatus ? "ERROR" : "WARNING",
                            errorStatus
                                ? "El pipeline terminó con estado de error."
                                : "El asset requiere una decisión humana."));

                    continue;
                }

                for (int index = 0;
                     index < problems.Count;
                     index++)
                {
                    SavicValidationRecord problem =
                        problems[index];

                    SavicEditorReviewRow row =
                        CreateManifestReviewRow(
                            manifest,
                            displayName,
                            timestamp,
                            NormalizeValue(problem.severity),
                            FirstNonEmpty(
                                problem.message,
                                problem.validationId,
                                "Validación pendiente"));

                    row.StableKey +=
                        ":" +
                        (problem.validationId ?? index.ToString(
                            CultureInfo.InvariantCulture));

                    result.Add(row);
                }
            }

            string inventoryTimestamp =
                inventory.generatedUtc ?? string.Empty;

            for (int index = 0;
                 index < inventoryIssues.Count;
                 index++)
            {
                SavicProjectInventoryIssueRecord issue =
                    inventoryIssues[index];

                if (issue == null)
                    continue;

                result.Add(
                    new SavicEditorReviewRow
                    {
                        StableKey =
                            "inventory:" +
                            (issue.assetPath ?? string.Empty) +
                            ":" +
                            (issue.code ?? index.ToString(
                                CultureInfo.InvariantCulture)),
                        Source = "INVENTARIO",
                        DisplayName = FirstNonEmpty(
                            issue.itemId,
                            issue.assetPath,
                            "Entrada de inventario"),
                        Status = "REQUIERE ATENCIÓN",
                        Severity = NormalizeValue(issue.severity),
                        Issue = issue.message ?? string.Empty,
                        AssetPath = issue.assetPath ?? string.Empty,
                        UpdatedUtc = inventoryTimestamp,
                        SortTimestamp = ParseTimestamp(inventoryTimestamp)
                    });
            }

            result.Sort(
                (left, right) =>
                {
                    int severity =
                        SeverityRank(left.Severity).CompareTo(
                            SeverityRank(right.Severity));

                    if (severity != 0)
                        return severity;

                    int timestamp =
                        right.SortTimestamp.CompareTo(
                            left.SortTimestamp);

                    return timestamp != 0
                        ? timestamp
                        : string.Compare(
                            left.StableKey,
                            right.StableKey,
                            StringComparison.Ordinal);
                });

            return result;
        }

        private static SavicEditorReviewRow CreateManifestReviewRow(
            SavicManifest manifest,
            string displayName,
            string timestamp,
            string severity,
            string issue)
        {
            return new SavicEditorReviewRow
            {
                Manifest = manifest,
                StableKey =
                    "manifest:" +
                    (manifest.savicId ?? string.Empty),
                Source = "MANIFEST",
                SavicId = manifest.savicId ?? string.Empty,
                DisplayName = displayName,
                Status = NormalizeValue(manifest.status),
                Severity = NormalizeValue(severity),
                Issue = issue ?? string.Empty,
                UpdatedUtc = timestamp,
                SortTimestamp = ParseTimestamp(timestamp)
            };
        }

        private static List<SavicEditorHistoryRow> BuildHistory(
            IEnumerable<SavicEditorAssetRow> assets,
            IEnumerable<SavicEditorJobRow> jobs)
        {
            List<SavicEditorHistoryRow> result =
                new List<SavicEditorHistoryRow>();

            foreach (SavicEditorAssetRow asset in assets)
            {
                result.Add(
                    new SavicEditorHistoryRow
                    {
                        StableKey = "asset:" + asset.SavicId,
                        Kind = "ASSET",
                        Title = asset.DisplayName,
                        Detail =
                            asset.Status +
                            " · " +
                            asset.Type +
                            (string.IsNullOrWhiteSpace(
                                asset.CanonicalContentId)
                                ? string.Empty
                                : " · " + asset.CanonicalContentId),
                        TimestampUtc = asset.UpdatedUtc,
                        SortTimestamp = asset.SortTimestamp
                    });
            }

            foreach (SavicEditorJobRow job in jobs)
            {
                result.Add(
                    new SavicEditorHistoryRow
                    {
                        StableKey = "job:" + job.JobId,
                        Kind = "JOB",
                        Title = job.DisplayName,
                        Detail =
                            job.State +
                            (string.IsNullOrWhiteSpace(job.Message)
                                ? string.Empty
                                : " · " + job.Message),
                        TimestampUtc = job.UpdatedUtc,
                        SortTimestamp = job.SortTimestamp
                    });
            }

            result.Sort(
                (left, right) =>
                {
                    int timestamp =
                        right.SortTimestamp.CompareTo(
                            left.SortTimestamp);

                    return timestamp != 0
                        ? timestamp
                        : string.Compare(
                            left.StableKey,
                            right.StableKey,
                            StringComparison.Ordinal);
                });

            return result;
        }

        private static SavicEditorSummary BuildSummary(
            IReadOnlyList<SavicManifest> manifests,
            IReadOnlyList<SavicJobRecord> jobs,
            SavicProjectInventorySnapshot inventory,
            IReadOnlyList<SavicEditorReviewRow> reviews)
        {
            return new SavicEditorSummary
            {
                TotalManaged = manifests.Count,
                Passed = manifests.Count(
                    manifest =>
                        IsStatus(manifest.status, "PASS") ||
                        IsStatus(manifest.status, "PUBLISHED") ||
                        IsStatus(manifest.status, "AUTO_CORRECTED")),
                AutoCorrected = manifests.Count(
                    manifest =>
                        IsStatus(manifest.status, "AUTO_CORRECTED")),
                NeedsReview = reviews.Count,
                Errors = manifests.Count(
                    manifest => IsErrorStatus(manifest.status)) +
                    jobs.Count(
                        job =>
                            IsStatus(job.state, "FailedSource") ||
                            IsStatus(job.state, "Quarantined")),
                Stale = manifests.Count(
                    manifest => IsStatus(manifest.status, "STALE")),
                QueuedOrActive = jobs.Count(
                    job =>
                        IsStatus(job.state, "Waiting") ||
                        IsStatus(job.state, "Hashing")),
                InventoryIssues = inventory.issueCount,
                LegacyPendingAdoption = inventory.legacyPendingAdoption
            };
        }

        private static int CompareValidations(
            SavicEditorValidationRow left,
            SavicEditorValidationRow right)
        {
            int severity =
                SeverityRank(left.Severity).CompareTo(
                    SeverityRank(right.Severity));

            if (severity != 0)
                return severity;

            int result =
                ResultRank(left.Result).CompareTo(
                    ResultRank(right.Result));

            if (result != 0)
                return result;

            int timestamp =
                right.SortTimestamp.CompareTo(
                    left.SortTimestamp);

            return timestamp != 0
                ? timestamp
                : string.Compare(
                    left.StableKey,
                    right.StableKey,
                    StringComparison.Ordinal);
        }

        private static bool IsProblemValidation(
            SavicValidationRecord validation)
        {
            if (validation == null)
                return false;

            return !IsStatus(validation.result, "PASS") ||
                   IsStatus(validation.severity, "ERROR") ||
                   IsStatus(validation.severity, "BLOCKER");
        }

        private static bool IsErrorStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return false;

            return status.StartsWith(
                       "FAILED",
                       StringComparison.OrdinalIgnoreCase) ||
                   IsStatus(status, "ERROR") ||
                   IsStatus(status, "QUARANTINED");
        }

        private static bool MatchesChoice(
            string value,
            string choice)
        {
            return string.IsNullOrWhiteSpace(choice) ||
                   string.Equals(
                       choice,
                       AllChoice,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       NormalizeValue(value),
                       choice,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesSearch(
            string search,
            params string[] candidates)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            string query = search.Trim();

            for (int index = 0;
                 index < candidates.Length;
                 index++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[index]) &&
                    candidates[index].IndexOf(
                        query,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStatus(
            string actual,
            string expected)
        {
            return string.Equals(
                actual,
                expected,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstUsefulValue(
            string primary,
            string secondary)
        {
            if (!string.IsNullOrWhiteSpace(primary) &&
                !IsStatus(primary, "Unknown"))
            {
                return primary;
            }

            return FirstNonEmpty(secondary, primary);
        }

        private static string FirstNonEmpty(
            params string[] candidates)
        {
            for (int index = 0;
                 index < candidates.Length;
                 index++)
            {
                if (!string.IsNullOrWhiteSpace(candidates[index]))
                    return candidates[index];
            }

            return string.Empty;
        }

        private static string NormalizeValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "Sin dato"
                : value;
        }

        private static DateTimeOffset ParseTimestamp(string value)
        {
            return DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal |
                DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset timestamp)
                    ? timestamp
                    : DateTimeOffset.MinValue;
        }

        private static int SeverityRank(string severity)
        {
            if (IsStatus(severity, "BLOCKER"))
                return 0;
            if (IsStatus(severity, "ERROR"))
                return 1;
            if (IsStatus(severity, "WARNING"))
                return 2;
            if (IsStatus(severity, "INFO"))
                return 3;

            return 4;
        }

        private static int ResultRank(string result)
        {
            if (IsStatus(result, "FAIL") ||
                IsStatus(result, "ERROR"))
            {
                return 0;
            }

            if (IsStatus(result, "REVIEW") ||
                IsStatus(result, "NEEDS_REVIEW"))
            {
                return 1;
            }

            if (IsStatus(result, "PASS"))
                return 2;

            return 3;
        }
    }
}
