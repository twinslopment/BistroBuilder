param(
    [switch]$Commit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

function Invoke-GitNative([string[]]$Args) {
    & git.exe @Args
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Args -join ' ') failed with exit code $LASTEXITCODE"
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

$knownNoisePatterns = @(
    'Assets/BistroBuilder/UI/Iconography/Icons/*.svg.meta',
    'Assets/Resources/BistroBuilder/UI/Typography/*.asset',
    'Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset',
    '*.slnx'
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

$keep = @($changed | Where-Object { MatchesAny $_ $keepPatterns })
$noise = @($changed | Where-Object { MatchesAny $_ $knownNoisePatterns })
$unknown = @($changed | Where-Object {
    -not (MatchesAny $_ $keepPatterns) -and
    -not (MatchesAny $_ $knownNoisePatterns)
})

Write-Output ('SAVIC_CLEANUP|KEEP=' + $keep.Count)
$keep | ForEach-Object { Write-Output ('KEEP|' + $_) }
Write-Output ('SAVIC_CLEANUP|NOISE=' + $noise.Count)
$noise | ForEach-Object { Write-Output ('NOISE|' + $_) }
Write-Output ('SAVIC_CLEANUP|UNKNOWN=' + $unknown.Count)
$unknown | ForEach-Object { Write-Output ('UNKNOWN|' + $_) }

foreach ($path in $noise) {
    $tracked = git ls-files --error-unmatch -- $path 2>$null
    if ($LASTEXITCODE -eq 0) {
        Invoke-GitNative @('restore','--worktree','--',$path)
    }
}

if ($keep.Count -gt 0) {
    Invoke-GitNative (@('add','--') + $keep)
}

if ($unknown.Count -gt 0) {
    Write-Output 'SAVIC_CLEANUP|SAFE_STOP|Unknown changes were preserved and left unstaged.'
    exit 2
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
exit 0
