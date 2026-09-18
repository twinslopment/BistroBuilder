param(
    [Parameter(Mandatory = $true)] [string]$ProjectPath,
    [string]$UnityExe,
    [string[]]$UnityArguments = @()
)

$commonData = [Environment]::GetFolderPath('CommonApplicationData')
if ([string]::IsNullOrWhiteSpace($env:PROGRAMDATA)) { $env:PROGRAMDATA = $commonData }
if ([string]::IsNullOrWhiteSpace($env:ALLUSERSPROFILE)) { $env:ALLUSERSPROFILE = $commonData }

if ([string]::IsNullOrWhiteSpace($UnityExe)) {
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (!(Test-Path $versionFile)) { throw "Unity project version file not found: $versionFile" }
    $match = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if ($match -eq $null) { throw 'Unity editor version could not be resolved.' }
    $version = $match.Matches[0].Groups[1].Value.Trim()
    $UnityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
}

if (!(Test-Path $UnityExe)) { throw "Unity executable not found: $UnityExe" }
$arguments = @($UnityArguments) + @('-projectPath', ('"' + $ProjectPath + '"'))
$process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -Wait -PassThru -NoNewWindow
exit $process.ExitCode
