@echo off
setlocal EnableExtensions
cd /d "%~dp0"

where dotnet >nul 2>nul || (
    echo ERRO: .NET SDK 8 nao encontrado.
    exit /b 1
)

if exist artifacts rmdir /s /q artifacts
mkdir artifacts

for %%R in (win-x86 win-x64) do (
    echo Publicando %%R...
    dotnet restore ThunderbirdMboxRecovery.sln -r %%R || exit /b 1
    dotnet publish src\ThunderbirdMboxRecovery\ThunderbirdMboxRecovery.csproj ^
        -c Release -r %%R --self-contained true --no-restore ^
        -p:PublishSingleFile=true -p:PublishTrimmed=false ^
        -o artifacts\%%R || exit /b 1
    move /y artifacts\%%R\ThunderbirdMboxRecovery.exe artifacts\%%R\ThunderbirdMboxRecovery-%%R.exe >nul
    certutil -hashfile artifacts\%%R\ThunderbirdMboxRecovery-%%R.exe SHA256 > artifacts\%%R\SHA256-%%R.txt
)

echo.
echo Builds concluidos em: %CD%\artifacts
endlocal
