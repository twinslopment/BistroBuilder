$dataPath = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Persistence\Service\BistroBuilderActiveServiceSaveData.cs'
$t = [IO.File]::ReadAllText($dataPath)
$old = @'
    public BistroBuilderCustomerDiningRuntimeSnapshot customerDining;
    public List<BistroBuilderKitchenRuntimeSnapshot> kitchens =
'@
$new = @'
    public BistroBuilderCustomerDiningRuntimeSnapshot customerDining;
    public BistroBuilderAdvancedFrontOfHouseRuntimeSnapshot advancedFrontOfHouse;
    public List<BistroBuilderKitchenRuntimeSnapshot> kitchens =
'@
if (-not $t.Contains($old)) { throw 'save data aggregate marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (!canonicalOrders.TryValidate(out error) ||
            !coursesAndSharing.TryValidate(out error) ||
            !customerDining.TryValidate(out error))
        {
            return false;
        }
'@
$new = @'
        if (!canonicalOrders.TryValidate(out error) ||
            !coursesAndSharing.TryValidate(out error) ||
            !customerDining.TryValidate(out error) ||
            (advancedFrontOfHouse != null &&
             !advancedFrontOfHouse.TryValidate(out error)))
        {
            return false;
        }
'@
if (-not $t.Contains($old)) { throw 'save data validate marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($dataPath, $t, (New-Object Text.UTF8Encoding($false)))

$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Persistence\Service\BistroBuilderActiveServiceSaveSectionProvider.cs'
$t = [IO.File]::ReadAllText($p)
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
if (-not $t.Contains($old)) { throw 'provider field marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (customerGroupSpawner == null || tableRegistry == null ||
            tableAssignmentSystem == null || barRegistry == null ||
            barServiceSystem == null)
'@
$new = @'
        if (customerGroupSpawner == null || tableRegistry == null ||
            tableAssignmentSystem == null || barRegistry == null ||
            barServiceSystem == null || advancedFrontOfHouseService == null)
'@
if (-not $t.Contains($old)) { throw 'provider validation dependency marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (!orderSystem.ValidateConfiguration(out error) ||
            !barRegistry.ValidateConfiguration(out error) ||
            !barServiceSystem.ValidateConfiguration(out error) ||
            !orderInventoryLifecycleService.ValidateConfiguration(out error))
'@
$new = @'
        if (!orderSystem.ValidateConfiguration(out error) ||
            !barRegistry.ValidateConfiguration(out error) ||
            !barServiceSystem.ValidateConfiguration(out error) ||
            !advancedFrontOfHouseService.ValidateConfiguration(out error) ||
            !orderInventoryLifecycleService.ValidateConfiguration(out error))
'@
if (-not $t.Contains($old)) { throw 'provider validation chain marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (!CaptureCustomers(data, out error) ||
            !CaptureTables(data, out error) ||
            !CapturePendingBarTableReservations(data, out error) ||
            !CaptureWaiters(data, out error) ||
            !CaptureOrdersAndSubsystems(data, out error))
'@
$new = @'
        if (!CaptureCustomers(data, out error) ||
            !CaptureTables(data, out error) ||
            !CapturePendingBarTableReservations(data, out error) ||
            !CaptureWaiters(data, out error) ||
            !advancedFrontOfHouseService.TryCaptureRuntimeSnapshot(
                out data.advancedFrontOfHouse, out error) ||
            !CaptureOrdersAndSubsystems(data, out error))
'@
if (-not $t.Contains($old)) { throw 'provider capture chain marker missing' }
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
if (-not $t.Contains($old)) { throw 'prepare reset marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (!RestoreAssignmentsAndStates(pendingData, out error))
        {
            context.Fail(error);
            yield break;
        }

        if (!orderSystem.TryRestoreRuntimeOrders(
'@
$new = @'
        if (!RestoreAssignmentsAndStates(pendingData, out error) ||
            !advancedFrontOfHouseService.TryRestoreRuntimeSnapshot(
                pendingData.advancedFrontOfHouse, out error))
        {
            context.Fail(error);
            yield break;
        }

        if (!orderSystem.TryRestoreRuntimeOrders(
'@
if (-not $t.Contains($old)) { throw 'restore snapshot marker missing' }
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
if (-not $t.Contains($old)) { throw 'cache dependency marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'PERSISTENCE_PATCHED'