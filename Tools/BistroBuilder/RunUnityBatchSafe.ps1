param(
    [Parameter(Mandatory = $true)]
    [string]$ExecuteMethod,
    [string]$LogName = "bb_safe_batch.log",
    [int]$TimeoutSeconds = 300,
    [switch]$NoQuit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot '..\..'))
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe'
$guard = Join-Path $PSScriptRoot 'SceneLockGuard.ps1'
$logDir = Join-Path $projectRoot 'Logs'
$tempDir = Join-Path $logDir '.batchstate'
New-Item -ItemType Directory -Force -Path $logDir, $tempDir | Out-Null

$projectPattern = [regex]::Escape($projectRoot)
$methodPattern = [regex]::Escape($ExecuteMethod)

function Get-BBTargetUnityProcesses {
    return @(
        Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and
            $_.CommandLine -match $projectPattern -and
            $_.CommandLine -match $methodPattern
        })
}

function Stop-BBTargetUnityProcesses {
    $targets = @(Get-BBTargetUnityProcesses)
    if ($targets.Count -eq 0) { return }

    Write-Output (
        "BB_SAFE_BATCH|TERMINATE_TIMEOUT_UNITY|PIDS=" +
        (($targets.ProcessId) -join ','))

    foreach ($target in $targets) {
        Stop-Process -Id ([int]$target.ProcessId) -Force -ErrorAction SilentlyContinue
    }

    $killDeadline = [DateTime]::UtcNow.AddSeconds(10)
    while ((@(Get-BBTargetUnityProcesses)).Count -gt 0 -and
           [DateTime]::UtcNow -lt $killDeadline) {
        Start-Sleep -Milliseconds 250
    }
}

# Serializa todos los Unity batch del proyecto mediante un lock real de proceso.
# Evita que dos chats/sistemas abran simultáneamente el mismo proyecto y corrompan gates.
$slotPath = Join-Path $tempDir 'unity_project_slot.lock'
$slotDeadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(30, $TimeoutSeconds))
$slotStream = $null
$waitLogged = $false
while ($null -eq $slotStream) {
    try {
        $slotStream = [IO.File]::Open(
            $slotPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None)
    }
    catch [IO.IOException] {
        if (-not $waitLogged) {
            Write-Output "BB_SAFE_BATCH|WAIT_SLOT|$ExecuteMethod"
            $waitLogged = $true
        }
        if ([DateTime]::UtcNow -ge $slotDeadline) {
            Write-Error "BB_SAFE_BATCH|SLOT_TIMEOUT|$ExecuteMethod"
            exit 125
        }
        Start-Sleep -Milliseconds 500
    }
}

# También respeta Unity externos que no hayan sido lanzados por este helper.
while ($true) {
    $projectUnity = @(
        Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -match $projectPattern })
    if ($projectUnity.Count -eq 0) { break }
    if (-not $waitLogged) {
        Write-Output ("BB_SAFE_BATCH|WAIT_PROJECT_UNITY|PIDS=" + (($projectUnity.ProcessId) -join ','))
        $waitLogged = $true
    }
    if ([DateTime]::UtcNow -ge $slotDeadline) {
        $slotStream.Dispose()
        Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
        Write-Error "BB_SAFE_BATCH|PROJECT_BUSY_TIMEOUT|$ExecuteMethod"
        exit 126
    }
    Start-Sleep -Milliseconds 500
}

# Repara únicamente locks de Unity huérfanos; nunca toca un proceso vivo.
$editorInstance = Join-Path $projectRoot 'Library\EditorInstance.json'
$unityLock = Join-Path $projectRoot 'Temp\UnityLockfile'
$recordedAlive = $false
if (Test-Path -LiteralPath $editorInstance) {
    try {
        $instance = Get-Content -LiteralPath $editorInstance -Raw | ConvertFrom-Json
        $recordedAlive = $null -ne (Get-Process -Id ([int]$instance.process_id) -ErrorAction SilentlyContinue)
    } catch { $recordedAlive = $false }
}
if (-not $recordedAlive) {
    $hadStale = (Test-Path -LiteralPath $editorInstance) -or (Test-Path -LiteralPath $unityLock)
    Remove-Item -LiteralPath $editorInstance, $unityLock -Force -ErrorAction SilentlyContinue
    if ($hadStale) { Write-Output "BB_SAFE_BATCH|STALE_PROJECT_LOCK_REPAIRED" }
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $guard -RepairKnownResidual
if ($LASTEXITCODE -ne 0) {
    $slotStream.Dispose()
    Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
    throw "BB Scene Lock Guard rechazó el preflight."
}

$token = [Guid]::NewGuid().ToString('N')
$cmdPath = Join-Path $tempDir ("bb_safe_batch_" + $token + '.cmd')
$resultPath = Join-Path $tempDir ("bb_safe_batch_" + $token + '.exit')
$logPath = Join-Path $logDir $LogName

$unityArguments = @(
    '-batchmode',
    '-projectPath', ('"' + $projectRoot + '"'),
    '-executeMethod', $ExecuteMethod,
    '-logFile', ('"' + $logPath + '"')
)
if (-not $NoQuit) {
    $unityArguments += '-quit'
}

$commandLine = '"' + $unity + '" ' +
    ($unityArguments -join ' ')
$cmdLines = @(
    '@echo off',
    $commandLine,
    'set BB_EXIT=%ERRORLEVEL%',
    ('echo %BB_EXIT% > "' + $resultPath + '"'),
    'exit /b %BB_EXIT%'
)
[IO.File]::WriteAllLines(
    $cmdPath,
    $cmdLines,
    [Text.Encoding]::ASCII)

Start-Process explorer.exe -ArgumentList ('"' + $cmdPath + '"')
$deadline = [DateTime]::UtcNow.AddSeconds(
    [Math]::Max(30, $TimeoutSeconds))

while ($true) {
    $ready = $false
    if (Test-Path -LiteralPath $resultPath) {
        try { $ready = (Get-Item -LiteralPath $resultPath).Length -gt 0 }
        catch { $ready = $false }
    }
    if ($ready) { break }
    if ([DateTime]::UtcNow -ge $deadline) {
        Stop-BBTargetUnityProcesses
        Start-Sleep -Milliseconds 500
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $guard -RepairKnownResidual
        Remove-Item -LiteralPath $cmdPath, $resultPath -Force -ErrorAction SilentlyContinue
        $slotStream.Dispose()
        Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
        Write-Error (
            "BB_SAFE_BATCH|TIMEOUT|" + $ExecuteMethod +
            "|LOG=" + $logPath)
        exit 124
    }
    Start-Sleep -Milliseconds 250
}

$exitText = (Get-Content -LiteralPath $resultPath -Raw).Trim()
$unityExit = 1
if (-not [int]::TryParse($exitText, [ref]$unityExit)) {
    Write-Error "BB_SAFE_BATCH|INVALID_EXIT|$exitText"
    $unityExit = 1
}

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $guard -RepairKnownResidual
$postflightExit = $LASTEXITCODE

Remove-Item -LiteralPath $cmdPath, $resultPath -Force -ErrorAction SilentlyContinue
if ($postflightExit -ne 0) {
    $slotStream.Dispose()
    Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
    Write-Error "BB_SAFE_BATCH|POSTFLIGHT_LOCKED|$ExecuteMethod"
    exit 4
}

Write-Output (
    "BB_SAFE_BATCH|EXIT={0}|METHOD={1}|LOG={2}" -f
    $unityExit, $ExecuteMethod, $logPath)
$slotStream.Dispose()
Remove-Item -LiteralPath $slotPath -Force -ErrorAction SilentlyContinue
exit $unityExit
