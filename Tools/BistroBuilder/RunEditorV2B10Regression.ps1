param(
    [string] $UnityPath = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe",
    [string] $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
)
$ErrorActionPreference = "Stop"
$jobs = @(
    @{ Block = "B4"; Method = "BistroBuilderEditorV2B4GlobalHistorySelfTest.RunFromCommandLine" },
    @{ Block = "B5"; Method = "BistroBuilderEditorV2B5RenovationSelfTest.RunFromCommandLine" },
    @{ Block = "B8"; Method = "BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest.RunFromCommandLine" },
    @{ Block = "B9"; Method = "BistroBuilderEditorV2B9ScalableCatalogSelfTest.RunFromCommandLine" },
    @{ Block = "B10"; Method = "BistroBuilderEditorV2B10ReplacementSelfTest.RunFromCommandLine" }
)
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity not found: $UnityPath" }
$failures = New-Object System.Collections.Generic.List[string]
foreach ($job in $jobs) {
    $block = $job.Block
    $log = Join-Path $ProjectPath ("Logs\EditorV2_" + $block + "_Regress_B10.log")
    Write-Output "RUN $block"
    $argsForUnity = @(
        "-batchmode", "-nographics", "-quit",
        "-projectPath", ('"' + $ProjectPath + '"'),
        "-logFile", ('"' + $log + '"'),
        "-executeMethod", $job.Method
    )
    $process = Start-Process -FilePath $UnityPath -ArgumentList $argsForUnity -Wait -PassThru
    $exitCode = $process.ExitCode
    Write-Output "RESULT $block EXIT=$exitCode LOG=$log"
    if ($exitCode -ne 0) { $failures.Add($block) }
}
if ($failures.Count -ne 0) {
    Write-Error ("REGRESSION FAILED: " + ($failures -join ", "))
    exit 1
}
Write-Output "EDITOR V2 B4,B5,B8,B9,B10 REGRESSION PASS"
exit 0
