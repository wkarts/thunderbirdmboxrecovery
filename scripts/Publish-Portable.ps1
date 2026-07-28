[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputDirectory = "release",

    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string] $Version = "2.2.1",

    [Parameter()]
    [bool] $GenerateFrameworkDependent = $true,

    [Parameter()]
    [bool] $EnableUpx = $false
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

function Find-Executable {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,
        [Parameter()]
        [string[]] $Candidates = @()
    )

    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    foreach ($candidate in $Candidates) {
        if (-not [string]::IsNullOrWhiteSpace($candidate) -and (Test-Path $candidate -PathType Leaf)) {
            return $candidate
        }
    }

    return $null
}

function New-SevenZipPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $SevenZipPath,
        [Parameter(Mandatory = $true)]
        [string] $Destination,
        [Parameter(Mandatory = $true)]
        [string[]] $SourcePaths
    )

    if (Test-Path $Destination) {
        Remove-Item $Destination -Force
    }

    $arguments = @("a", "-t7z", "-mx=9", "-m0=lzma2", "-y", $Destination) + $SourcePaths
    & $SevenZipPath @arguments | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $Destination -PathType Leaf)) {
        throw "Falha ao criar o pacote 7Z: $Destination"
    }
}

function New-ZipPackage {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Destination,
        [Parameter(Mandatory = $true)]
        [string[]] $SourcePaths
    )

    Compress-Archive `
        -Path $SourcePaths `
        -DestinationPath $Destination `
        -CompressionLevel Optimal `
        -Force
}

$programFilesX86 = [Environment]::GetEnvironmentVariable("ProgramFiles(x86)")
$chocolateyInstall = [Environment]::GetEnvironmentVariable("ChocolateyInstall")
$sevenZipPath = Find-Executable -Name "7z" -Candidates @(
    (Join-Path $env:ProgramFiles "7-Zip\7z.exe"),
    $(if ([string]::IsNullOrWhiteSpace($programFilesX86)) { $null } else { Join-Path $programFilesX86 "7-Zip\7z.exe" })
)
$upxPath = if ($EnableUpx) {
    Find-Executable -Name "upx" -Candidates @(
        (Join-Path $env:ProgramFiles "upx\upx.exe"),
        $(if ([string]::IsNullOrWhiteSpace($chocolateyInstall)) { $null } else { Join-Path $chocolateyInstall "bin\upx.exe" })
    )
} else {
    $null
}

if ($null -eq $sevenZipPath) {
    Write-Warning "7-Zip não foi localizado. Os pacotes ZIP serão gerados, mas os pacotes .7z não serão criados."
}
if ($EnableUpx -and $null -eq $upxPath) {
    Write-Warning "UPX foi solicitado, mas não foi localizado. As variantes UPX serão ignoradas."
}

$runtimes = @("win-x86", "win-x64")

