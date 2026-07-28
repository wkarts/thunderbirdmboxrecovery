[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputDirectory = "release",

    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = "1.4.0"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$env:DOTNET_NOLOGO = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src/ThunderbirdMboxRecovery/ThunderbirdMboxRecovery.csproj"
$readmePath = Join-Path $repositoryRoot "README.md"
$operationGuidePath = Join-Path $repositoryRoot "docs/OPERACAO_TECNICA.md"
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repositoryRoot $OutputDirectory
}

$repositoryRootFull = [System.IO.Path]::GetFullPath($repositoryRoot)
$outputRoot = [System.IO.Path]::GetFullPath($outputRoot)

if ($outputRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) -eq
    $repositoryRootFull.TrimEnd([System.IO.Path]::DirectorySeparatorChar)) {
    throw "A pasta de saída não pode ser a raiz do repositório."
}

if (Test-Path $outputRoot) {
    Remove-Item $outputRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

$numericVersion = ($Version -split '-', 2)[0]
$fileVersion = "$numericVersion.0"
$informationalVersion = if ([string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $Version
} else {
    "$Version+$($env:GITHUB_SHA.Substring(0, [Math]::Min(12, $env:GITHUB_SHA.Length)))"
}

$runtimes = @("win-x86", "win-x64")

foreach ($runtime in $runtimes) {
    Write-Host "::group::Publicando $runtime - versão $Version"

    $runtimeOutput = Join-Path $outputRoot $runtime
    New-Item -ItemType Directory -Path $runtimeOutput -Force | Out-Null

    & dotnet restore $projectPath `
        -r $runtime `
        -p:SelfContained=true `
        -p:PublishSingleFile=true
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no restore para $runtime."
    }

    & dotnet publish $projectPath `
        -c Release `
        -r $runtime `
        --self-contained true `
        --no-restore `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -p:FileVersion=$fileVersion `
        -p:InformationalVersion=$informationalVersion `
        -o $runtimeOutput
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no publish para $runtime."
    }

    $publishedExecutable = Join-Path $runtimeOutput "ThunderbirdMboxRecovery.exe"
    $portableName = "ThunderbirdMboxRecovery-v$Version-$runtime.exe"
    $portableExecutable = Join-Path $runtimeOutput $portableName

    if (-not (Test-Path $publishedExecutable -PathType Leaf)) {
        throw "Executável não encontrado após o publish: $publishedExecutable"
    }

    Move-Item $publishedExecutable $portableExecutable -Force

    $executableInfo = Get-Item $portableExecutable
    if ($executableInfo.Length -lt 1MB) {
        throw "Executável $portableName possui tamanho inesperado: $($executableInfo.Length) bytes."
    }

    $hashFileName = "SHA256-v$Version-$runtime.txt"
    $hashFile = Join-Path $runtimeOutput $hashFileName
    $hash = (Get-FileHash $portableExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$portableName" | Set-Content $hashFile -Encoding ascii

    $zipName = "ThunderbirdMboxRecovery-v$Version-$runtime.zip"
    $zipPath = Join-Path $outputRoot $zipName
    Compress-Archive `
        -Path $portableExecutable, $hashFile, $readmePath, $operationGuidePath `
        -DestinationPath $zipPath `
        -CompressionLevel Optimal `
        -Force

    Copy-Item $portableExecutable (Join-Path $outputRoot $portableName) -Force
    Copy-Item $hashFile (Join-Path $outputRoot $hashFileName) -Force

    Write-Host "Gerado: $portableName ($([math]::Round($executableInfo.Length / 1MB, 2)) MiB)"
    Write-Host "::endgroup::"
}

$versionPath = Join-Path $outputRoot "VERSION.txt"
$Version | Set-Content $versionPath -Encoding ascii

$checksumFiles = Get-ChildItem $outputRoot -File |
    Where-Object { $_.Extension -in @(".exe", ".zip") } |
    Sort-Object Name

$checksumLines = foreach ($file in $checksumFiles) {
    $hash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$($file.Name)"
}

$checksumsPath = Join-Path $outputRoot "SHA256SUMS.txt"
$checksumLines | Set-Content $checksumsPath -Encoding ascii

Write-Host "Arquivos finais da versão ${Version}:"
Get-ChildItem $outputRoot -File | Sort-Object Name | ForEach-Object {
    Write-Host ("- {0} ({1:N2} MiB)" -f $_.Name, ($_.Length / 1MB))
}
