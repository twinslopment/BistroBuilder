$ErrorActionPreference='Stop'
$v2FilePath=Join-Path $PSScriptRoot 'index.html'
$v2PageUri=([Uri]$v2FilePath).AbsoluteUri+'?fullscreen=1'
Start-Process $v2PageUri
