@echo off
setlocal
cd /d "%~dp0\..\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RecoverSavicJobOnlyContent.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if "%EXITCODE%"=="0" (
  echo SAVIC job-only recovery completed successfully.
) else (
  echo SAVIC job-only recovery failed with exit code %EXITCODE%.
)
echo.
pause
exit /b %EXITCODE%
