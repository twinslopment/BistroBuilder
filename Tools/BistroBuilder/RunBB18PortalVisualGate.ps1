param(
    [int]$MaxAttempts = 3
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runner = Join-Path $PSScriptRoot 'RunUnityBatchSafe.ps1'
$buildFolder = Join-Path $projectRoot 'Logs\BB18PortalRuntime'
$exe = Join-Path $buildFolder 'BB18PortalVisualProbe.exe'
$report = Join-Path $buildFolder 'BB18PortalReport.txt'
$evidence = Join-Path $buildFolder 'Evidence'

for ($attempt = 1; $attempt -le [Math]::Max(1, $MaxAttempts); $attempt++) {
    $buildLogName = "bb18_4_portal_build_auto_$attempt.log"
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner `
        -ExecuteMethod 'BistroBuilderAnimation18PortalVisualStandaloneGate.BuildFromCommandLine' `
        -LogName $buildLogName -TimeoutSeconds 900
    if ($LASTEXITCODE -ne 0) {
        Write-Output "BB18_PORTAL_AUTO|BUILD_RETRY|ATTEMPT=$attempt"
        continue
    }
    $level0 = Join-Path $buildFolder 'BB18PortalVisualProbe_Data\level0'
    if (!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $level0) -or
        (Get-Item -LiteralPath $level0).Length -lt 1024) {
        Write-Output "BB18_PORTAL_AUTO|ARTIFACT_INVALID|ATTEMPT=$attempt"
        Remove-Item -LiteralPath $buildFolder -Recurse -Force -ErrorAction SilentlyContinue
        continue
    }

    Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $evidence -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    $playerLog = Join-Path $projectRoot ("Logs\bb18_4_portal_player_auto_$attempt.log")
    $env:BB18_PORTAL_REPORT_PATH = $report
    $env:BB18_PORTAL_EVIDENCE_DIR = $evidence

    $player = Start-Process -FilePath $exe -ArgumentList @(
        '-force-d3d11', '-screen-fullscreen', '0',
        '-screen-width', '1280', '-screen-height', '720',
        '-logFile', $playerLog
    ) -PassThru -Wait
    $passed = $false
    if ($player.ExitCode -eq 0 -and (Test-Path -LiteralPath $report)) {
        $reportText = Get-Content -LiteralPath $report -Raw
        $passed = $reportText.Contains('[PASS]')
    }
    if ($passed) {
        Write-Output "BB18_PORTAL_AUTO|PASS|ATTEMPT=$attempt|REPORT=$report"
        exit 0
    }

    $corruptLevel = $false
    if (Test-Path -LiteralPath $playerLog) {
        $playerText = Get-Content -LiteralPath $playerLog -Raw
        $corruptLevel = $playerText.Contains('level0') -and $playerText.Contains('corrupted')
    }
    Write-Output (
        "BB18_PORTAL_AUTO|RUNTIME_RETRY|ATTEMPT={0}|EXIT={1}|CORRUPT_LEVEL0={2}" -f
        $attempt, $player.ExitCode, $corruptLevel)
    Remove-Item -LiteralPath $buildFolder -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Error "BB18_PORTAL_AUTO|FAIL|ATTEMPTS=$MaxAttempts"
exit 1
