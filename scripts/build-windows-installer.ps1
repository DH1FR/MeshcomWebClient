#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes MeshcomWebDesk as a self-contained Windows x64 executable
    and optionally compiles an Inno Setup installer.

.EXAMPLE
    # Build only (no installer)
    .\build-windows-installer.ps1

    # Build + compile installer (requires Inno Setup to be installed)
    .\build-windows-installer.ps1 -CompileInstaller
#>
param(
    [switch]$CompileInstaller
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root        = Split-Path $PSScriptRoot -Parent
$project     = Join-Path $root 'MeshcomWebDesk\MeshcomWebDesk.csproj'
$publishOut  = Join-Path $root 'installer\publish\win-x64'
$issFile     = Join-Path $root 'installer\MeshcomWebDesk.iss'

Write-Host "`n=== MeshcomWebDesk – Windows Build ===" -ForegroundColor Cyan

# ── 1. Publish ────────────────────────────────────────────────────────────────
Write-Host "`n[1/2] Publishing self-contained win-x64 single-file ..." -ForegroundColor Yellow

if (Test-Path $publishOut) { Remove-Item $publishOut -Recurse -Force }

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output $publishOut

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

$exe = Join-Path $publishOut 'MeshcomWebDesk.exe'
Write-Host "Output: $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)" -ForegroundColor Green

# ── 2. Compile Installer (optional) ──────────────────────────────────────────
if (-not $CompileInstaller) {
    Write-Host "`n[2/2] Skipped installer compilation (-CompileInstaller not set)." -ForegroundColor DarkGray
    Write-Host "      To compile the installer manually, open:" -ForegroundColor DarkGray
    Write-Host "      $issFile" -ForegroundColor DarkGray
    exit 0
}

Write-Host "`n[2/2] Compiling Inno Setup installer ..." -ForegroundColor Yellow

$iscc = @(
    "$env:ProgramFiles (x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    (Get-Command ISCC.exe -ErrorAction SilentlyContinue)?.Source
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Warning "Inno Setup Compiler (ISCC.exe) not found. Download: https://jrsoftware.org/isdl.php"
    exit 1
}

Push-Location (Join-Path $root 'installer')
& $iscc $issFile
$rc = $LASTEXITCODE
Pop-Location

if ($rc -ne 0) { Write-Error "ISCC.exe failed (exit $rc)."; exit $rc }

$setup = Get-ChildItem (Join-Path $root 'installer\output') 'MeshcomWebDesk-Setup-*.exe' |
         Sort-Object LastWriteTime -Descending | Select-Object -First 1

Write-Host "`nInstaller ready: $($setup.FullName) ($([math]::Round($setup.Length / 1MB, 1)) MB)" -ForegroundColor Green
