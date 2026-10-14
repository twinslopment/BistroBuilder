@echo off
set "BB_V2_R4_PAGE=%~dp0index.html"
powershell.exe -NoProfile -Command "$v2PageUri=([Uri]$env:BB_V2_R4_PAGE).AbsoluteUri+'?fullscreen=1&revision=R4'; Start-Process $v2PageUri"
set "BB_V2_R4_PAGE="
exit /b
