param(
    [string]$ProjectPath = "C:\Users\mruperez\ProyectoBB\BistroBuilder_EditorV2_B13Walls"
)
$ErrorActionPreference = "Stop"
$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe"
$steps = @(
    @("B4", "BistroBuilderEditorV2B4GlobalHistorySelfTest.RunFromCommandLine", "EditorV2_B4_GlobalHistory_Report.txt", "Resultado: 63 OK / 0 fallos."),
    @("B5", "BistroBuilderEditorV2B5RenovationSelfTest.RunFromCommandLine", "EditorV2_B5_Renovation_Report.txt", "Resultado: 48 OK / 0 fallos."),
    @("B8", "BistroBuilderEditorV2B8MultiSelectionGroupsSelfTest.RunFromCommandLine", "EditorV2_B8_MultiSelection_Groups_Report.txt", "Resultado:"),
    @("B9", "BistroBuilderEditorV2B9ScalableCatalogSelfTest.RunFromCommandLine", "EditorV2_B9_ScalableCatalog_Report.txt", "Resultado: 42 OK / 0 fallos."),
    @("B10", "BistroBuilderEditorV2B10ReplacementSelfTest.RunFromCommandLine", "EditorV2_B10_Replacement_Report.txt", "Resultado: 40 OK / 0 fallos.")
)
$results = @()
foreach ($step in $steps) {
    $label, $method, $report, $needle = $step
    $fullReport = Join-Path $ProjectPath $report
    if (Test-Path $fullReport) { Remove-Item $fullReport -Force }
    $log = Join-Path $ProjectPath ("B13_Final_" + $label + ".log")
    $args = @("-batchmode", "-nographics", "-quit", "-projectPath", $ProjectPath, "-executeMethod", $method, "-logFile", $log)
    $started = Get-Date
    $process = Start-Process -FilePath $unity -ArgumentList $args -PassThru -Wait
    $ok = $process.ExitCode -eq 0 -and (Test-Path $fullReport)
    if ($ok) {
        $body = Get-Content $fullReport -Raw
        $ok = $body.Contains($needle) -and -not $body.Contains("FAIL -")
        if ($label -eq "B8") { $ok = $ok -and $body.Contains("0 fallos.") }
    }
    $results += ("$label|" + $(if ($ok) { "PASS" } else { "FAIL" }) + "|Exit=" + $process.ExitCode + "|ElapsedSec=" + [math]::Round(((Get-Date) - $started).TotalSeconds, 1))
    $results | Set-Content (Join-Path $ProjectPath "B13_FinalGates_Results.txt")
    if (-not $ok) { exit 1 }
}
exit 0
