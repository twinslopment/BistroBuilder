$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Service\TableAssignmentSystem.cs'
$t = [IO.File]::ReadAllText($p)
$old = @'
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;
'@
$new = @'
    [SerializeField]
    private BistroBuilderBarServiceSystem barServiceSystem;

    [Tooltip("Autoridad avanzada de cola, reservas y rotación del Bloque 14.")]
    [SerializeField]
    private BistroBuilderAdvancedFrontOfHouseService advancedFrontOfHouseService;
'@
if (-not $t.Contains($old)) { throw 'bar field marker missing' }
$t = $t.Replace($old, $new)
$t = $t.Replace('    public event Action<CustomerGroup> CustomerGroupRegistered;', "    public event Action<CustomerGroup> CustomerGroupRegistered;`n`n    public event Action<CustomerGroup, RestaurantTable> TableAssigned;")
$old = @'
    public void RequestReevaluation()
    {
        TryAssignWaitingGroups();
    }
'@
$new = @'
    public void RequestReevaluation()
    {
        TryAssignWaitingGroups();
    }

    public bool TryReleasePreferredTableReservation(CustomerGroup customerGroup)
    {
        if (customerGroup == null || !preferredTableReservations.ContainsKey(customerGroup))
            return false;
        ReleasePreferredTableReservation(customerGroup);
        TryAssignWaitingGroups();
        return true;
    }
'@
if (-not $t.Contains($old)) { throw 'reevaluation marker missing' }
$t = $t.Replace($old, $new)
$old = @'
    private void TryAssignWaitingGroups()
    {
        int groupIndex = 0;
'@
$new = @'
    private void TryAssignWaitingGroups()
    {
        if (advancedFrontOfHouseService != null && waitingGroups.Count > 1)
            waitingGroups.Sort(advancedFrontOfHouseService.CompareWaitingGroups);

        int groupIndex = 0;
'@
if (-not $t.Contains($old)) { throw 'assignment loop marker missing' }
$t = $t.Replace($old, $new)
$old = @'
            customerGroup.SetState(
                CustomerGroupState.WalkingToTable
            );

            Debug.Log(
'@
$new = @'
            customerGroup.SetState(
                CustomerGroupState.WalkingToTable
            );

            TableAssigned?.Invoke(customerGroup, bestTable);

            Debug.Log(
'@
if (-not $t.Contains($old)) { throw 'table assigned marker missing' }
$t = $t.Replace($old, $new)
$old = @'
        if (barServiceSystem == null)
        {
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem
            >();
        }
'@
$new = @'
        if (barServiceSystem == null)
        {
            barServiceSystem = FindFirstObjectByType<
                BistroBuilderBarServiceSystem
            >();
        }

        if (advancedFrontOfHouseService == null)
            advancedFrontOfHouseService = FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
'@
if (-not $t.Contains($old)) { throw 'cache marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'TABLE_PATCHED'