using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Almacenamiento local por generaciones inmutables.
///
/// El contenido completo se escribe en una generación nueva. Solo cuando
/// todos los archivos y checksums son válidos se sustituye el pequeño
/// puntero current.json. Una interrupción nunca sobrescribe la última
/// partida válida.
/// </summary>
public sealed class BistroBuilderFileSaveStorage :
    IBistroBuilderSaveStorage
{
    private const string CurrentPointerFileName = "current.json";
    private const string PreviousPointerFileName = "previous.json";
    private const string GenerationsFolderName = "generations";
    private const string ManifestFileName = "manifest.json";
    private const string SectionsFolderName = "sections";

    private readonly string rootPath;
    private readonly IBistroBuilderSaveSerializer metadataSerializer;
    private readonly int retainedGenerationCount;
    private readonly bool prettyPrintMetadata;

    public string RootPath => rootPath;

    public BistroBuilderFileSaveStorage(
        string rootPath,
        IBistroBuilderSaveSerializer metadataSerializer,
        int retainedGenerationCount,
        bool prettyPrintMetadata
    )
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(
                "La ruta raíz de guardado está vacía.",
                nameof(rootPath)
            );
        }

        this.rootPath = Path.GetFullPath(rootPath);
        this.metadataSerializer = metadataSerializer ??
            throw new ArgumentNullException(
                nameof(metadataSerializer)
            );
        this.retainedGenerationCount = Math.Max(
            2,
            retainedGenerationCount
        );
        this.prettyPrintMetadata = prettyPrintMetadata;
    }

    public Task<BistroBuilderStorageWriteResult>
        WriteGenerationAsync(
            BistroBuilderStorageWriteRequest request,
            CancellationToken cancellationToken
        )
    {
        return Task.Run(
            () => WriteGenerationInternal(
                request,
                cancellationToken
            ),
            cancellationToken
        );
    }

    public Task<BistroBuilderStorageReadResult>
        ReadLatestValidGenerationAsync(
            int slotIndex,
            CancellationToken cancellationToken
        )
    {
        return Task.Run(
            () => ReadLatestValidGenerationInternal(
                slotIndex,
                cancellationToken
            ),
            cancellationToken
        );
    }

    /// <summary>
    /// Lee únicamente manifiestos para construir el menú de partidas.
    /// No carga payloads de sistemas ni toca objetos de Unity.
    /// </summary>
    public Task<IReadOnlyList<BistroBuilderSaveSlotSummary>>
        ReadAllSlotSummariesAsync(
            CancellationToken cancellationToken
        )
    {
        return Task.Run<IReadOnlyList<BistroBuilderSaveSlotSummary>>(
            () => ReadAllSlotSummariesInternal(cancellationToken),
            cancellationToken
        );
    }

    public Task<bool> DeleteSlotAsync(
        int slotIndex,
        CancellationToken cancellationToken
    )
    {
        return Task.Run(
            () =>
            {
                ValidateSlotIndex(slotIndex);
                cancellationToken.ThrowIfCancellationRequested();

                string slotPath = GetSlotPath(slotIndex);

                if (!Directory.Exists(slotPath))
                {
                    return true;
                }

                Directory.Delete(slotPath, true);
                return true;
            },
            cancellationToken
        );
    }

    public bool SlotExists(int slotIndex)
    {
        string slotPath = GetSlotPath(slotIndex);

        if (File.Exists(
                Path.Combine(slotPath, CurrentPointerFileName)
            ) ||
            File.Exists(
                Path.Combine(slotPath, PreviousPointerFileName)
            ))
        {
            return true;
        }

        string generationsPath = Path.Combine(
            slotPath,
            GenerationsFolderName
        );

        if (!Directory.Exists(generationsPath))
        {
            return false;
        }

        string[] generations = Directory.GetDirectories(generationsPath);

        for (int index = 0; index < generations.Length; index++)
        {
            string name = Path.GetFileName(generations[index]);

            if (!name.EndsWith(
                    ".tmp",
                    StringComparison.OrdinalIgnoreCase
                ) &&
                File.Exists(
                    Path.Combine(
                        generations[index],
                        ManifestFileName
                    )
                ))
            {
                return true;
            }
        }

        return false;
    }

    public string GetSlotPath(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);

        return Path.Combine(
            rootPath,
            BuildSlotFolderName(slotIndex)
        );
    }

    public static string BuildSlotFolderName(int slotIndex)
    {
        ValidateSlotIndex(slotIndex);

        return "slot_" +
               slotIndex.ToString(
                   "D3",
                   CultureInfo.InvariantCulture
               );
    }

    public static string ComputeSha256(string value)
    {
        return ComputeSha256(
            Encoding.UTF8.GetBytes(value ?? string.Empty)
        );
    }

    public static string ComputeSha256(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        using (SHA256 algorithm = SHA256.Create())
        {
            byte[] hash = algorithm.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(
                hash.Length * 2
            );

            for (int index = 0;
                 index < hash.Length;
                 index++)
            {
                builder.Append(
                    hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture
                    )
                );
            }

            return builder.ToString();
        }
    }

    private BistroBuilderStorageWriteResult
        WriteGenerationInternal(
            BistroBuilderStorageWriteRequest request,
            CancellationToken cancellationToken
        )
    {
        if (request == null)
        {
            return BistroBuilderStorageWriteResult.Failure(
                "La solicitud de escritura es nula."
            );
        }

        if (request.Sections == null ||
            request.Sections.Count == 0)
        {
            return BistroBuilderStorageWriteResult.Failure(
                "La partida no contiene ninguna sección."
            );
        }

        ValidateSlotIndex(request.SlotIndex);
        cancellationToken.ThrowIfCancellationRequested();

        string slotPath = GetSlotPath(request.SlotIndex);
        string generationsPath = Path.Combine(
            slotPath,
            GenerationsFolderName
        );

        Directory.CreateDirectory(generationsPath);

        string generationId = BuildGenerationId();
        string temporaryGenerationPath = Path.Combine(
            generationsPath,
            generationId + ".tmp"
        );
        string finalGenerationPath = Path.Combine(
            generationsPath,
            generationId
        );

        DeleteDirectoryIfPresent(temporaryGenerationPath);
        Directory.CreateDirectory(temporaryGenerationPath);

        try
        {
            string sectionsPath = Path.Combine(
                temporaryGenerationPath,
                SectionsFolderName
            );
            Directory.CreateDirectory(sectionsPath);

            BistroBuilderSaveManifest manifest =
                new BistroBuilderSaveManifest
                {
                    formatVersion = 1,
                    slotIndex = request.SlotIndex,
                    slotDisplayName = request.SlotDisplayName,
                    generationId = generationId,
                    createdUtc = DateTime.UtcNow.ToString("O"),
                    applicationVersion = request.ApplicationVersion,
                    sceneName = request.SceneName,
                    metadataSerializerId =
                        metadataSerializer.SerializerId
                };

            manifest.sections.Capacity = request.Sections.Count;

            HashSet<string> usedSectionIds =
                new HashSet<string>(StringComparer.Ordinal);
            long totalPayloadBytes = 0L;

            for (int index = 0;
                 index < request.Sections.Count;
                 index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                BistroBuilderSerializedSaveSection section =
                    request.Sections[index];

                ValidateSerializedSection(section);

                string sectionId = NormalizeId(section.SectionId);

                if (!usedSectionIds.Add(sectionId))
                {
                    throw new InvalidDataException(
                        "La sección " + sectionId + " está duplicada."
                    );
                }

                string fileName =
                    SanitizeFileName(sectionId) +
                    NormalizeExtension(section.FileExtension);
                string relativePath = Path.Combine(
                        SectionsFolderName,
                        fileName
                    )
                    .Replace('\\', '/');
                string absolutePath = ResolveContainedPath(
                    temporaryGenerationPath,
                    relativePath
                );

                WriteBytesDurably(
                    absolutePath,
                    section.Payload,
                    cancellationToken
                );

                manifest.sections.Add(
                    new BistroBuilderSaveSectionManifest
                    {
                        sectionId = sectionId,
                        sectionVersion = section.SectionVersion,
                        serializerId = NormalizeId(
                            section.SerializerId
                        ),
                        relativePath = relativePath,
                        byteCount = section.Payload.LongLength,
                        sha256 = ComputeSha256(section.Payload)
                    }
                );

                totalPayloadBytes += section.Payload.LongLength;
            }

            manifest.totalPayloadBytes = totalPayloadBytes;
            byte[] manifestBytes = metadataSerializer.Serialize(
                manifest,
                prettyPrintMetadata
            );

            WriteBytesDurably(
                Path.Combine(
                    temporaryGenerationPath,
                    ManifestFileName
                ),
                manifestBytes,
                cancellationToken
            );

            string manifestHash = ComputeSha256(manifestBytes);
            cancellationToken.ThrowIfCancellationRequested();

            Directory.Move(
                temporaryGenerationPath,
                finalGenerationPath
            );

            ReplaceCurrentPointer(
                slotPath,
                new BistroBuilderSaveSlotPointer
                {
                    formatVersion = 1,
                    generationId = generationId,
                    manifestSha256 = manifestHash,
                    updatedUtc = DateTime.UtcNow.ToString("O")
                },
                cancellationToken
            );

            CleanupOldGenerations(
                slotPath,
                cancellationToken
            );

            return BistroBuilderStorageWriteResult.Success(
                generationId,
                totalPayloadBytes
            );
        }
        catch (OperationCanceledException)
        {
            DeleteDirectoryIfPresent(temporaryGenerationPath);
            throw;
        }
        catch (Exception exception)
        {
            DeleteDirectoryIfPresent(temporaryGenerationPath);

            return BistroBuilderStorageWriteResult.Failure(
                "No se pudo guardar la partida: " +
                exception.Message
            );
        }
    }

    private BistroBuilderStorageReadResult
        ReadLatestValidGenerationInternal(
            int slotIndex,
            CancellationToken cancellationToken
        )
    {
        ValidateSlotIndex(slotIndex);
        cancellationToken.ThrowIfCancellationRequested();

        string slotPath = GetSlotPath(slotIndex);

        if (!Directory.Exists(slotPath))
        {
            return BistroBuilderStorageReadResult.Failure(
                "El slot " + slotIndex + " no existe."
            );
        }

        List<GenerationCandidate> candidates =
            BuildGenerationCandidates(
                slotPath,
                cancellationToken
            );

        if (candidates.Count == 0)
        {
            return BistroBuilderStorageReadResult.Failure(
                "No existe ninguna generación recuperable."
            );
        }

        List<string> errors = new List<string>();

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GenerationCandidate candidate = candidates[index];

            if (TryReadGeneration(
                    slotIndex,
                    slotPath,
                    candidate,
                    cancellationToken,
                    out BistroBuilderStorageReadPackage package,
                    out string error
                ))
            {
                return BistroBuilderStorageReadResult.Success(package);
            }

            errors.Add(candidate.GenerationId + ": " + error);
        }

        return BistroBuilderStorageReadResult.Failure(
            "Todas las generaciones del slot son inválidas. " +
            string.Join(" | ", errors.ToArray())
        );
    }

    private bool TryReadGeneration(
        int expectedSlotIndex,
        string slotPath,
        GenerationCandidate candidate,
        CancellationToken cancellationToken,
        out BistroBuilderStorageReadPackage package,
        out string error
    )
    {
        package = null;
        error = string.Empty;

        if (candidate == null ||
            string.IsNullOrWhiteSpace(candidate.GenerationId))
        {
            error = "El puntero no contiene generación.";
            return false;
        }

        string generationPath = Path.Combine(
            slotPath,
            GenerationsFolderName,
            candidate.GenerationId
        );
        string manifestPath = Path.Combine(
            generationPath,
            ManifestFileName
        );

        if (!File.Exists(manifestPath))
        {
            error = "Falta manifest.json.";
            return false;
        }

        cancellationToken.ThrowIfCancellationRequested();
        byte[] manifestBytes = File.ReadAllBytes(manifestPath);

        if (!string.IsNullOrWhiteSpace(
                candidate.ManifestSha256
            ) &&
            !HashesMatch(
                candidate.ManifestSha256,
                ComputeSha256(manifestBytes)
            ))
        {
            error = "El checksum del manifiesto no coincide.";
            return false;
        }

        BistroBuilderSaveManifest manifest;

        try
        {
            manifest =
                (BistroBuilderSaveManifest)
                metadataSerializer.Deserialize(
                    manifestBytes,
                    typeof(BistroBuilderSaveManifest)
                );
        }
        catch (Exception exception)
        {
            error = "El manifiesto no puede leerse: " +
                    exception.Message;
            return false;
        }

        if (!ValidateManifest(
                manifest,
                candidate.GenerationId,
                out error
            ))
        {
            return false;
        }

        if (manifest.slotIndex != expectedSlotIndex)
        {
            error = "El manifiesto pertenece a otro slot.";
            return false;
        }

        List<BistroBuilderStoredSaveSection> sections =
            new List<BistroBuilderStoredSaveSection>(
                manifest.sections.Count
            );
        long measuredPayloadBytes = 0L;

        for (int index = 0;
             index < manifest.sections.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BistroBuilderSaveSectionManifest section =
                manifest.sections[index];
            string sectionPath;

            try
            {
                sectionPath = ResolveContainedPath(
                    generationPath,
                    section.relativePath
                );
            }
            catch (Exception exception)
            {
                error = "Ruta de sección inválida: " +
                        exception.Message;
                return false;
            }

            if (!File.Exists(sectionPath))
            {
                error = "Falta la sección " +
                        section.sectionId + ".";
                return false;
            }

            byte[] payload = File.ReadAllBytes(sectionPath);

            if (payload.LongLength != section.byteCount)
            {
                error = "El tamaño de " + section.sectionId +
                        " no coincide.";
                return false;
            }

            if (!HashesMatch(
                    section.sha256,
                    ComputeSha256(payload)
                ))
            {
                error = "El checksum de " + section.sectionId +
                        " no coincide.";
                return false;
            }

            sections.Add(
                new BistroBuilderStoredSaveSection(
                    NormalizeId(section.sectionId),
                    section.sectionVersion,
                    NormalizeId(section.serializerId),
                    payload
                )
            );
            measuredPayloadBytes += payload.LongLength;
        }

        if (measuredPayloadBytes != manifest.totalPayloadBytes)
        {
            error = "El payload total del manifiesto no coincide.";
            return false;
        }

        package = new BistroBuilderStorageReadPackage(
            manifest,
            sections,
            candidate.IsFallback
        );
        return true;
    }

    private static bool ValidateManifest(
        BistroBuilderSaveManifest manifest,
        string expectedGenerationId,
        out string error
    )
    {
        error = string.Empty;

        if (manifest == null)
        {
            error = "El manifiesto es nulo.";
            return false;
        }

        if (manifest.formatVersion != 1)
        {
            error = "Versión de manifiesto no compatible.";
            return false;
        }

        if (!string.Equals(
                manifest.generationId,
                expectedGenerationId,
                StringComparison.Ordinal
            ))
        {
            error = "La identidad del manifiesto no coincide.";
            return false;
        }

        if (manifest.slotIndex < 1 ||
            manifest.slotIndex > 999 ||
            string.IsNullOrWhiteSpace(manifest.createdUtc) ||
            string.IsNullOrWhiteSpace(manifest.sceneName) ||
            !IsSafeStableId(manifest.metadataSerializerId) ||
            manifest.totalPayloadBytes < 1 ||
            manifest.sections == null ||
            manifest.sections.Count == 0)
        {
            error = "El manifiesto está incompleto.";
            return false;
        }

        HashSet<string> sectionIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> relativePaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < manifest.sections.Count;
             index++)
        {
            BistroBuilderSaveSectionManifest section =
                manifest.sections[index];

            if (section == null ||
                !IsSafeStableId(section.sectionId) ||
                section.sectionVersion < 1 ||
                !IsSafeStableId(section.serializerId) ||
                string.IsNullOrWhiteSpace(section.relativePath) ||
                section.byteCount < 1 ||
                string.IsNullOrWhiteSpace(section.sha256))
            {
                error = "Existe una sección de manifiesto inválida.";
                return false;
            }

            string normalizedSectionId =
                NormalizeId(section.sectionId);
            string normalizedRelativePath =
                section.relativePath.Replace('\\', '/');

            if (!sectionIds.Add(normalizedSectionId))
            {
                error = "El manifiesto contiene secciones duplicadas.";
                return false;
            }

            if (!normalizedRelativePath.StartsWith(
                    SectionsFolderName + "/",
                    StringComparison.Ordinal
                ) ||
                !relativePaths.Add(normalizedRelativePath))
            {
                error =
                    "El manifiesto contiene rutas de sección inválidas " +
                    "o duplicadas.";
                return false;
            }
        }

        return true;
    }

    private List<GenerationCandidate> BuildGenerationCandidates(
        string slotPath,
        CancellationToken cancellationToken
    )
    {
        List<GenerationCandidate> candidates =
            new List<GenerationCandidate>();
        HashSet<string> usedPointerIds =
            new HashSet<string>(StringComparer.Ordinal);

        AddPointerCandidate(
            slotPath,
            CurrentPointerFileName,
            false,
            candidates,
            usedPointerIds
        );
        AddPointerCandidate(
            slotPath,
            PreviousPointerFileName,
            true,
            candidates,
            usedPointerIds
        );

        string generationsPath = Path.Combine(
            slotPath,
            GenerationsFolderName
        );

        if (!Directory.Exists(generationsPath))
        {
            return candidates;
        }

        DirectoryInfo[] directories =
            new DirectoryInfo(generationsPath).GetDirectories();
        /*
         * La identidad contiene ticks UTC. Ordenar por nombre evita
         * depender de CreationTimeUtc, que puede cambiar al copiar una
         * carpeta entre discos o restaurarla desde la nube.
         */
        Array.Sort(
            directories,
            (first, second) => string.Compare(
                second.Name,
                first.Name,
                StringComparison.Ordinal
            )
        );

        HashSet<string> scannedIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < directories.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo directory = directories[index];

            if (directory.Name.EndsWith(
                    ".tmp",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !scannedIds.Add(directory.Name))
            {
                continue;
            }

            /*
             * Se añade incluso si current/previous ya apuntan a la
             * misma generación. De este modo un checksum corrupto en el
             * puntero no impide recuperar una generación cuyo manifiesto
             * y payload siguen siendo válidos.
             */
            candidates.Add(
                new GenerationCandidate(
                    directory.Name,
                    string.Empty,
                    true
                )
            );
        }

        return candidates;
    }

    private void AddPointerCandidate(
        string slotPath,
        string pointerFileName,
        bool isFallback,
        List<GenerationCandidate> candidates,
        HashSet<string> usedGenerationIds
    )
    {
        string pointerPath = Path.Combine(
            slotPath,
            pointerFileName
        );

        if (!File.Exists(pointerPath))
        {
            return;
        }

        try
        {
            BistroBuilderSaveSlotPointer pointer =
                (BistroBuilderSaveSlotPointer)
                metadataSerializer.Deserialize(
                    File.ReadAllBytes(pointerPath),
                    typeof(BistroBuilderSaveSlotPointer)
                );

            if (pointer == null ||
                pointer.formatVersion != 1 ||
                string.IsNullOrWhiteSpace(pointer.generationId) ||
                !usedGenerationIds.Add(pointer.generationId))
            {
                return;
            }

            candidates.Add(
                new GenerationCandidate(
                    pointer.generationId,
                    pointer.manifestSha256,
                    isFallback
                )
            );
        }
        catch
        {
            // Un puntero corrupto no invalida las generaciones.
        }
    }

    private IReadOnlyList<BistroBuilderSaveSlotSummary>
        ReadAllSlotSummariesInternal(
            CancellationToken cancellationToken
        )
    {
        List<BistroBuilderSaveSlotSummary> summaries =
            new List<BistroBuilderSaveSlotSummary>();

        if (!Directory.Exists(rootPath))
        {
            return summaries;
        }

        DirectoryInfo[] slotDirectories =
            new DirectoryInfo(rootPath).GetDirectories("slot_*");

        for (int index = 0;
             index < slotDirectories.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryParseSlotIndex(
                    slotDirectories[index].Name,
                    out int slotIndex
                ))
            {
                continue;
            }

            if (TryReadSlotSummary(
                    slotIndex,
                    cancellationToken,
                    out BistroBuilderSaveSlotSummary summary
                ))
            {
                summaries.Add(summary);
            }
        }

        summaries.Sort(
            (first, second) =>
                first.SlotIndex.CompareTo(second.SlotIndex)
        );
        return summaries;
    }

    private bool TryReadSlotSummary(
        int slotIndex,
        CancellationToken cancellationToken,
        out BistroBuilderSaveSlotSummary summary
    )
    {
        summary = null;
        string slotPath = GetSlotPath(slotIndex);
        List<GenerationCandidate> candidates =
            BuildGenerationCandidates(slotPath, cancellationToken);

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GenerationCandidate candidate = candidates[index];
            string manifestPath = Path.Combine(
                slotPath,
                GenerationsFolderName,
                candidate.GenerationId,
                ManifestFileName
            );

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            try
            {
                byte[] manifestBytes = File.ReadAllBytes(manifestPath);

                if (!string.IsNullOrWhiteSpace(
                        candidate.ManifestSha256
                    ) &&
                    !HashesMatch(
                        candidate.ManifestSha256,
                        ComputeSha256(manifestBytes)
                    ))
                {
                    continue;
                }

                BistroBuilderSaveManifest manifest =
                    (BistroBuilderSaveManifest)
                    metadataSerializer.Deserialize(
                        manifestBytes,
                        typeof(BistroBuilderSaveManifest)
                    );

                if (!ValidateManifest(
                        manifest,
                        candidate.GenerationId,
                        out _
                    ) ||
                    manifest.slotIndex != slotIndex)
                {
                    continue;
                }

                summary = new BistroBuilderSaveSlotSummary(
                    manifest.slotIndex,
                    manifest.slotDisplayName,
                    manifest.generationId,
                    manifest.createdUtc,
                    manifest.applicationVersion,
                    manifest.sceneName,
                    manifest.totalPayloadBytes,
                    candidate.IsFallback
                );
                return true;
            }
            catch
            {
                // El menú omite slots sin manifiesto recuperable.
            }
        }

        return false;
    }

    private void ReplaceCurrentPointer(
        string slotPath,
        BistroBuilderSaveSlotPointer pointer,
        CancellationToken cancellationToken
    )
    {
        Directory.CreateDirectory(slotPath);

        string currentPath = Path.Combine(
            slotPath,
            CurrentPointerFileName
        );
        string previousPath = Path.Combine(
            slotPath,
            PreviousPointerFileName
        );
        string temporaryPath = Path.Combine(
            slotPath,
            "current." + Guid.NewGuid().ToString("N") + ".tmp"
        );

        WriteBytesDurably(
            temporaryPath,
            metadataSerializer.Serialize(
                pointer,
                prettyPrintMetadata
            ),
            cancellationToken
        );

        if (!File.Exists(currentPath))
        {
            File.Move(temporaryPath, currentPath);
            return;
        }

        try
        {
            if (File.Exists(previousPath))
            {
                File.Delete(previousPath);
            }

            File.Replace(
                temporaryPath,
                currentPath,
                previousPath,
                true
            );
        }
        catch (PlatformNotSupportedException)
        {
            ReplacePointerFallback(
                temporaryPath,
                currentPath,
                previousPath
            );
        }
        catch (IOException)
        {
            ReplacePointerFallback(
                temporaryPath,
                currentPath,
                previousPath
            );
        }
    }

    private static void ReplacePointerFallback(
        string temporaryPath,
        string currentPath,
        string previousPath
    )
    {
        if (File.Exists(currentPath))
        {
            File.Copy(currentPath, previousPath, true);
            File.Delete(currentPath);
        }

        File.Move(temporaryPath, currentPath);
    }

    private void CleanupOldGenerations(
        string slotPath,
        CancellationToken cancellationToken
    )
    {
        string generationsPath = Path.Combine(
            slotPath,
            GenerationsFolderName
        );

        if (!Directory.Exists(generationsPath))
        {
            return;
        }

        HashSet<string> protectedIds =
            new HashSet<string>(StringComparer.Ordinal);
        AddPointerGenerationId(
            Path.Combine(slotPath, CurrentPointerFileName),
            protectedIds
        );
        AddPointerGenerationId(
            Path.Combine(slotPath, PreviousPointerFileName),
            protectedIds
        );

        DirectoryInfo[] directories =
            new DirectoryInfo(generationsPath).GetDirectories();
        /*
         * La identidad contiene ticks UTC. Ordenar por nombre evita
         * depender de CreationTimeUtc, que puede cambiar al copiar una
         * carpeta entre discos o restaurarla desde la nube.
         */
        Array.Sort(
            directories,
            (first, second) => string.Compare(
                second.Name,
                first.Name,
                StringComparison.Ordinal
            )
        );

        int retained = 0;

        for (int index = 0;
             index < directories.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo directory = directories[index];

            if (directory.Name.EndsWith(
                    ".tmp",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                DeleteDirectoryIfPresent(directory.FullName);
                continue;
            }

            bool mustKeep =
                protectedIds.Contains(directory.Name) ||
                retained < retainedGenerationCount;

            if (mustKeep)
            {
                retained++;
                continue;
            }

            DeleteDirectoryIfPresent(directory.FullName);
        }
    }

    private void AddPointerGenerationId(
        string pointerPath,
        HashSet<string> results
    )
    {
        if (!File.Exists(pointerPath))
        {
            return;
        }

        try
        {
            BistroBuilderSaveSlotPointer pointer =
                (BistroBuilderSaveSlotPointer)
                metadataSerializer.Deserialize(
                    File.ReadAllBytes(pointerPath),
                    typeof(BistroBuilderSaveSlotPointer)
                );

            if (pointer != null &&
                pointer.formatVersion == 1 &&
                !string.IsNullOrWhiteSpace(pointer.generationId))
            {
                results.Add(pointer.generationId);
            }
        }
        catch
        {
            // La limpieza no destruye datos por un puntero ilegible.
        }
    }

    private static void ValidateSerializedSection(
        BistroBuilderSerializedSaveSection section
    )
    {
        if (section == null ||
            !IsSafeStableId(section.SectionId) ||
            section.SectionVersion < 1 ||
            !IsSafeStableId(section.SerializerId) ||
            !IsSafeFileExtension(section.FileExtension) ||
            section.Payload == null ||
            section.Payload.Length == 0)
        {
            throw new InvalidDataException(
                "Existe una sección serializada inválida."
            );
        }
    }

    private static bool IsSafeStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        for (int index = 0; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= 'A' && character <= 'Z' ||
                character >= '0' && character <= '9' ||
                character == '.' ||
                character == '_' ||
                character == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        string trimmed = extension.Trim();

        if (!trimmed.StartsWith(".", StringComparison.Ordinal) ||
            trimmed.Length < 2)
        {
            return false;
        }

        for (int index = 1; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= 'A' && character <= 'Z' ||
                character >= '0' && character <= '9' ||
                character == '_' ||
                character == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveContainedPath(
        string root,
        string relativePath
    )
    {
        if (string.IsNullOrWhiteSpace(root) ||
            string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidDataException(
                "La ruta relativa está vacía o es absoluta."
            );
        }

        string fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string combined = Path.GetFullPath(
            Path.Combine(
                fullRoot,
                relativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar
                )
            )
        );

        StringComparison pathComparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        if (!combined.StartsWith(
                fullRoot,
                pathComparison
            ))
        {
            throw new InvalidDataException(
                "La ruta intenta salir de la generación."
            );
        }

        return combined;
    }

    private static void WriteBytesDurably(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string directory = Path.GetDirectoryName(path);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (FileStream stream = new FileStream(
                   path,
                   FileMode.Create,
                   FileAccess.Write,
                   FileShare.None,
                   65536,
                   FileOptions.SequentialScan
               ))
        {
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(true);
        }
    }

    private static string BuildGenerationId()
    {
        return "generation_" +
               DateTime.UtcNow.Ticks.ToString(
                   "D19",
                   CultureInfo.InvariantCulture
               ) +
               "_" + Guid.NewGuid().ToString("N");
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".dat";
        }

        string trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal)
            ? trimmed
            : "." + trimmed;
    }

    private static string SanitizeFileName(string value)
    {
        string safeValue = string.IsNullOrWhiteSpace(value)
            ? "section"
            : value.Trim().ToLowerInvariant();
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        StringBuilder builder = new StringBuilder(safeValue.Length);

        for (int index = 0;
             index < safeValue.Length;
             index++)
        {
            char character = safeValue[index];
            bool invalid = false;

            for (int invalidIndex = 0;
                 invalidIndex < invalidCharacters.Length;
                 invalidIndex++)
            {
                if (character == invalidCharacters[invalidIndex])
                {
                    invalid = true;
                    break;
                }
            }

            builder.Append(invalid ? '_' : character);
        }

        return builder.ToString();
    }

    private static bool HashesMatch(
        string first,
        string second
    )
    {
        return !string.IsNullOrWhiteSpace(first) &&
               !string.IsNullOrWhiteSpace(second) &&
               string.Equals(
                   first.Trim(),
                   second.Trim(),
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static bool TryParseSlotIndex(
        string folderName,
        out int slotIndex
    )
    {
        slotIndex = 0;

        if (string.IsNullOrWhiteSpace(folderName) ||
            !folderName.StartsWith(
                "slot_",
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return false;
        }

        return int.TryParse(
                   folderName.Substring(5),
                   NumberStyles.None,
                   CultureInfo.InvariantCulture,
                   out slotIndex
               ) &&
               slotIndex >= 1 &&
               slotIndex <= 999;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
        }
    }

    private static void ValidateSlotIndex(int slotIndex)
    {
        if (slotIndex < 1 || slotIndex > 999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slotIndex),
                slotIndex,
                "El slot debe estar entre 1 y 999."
            );
        }
    }

    private sealed class GenerationCandidate
    {
        public string GenerationId { get; }

        public string ManifestSha256 { get; }

        public bool IsFallback { get; }

        public GenerationCandidate(
            string generationId,
            string manifestSha256,
            bool isFallback
        )
        {
            GenerationId = generationId ?? string.Empty;
            ManifestSha256 = manifestSha256 ?? string.Empty;
            IsFallback = isFallback;
        }
    }
}
