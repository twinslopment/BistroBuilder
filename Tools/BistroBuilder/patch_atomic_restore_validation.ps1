$ErrorActionPreference = 'Stop'
$validationPath = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Restaurant\Placement\RestaurantPlacementValidationService.cs'
$providerPath = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Persistence\Restaurant\RestaurantStructureSaveSectionProvider.cs'
$utf8 = New-Object System.Text.UTF8Encoding($false)

$validation = [IO.File]::ReadAllText($validationPath)
$oldSignature = @'
        ValidatePlacement(
            RestaurantAreaMember member,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation
        )
'@
$newSignature = @'
        ValidatePlacement(
            RestaurantAreaMember member,
            Vector3 candidateRootPosition,
            Quaternion candidateRootRotation,
            bool includeSpecializedConstraints = true
        )
'@
if (-not $validation.Contains($oldSignature)) { throw 'ValidatePlacement signature marker missing' }
$validation = $validation.Replace($oldSignature, $newSignature)
$validation = $validation.Replace('        if (constraintService != null)', '        if (includeSpecializedConstraints && constraintService != null)')
[IO.File]::WriteAllText($validationPath, $validation, $utf8)
$provider = [IO.File]::ReadAllText($providerPath)
$oldIncremental = @'
            RestaurantPlacementValidationResult placementResult =
                validationService.ValidateCurrentPlacement(member);
'@
$newIncremental = @'
            // Durante una restauración atómica las relaciones entre colocables
            // todavía no están reconstruidas. Se difieren las reglas cruzadas
            // hasta que el conjunto completo y su topología estén disponibles.
            RestaurantPlacementValidationResult placementResult =
                validationService.ValidatePlacement(
                    member,
                    member.transform.position,
                    member.transform.rotation,
                    false);
'@
if (-not $provider.Contains($oldIncremental)) { throw 'Incremental restore marker missing' }
$provider = $provider.Replace($oldIncremental, $newIncremental)

$oldFinalGate = @'
        Physics.SyncTransforms();
        seatingTopologyService.RebuildImmediately();

        if (!ValidateRestoredSeatLinks(
'@
$newFinalGate = @'
        Physics.SyncTransforms();
        seatingTopologyService.RebuildImmediately();

        if (!ValidateRestoredPlacements(out string restoredPlacementError))
        {
            context.Fail(restoredPlacementError);
            yield break;
        }

        if (!ValidateRestoredSeatLinks(
'@
if (-not $provider.Contains($oldFinalGate)) { throw 'Final restore gate marker missing' }
$provider = $provider.Replace($oldFinalGate, $newFinalGate)

$helperMarker = '    private bool ValidateRestoredSeatLinks('
$helper = @'
    private bool ValidateRestoredPlacements(out string error)
    {
        error = string.Empty;
        for (int index = 0; index < loadOrderBuffer.Count; index++)
        {
            RestaurantPlaceableSaveRecord record = loadOrderBuffer[index];
            if (record == null) continue;
            string instanceId = NormalizeId(record.instanceId);
            if (!loadedPlaceablesById.TryGetValue(
                    instanceId,
                    out RestaurantPlaceableObject placeable) ||
                placeable == null ||
                !placeable.TryGetComponent(out RestaurantAreaMember member))
            {
                error = "No se pudo validar el colocable restaurado " +
                        instanceId + ".";
                return false;
            }

            RestaurantPlacementValidationResult result =
                validationService.ValidateCurrentPlacement(member);
            if (!result.IsValid || result.CandidateArea == null)
            {
                error = BuildPlacementLoadError(placeable, result);
                return false;
            }
            member.SetArea(result.CandidateArea);
        }

'@
$helper += @'
        return true;
    }

'@
if (-not $provider.Contains($helperMarker)) { throw 'ValidateRestoredSeatLinks marker missing' }
$provider = $provider.Replace($helperMarker, $helper + $helperMarker)
[IO.File]::WriteAllText($providerPath, $provider, $utf8)
Write-Output 'ATOMIC_RESTORE_VALIDATION_PATCH_PASS'