foreach ($runtime in $runtimes) {
    Write-Host "::group::Publicando $runtime - versão $Version"

    $runtimeOutput = Join-Path $outputRoot "publish-$runtime-self-contained"
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
        throw "Falha no publish self-contained para $runtime."
    }

    $publishedExecutable = Join-Path $runtimeOutput "ThunderbirdRecoverySuite.exe"
    $portableName = "ThunderbirdRecoverySuite-v$Version-$runtime.exe"
    $portableExecutable = Join-Path $outputRoot $portableName

    if (-not (Test-Path $publishedExecutable -PathType Leaf)) {
        throw "Executável não encontrado após o publish: $publishedExecutable"
    }

    Move-Item $publishedExecutable $portableExecutable -Force
    $executableInfo = Get-Item $portableExecutable
    if ($executableInfo.Length -lt 1MB) {
        throw "Executável $portableName possui tamanho inesperado: $($executableInfo.Length) bytes."
    }

    $hashFileName = "SHA256-v$Version-$runtime.txt"
    $hashFile = Join-Path $outputRoot $hashFileName
    $hash = (Get-FileHash $portableExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$portableName" | Set-Content $hashFile -Encoding ascii

    $packageFiles = @($portableExecutable, $hashFile, $readmePath, $operationGuidePath)
    $zipName = "ThunderbirdRecoverySuite-v$Version-$runtime.zip"
    New-ZipPackage -Destination (Join-Path $outputRoot $zipName) -SourcePaths $packageFiles

    if ($null -ne $sevenZipPath) {
        $sevenZipName = "ThunderbirdRecoverySuite-v$Version-$runtime.7z"
        New-SevenZipPackage -SevenZipPath $sevenZipPath -Destination (Join-Path $outputRoot $sevenZipName) -SourcePaths $packageFiles
    }

    if ($GenerateFrameworkDependent) {
        $runtimeRequiredOutput = Join-Path $outputRoot "publish-$runtime-runtime-required"
        New-Item -ItemType Directory -Path $runtimeRequiredOutput -Force | Out-Null

        & dotnet restore $projectPath `
            -r $runtime `
            -p:SelfContained=false `
            -p:PublishSingleFile=true
        if ($LASTEXITCODE -ne 0) {
            throw "Falha no restore framework-dependent para $runtime."
        }

        & dotnet publish $projectPath `
            -c Release `
            -r $runtime `
            --self-contained false `
            --no-restore `
            -p:PublishSingleFile=true `
            -p:PublishTrimmed=false `
            -p:Version=$Version `
            -p:FileVersion=$fileVersion `
            -p:InformationalVersion=$informationalVersion `
            -o $runtimeRequiredOutput
        if ($LASTEXITCODE -ne 0) {
            throw "Falha no publish framework-dependent para $runtime."
        }

        $runtimeRequiredPublished = Join-Path $runtimeRequiredOutput "ThunderbirdRecoverySuite.exe"
        $runtimeRequiredName = "ThunderbirdRecoverySuite-v$Version-$runtime-runtime-required.exe"
        $runtimeRequiredExecutable = Join-Path $outputRoot $runtimeRequiredName
        if (-not (Test-Path $runtimeRequiredPublished -PathType Leaf)) {
            throw "Executável framework-dependent não encontrado para $runtime."
        }
        Move-Item $runtimeRequiredPublished $runtimeRequiredExecutable -Force

        $runtimeRequiredHashName = "SHA256-v$Version-$runtime-runtime-required.txt"
        $runtimeRequiredHashPath = Join-Path $outputRoot $runtimeRequiredHashName
        $runtimeRequiredHash = (Get-FileHash $runtimeRequiredExecutable -Algorithm SHA256).Hash.ToLowerInvariant()
        "$runtimeRequiredHash *$runtimeRequiredName" | Set-Content $runtimeRequiredHashPath -Encoding ascii

        $runtimeReadme = Join-Path $outputRoot "LEIA-ME-RUNTIME-OBRIGATORIO.txt"
        if (-not (Test-Path $runtimeReadme)) {
            @"
Esta variante é menor porque não incorpora o runtime do .NET.
Requer o Microsoft .NET 8 Desktop Runtime instalado na arquitetura correspondente (x86 ou x64).
Para uso portátil sem dependências, utilize o executável sem o sufixo runtime-required.
"@ | Set-Content $runtimeReadme -Encoding utf8
        }

        $runtimePackageFiles = @($runtimeRequiredExecutable, $runtimeRequiredHashPath, $runtimeReadme, $readmePath)
        $runtimeZipName = "ThunderbirdRecoverySuite-v$Version-$runtime-runtime-required.zip"
        New-ZipPackage -Destination (Join-Path $outputRoot $runtimeZipName) -SourcePaths $runtimePackageFiles
        if ($null -ne $sevenZipPath) {
            $runtimeSevenZipName = "ThunderbirdRecoverySuite-v$Version-$runtime-runtime-required.7z"
            New-SevenZipPackage -SevenZipPath $sevenZipPath -Destination (Join-Path $outputRoot $runtimeSevenZipName) -SourcePaths $runtimePackageFiles
        }
    }

    if ($EnableUpx -and $null -ne $upxPath) {
        $upxName = "ThunderbirdRecoverySuite-v$Version-$runtime-upx.exe"
        $upxExecutable = Join-Path $outputRoot $upxName
        Copy-Item $portableExecutable $upxExecutable -Force
        & $upxPath "--best" "--lzma" $upxExecutable | Out-Host
        if ($LASTEXITCODE -eq 0) {
            & $upxPath "-t" $upxExecutable | Out-Host
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "A compactação UPX falhou para $runtime. A variante será removida."
            Remove-Item $upxExecutable -Force -ErrorAction SilentlyContinue
        }
    }

    Remove-Item $runtimeOutput -Recurse -Force -ErrorAction SilentlyContinue
    if ($GenerateFrameworkDependent) {
        Remove-Item (Join-Path $outputRoot "publish-$runtime-runtime-required") -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Gerado: $portableName ($([math]::Round($executableInfo.Length / 1MB, 2)) MiB)"
    Write-Host "::endgroup::"
}

$versionPath = Join-Path $outputRoot "VERSION.txt"
$Version | Set-Content $versionPath -Encoding ascii

$checksumFiles = Get-ChildItem $outputRoot -File |
    Where-Object { $_.Extension -in @(".exe", ".zip", ".7z") } |
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
