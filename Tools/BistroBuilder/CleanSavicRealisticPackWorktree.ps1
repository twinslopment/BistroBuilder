param(
    [switch]$Commit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Invoke-GitNative([string[]]$GitArgs) {
    & git.exe @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "git $($GitArgs -join ' ') failed with exit code $LASTEXITCODE"
    }
}

$keepPatterns = @(
    'Assets/Data/Restaurant/EditMode/Catalog/RestaurantPlaceableCatalog_Main.asset',
    'Assets/Data/Restaurant/EditMode/EditableDefinitions/EditableObjectDefinition_SillaBistro*.asset',
    'Assets/Data/Restaurant/EditMode/EditableDefinitions/EditableObjectDefinition_SillaBistro*.asset.meta',
    'Assets/Data/Restaurant/EditMode/PlaceableItems/PlaceableItemDefinition_SillaBistro*.asset',
    'Assets/Data/Restaurant/EditMode/PlaceableItems/PlaceableItemDefinition_SillaBistro*.asset.meta',
    'Assets/Prefabs/Restaurant/Generated/Seating/SillaBistro*.prefab',
    'Assets/Prefabs/Restaurant/Generated/Seating/SillaBistro*.prefab.meta',
    'Assets/Generated/BistroBuilder/CatalogIcons/chair_bistro_01_*.png',
    'Assets/Generated/BistroBuilder/CatalogIcons/chair_bistro_01_*.png.meta',
    'Assets/Editor/BistroBuilder/SAVIC/Packs.meta',
    'Assets/Editor/BistroBuilder/SAVIC/Packs/SavicRealisticTestPackV1Installer.cs.meta',
    'Assets/Editor/BistroBuilder/SAVIC/Inventory/SavicCanonicalContentInventoryService.cs.meta',
    'Assets/Editor/BistroBuilder/SAVIC/Diagnostics/SavicCanonicalContentInventoryProbe.cs.meta'
)

$status = git status --porcelain=v1
if ($LASTEXITCODE -ne 0) { throw 'git status failed.' }

$changed = @()
foreach ($line in $status) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.Length -lt 4) { continue }
    $path = $line.Substring(3)
    if ($path.Contains(' -> ')) { $path = $path.Split(' -> ')[-1] }
    $changed += $path.Trim('"')
}

function MatchesAny([string]$Path, [string[]]$Patterns) {
    foreach ($pattern in $Patterns) {
        if ($Path -like $pattern) { return $true }
    }
    return $false
}

# Never assume that an unrelated local change is disposable.
# Only explicitly recognised pack outputs are committed. Everything
# else is preserved automatically in a safety stash.
Invoke-GitNative @('restore','--staged','.')

$keep = @($changed | Where-Object { MatchesAny $_ $keepPatterns })
$safety = @($changed | Where-Object { -not (MatchesAny $_ $keepPatterns) })

Write-Output ('SAVIC_CLEANUP|KEEP=' + $keep.Count)
$keep | ForEach-Object { Write-Output ('KEEP|' + $_) }
Write-Output ('SAVIC_CLEANUP|SAFETY_BACKUP=' + $safety.Count)

$stashCreated = $false
$stashName = ''

if ($safety.Count -gt 0) {
    $stashName =
        'SAVIC cleanup safety backup ' +
        (Get-Date -Format 'yyyyMMdd-HHmmss')

    $stashArgs =
        @(
            'stash',
            'push',
            '--include-untracked',
            '--message',
            $stashName,
            '--'
        ) + $safety

    Invoke-GitNative $stashArgs
    $stashCreated = $true

    Write-Output (
        'SAVIC_CLEANUP|SAFETY_STASH|' +
        $stashName)
}

if ($keep.Count -gt 0) {
    Invoke-GitNative (@('add','--') + $keep)
}

if (-not $Commit) {
    Write-Output 'SAVIC_CLEANUP|READY|Valid SAVIC changes staged; known noise restored.'
    exit 0
}

$staged = git diff --cached --name-only
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect staged changes.' }

if (-not $staged) {
    Write-Output 'SAVIC_CLEANUP|NO_COMMIT|No valid SAVIC changes to commit.'
    exit 0
}

Invoke-GitNative @('commit','-m','feat: materialize SAVIC realistic test pack v1')
Write-Output 'SAVIC_CLEANUP|COMMITTED'
if ($stashCreated) {
    Write-Output (
        'SAVIC_CLEANUP|PRESERVED_UNRELATED_CHANGES|' +
        $stashName)
}
exit 0
