@echo off
setlocal EnableExtensions
chcp 65001 >nul

set "ROOT=%~dp0"
set "VERSION=%~1"
if "%VERSION%"=="" set "VERSION=2.2.1"

where pwsh >nul 2>nul
if errorlevel 1 (
    echo ERRO: PowerShell 7 ^(pwsh^) nao foi encontrado.
    exit /b 1
)

pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\Publish-Portable.ps1" -Version "%VERSION%"
exit /b %ERRORLEVEL%
