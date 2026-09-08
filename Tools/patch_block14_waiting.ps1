$p = 'C:\Users\mruperez\ProyectoBB\BistroBuilder\Assets\Scripts\Application\Service\CustomerWaitingAreaSystem.cs'
$t = [IO.File]::ReadAllText($p)
$old = @'
    [SerializeField]
    private Transform[] waitingPoints;
'@
$new = @'
    [SerializeField]
    private Transform[] waitingPoints;

    [SerializeField]
    private BistroBuilderAdvancedFrontOfHouseService advancedFrontOfHouseService;
'@
if (-not $t.Contains($old)) { throw 'waiting points marker missing' }
$t = $t.Replace($old, $new)
$old = @'
    private void Start()
    {
        ValidateConfiguration();
    }
'@
$new = @'
    private void Start()
    {
        if (advancedFrontOfHouseService == null)
            advancedFrontOfHouseService = FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
        ValidateConfiguration();
    }
'@
if (-not $t.Contains($old)) { throw 'start marker missing' }
$t = $t.Replace($old, $new)
$old = @'
    private void ReorganizeWaitingQueue()
    {
        // Eliminamos referencias destruidas o grupos que ya no esperan.
'@
$new = @'
    private void ReorganizeWaitingQueue()
    {
        if (advancedFrontOfHouseService == null)
            advancedFrontOfHouseService = FindFirstObjectByType<BistroBuilderAdvancedFrontOfHouseService>();
        if (advancedFrontOfHouseService != null && waitingGroups.Count > 1)
            waitingGroups.Sort(advancedFrontOfHouseService.CompareWaitingGroups);

        // Eliminamos referencias destruidas o grupos que ya no esperan.
'@
if (-not $t.Contains($old)) { throw 'reorganize marker missing' }
$t = $t.Replace($old, $new)
[IO.File]::WriteAllText($p, $t, (New-Object Text.UTF8Encoding($false)))
Write-Output 'WAITING_PATCHED'