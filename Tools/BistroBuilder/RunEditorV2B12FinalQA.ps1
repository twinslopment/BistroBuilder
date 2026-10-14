param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.19f1\Editor\Unity.exe',
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [switch]$SkipWindowsBuild
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable absent: $UnityPath" }
$report = Join-Path $ProjectPath 'Logs\B12_Final_Sequential_Suite.txt'
Set-Content -LiteralPath $report -Value 'EDITOR V2 B12 SEQUENTIAL FINAL QA - actual Unity process exit codes' -Encoding UTF8
$failures = New-Object System.Collections.Generic.List[string]
$cases = @(
    @{ Name='B12_RUNTIME'; Method='BistroBuilderEditorV2B12RuntimeSelfTest.RunFromCommandLine'; File='EditorV2_B12_Runtime_Report.txt'; Expected='PASS ' },
    @{ Name='B12_FIVESAVELOAD'; Method='BistroBuilderEditorV2B12FiveSaveLoadSelfTest.RunFromCommandLine'; File='EditorV2_B12_FiveSaveLoad_Report.txt'; Expected='PASS ' },
    @{ Name='B12_VALID64_A'; Method='BistroBuilderEditorV2B12Valid64SelfTest.RunFromCommandLine'; File='EditorV2_B12_Valid64_Report.txt'; Expected='PASS ' },
    @{ Name='B12_VALID64_B'; Method='BistroBuilderEditorV2B12Valid64SelfTest.RunFromCommandLine'; File='EditorV2_B12_Valid64_Report.txt'; Expected='PASS ' },
    @{ Name='B12_VALID64_C'; Method='BistroBuilderEditorV2B12Valid64SelfTest.RunFromCommandLine'; File='EditorV2_B12_Valid64_Report.txt'; Expected='PASS ' },
    @{ Name='BBSIS_PHASE2C'; Method='BistroBuilderBBSISPhase2CPlayModeSelfTest.RunFromCommandLine'; File=''; Expected='[PASS] PASS - preflight' }
)
function Assert-FreeUnity {
    $editors = @(Get-CimInstance Win32_Process -Filter "name = 'Unity.exe'" |
        Where-Object { $_.ExecutablePath -eq $UnityPath })
    if ($editors.Count -ne 0) {
        throw ('Another Unity editor is using this workstation: ' +
            (($editors | ForEach-Object { $_.ProcessId }) -join ','))
    }
}
function Run-Unity([string]$Name, [string]$Method, [bool]$Quit, [string]$ExpectedFile, [string]$ExpectedText) {
    Assert-FreeUnity
    $log = Join-Path $ProjectPath ('Logs\B12_SEQ_' + $Name + '.log')
    $startTime = Get-Date
    $args = @('-batchmode','-nographics')
    if ($Quit) { $args += '-quit' }
    $args += @('-projectPath', ('"' + $ProjectPath + '"'),
        '-logFile', ('"' + $log + '"'), '-executeMethod', $Method)
    Write-Output "START $Name"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    # Wait for this Unity process only. Start-Process -Wait may block on
    # unrelated descendants after Unity itself has already exited.
    $process = Start-Process -FilePath $UnityPath -ArgumentList $args -PassThru
    $process.WaitForExit()
    $timer.Stop()
    $code = $process.ExitCode
    $hasFreshPass = $true
    if (-not [string]::IsNullOrEmpty($ExpectedFile)) {
        $filePath = Join-Path $ProjectPath $ExpectedFile
        $hasFreshPass = (Test-Path -LiteralPath $filePath) -and
            ((Get-Item -LiteralPath $filePath).LastWriteTime -ge $startTime.AddSeconds(-2)) -and
            ((Get-Content -LiteralPath $filePath -Raw).Contains($ExpectedText))
    } elseif (-not [string]::IsNullOrEmpty($ExpectedText)) {
        # This runtime test writes only to its Unity log, not to the 2026-10-07
        # historical report. Verify an up-to-date PASS marker from THIS run.
        $hasFreshPass = (Test-Path -LiteralPath $log) -and
            ((Get-Item -LiteralPath $log).LastWriteTime -ge $startTime.AddSeconds(-2)) -and
            ((Get-Content -LiteralPath $log -Raw).Contains($ExpectedText))
    }
    $passed = ($code -eq 0) -and $hasFreshPass
    $line = ('{0} EXIT={1} REPORT_PASS={2} ELAPSED_SEC={3} LOG={4}' -f
        $Name, $code, $hasFreshPass, [math]::Round($timer.Elapsed.TotalSeconds,1), $log)
    Add-Content -LiteralPath $report -Value $line
    Write-Output $line
    if (-not $passed) { $failures.Add($Name) }
}
foreach ($case in $cases) {
    Run-Unity $case.Name $case.Method $false $case.File $case.Expected
}
Assert-FreeUnity
Write-Output 'START REGRESSION B4-B10'
& (Join-Path $ProjectPath 'Tools\BistroBuilder\RunEditorV2B10Regression.ps1')
$regCode = $LASTEXITCODE
$regLine = "B4_B10 EXIT=$regCode"
Add-Content -LiteralPath $report -Value $regLine
Write-Output $regLine
if ($regCode -ne 0) { $failures.Add('B4_B10') }
if (-not $SkipWindowsBuild) {
    Run-Unity 'WINDOWS_BUILD' 'BistroBuilderPlaytestBuild.BuildWindowsPlaytestFromCommandLine' $true '' ''
}
if ($failures.Count -eq 0) {
    Add-Content -LiteralPath $report -Value 'OVERALL PASS'
    Write-Output 'OVERALL PASS'
    exit 0
}
Add-Content -LiteralPath $report -Value ('OVERALL FAIL: ' + ($failures -join ', '))
Write-Output ('OVERALL FAIL: ' + ($failures -join ', '))
exit 1
