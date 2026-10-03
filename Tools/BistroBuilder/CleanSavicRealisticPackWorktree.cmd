@echo off
setlocal
cd /d "%~dp0\..\.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0CleanSavicRealisticPackWorktree.ps1" -Commit
set EXITCODE=%ERRORLEVEL%
echo.
if "%EXITCODE%"=="0" (
  echo SAVIC cleanup completed successfully.
) else if "%EXITCODE%"=="2" (
  echo SAVIC cleanup stopped safely because unknown changes were preserved.
) else (
  echo SAVIC cleanup failed with exit code %EXITCODE%.
)
echo.
pause
exit /b %EXITCODE%
