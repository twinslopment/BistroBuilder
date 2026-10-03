using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicLegacyAdoptionCandidate
    {
        internal SavicLegacyAdoptionCandidate(
            SavicProjectInventoryItemRecord record,
            bool eligible,
            bool recommendedForBatch,
            string reason,
            string proposedSavicId)
        {
            Record = record;
            Eligible = eligible;
            RecommendedForBatch = recommendedForBatch;
            Reason = reason ?? string.Empty;
            ProposedSavicId = proposedSavicId ?? string.Empty;
        }

        internal SavicProjectInventoryItemRecord Record { get; }
        internal bool Eligible { get; }
        internal bool RecommendedForBatch { get; }
        internal string Reason { get; }
        internal string ProposedSavicId { get; }
    }

    internal readonly struct SavicLegacyAdoptionResult
    {
        internal SavicLegacyAdoptionResult(
            bool succeeded,
            bool alreadyManaged,
            string savicId,
            string message)
        {
            Succeeded = succeeded;
            AlreadyManaged = alreadyManaged;
            SavicId = savicId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool AlreadyManaged { get; }
        internal string SavicId { get; }
        internal string Message { get; }
    }

    internal readonly struct SavicLegacyAdoptionBatchResult
    {
        internal SavicLegacyAdoptionBatchResult(
            int adopted,
            int alreadyManaged,
            int skipped)
        {
            Adopted = adopted;
            AlreadyManaged = alreadyManaged;
            Skipped = skipped;
        }

        internal int Adopted { get; }
        internal int AlreadyManaged { get; }
        internal int Skipped { get; }
    }

    internal sealed class SavicLegacyAdoptionService
    {
        internal const string Version = "1.0.0";

        private const string OriginDecisionKey = "Origin";
        private const string OriginDecisionValue = "LEGACY_ADOPTION";

        private readonly SavicStorageLayout layout;
        private readonly SavicManifestRepository manifests;
        private readonly SavicProjectInventoryService inventory;

        internal SavicLegacyAdoptionService(
            SavicStorageLayout layout,
            SavicManifestRepository manifests,
            SavicProjectInventoryService inventory)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            this.manifests = manifests ?? throw new ArgumentNullException(nameof(manifests));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        internal IReadOnlyList<SavicLegacyAdoptionCandidate> GetPreview(
            bool rescan)
        {
            SavicProjectInventorySnapshot snapshot;

            if (rescan ||
                !inventory.TryLoadPersisted(out snapshot))
            {
                snapshot = inventory.ScanAndPersist();
            }

            List<SavicLegacyAdoptionCandidate> result =
                new List<SavicLegacyAdoptionCandidate>();

            IReadOnlyList<SavicProjectInventoryItemRecord> items =
                snapshot?.items != null
                    ? snapshot.items
                    : Array.Empty<SavicProjectInventoryItemRecord>();

            for (int index = 0; index < items.Count; index++)
            {
                SavicProjectInventoryItemRecord record = items[index];

                if (record == null ||
                    !string.Equals(
                        record.adoptionState,
                        "LEGACY_PENDING_ADOPTION",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                result.Add(BuildCandidate(snapshot, record));
            }

            result.Sort(
                (left, right) =>
                {
                    int recommendation =
                        right.RecommendedForBatch.CompareTo(
                            left.RecommendedForBatch);

                    if (recommendation != 0)
                        return recommendation;

                    return string.Compare(
                        left.Record?.itemId,
                        right.Record?.itemId,
                        StringComparison.Ordinal);
                });

            return result;
        }

        internal SavicLegacyAdoptionResult AdoptOne(
            string itemAssetPath)
        {
            if (string.IsNullOrWhiteSpace(itemAssetPath))
            {
                return new SavicLegacyAdoptionResult(
                    false,
                    false,
                    string.Empty,
                    "No se indicó un asset legacy para adoptar.");
            }

            SavicProjectInventorySnapshot snapshot =
                inventory.ScanAndPersist();

            SavicProjectInventoryItemRecord record =
                snapshot.items?.FirstOrDefault(
                    item =>
                        item != null &&
                        string.Equals(
                            item.itemAssetPath,
                            itemAssetPath,
                            StringComparison.OrdinalIgnoreCase));

            if (record == null)
            {
                return new SavicLegacyAdoptionResult(
                    false,
                    false,
                    string.Empty,
                    "El asset ya no existe en el inventario canónico.");
            }

            if (record.managedBySavic)
            {
                return new SavicLegacyAdoptionResult(
                    true,
                    true,
                    record.savicId,
                    "El asset ya está gestionado por SAVIC.");
            }

            SavicLegacyAdoptionCandidate candidate =
                BuildCandidate(snapshot, record);

            if (!candidate.Eligible)
            {
                return new SavicLegacyAdoptionResult(
                    false,
                    false,
                    string.Empty,
                    candidate.Reason);
            }

            SavicLegacyAdoptionResult result =
                AdoptRecord(record);

            inventory.ScanAndPersist();
            return result;
        }

        internal SavicLegacyAdoptionBatchResult AdoptRecommended()
        {
            SavicProjectInventorySnapshot snapshot =
                inventory.ScanAndPersist();

            List<SavicLegacyAdoptionCandidate> candidates =
                (snapshot.items ??
                    new List<SavicProjectInventoryItemRecord>())
                .Where(
                    record =>
                        record != null &&
                        string.Equals(
                            record.adoptionState,
                            "LEGACY_PENDING_ADOPTION",
                            StringComparison.Ordinal))
                .Select(record => BuildCandidate(snapshot, record))
                .ToList();

            int adopted = 0;
            int alreadyManaged = 0;
            int skipped = 0;

            foreach (SavicLegacyAdoptionCandidate candidate in candidates)
            {
                if (!candidate.Eligible ||
                    !candidate.RecommendedForBatch)
                {
                    skipped++;
                    continue;
                }

                SavicLegacyAdoptionResult result =
                    AdoptRecord(candidate.Record);

                if (!result.Succeeded)
                {
                    skipped++;
                    continue;
                }

                if (result.AlreadyManaged)
                    alreadyManaged++;
                else
                    adopted++;
            }

            inventory.ScanAndPersist();

            return new SavicLegacyAdoptionBatchResult(
                adopted,
                alreadyManaged,
                skipped);
        }

        internal static string BuildDeterministicSavicId(
            string assetGuid)
        {
            string normalized =
                (assetGuid ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            if (normalized.Length == 0)
                throw new ArgumentException(
                    "Legacy adoption requires an asset GUID.",
                    nameof(assetGuid));

            string hash =
                SavicHashService.ComputeSha256Text(
                    "savic-legacy-placeable-id-v1|" + normalized);

            return hash.Substring(0, 32);
        }

        internal static string BuildDeterministicSourceHash(
            string assetGuid,
            string itemId)
        {
            string normalizedGuid =
                (assetGuid ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

            string normalizedItemId =
                (itemId ?? string.Empty)
                .Trim();

            if (normalizedGuid.Length == 0 ||
                normalizedItemId.Length == 0)
            {
                throw new ArgumentException(
                    "Legacy adoption requires stable GUID and ItemId.");
            }

            return SavicHashService.ComputeSha256Text(
                "savic-legacy-placeable-source-v1|" +
                normalizedGuid +
                "|" +
                normalizedItemId);
        }

        internal static bool LooksLikeTestContent(
            SavicProjectInventoryItemRecord record)
        {
            if (record == null)
                return false;

            string haystack =
                string.Join(
                    "|",
                    record.itemId ?? string.Empty,
                    record.displayName ?? string.Empty,
                    record.itemAssetPath ?? string.Empty);

            return haystack.IndexOf(
                       "test",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   haystack.IndexOf(
                       "prueba",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsLegacyAdoptionManifest(
            SavicManifest manifest)
        {
            if (manifest?.decisions == null)
                return false;

            return manifest.decisions.Any(
                decision =>
                    decision != null &&
                    string.Equals(
                        decision.key,
                        OriginDecisionKey,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        decision.value,
                        OriginDecisionValue,
                        StringComparison.Ordinal));
        }

        private SavicLegacyAdoptionCandidate BuildCandidate(
            SavicProjectInventorySnapshot snapshot,
            SavicProjectInventoryItemRecord record)
        {
            string reason = string.Empty;
            bool eligible = true;

            if (record == null)
            {
                eligible = false;
                reason = "Entrada de inventario nula.";
            }
            else if (!record.inMainCatalog)
            {
                eligible = false;
                reason = "El asset no pertenece al catálogo canónico.";
            }
            else if (string.IsNullOrWhiteSpace(record.itemId))
            {
                eligible = false;
                reason = "El asset no tiene ItemId canónico.";
            }
            else if (string.IsNullOrWhiteSpace(record.assetGuid))
            {
                eligible = false;
                reason = "El asset no tiene GUID estable.";
            }
            else if (
                AssetDatabase.LoadAssetAtPath
                    <RestaurantPlaceableItemDefinition>(
                        record.itemAssetPath) == null)
            {
                eligible = false;
                reason = "No se puede resolver el PlaceableItemDefinition.";
            }
            else if (string.IsNullOrWhiteSpace(record.prefabAssetPath) ||
                     AssetDatabase.LoadAssetAtPath<GameObject>(
                         record.prefabAssetPath) == null)
            {
                eligible = false;
                reason = "No se puede resolver el prefab actual.";
            }

            if (eligible &&
                snapshot?.issues != null &&
                snapshot.issues.Any(
                    issue =>
                        issue != null &&
                        string.Equals(
                            issue.assetPath,
                            record.itemAssetPath,
                            StringComparison.OrdinalIgnoreCase) &&
                        (string.Equals(
                             issue.severity,
                             "ERROR",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(
                             issue.severity,
                             "BLOCKER",
                             StringComparison.OrdinalIgnoreCase))))
            {
                eligible = false;
                reason =
                    "El inventario tiene una incidencia bloqueante para este asset.";
            }

            if (eligible &&
                FindManifestByContentId(record.itemId) != null)
            {
                eligible = false;
                reason =
                    "Ya existe una identidad SAVIC para este ContentId.";
            }

            bool testContent =
                LooksLikeTestContent(record);

            bool recommended =
                eligible && !testContent;

            if (eligible)
            {
                reason = testContent
                    ? "Contenido de prueba detectado: adopción manual únicamente."
                    : "Listo para adopción no destructiva.";
            }

            string proposedSavicId =
                string.IsNullOrWhiteSpace(record?.assetGuid)
                    ? string.Empty
                    : BuildDeterministicSavicId(record.assetGuid);

            return new SavicLegacyAdoptionCandidate(
                record,
                eligible,
                recommended,
                reason,
                proposedSavicId);
        }

        private SavicLegacyAdoptionResult AdoptRecord(
            SavicProjectInventoryItemRecord record)
        {
            SavicManifest existing =
                FindManifestByContentId(record.itemId);

            if (existing != null)
            {
                return new SavicLegacyAdoptionResult(
                    true,
                    true,
                    existing.savicId,
                    "La identidad SAVIC ya existía; no se duplicó nada.");
            }

            string savicId =
                BuildDeterministicSavicId(
                    record.assetGuid);

            string sourceHash =
                BuildDeterministicSourceHash(
                    record.assetGuid,
                    record.itemId);

            if (manifests.TryGetBySavicId(
                    savicId,
                    out SavicManifest existingId))
            {
                if (string.Equals(
                        existingId.canonicalContentId,
                        record.itemId,
                        StringComparison.Ordinal))
                {
                    return new SavicLegacyAdoptionResult(
                        true,
                        true,
                        existingId.savicId,
                        "La identidad determinista ya estaba registrada.");
                }

                return new SavicLegacyAdoptionResult(
                    false,
                    false,
                    string.Empty,
                    "La identidad determinista colisiona con otro manifest.");
            }

            if (manifests.TryGetBySourceHash(
                    sourceHash,
                    out SavicManifest existingSource))
            {
                if (string.Equals(
                        existingSource.canonicalContentId,
                        record.itemId,
                        StringComparison.Ordinal))
                {
                    return new SavicLegacyAdoptionResult(
                        true,
                        true,
                        existingSource.savicId,
                        "La fuente legacy ya estaba registrada.");
                }

                return new SavicLegacyAdoptionResult(
                    false,
                    false,
                    string.Empty,
                    "El fingerprint legacy pertenece a otro manifest.");
            }

            SavicManifest manifest =
                BuildManifest(
                    record,
                    savicId,
                    sourceHash);

            manifests.Save(manifest);

            return new SavicLegacyAdoptionResult(
                true,
                false,
                manifest.savicId,
                "Asset adoptado sin modificar prefab, GUID, materiales ni catálogo.");
        }

        private SavicManifest BuildManifest(
            SavicProjectInventoryItemRecord record,
            string savicId,
            string sourceHash)
        {
            string now =
                DateTime.UtcNow.ToString("O");

            string itemAbsolutePath =
                layout.FromProjectRelativePath(
                    record.itemAssetPath);

            long byteLength = 0;
            long lastWriteTicks = 0;

            if (File.Exists(itemAbsolutePath))
            {
                FileInfo info = new FileInfo(itemAbsolutePath);
                byteLength = info.Length;
                lastWriteTicks = info.LastWriteTimeUtc.Ticks;
            }

            string type =
                InferType(record);

            string family =
                InferFamily(record);

            SavicManifest manifest =
                new SavicManifest
                {
                    savicId = savicId,
                    canonicalContentId = record.itemId,
                    status = "PUBLISHED",
                    family = family,
                    type = type,
                    category = record.category ?? "Unknown",
                    createdUtc = now,
                    updatedUtc = now,
                    source = new SavicSourceRecord
                    {
                        sourceHash = sourceHash,
                        originalFileName =
                            Path.GetFileName(record.itemAssetPath),
                        extension =
                            Path.GetExtension(record.itemAssetPath)
                                .ToLowerInvariant(),
                        sourceKind =
                            SavicSourceKind.StructuredData.ToString(),
                        archivedRelativePath =
                            record.itemAssetPath ?? string.Empty,
                        byteLength = byteLength,
                        originalLastWriteUtcTicks = lastWriteTicks,
                        ingestedUtc = now
                    },
                    classification =
                        new SavicClassificationRecord
                        {
                            classified = true,
                            classifierVersion = Version,
                            family = family,
                            type = type,
                            category =
                                record.category ?? "Unknown",
                            confidence = "EXPLICIT",
                            score = 1f,
                            explicitTypeToken = true,
                            nameBacked = true,
                            geometryBacked = false,
                            evidence =
                                "Adopted from the canonical Bistro Builder " +
                                "placeable definition without geometry mutation.",
                            classifiedUtc = now
                        }
                };

            manifest.decisions.Add(
                new SavicDecisionRecord
                {
                    key = OriginDecisionKey,
                    value = OriginDecisionValue,
                    confidence = "EXPLICIT",
                    evidence =
                        "Existing canonical content adopted in place.",
                    ruleId = "LegacyAdoption.Origin.v1"
                });

            manifest.decisions.Add(
                new SavicDecisionRecord
                {
                    key = "Legacy.AssetGuid",
                    value = record.assetGuid ?? string.Empty,
                    confidence = "EXPLICIT",
                    evidence = record.itemAssetPath ?? string.Empty,
                    ruleId = "LegacyAdoption.Identity.v1"
                });

            manifest.decisions.Add(
                new SavicDecisionRecord
                {
                    key = "Legacy.PrefabGuid",
                    value = record.prefabGuid ?? string.Empty,
                    confidence = "EXPLICIT",
                    evidence = record.prefabAssetPath ?? string.Empty,
                    ruleId = "LegacyAdoption.Identity.v1"
                });

            AddArtifact(
                manifest,
                "catalog.item_definition",
                record.itemAssetPath,
                record.dependencyHash);

            AddArtifact(
                manifest,
                TypePrefabRole(type),
                record.prefabAssetPath,
                Fingerprint(record.prefabAssetPath));

            AddArtifact(
                manifest,
                "preview.catalog",
                record.catalogIconAssetPath,
                Fingerprint(record.catalogIconAssetPath));

            AddArtifact(
                manifest,
                "preview.large",
                record.inspectorPreviewAssetPath,
                Fingerprint(record.inspectorPreviewAssetPath));

            manifest.validations.Add(
                new SavicValidationRecord
                {
                    validationId = "LegacyAdoption.Identity",
                    result = "PASS",
                    severity = "INFO",
                    message =
                        "Stable ItemId, asset GUID and deterministic SAVIC identity recorded.",
                    validatorVersion = Version
                });

            manifest.validations.Add(
                new SavicValidationRecord
                {
                    validationId = "LegacyAdoption.References",
                    result = "PASS",
                    severity = "INFO",
                    message =
                        "Existing item definition and prefab references resolve without replacement.",
                    validatorVersion = Version
                });

            manifest.validations.Add(
                new SavicValidationRecord
                {
                    validationId = "LegacyAdoption.NonDestructive",
                    result = "PASS",
                    severity = "INFO",
                    message =
                        "Adoption writes only SAVIC metadata; existing game assets are not rewritten.",
                    validatorVersion = Version
                });

            return manifest;
        }

        private SavicManifest FindManifestByContentId(
            string contentId)
        {
            if (string.IsNullOrWhiteSpace(contentId))
                return null;

            IReadOnlyList<SavicManifest> all =
                manifests.GetAll();

            for (int index = 0; index < all.Count; index++)
            {
                SavicManifest manifest = all[index];

                if (manifest != null &&
                    string.Equals(
                        manifest.canonicalContentId,
                        contentId,
                        StringComparison.Ordinal))
                {
                    return manifest;
                }
            }

            return null;
        }

        private static void AddArtifact(
            SavicManifest manifest,
            string role,
            string assetPath,
            string fingerprint)
        {
            if (manifest == null ||
                string.IsNullOrWhiteSpace(assetPath))
            {
                return;
            }

            if (manifest.artifacts.Any(
                    artifact =>
                        artifact != null &&
                        string.Equals(
                            artifact.role,
                            role,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            artifact.projectRelativePath,
                            assetPath,
                            StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            manifest.artifacts.Add(
                new SavicArtifactRecord
                {
                    role = role ?? string.Empty,
                    projectRelativePath =
                        assetPath ?? string.Empty,
                    builderId = "savic.legacy-adoption",
                    builderVersion = Version,
                    inputFingerprint =
                        fingerprint ?? string.Empty
                });
        }

        private static string Fingerprint(
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            try
            {
                return AssetDatabase
                    .GetAssetDependencyHash(assetPath)
                    .ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string TypePrefabRole(
            string type)
        {
            if (string.Equals(
                    type,
                    "Table",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "published.table.prefab";
            }

            if (string.Equals(
                    type,
                    "Chair",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "published.chair.prefab";
            }

            return "published.legacy.prefab";
        }

        private static string InferFamily(
            SavicProjectInventoryItemRecord record)
        {
            string category =
                record?.category ?? string.Empty;

            if (string.Equals(
                    category,
                    "Furniture",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    category,
                    "Seating",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Furniture";
            }

            if (string.IsNullOrWhiteSpace(category))
                return "Unknown";

            return category;
        }

        private static string InferType(
            SavicProjectInventoryItemRecord record)
        {
            string category =
                record?.category ?? string.Empty;

            if (string.Equals(
                    category,
                    "Seating",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Chair";
            }

            string haystack =
                string.Join(
                    "|",
                    record?.itemId ?? string.Empty,
                    record?.displayName ?? string.Empty,
                    record?.prefabAssetPath ?? string.Empty);

            if (string.Equals(
                    category,
                    "Furniture",
                    StringComparison.OrdinalIgnoreCase) &&
                (haystack.IndexOf(
                     "table",
                     StringComparison.OrdinalIgnoreCase) >= 0 ||
                 haystack.IndexOf(
                     "mesa",
                     StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return "Table";
            }

            return string.IsNullOrWhiteSpace(category)
                ? "Unknown"
                : category;
        }
    }
}
