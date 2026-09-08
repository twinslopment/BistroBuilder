$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Persistence\Service\BistroBuilderActiveServiceSaveSectionProvider.cs'
$t = [IO.File]::ReadAllText($p).Replace("`r`n", "`n")
$old = @'
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Header("Comandas y cocina")]
'@
$new = @'
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [SerializeField]
    private BistroBuilderAdvancedFrontOfHouseService advancedFrontOfHouseService;

    [Header("Comandas y cocina")]
'@
if (-not $t.Contains($old)) { throw 'field marker missing' }
$t = $t.Replace($old, $new)
$old = @'
            tableAssignmentSystem == null || barRegistry == null ||
            barServiceSystem == null)
'@
$new = @'
            tableAssignmentSystem == null || barRegistry == null ||
            barServiceSystem == null || advancedFrontOfHouseService == null)
'@
if (-not $t.Contains($old)) { throw 'validation dependency marker missing' }
$t = $t.Replace($old, $new)
$old = @'
            !barServiceSystem.ValidateConfiguration(out error) ||
            !orderInventoryLifecycleService.ValidateConfiguration(out error))
'@
$new = @'
            !barServiceSystem.ValidateConfiguration(out error) ||
            !advancedFrontOfHouseService.ValidateConfiguration(out error) ||
            !orderInventoryLifecycleService.ValidateConfiguration(out error))
'@
if (-not $t.Contains($old)) { throw 'validation chain marker missing' }
$t = $t.Replace($old, $new)
$old = @'
            !CapturePendingBarTableReservations(data, out error) ||
            !CaptureWaiters(data, out error) ||
            !CaptureOrdersAndSubsystems(data, out error))
'@
$new = @'
            !CapturePendingBarTableReservations(data, out error) ||
            !CaptureWaiters(data, out error) ||
            !advancedFrontOfHouseService.TryCaptureRuntimeSnapshot(
                out data.advancedFrontOfHouse, out error) ||
            !CaptureOrdersAndSubsystems(data, out error))
'@
if (-not $t.Contains($old)) { throw 'capture chain marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        tableAssignmentSystem.ClearPendingBarTransitionReservationsForRuntimeLoad();
        barServiceSystem.ClearRuntimeForLoad();
'@
$new = @'
        tableAssignmentSystem.ClearPendingBarTransitionReservationsForRuntimeLoad();
        advancedFrontOfHouseService.ResetForRuntimeLoad();
        barServiceSystem.ClearRuntimeForLoad();
'@
if (-not $t.Contains($old)) { throw 'prepare marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (!RestoreAssignmentsAndStates(pendingData, out error))
        {
'@
$new = @'
        if (!RestoreAssignmentsAndStates(pendingData, out error) ||
            !advancedFrontOfHouseService.TryRestoreRuntimeSnapshot(
                pendingData.advancedFrontOfHouse, out error))
        {
'@
if (-not $t.Contains($old)) { throw 'restore marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (barServiceSystem == null) TryGetComponent(out barServiceSystem);
        if (orderSystem == null) TryGetComponent(out orderSystem);
'@
$new = @'
        if (barServiceSystem == null) TryGetComponent(out barServiceSystem);
        if (advancedFrontOfHouseService == null)
            TryGetComponent(out advancedFrontOfHouseService);
        if (orderSystem == null) TryGetComponent(out orderSystem);
'@
if (-not $t.Contains($old)) { throw 'cache marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'PROVIDER_PATCHED'