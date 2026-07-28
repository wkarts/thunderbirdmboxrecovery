@echo off
setlocal EnableExtensions
cd /d "%~dp0"

where dotnet >nul 2>nul || (
    echo ERRO: .NET SDK 8 nao encontrado.
    exit /b 1
)

set "VERSION=%~1"
if "%VERSION%"=="" set "VERSION=2.0.0"

where pwsh >nul 2>nul
if not errorlevel 1 (
    pwsh -NoProfile -File "%~dp0scripts\Publish-Portable.ps1" -OutputDirectory artifacts -Version "%VERSION%"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Publish-Portable.ps1" -OutputDirectory artifacts -Version "%VERSION%"
)

if errorlevel 1 exit /b %errorlevel%

echo.
echo Builds da versao %VERSION% concluidos em: %CD%\artifacts
endlocal
