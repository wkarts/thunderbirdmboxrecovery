[CmdletBinding()]
param(
    [Parameter()]
    [string[]] $Paths
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $Paths -or $Paths.Count -eq 0) {
    $Paths = @(
        (Join-Path $PSScriptRoot "Publish-Portable.ps1"),
        $PSCommandPath
    )
}

$hasErrors = $false

foreach ($path in $Paths) {
    $fullPath = [System.IO.Path]::GetFullPath($path)
    if (-not (Test-Path $fullPath -PathType Leaf)) {
        throw "Script PowerShell não encontrado: $fullPath"
    }

    $tokens = $null
    $parseErrors = $null
    [System.Management.Automation.Language.Parser]::ParseFile(
        $fullPath,
        [ref] $tokens,
        [ref] $parseErrors
    ) | Out-Null

    if ($parseErrors.Count -eq 0) {
        Write-Host "Sintaxe PowerShell válida: $fullPath"
        continue
    }

    $hasErrors = $true
    foreach ($parseError in $parseErrors) {
        Write-Error (
            "{0}:{1}:{2}: {3}" -f
            $fullPath,
            $parseError.Extent.StartLineNumber,
            $parseError.Extent.StartColumnNumber,
            $parseError.Message
        )
    }
}

if ($hasErrors) {
    throw "Foram encontrados erros de sintaxe em scripts PowerShell."
}
