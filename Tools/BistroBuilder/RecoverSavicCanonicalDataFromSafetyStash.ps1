param()

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

$stashList = git.exe stash list --format='%gd|%s'
if ($LASTEXITCODE -ne 0) { throw 'Could not read git stash list.' }

$target = $stashList |
    Where-Object { $_ -like '*SAVIC cleanup safety backup*' } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($target)) {
    throw 'No SAVIC cleanup safety backup stash was found.'
}

$parts = $target -split '\|', 2
$stashRef = $parts[0]
Write-Output ('SAVIC_RECOVERY|STASH=' + $stashRef)

$restoreRoots = @(
    'SAVIC/Manifests',
    'ContentSource/SHA256'
)

foreach ($restoreRoot in $restoreRoots) {
    $trackedPaths = git.exe ls-tree -r --name-only $stashRef -- $restoreRoot
    if ($LASTEXITCODE -eq 0 -and $trackedPaths) {
        foreach ($path in $trackedPaths) {
            $parent = Split-Path -Parent $path
            if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
            & git.exe checkout $stashRef -- $path
            if ($LASTEXITCODE -ne 0) { throw ('Failed to restore tracked SAVIC file: ' + $path) }
            & git.exe restore --staged -- $path 2>$null
            Write-Output ('RESTORED_TRACKED|' + $path)
        }
    }

    $untrackedParent = $stashRef + '^3'
    git.exe cat-file -e ($untrackedParent + '^{tree}') 2>$null
    if ($LASTEXITCODE -eq 0) {
        $untrackedPaths = git.exe ls-tree -r --name-only $untrackedParent -- $restoreRoot
        if ($LASTEXITCODE -eq 0 -and $untrackedPaths) {
            foreach ($path in $untrackedPaths) {
                $parent = Split-Path -Parent $path
                if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
                & git.exe checkout $untrackedParent -- $path
                if ($LASTEXITCODE -ne 0) { throw ('Failed to restore untracked SAVIC file: ' + $path) }
                & git.exe restore --staged -- $path 2>$null
                Write-Output ('RESTORED_UNTRACKED|' + $path)
            }
        }
    }
}

$manifestCount = 0
if (Test-Path 'SAVIC/Manifests') {
    $manifestCount = @(Get-ChildItem 'SAVIC/Manifests' -Filter '*.json' -File).Count
}

$sourceCount = 0
if (Test-Path 'ContentSource/SHA256') {
    $sourceCount = @(Get-ChildItem 'ContentSource/SHA256' -File -Recurse).Count
}

Write-Output ('SAVIC_RECOVERY|MANIFESTS=' + $manifestCount)
Write-Output ('SAVIC_RECOVERY|SOURCES=' + $sourceCount)

if ($manifestCount -lt 2 -or $sourceCount -lt 2) {
    throw 'Recovery did not restore the expected SAVIC canonical data.'
}

Write-Output 'SAVIC_RECOVERY|PASS|Canonical SAVIC data restored from safety stash.'
exit 0
