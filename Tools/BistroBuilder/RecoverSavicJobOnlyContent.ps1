param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $root

$queuePath = Join-Path $root 'Library\BistroBuilder\SAVIC\Jobs\queue.json'
if (!(Test-Path -LiteralPath $queuePath)) {
    throw "SAVIC queue not found: $queuePath"
}

$queue = Get-Content -LiteralPath $queuePath -Raw | ConvertFrom-Json
$jobs = @($queue.jobs) |
    Where-Object {
        $_ -ne $null -and
        -not [string]::IsNullOrWhiteSpace([string]$_.sourceHash) -and
        -not [string]::IsNullOrWhiteSpace([string]$_.archivedRelativePath)
    } |
    Group-Object sourceHash |
    ForEach-Object {
        $_.Group |
            Sort-Object {
                try { [DateTimeOffset]::Parse([string]$_.updatedUtc) }
                catch { [DateTimeOffset]::MinValue }
            } -Descending |
            Select-Object -First 1
    }

if ($jobs.Count -eq 0) {
    throw 'SAVIC queue contains no recoverable source jobs.'
}

$projectParent = Split-Path -Parent $root
$siblingRoots = @(
    Get-ChildItem -LiteralPath $projectParent -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -ne $root -and $_.Name -like 'BistroBuilder*' } |
        Select-Object -ExpandProperty FullName
)

$gitRefs = New-Object System.Collections.Generic.List[string]

$stashRefs = @(git.exe stash list --format='%gd')
if ($LASTEXITCODE -eq 0) {
    foreach ($ref in $stashRefs) {
        if (-not [string]::IsNullOrWhiteSpace($ref)) {
            $gitRefs.Add($ref)
            git.exe cat-file -e ($ref + '^3^{tree}') 2>$null
            if ($LASTEXITCODE -eq 0) {
                $gitRefs.Add($ref + '^3')
            }
        }
    }
}

$normalRefs = @(git.exe for-each-ref --format='%(refname)' refs/heads refs/remotes refs/tags)
if ($LASTEXITCODE -eq 0) {
    foreach ($ref in $normalRefs) {
        if (-not [string]::IsNullOrWhiteSpace($ref)) {
            $gitRefs.Add($ref)
        }
    }
}

$reflogRefs = @(git.exe reflog --all --format='%H')
if ($LASTEXITCODE -eq 0) {
    foreach ($ref in $reflogRefs) {
        if (-not [string]::IsNullOrWhiteSpace($ref)) {
            $gitRefs.Add($ref)
        }
    }
}

$gitRefs = @($gitRefs | Select-Object -Unique)

