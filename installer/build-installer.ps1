<#
    build-installer.ps1
    -------------------
    One command to (re)build the Windows installer for NHI Inventory Management System.

        1. Publishes the WPF app self-contained for win-x64  ->  installer\publish\
        2. Compiles installer\NHiInventory.iss with Inno Setup ->  installer\Output\NHI-Inventory-Setup.exe
        3. Copies the finished Setup.exe to your Desktop\NHI Inventory Installer\

    USAGE
        pwsh -File installer\build-installer.ps1                 # build version from the .csproj
        pwsh -File installer\build-installer.ps1 -Version 1.1.0  # override version (for an update release)

    RELEASING AN UPDATE
        Bump <Version> in src\NHi.Inventory\NHi.Inventory.csproj (or pass -Version), then run this.
        Users just run the new Setup.exe over the old install: same AppId = in-place upgrade,
        and the database + settings in %LOCALAPPDATA%\NHiInventory\ are never touched.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repo      = Split-Path $PSScriptRoot -Parent
$csproj    = Join-Path $repo "src\NHi.Inventory\NHi.Inventory.csproj"
$publishIn = Join-Path $PSScriptRoot "publish"
$issFile   = Join-Path $PSScriptRoot "NHiInventory.iss"
$outDir    = Join-Path $PSScriptRoot "Output"
$deskCopy  = Join-Path ([Environment]::GetFolderPath("Desktop")) "NHI Inventory Installer"

function Find-ISCC {
    $c = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $cands = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    foreach ($p in $cands) { if (Test-Path $p) { return $p } }
    throw "Inno Setup (ISCC.exe) not found. Install it with:  winget install JRSoftware.InnoSetup"
}

Write-Host "==> Stopping any running NHi.Inventory.exe" -ForegroundColor Cyan
Get-Process NHi.Inventory -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Write-Host "==> Cleaning previous publish output" -ForegroundColor Cyan
if (Test-Path $publishIn) { Remove-Item $publishIn -Recurse -Force }

$verArg = @()
if ($Version) {
    $verArg = @("-p:Version=$Version", "-p:FileVersion=$Version.0", "-p:AssemblyVersion=$Version.0")
    Write-Host "==> Version override: $Version" -ForegroundColor Cyan
}

Write-Host "==> dotnet publish (self-contained, win-x64) ..." -ForegroundColor Cyan
& dotnet publish $csproj -c $Configuration -r win-x64 --self-contained true `
    -p:PublishReadyToRun=true -p:PublishSingleFile=false `
    -p:DebugType=none -p:DebugSymbols=false `
    @verArg -o $publishIn
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

if (-not (Test-Path (Join-Path $publishIn "NHi.Inventory.exe"))) {
    throw "publish\NHi.Inventory.exe missing - publish did not produce the app."
}

$iscc = Find-ISCC
Write-Host "==> Compiling installer with $iscc" -ForegroundColor Cyan
$isccArgs = @($issFile)
if ($Version) { $isccArgs = @("/DAppVersion=$Version", $issFile) }
& $iscc @isccArgs
if ($LASTEXITCODE -ne 0) { throw "ISCC failed (exit $LASTEXITCODE)" }

$setup = Join-Path $outDir "NHI-Inventory-Setup.exe"
if (-not (Test-Path $setup)) { throw "Expected $setup was not produced." }

New-Item -ItemType Directory -Force -Path $deskCopy | Out-Null
Copy-Item $setup $deskCopy -Force
Copy-Item (Join-Path $PSScriptRoot "README.txt") $deskCopy -Force -ErrorAction SilentlyContinue

$mb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host ""
Write-Host "DONE." -ForegroundColor Green
Write-Host "  Installer : $setup  ($mb MB)"
Write-Host "  Copied to : $deskCopy"
