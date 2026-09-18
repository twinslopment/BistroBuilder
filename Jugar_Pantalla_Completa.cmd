@echo off
set "BISTRO_PLAYER=%~dp0Builds\Windows\BistroBuilder_Edicion\BistroBuilder.exe"
if not exist "%BISTRO_PLAYER%" (
    echo No se encuentra la build de Bistro Builder.
    echo Generala desde Unity: Tools / Bistro Builder / Build / Windows Playtest.
    pause
    exit /b 1
)
start "Bistro Builder" /D "%~dp0Builds\Windows\BistroBuilder_Edicion" "%BISTRO_PLAYER%" -screen-fullscreen 1 -window-mode borderless