function Ensure-Parent([string]$AbsolutePath) {
    $parent = Split-Path -Parent $AbsolutePath
    if ($parent -and !(Test-Path -LiteralPath $parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }
}

function Get-Sha256([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return '' }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Test-ManifestIdentity(
    [string]$Path,
    [string]$ExpectedSavicId,
    [string]$ExpectedSourceHash
) {
    if (!(Test-Path -LiteralPath $Path)) { return $false }

    try {
        $manifest = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $actualId = [string]$manifest.savicId
        $actualHash = [string]$manifest.source.sourceHash

        return [string]::Equals(
                   $actualId,
                   $ExpectedSavicId,
                   [StringComparison]::OrdinalIgnoreCase) -and
               [string]::Equals(
                   $actualHash,
                   $ExpectedSourceHash,
                   [StringComparison]::OrdinalIgnoreCase)
    }
    catch {
        return $false
    }
}

function Try-CopyValidatedSource(
    [string]$Candidate,
    [string]$Destination,
    [string]$ExpectedHash,
    [string]$Origin
) {
    if (!(Test-Path -LiteralPath $Candidate)) { return $false }

    $actual = Get-Sha256 $Candidate
    if (-not [string]::Equals(
            $actual,
            $ExpectedHash,
            [StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    Ensure-Parent $Destination
    Copy-Item -LiteralPath $Candidate -Destination $Destination -Force

    $copied = Get-Sha256 $Destination
    if (-not [string]::Equals(
            $copied,
            $ExpectedHash,
            [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        throw "Recovered source failed SHA-256 verification: $Destination"
    }

    Write-Output ('SOURCE_RECOVERED|' + $Origin + '|' + $Destination)
    return $true
}

function Try-CopyValidatedManifest(
    [string]$Candidate,
    [string]$Destination,
    [string]$ExpectedSavicId,
    [string]$ExpectedHash,
    [string]$Origin
) {
    if (!(Test-ManifestIdentity $Candidate $ExpectedSavicId $ExpectedHash)) {
        return $false
    }

    Ensure-Parent $Destination
    Copy-Item -LiteralPath $Candidate -Destination $Destination -Force

    if (!(Test-ManifestIdentity $Destination $ExpectedSavicId $ExpectedHash)) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        throw "Recovered manifest failed identity verification: $Destination"
    }

    Write-Output ('MANIFEST_RECOVERED|' + $Origin + '|' + $Destination)
    return $true
}

function Try-RestoreGitPath(
    [string]$GitRef,
    [string]$RelativePath,
    [string]$Destination,
    [bool]$IsManifest,
    [string]$ExpectedSavicId,
    [string]$ExpectedHash
) {
    git.exe cat-file -e ($GitRef + ':' + $RelativePath) 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }

    Ensure-Parent $Destination
    git.exe checkout $GitRef -- $RelativePath 2>$null
    if ($LASTEXITCODE -ne 0) { return $false }

    git.exe restore --staged -- $RelativePath 2>$null

    if ($IsManifest) {
        if (Test-ManifestIdentity $Destination $ExpectedSavicId $ExpectedHash) {
            Write-Output ('MANIFEST_RECOVERED|git:' + $GitRef + '|' + $Destination)
            return $true
        }
    }
    else {
        $actual = Get-Sha256 $Destination

        if (-not [string]::Equals(
                $actual,
                $ExpectedHash,
                [StringComparison]::OrdinalIgnoreCase)) {
            git.exe lfs checkout -- $RelativePath 2>$null
            $actual = Get-Sha256 $Destination
        }

        if ([string]::Equals(
                $actual,
                $ExpectedHash,
                [StringComparison]::OrdinalIgnoreCase)) {
            Write-Output ('SOURCE_RECOVERED|git:' + $GitRef + '|' + $Destination)
            return $true
        }
    }

    Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
    return $false
}

$results = New-Object System.Collections.Generic.List[object]

foreach ($job in $jobs) {
    $sourceHash = ([string]$job.sourceHash).ToLowerInvariant()
    $savicId = [string]$job.manifestSavicId
    $sourceRel = ([string]$job.archivedRelativePath).Replace('/', '\')
    $sourceDest = Join-Path $root $sourceRel

    $manifestRel = ''
    $manifestDest = ''

    if (-not [string]::IsNullOrWhiteSpace($savicId)) {
        $manifestRel = 'SAVIC\Manifests\' + $savicId + '.json'
        $manifestDest = Join-Path $root $manifestRel
    }

    $sourceOk =
        (Test-Path -LiteralPath $sourceDest) -and
        [string]::Equals(
            (Get-Sha256 $sourceDest),
            $sourceHash,
            [StringComparison]::OrdinalIgnoreCase)

    $manifestOk =
        -not [string]::IsNullOrWhiteSpace($savicId) -and
        (Test-ManifestIdentity $manifestDest $savicId $sourceHash)

    if (-not $sourceOk) {
        foreach ($sibling in $siblingRoots) {
            $candidate = Join-Path $sibling $sourceRel
            if (Try-CopyValidatedSource $candidate $sourceDest $sourceHash ('worktree:' + $sibling)) {
                $sourceOk = $true
                break
            }
        }
    }

    if (-not $manifestOk -and -not [string]::IsNullOrWhiteSpace($savicId)) {
        foreach ($sibling in $siblingRoots) {
            $candidate = Join-Path $sibling $manifestRel
            if (Try-CopyValidatedManifest $candidate $manifestDest $savicId $sourceHash ('worktree:' + $sibling)) {
                $manifestOk = $true
                break
            }
        }
    }

    if (-not $sourceOk) {
        foreach ($ref in $gitRefs) {
            if (Try-RestoreGitPath $ref ($sourceRel.Replace('\','/')) $sourceDest $false $savicId $sourceHash) {
                $sourceOk = $true
                break
            }
        }
    }

    if (-not $manifestOk -and -not [string]::IsNullOrWhiteSpace($savicId)) {
        foreach ($ref in $gitRefs) {
            if (Try-RestoreGitPath $ref ($manifestRel.Replace('\','/')) $manifestDest $true $savicId $sourceHash) {
                $manifestOk = $true
                break
            }
        }
    }

    $results.Add([PSCustomObject]@{
        Name = [string]$job.originalFileName
        SourceHash = $sourceHash
        SavicId = $savicId
        SourceRecovered = $sourceOk
        ManifestRecovered = $manifestOk
        SourcePath = $sourceRel
        ManifestPath = $manifestRel
    })
}

$sourceRecovered = @($results | Where-Object { $_.SourceRecovered }).Count
$manifestRecovered = @($results | Where-Object { $_.ManifestRecovered }).Count
$complete = @($results | Where-Object { $_.SourceRecovered -and $_.ManifestRecovered }).Count
$incomplete = @($results | Where-Object { -not ($_.SourceRecovered -and $_.ManifestRecovered) })

Write-Output ('SAVIC_RECOVERY|QUEUE_UNIQUE=' + $results.Count)
Write-Output ('SAVIC_RECOVERY|SOURCE_OK=' + $sourceRecovered)
Write-Output ('SAVIC_RECOVERY|MANIFEST_OK=' + $manifestRecovered)
Write-Output ('SAVIC_RECOVERY|COMPLETE=' + $complete)
Write-Output ('SAVIC_RECOVERY|INCOMPLETE=' + $incomplete.Count)

foreach ($row in $incomplete) {
    Write-Output (
        'MISSING|' +
        $row.Name +
        '|SOURCE=' +
        $row.SourceRecovered +
        '|MANIFEST=' +
        $row.ManifestRecovered +
        '|HASH=' +
        $row.SourceHash +
        '|SAVIC=' +
        $row.SavicId)
}

$reportDir = Join-Path $root 'Library\BistroBuilder\SAVIC\Logs'
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$reportPath = Join-Path $reportDir 'job-only-recovery-report.json'
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8

if ($incomplete.Count -gt 0) {
    Write-Output ('SAVIC_RECOVERY|REPORT=' + $reportPath)
    throw ('Recovery incomplete: ' + $incomplete.Count + ' canonical item(s) are still missing source and/or manifest.')
}

$pathsToCommit = @(
    'SAVIC/Manifests',
    'ContentSource/SHA256'
)

git.exe add -- $pathsToCommit
if ($LASTEXITCODE -ne 0) {
    throw 'Recovered canonical data could not be staged.'
}

$staged = @(git.exe diff --cached --name-only -- $pathsToCommit)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect recovered canonical data staging.'
}

if ($staged.Count -gt 0) {
    git.exe commit -m 'recover: restore SAVIC canonical source records'
    if ($LASTEXITCODE -ne 0) {
        throw 'Recovered canonical data could not be committed.'
    }

    Write-Output 'SAVIC_RECOVERY|COMMITTED'
}

Write-Output ('SAVIC_RECOVERY|REPORT=' + $reportPath)
Write-Output 'SAVIC_RECOVERY|PASS|All queued canonical SAVIC sources and manifests are verified.'
exit 0
