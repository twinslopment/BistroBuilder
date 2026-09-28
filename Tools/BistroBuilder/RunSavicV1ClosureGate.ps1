param(
    [int]$TimeoutSeconds = 900
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$runner = Join-Path $PSScriptRoot 'RunUnityBatchSafe.ps1'
if (!(Test-Path -LiteralPath $runner)) {
    throw "SAVIC batch runner not found: $runner"
}

$method = 'BistroBuilder.Editor.Savic.SavicV1ClosureGate.RunFromCommandLine'

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $runner -ExecuteMethod $method -LogName 'SAVIC_V1_ClosureGate.log' -TimeoutSeconds $TimeoutSeconds -AdditionalUnityArguments '-nographics','-accept-apiupdate'
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Output 'SAVIC_V1_HEADLESS|PASS'
}
else {
    [Console]::Error.WriteLine('SAVIC_V1_HEADLESS|FAIL|EXIT=' + $exitCode)
}

exit $exitCode
