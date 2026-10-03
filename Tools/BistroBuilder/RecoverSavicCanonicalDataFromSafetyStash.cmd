@echo off
setlocal
cd /d "%~dp0\..\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0RecoverSavicCanonicalDataFromSafetyStash.ps1"
set EXITCODE=%ERRORLEVEL%
echo.
if "%EXITCODE%"=="0" (
  echo SAVIC canonical data recovery completed successfully.
) else (
  echo SAVIC canonical data recovery failed with exit code %EXITCODE%.
)
echo.
pause
exit /b %EXITCODE%
