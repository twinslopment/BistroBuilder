param(
    [int]$TimeoutSeconds = 600
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$runner = Join-Path $PSScriptRoot 'RunUnityBatchSafe.ps1'
if (!(Test-Path -LiteralPath $runner)) {
    throw "Bistro Builder batch runner not found: $runner"
}

$installMethod = 'BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.InstallOrRepairFromCommandLine'
$validateMethod = 'BistroBuilder.Editor.Savic.SavicRealisticTestPackV1Installer.ValidateFromCommandLine'
$unityArguments = @('-nographics', '-accept-apiupdate')

Write-Output 'SAVIC_REALISTIC_PACK_V1|INSTALL_START'
& $runner -ExecuteMethod $installMethod -LogName 'SAVIC_RealisticTestPackV1_Install.log' -TimeoutSeconds $TimeoutSeconds -AdditionalUnityArguments $unityArguments
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine('SAVIC_REALISTIC_PACK_V1|INSTALL_FAIL|EXIT=' + $LASTEXITCODE)
    exit $LASTEXITCODE
}

Write-Output 'SAVIC_REALISTIC_PACK_V1|VALIDATE_START'
& $runner -ExecuteMethod $validateMethod -LogName 'SAVIC_RealisticTestPackV1_Validate.log' -TimeoutSeconds $TimeoutSeconds -AdditionalUnityArguments $unityArguments
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine('SAVIC_REALISTIC_PACK_V1|VALIDATE_FAIL|EXIT=' + $LASTEXITCODE)
    exit $LASTEXITCODE
}

Write-Output 'SAVIC_REALISTIC_PACK_V1|PASS'
exit 0
