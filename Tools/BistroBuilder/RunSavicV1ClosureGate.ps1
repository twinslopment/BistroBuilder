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
$unityArguments = @('-nographics', '-accept-apiupdate')

Write-Output 'SAVIC_V1_HEADLESS|START'

& $runner `
    -ExecuteMethod $method `
    -LogName 'SAVIC_V1_ClosureGate.log' `
    -TimeoutSeconds $TimeoutSeconds `
    -AdditionalUnityArguments $unityArguments
